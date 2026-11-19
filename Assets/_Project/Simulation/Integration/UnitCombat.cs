using System;
using System.Collections.Generic;
using System.Diagnostics;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace RTSWorld.Simulation
{
    public enum CombatPhase : byte { Idle, Approaching, Attacking, Blocked }

    public struct UnitCombat : IComponentData
    {
        public Entity Target;
        public CombatPhase Phase;
        public double Interval, NextStrike, NextRoute;
        public double HitUntil, StrikeUntil;
    }

    public sealed partial class UnitSession
    {
        private readonly List<Entity> attackers = new();
        private readonly HashSet<Entity> scheduledAttackers = new();
        private readonly Dictionary<Entity, (int Faction, double2 Position)> liveTargets = new();
        private readonly Dictionary<Entity, double> pendingDamage = new();
        private readonly List<Entity> defeated = new();
        private double combatTime;
        public double CombatTime => combatTime;
        public int EngagedUnits => attackers.Count;
        public int InRangeUnits { get; private set; }
        public int DefeatedUnits { get; private set; }
        public double TargetMilliseconds { get; private set; }
        public double PursuitMilliseconds { get; private set; }
        public double StrikeMilliseconds { get; private set; }
        public double DamageMilliseconds { get; private set; }
        public double DeathMilliseconds { get; private set; }

        public static bool AreHostile(int faction, int otherFaction) => faction != otherFaction;

        private bool IsCombatTarget(EntityManager manager, Entity entity, int faction) =>
            manager.Exists(entity) && manager.HasComponent<UnitCombat>(entity) &&
            manager.GetComponentData<UnitHealth>(entity).Current > 0 &&
            AreHostile(faction, manager.GetComponentData<UnitIdentity>(entity).Owner);

        private UnitOrderResult OrderAttack(EntityManager manager, Entity target, int faction, NativeArray<Entity> entities)
        {
            if (!IsCombatTarget(manager, target, faction)) return UnitOrderResult.InvalidTarget;
            int accepted = 0;
            foreach (var entity in entities)
            {
                if (manager.GetComponentData<UnitIdentity>(entity).Kind != UnitKind.Soldier ||
                    manager.GetComponentData<UnitHealth>(entity).Current <= 0) continue;
                var stats = manager.GetComponentData<UnitAttributes>(entity);
                if (stats.Attack <= 0 || stats.Range <= 0) continue;
                var combat = manager.GetComponentData<UnitCombat>(entity);
                if (combat.Target != target)
                {
                    HoldPosition(manager, entity);
                    combat.Target = target;
                    combat.Phase = CombatPhase.Approaching;
                    combat.NextRoute = combatTime;
                    manager.SetComponentData(entity, combat);
                }
                if (scheduledAttackers.Add(entity)) attackers.Add(entity);
                accepted++;
            }
            return accepted > 0 ? UnitOrderResult.Accepted : UnitOrderResult.Empty;
        }

        private void CancelAttack(EntityManager manager, Entity entity)
        {
            var combat = manager.GetComponentData<UnitCombat>(entity);
            if (combat.Target == Entity.Null) return;
            combat.Target = Entity.Null;
            combat.Phase = CombatPhase.Idle;
            manager.SetComponentData(entity, combat);
        }

        private static void HoldPosition(EntityManager manager, Entity entity)
        {
            var motion = manager.GetComponentData<UnitMotion>(entity);
            motion.Field = 0;
            motion.HasFormation = false;
            motion.DesiredVelocity = default;
            manager.SetComponentData(entity, motion);
            manager.GetBuffer<FormationWaypoint>(entity).Clear();
        }

        internal void TickCombat(float seconds)
        {
            combatTime += seconds;
            InRangeUnits = 0;
            if (attackers.Count == 0 || !movement.TerrainAvailable)
            {
                TargetMilliseconds = PursuitMilliseconds = StrikeMilliseconds = DamageMilliseconds = DeathMilliseconds = 0;
                return;
            }
            CompleteJobs();
            var manager = World.EntityManager;
            long started = Stopwatch.GetTimestamp();
            PruneAttackers(manager);
            TargetMilliseconds = MillisecondsSince(started);

            started = Stopwatch.GetTimestamp();
            bool requestedField = false;
            foreach (var entity in attackers)
                UpdatePursuit(manager, entity, ref requestedField);
            PursuitMilliseconds = MillisecondsSince(started);

            started = Stopwatch.GetTimestamp();
            pendingDamage.Clear();
            foreach (var entity in attackers)
            {
                var combat = manager.GetComponentData<UnitCombat>(entity);
                if (combat.Phase != CombatPhase.Attacking) continue;
                InRangeUnits++;
                if (combatTime + seconds * 1e-6 < combat.NextStrike) continue;
                pendingDamage.TryGetValue(combat.Target, out double accumulated);
                pendingDamage[combat.Target] = accumulated + manager.GetComponentData<UnitAttributes>(entity).Attack;
                combat.NextStrike = combatTime + combat.Interval;
                combat.StrikeUntil = combatTime + .18;
                manager.SetComponentData(entity, combat);
            }
            StrikeMilliseconds = MillisecondsSince(started);

            started = Stopwatch.GetTimestamp();
            defeated.Clear();
            foreach (var hit in pendingDamage)
            {
                var health = manager.GetComponentData<UnitHealth>(hit.Key);
                health.Current = (float)math.max(0, health.Current - hit.Value);
                manager.SetComponentData(hit.Key, health);
                var combat = manager.GetComponentData<UnitCombat>(hit.Key);
                combat.HitUntil = combatTime + .25;
                if (EconomyEnabled)
                {
                    var worker = manager.GetComponentData<GatherWorker>(hit.Key);
                    if (worker.Phase is GatherPhase.Approaching or GatherPhase.Gathering or GatherPhase.Delivering)
                    {
                        CancelGather(manager, hit.Key);
                        HoldPosition(manager, hit.Key);
                    }
                }
                manager.SetComponentData(hit.Key, combat);
                if (health.Current == 0) defeated.Add(hit.Key);
            }
            pendingDamage.Clear();
            DamageMilliseconds = MillisecondsSince(started);

            started = Stopwatch.GetTimestamp();
            if (defeated.Count != 0)
            {
                using var dead = new NativeArray<Entity>(defeated.Count, Allocator.Temp);
                var handles = dead;
                for (int i = 0; i < defeated.Count; i++) handles[i] = defeated[i];
                DestroyBatch(dead);
                DefeatedUnits += defeated.Count;
                defeated.Clear();
            }
            DeathMilliseconds = MillisecondsSince(started);
        }

        private void PruneAttackers(EntityManager manager)
        {
            liveTargets.Clear();
            int kept = 0;
            for (int i = 0; i < attackers.Count; i++)
            {
                var entity = attackers[i];
                if (manager.Exists(entity))
                {
                    var combat = manager.GetComponentData<UnitCombat>(entity);
                    if (combat.Target != Entity.Null)
                    {
                        int owner = manager.GetComponentData<UnitIdentity>(entity).Owner;
                        if (manager.GetComponentData<UnitHealth>(entity).Current > 0 &&
                            TryResolveTarget(manager, combat.Target, owner))
                        {
                            attackers[kept++] = entity;
                            continue;
                        }
                        CancelAttack(manager, entity);
                        HoldPosition(manager, entity);
                    }
                }
                scheduledAttackers.Remove(entity);
            }
            attackers.RemoveRange(kept, attackers.Count - kept);
        }

        private bool TryResolveTarget(EntityManager manager, Entity entity, int faction)
        {
            if (!liveTargets.TryGetValue(entity, out var target))
            {
                if (!manager.Exists(entity) || !manager.HasComponent<UnitCombat>(entity) ||
                    manager.GetComponentData<UnitHealth>(entity).Current <= 0) return false;
                target = (manager.GetComponentData<UnitIdentity>(entity).Owner,
                    manager.GetComponentData<LogicalPose>(entity).Position.xz);
                liveTargets.Add(entity, target);
            }
            return AreHostile(faction, target.Faction);
        }

        private void UpdatePursuit(EntityManager manager, Entity entity, ref bool requestedField)
        {
            var combat = manager.GetComponentData<UnitCombat>(entity);
            double2 origin = manager.GetComponentData<LogicalPose>(entity).Position.xz;
            double2 target = liveTargets[combat.Target].Position;
            double range = manager.GetComponentData<UnitAttributes>(entity).Range;
            if (math.distancesq(origin, target) <= range * range && ClearAttackSegment(origin, target))
            {
                if (combat.Phase != CombatPhase.Attacking) HoldPosition(manager, entity);
                combat.Phase = CombatPhase.Attacking;
                combat.NextRoute = combatTime;
            }
            else if (combatTime >= combat.NextRoute)
            {
                int2 destination = (int2)math.floor(target);
                int2 current = (int2)math.floor(origin);
                bool cached = navigationSlots.TryGetValue(destination, out int slot);
                if (navigationGrid == null || !navigationGrid.TryGetCost(destination, out int cost) || cost == 0)
                {
                    HoldPosition(manager, entity);
                    combat.Phase = CombatPhase.Blocked;
                    combat.NextRoute = combatTime + .5;
                }
                else
                {
                    var motion = manager.GetComponentData<UnitMotion>(entity);
                    if (!cached && motion.Field != 0) HoldPosition(manager, entity);
                    combat.Phase = CombatPhase.Approaching;
                    if (cached || !requestedField)
                    {
                        requestedField |= !cached;
                        bool available = cached || AcquireField(destination, null, out slot);
                        bool reachable = available && sharedFields[slot].TryGetDirection(current, out _);
                        if (reachable)
                        {
                            if (motion.Field != slot + 1 || motion.NavigationRevision != navigationRevision || !cached)
                                SetMovementField(manager, entity, slot, false);
                        }
                        else if (cached) HoldPosition(manager, entity);
                        combat.Phase = reachable ? CombatPhase.Approaching : CombatPhase.Blocked;
                        combat.NextRoute = combatTime + (reachable ? .35 : .5);
                    }
                }
            }
            manager.SetComponentData(entity, combat);
        }

        private bool ClearAttackSegment(double2 from, double2 to)
        {
            if (navigationGrid == null) return false;
            int2 cell = (int2)math.floor(from);
            int2 end = (int2)math.floor(to);
            double2 delta = to - from;
            int2 step = (int2)math.sign(delta);
            double2 crossing = new(
                step.x == 0 ? double.PositiveInfinity : (cell.x + (step.x > 0 ? 1 : 0) - from.x) / delta.x,
                step.y == 0 ? double.PositiveInfinity : (cell.y + (step.y > 0 ? 1 : 0) - from.y) / delta.y);
            if (!Walkable(cell)) return false;
            while (math.any(cell != end))
            {
                double x = cell.x == end.x ? double.PositiveInfinity : crossing.x;
                double z = cell.y == end.y ? double.PositiveInfinity : crossing.y;
                if (x == z && (!Walkable(cell + new int2(step.x, 0)) || !Walkable(cell + new int2(0, step.y))))
                    return false;
                if (x <= z) { cell.x += step.x; crossing.x += math.abs(1 / delta.x); }
                if (z <= x) { cell.y += step.y; crossing.y += math.abs(1 / delta.y); }
                if (!Walkable(cell)) return false;
            }
            return true;

            bool Walkable(int2 test) => navigationGrid.TryGetCost(test, out int cost) && cost > 0;
        }
    }

    [DisableAutoCreation]
    [UpdateInGroup(typeof(FixedStepSimulationSystemGroup))]
    [UpdateBefore(typeof(UnitMovementSystem))]
    public partial class UnitCombatSystem : SystemBase
    {
        internal UnitSession Session;
        protected override void OnUpdate() => Session.TickCombat(SystemAPI.Time.DeltaTime);
    }
}
