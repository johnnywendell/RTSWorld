using System;
using System.Collections.Generic;
using System.Diagnostics;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using RTSWorld.World.Environment;

namespace RTSWorld.Simulation
{
    public enum AutonomyMode : byte { Work, Guard, HoldPosition }
    public enum AutonomyState : byte { Idle, Working, Building, Guard, HoldPosition, Attack, Chase, Return, Manual, Stopped, Blocked, Interrupted }

    public struct UnitAutonomy : IComponentData
    {
        public AutonomyMode Mode;
        public AutonomyState State;
        public int2 Anchor;
        public bool AutoBuild;
        internal bool Returning;
        internal int WorkOffset;
        internal Entity FailedTarget;
        internal ulong RetryTargetAt;
    }

    public sealed partial class UnitSession
    {
        private const int PerceptionRadius = 12;
        private readonly Dictionary<ResourceKey, int> resourceAssignments = new();
        private readonly Dictionary<int2, List<ResourceKey>> resourceBins = new();
        private readonly Dictionary<int2, List<Entity>> buildingBins = new();
        private readonly Dictionary<Entity, int> buildingAssignments = new();
        private readonly Dictionary<(int2 Cell, int Owner), Entity> militaryBins = new();
        private readonly HashSet<int> perceivedOwners = new();
        private readonly Dictionary<(int Owner, int2 Source), (bool Found, int2 Cell)> depotBySource = new();
        private readonly List<(ResourceKey Key, double Score)> workOptions = new();
        private EnvironmentalObstacles indexedObstacles;
        private ulong nextMilitaryRefresh;
        private EntityQuery autonomousUnits;
        private bool issuingAutomaticOrder;
        private int decisionInterval = 12;
        private double guardLeash = 8;

        public int AutonomyDecisions { get; private set; }
        public int AutonomyQueries { get; private set; }
        public int AutonomyAssignments { get; private set; }
        public int AutonomousDeliveries { get; private set; }
        public int AutonomousBuildings { get; private set; }
        public double AutonomyPerceptionMs { get; private set; }
        public double AutonomyDecisionMs { get; private set; }
        public double AutonomyExecutionMs { get; private set; }

        public void ConfigureAutonomy(int intervalTicks, double leash)
        {
            if (intervalTicks < 1 || !math.isfinite(leash) || leash < 1 || leash > PerceptionRadius)
                throw new ArgumentOutOfRangeException();
            decisionInterval = intervalTicks;
            guardLeash = leash;
        }

        public bool EnableAutonomy(Entity entity, AutonomyMode mode, bool autoBuild = false)
        {
            CompleteJobs();
            var manager = World.EntityManager;
            if (!manager.Exists(entity) || !manager.HasComponent<UnitIdentity>(entity) ||
                (manager.GetComponentData<UnitIdentity>(entity).Kind == UnitKind.Villager
                    ? mode != AutonomyMode.Work : mode is not (AutonomyMode.Guard or AutonomyMode.HoldPosition)))
                return false;
            int owner = manager.GetComponentData<UnitIdentity>(entity).Owner;
            using var members = new NativeArray<Entity>(1, Allocator.Temp);
            var handles = members;
            handles[0] = entity;
            ApplyOrder(UnitOrder.Stop(), owner, handles);
            if (!manager.HasComponent<UnitAutonomy>(entity)) manager.AddComponent<UnitAutonomy>(entity);
            manager.SetComponentData(entity, new UnitAutonomy
            {
                Mode = mode,
                AutoBuild = autoBuild,
                Anchor = CellOf(manager, entity)
            });
            return true;
        }

        private static bool IsAutomatic(EntityManager manager, Entity entity) =>
            manager.HasComponent<UnitAutonomy>(entity) &&
            manager.GetComponentData<UnitAutonomy>(entity).State is not (AutonomyState.Manual or AutonomyState.Stopped);

        private void MarkManual(EntityManager manager, Entity entity, bool stop = false)
        {
            if (issuingAutomaticOrder || !manager.HasComponent<UnitAutonomy>(entity)) return;
            var ai = manager.GetComponentData<UnitAutonomy>(entity);
            ai.State = stop ? AutonomyState.Stopped : AutonomyState.Manual;
            ai.Returning = false;
            manager.SetComponentData(entity, ai);
        }

        private static int2 CellOf(EntityManager manager, Entity entity) =>
            (int2)math.floor(manager.GetComponentData<LogicalPose>(entity).Position.xz);

        private static int2 Bin(int2 cell) => (int2)math.floor((double2)cell / 4);

        private static void AddToBin<T>(Dictionary<int2, List<T>> bins, int2 cell, T item)
        {
            if (!bins.TryGetValue(cell, out var items)) bins.Add(cell, items = new List<T>());
            items.Add(item);
        }

        private void RefreshPerception(EntityManager manager)
        {
            var obstacles = navigationGrid.Obstacles;
            if (!ReferenceEquals(obstacles, indexedObstacles))
            {
                resourceBins.Clear();
                if (obstacles != null)
                    foreach (var key in obstacles.ResourceKeys)
                        AddToBin(resourceBins, Bin(new int2((int)key.X, (int)key.Z)), key);
                indexedObstacles = obstacles;
            }

            resourceAssignments.Clear();
            foreach (var entity in workers)
            {
                if (!manager.Exists(entity)) continue;
                var worker = manager.GetComponentData<GatherWorker>(entity);
                if (worker.Phase is GatherPhase.Idle or GatherPhase.Blocked) continue;
                resourceAssignments.TryGetValue(worker.Resource, out int count);
                resourceAssignments[worker.Resource] = count + 1;
            }

            buildingAssignments.Clear();
            foreach (var site in buildTargets.Values)
            {
                buildingAssignments.TryGetValue(site, out int assigned);
                buildingAssignments[site] = assigned + 1;
            }
            foreach (var items in buildingBins.Values) items.Clear();
            foreach (var entity in buildings)
            {
                var building = manager.GetComponentData<Building>(entity);
                if (building.State is ConstructionState.Planned or ConstructionState.UnderConstruction)
                    AddToBin(buildingBins, Bin(building.Origin), entity);
            }

            if (Tick < nextMilitaryRefresh) return;
            nextMilitaryRefresh = Tick + 3;
            militaryBins.Clear();
            perceivedOwners.Clear();
            using var entities = units.ToEntityArray(Allocator.Temp);
            foreach (var entity in entities)
            {
                if (manager.GetComponentData<UnitHealth>(entity).Current <= 0) continue;
                int owner = manager.GetComponentData<UnitIdentity>(entity).Owner;
                perceivedOwners.Add(owner);
                militaryBins.TryAdd((CellOf(manager, entity), owner), entity);
            }
        }

        internal void TickAutonomy()
        {
            AutonomyDecisions = AutonomyQueries = 0;
            AutonomyPerceptionMs = AutonomyDecisionMs = AutonomyExecutionMs = 0;
            if (navigationGrid == null || !movement.TerrainAvailable || autonomousUnits.IsEmptyIgnoreFilter) return;
            CompleteJobs();
            var manager = World.EntityManager;
            long started = Stopwatch.GetTimestamp();
            depotBySource.Clear();
            RefreshPerception(manager);
            AutonomyPerceptionMs = MillisecondsSince(started);
            using var entities = autonomousUnits.ToEntityArray(Allocator.Temp);
            using var single = new NativeArray<Entity>(1, Allocator.Temp);
            var handle = single;
            issuingAutomaticOrder = true;
            try
            {
                ulong tick = Tick;
                foreach (var entity in entities)
                {
                    var ai = manager.GetComponentData<UnitAutonomy>(entity);
                    if (ai.State == AutonomyState.Stopped) continue;
                    if ((tick + (ulong)entity.Index) % (ulong)decisionInterval != 0) continue;
                    AutonomyDecisions++;
                    handle[0] = entity;
                    var identity = manager.GetComponentData<UnitIdentity>(entity);
                    var motion = manager.GetComponentData<UnitMotion>(entity);
                    var worker = manager.GetComponentData<GatherWorker>(entity);
                    var combat = manager.GetComponentData<UnitCombat>(entity);
                    bool working = worker.Phase is not (GatherPhase.Idle or GatherPhase.Blocked);
                    bool building = buildTargets.ContainsKey(entity);
                    if (ai.State == AutonomyState.Manual &&
                        (motion.Field != 0 || math.any(motion.DesiredVelocity != 0) || working ||
                        building || combat.Target != Entity.Null)) continue;

                    if (ai.State == AutonomyState.Manual && ai.Mode != AutonomyMode.Work)
                        ai.Anchor = CellOf(manager, entity);

                    if (ai.Mode == AutonomyMode.Work)
                    {
                        ai.State = working ? AutonomyState.Working
                            : building ? AutonomyState.Building
                            : combat.HitUntil > combatTime ? AutonomyState.Interrupted
                            : DecideWork(manager, entity, identity.Owner, ref ai, single);
                    }
                    else ai.State = DecideGuard(manager, entity, identity.Owner, ref ai, single);
                    manager.SetComponentData(entity, ai);
                }
            }
            finally
            {
                issuingAutomaticOrder = false;
            }
        }

        private AutonomyState DecideWork(EntityManager manager, Entity entity, int owner, ref UnitAutonomy ai,
            NativeArray<Entity> handles)
        {
            var cell = CellOf(manager, entity);
            var cargo = manager.GetComponentData<GatherWorker>(entity);
            if (cargo.Cargo > 0)
            {
                long delivery = Stopwatch.GetTimestamp();
                SendToDepot(manager, entity, ref cargo);
                manager.SetComponentData(entity, cargo);
                if (scheduledWorkers.Add(entity)) workers.Add(entity);
                AutonomyExecutionMs += MillisecondsSince(delivery);
                return cargo.Phase == GatherPhase.Delivering ? AutonomyState.Working : AutonomyState.Blocked;
            }

            long started = Stopwatch.GetTimestamp();
            workOptions.Clear();
            var center = Bin(cell);
            for (int z = -3; z <= 3; z++)
                for (int x = -3; x <= 3; x++)
                {
                    var bin = center + new int2(x, z);
                    AutonomyQueries++;
                    if (resourceBins.TryGetValue(bin, out var resources))
                        foreach (var key in resources)
                        {
                            double distance = math.distancesq((double2)cell, new double2(key.X, key.Z));
                            if (distance > PerceptionRadius * PerceptionRadius ||
                                !TryGetResource(key, out var amount) || amount.Remaining <= 0) continue;
                            resourceAssignments.TryGetValue(key, out int count);
                            workOptions.Add((key, distance + count * 32 - amount.Remaining / amount.Initial));
                        }
                    if (!ai.AutoBuild || !buildingBins.TryGetValue(bin, out var sites)) continue;
                    foreach (var site in sites)
                    {
                        var building = manager.GetComponentData<Building>(site);
                        buildingAssignments.TryGetValue(site, out int assigned);
                        if (assigned >= 2 || building.Owner != owner ||
                            math.distancesq((double2)cell, building.Origin) > PerceptionRadius * PerceptionRadius)
                            continue;
                        long execution = Stopwatch.GetTimestamp();
                        var result = OrderBuild(manager, site, owner, handles);
                        AutonomyExecutionMs += MillisecondsSince(execution);
                        if (result != UnitOrderResult.Accepted) continue;
                        buildingAssignments[site] = assigned + 1;
                        AutonomyAssignments++;
                        return AutonomyState.Building;
                    }
                }
            workOptions.Sort(CompareWork);
            AutonomyDecisionMs += MillisecondsSince(started);
            int start = workOptions.Count == 0 ? 0 : ai.WorkOffset % workOptions.Count;
            for (int i = 0; i < math.min(4, workOptions.Count); i++)
            {
                var option = workOptions[(start + i) % workOptions.Count];
                long execution = Stopwatch.GetTimestamp();
                var result = Gather(option.Key, owner, handles);
                AutonomyExecutionMs += MillisecondsSince(execution);
                if (result != UnitOrderResult.Accepted) continue;
                resourceAssignments.TryGetValue(option.Key, out int count);
                resourceAssignments[option.Key] = count + 1;
                ai.WorkOffset = 0;
                AutonomyAssignments++;
                return AutonomyState.Working;
            }
            ai.WorkOffset = workOptions.Count == 0 ? 0 : (start + 4) % workOptions.Count;
            return workOptions.Count == 0 ? AutonomyState.Idle : AutonomyState.Blocked;
        }

        private static int CompareWork((ResourceKey Key, double Score) left, (ResourceKey Key, double Score) right)
        {
            int score = left.Score.CompareTo(right.Score);
            if (score != 0) return score;
            int x = left.Key.X.CompareTo(right.Key.X);
            return x != 0 ? x : left.Key.Z.CompareTo(right.Key.Z);
        }

        private AutonomyState DecideGuard(EntityManager manager, Entity entity, int owner, ref UnitAutonomy ai,
            NativeArray<Entity> handles)
        {
            var combat = manager.GetComponentData<UnitCombat>(entity);
            var position = manager.GetComponentData<LogicalPose>(entity).Position.xz;
            double range = manager.GetComponentData<UnitAttributes>(entity).Range;
            double radius = ai.Mode == AutonomyMode.Guard ? guardLeash : range;
            if (combat.Target != Entity.Null && IsCombatTarget(manager, combat.Target, owner) &&
                combat.Phase != CombatPhase.Blocked && WithinGuardLimit(manager, combat.Target, ai, position, range))
                return combat.Phase == CombatPhase.Attacking ? AutonomyState.Attack : AutonomyState.Chase;

            if (combat.Target != Entity.Null)
            {
                if (combat.Phase == CombatPhase.Blocked)
                {
                    ai.FailedTarget = combat.Target;
                    ai.RetryTargetAt = Tick + (ulong)decisionInterval * 3;
                }
                CancelAttack(manager, entity);
                HoldPosition(manager, entity);
            }
            if (ai.Mode == AutonomyMode.Guard && !CellOf(manager, entity).Equals(ai.Anchor) &&
                (ai.Returning || ai.State is AutonomyState.Chase or AutonomyState.Attack))
            {
                ai.Returning = true;
                if (manager.GetComponentData<UnitMotion>(entity).Field != 0)
                    return AutonomyState.Return;
                long execution = Stopwatch.GetTimestamp();
                bool routed = SetDestination(handles, ai.Anchor);
                AutonomyExecutionMs += MillisecondsSince(execution);
                return routed ? AutonomyState.Return : AutonomyState.Blocked;
            }
            ai.Returning = false;

            long started = Stopwatch.GetTimestamp();
            var idle = ai.Mode == AutonomyMode.Guard ? AutonomyState.Guard : AutonomyState.HoldPosition;
            if (perceivedOwners.Count == 1 && perceivedOwners.Contains(owner)) return idle;
            Entity target = Entity.Null;
            ulong tick = Tick;
            double nearest = radius * radius;
            var center = CellOf(manager, entity);
            int bins = (int)math.ceil(radius);
            for (int z = -bins; z <= bins; z++)
                for (int x = -bins; x <= bins; x++)
                    foreach (int other in perceivedOwners)
                    {
                        if (other == owner) continue;
                        AutonomyQueries++;
                        if (!militaryBins.TryGetValue((center + new int2(x, z), other), out var candidate) ||
                            (candidate == ai.FailedTarget && tick < ai.RetryTargetAt) ||
                            !IsCombatTarget(manager, candidate, owner) ||
                            !WithinGuardLimit(manager, candidate, ai, position, range)) continue;
                        double distance = math.distancesq(position, manager.GetComponentData<LogicalPose>(candidate).Position.xz);
                        if (distance > nearest) continue;
                        nearest = distance;
                        target = candidate;
                    }
            AutonomyDecisionMs += MillisecondsSince(started);
            if (target != Entity.Null)
            {
                long execution = Stopwatch.GetTimestamp();
                var result = OrderAttack(manager, target, owner, handles);
                AutonomyExecutionMs += MillisecondsSince(execution);
                if (result == UnitOrderResult.Accepted) return AutonomyState.Chase;
            }
            return idle;
        }

        private bool WithinGuardLimit(EntityManager manager, Entity target, UnitAutonomy ai, double2 position, double range)
        {
            var point = manager.GetComponentData<LogicalPose>(target).Position.xz;
            return ai.Mode == AutonomyMode.Guard
                ? math.distancesq(point, ai.Anchor + new double2(.5)) <= guardLeash * guardLeash &&
                    math.distancesq(position, ai.Anchor + new double2(.5)) <= guardLeash * guardLeash
                : math.distancesq(point, position) <= range * range && ClearNavigationSegment(position, point);
        }


    }

    [DisableAutoCreation]
    [UpdateInGroup(typeof(FixedStepSimulationSystemGroup))]
    [UpdateBefore(typeof(UnitCombatSystem))]
    public partial class UnitAutonomySystem : SystemBase
    {
        internal UnitSession Session;
        protected override void OnUpdate() => Session.TickAutonomy();
    }
}
