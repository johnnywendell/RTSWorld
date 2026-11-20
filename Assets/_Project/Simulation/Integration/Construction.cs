using System;
using System.Collections.Generic;
using System.Diagnostics;
using RTSWorld.World.Environment;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace RTSWorld.Simulation
{
    public enum BuildingKind : byte { Depot, MainCenter, Workshop }
    public enum ConstructionState : byte { Planned, UnderConstruction, Completed, Destroyed }

    [Flags]
    public enum PlacementIssue
    {
        None = 0, Bounds = 1, Water = 2, Slope = 4, Obstacle = 8,
        Building = 16, Unit = 32, Access = 64, Cost = 128
    }

    public readonly struct BuildingDefinition
    {
        public readonly string Name;
        public readonly int2 Size;
        public readonly int Wood, Stone, Work, Health;
        public readonly bool ReceivesResources;

        private BuildingDefinition(string name, int x, int z, int wood, int stone, int work, int health, bool receives)
        {
            Name = name;
            Size = new int2(x, z);
            Wood = wood;
            Stone = stone;
            Work = work;
            Health = health;
            ReceivesResources = receives;
        }

        public static BuildingDefinition Get(BuildingKind kind) => kind switch
        {
            BuildingKind.Depot => new("Depósito", 2, 2, 20, 10, 8, 160, true),
            BuildingKind.MainCenter => new("Centro principal", 3, 3, 50, 30, 16, 400, false),
            BuildingKind.Workshop => new("Oficina", 2, 3, 15, 5, 6, 100, false),
            _ => throw new ArgumentOutOfRangeException(nameof(kind))
        };
    }

    public struct Building : IComponentData
    {
        public BuildingKind Kind;
        public ConstructionState State;
        public int Owner, Health;
        public int2 Origin;
        public double Work;
    }

    public sealed partial class UnitSession
    {
        private readonly Dictionary<Entity, Entity> buildTargets = new();
        private readonly List<Entity> finishedBuilders = new();
        private readonly List<Entity> buildings = new();
        private bool physicalDepots;
        private uint depotRevision;
        public IReadOnlyList<Entity> Buildings => buildings;
        public double PlacementMilliseconds { get; private set; }
        public double FootprintMilliseconds { get; private set; }
        public double ConstructionMilliseconds { get; private set; }
        public double LastDepotSearchMilliseconds { get; private set; }
        public bool UsesPhysicalDepots => physicalDepots;

        public Entity SpawnMainCenter(int2 near, int owner, bool preservePassages = false) =>
            SpawnStartingBuilding(BuildingKind.MainCenter, near, owner, preservePassages);

        private Entity SpawnStartingBuilding(BuildingKind kind, int2 near, int owner, bool preservePassages)
        {
            // A dense starting population can fill the original search area.
            int searchRadius = preservePassages ? 32 : 16;
            for (int radius = 0; radius <= searchRadius; radius++)
                for (int z = -radius; z <= radius; z++)
                    for (int x = -radius; x <= radius; x++)
                    {
                        if (math.max(math.abs(x), math.abs(z)) != radius) continue;
                        Entity entity = CreateBuilding(kind, near + new int2(x, z), owner, true, preservePassages);
                        if (entity != Entity.Null) return entity;
                    }
            return Entity.Null;
        }

        public bool EnablePhysicalDepots(int2 near, int owner, bool preservePassages = false)
        {
            if (!EconomyEnabled || physicalDepots) return false;
            Entity entity = SpawnStartingBuilding(BuildingKind.Depot, near, owner, preservePassages);
            if (entity == Entity.Null) return false;
            physicalDepots = true;
            var depot = World.EntityManager.GetComponentData<Building>(entity);
            TryBuildingAccess(depot.Origin, BuildingDefinition.Get(depot.Kind).Size, out int2 access);
            DepotCell = access;
            return true;
        }

        private bool TryDeliveryCell(int owner, int2 source, out int2 destination)
        {
            if (!issuingAutomaticOrder || !physicalDepots)
                return FindDeliveryCell(owner, source, out destination);
            if (!depotBySource.TryGetValue((owner, source), out var result))
            {
                bool found = FindDeliveryCell(owner, source, out var cell);
                result = (found, cell);
                depotBySource.Add((owner, source), result);
            }
            destination = result.Cell;
            return result.Found;
        }

        private bool FindDeliveryCell(int owner, int2 source, out int2 destination)
        {
            destination = DepotCell;
            if (!physicalDepots)
                return navigationGrid.TryGetCost(destination, out int cost) && cost > 0;

            long started = Stopwatch.GetTimestamp();
            var manager = World.EntityManager;
            int distance = int.MaxValue;
            bool bestFull = false;
            foreach (Entity entity in buildings)
            {
                var building = manager.GetComponentData<Building>(entity);
                var definition = BuildingDefinition.Get(building.Kind);
                if (building.Owner != owner || building.State != ConstructionState.Completed ||
                    !definition.ReceivesResources) continue;
                var size = definition.Size;
                for (int z = -1; z <= size.y; z++)
                    for (int x = -1; x <= size.x; x++)
                    {
                        bool edgeX = x == -1 || x == size.x;
                        bool edgeZ = z == -1 || z == size.y;
                        if (edgeX == edgeZ) continue;
                        int2 cell = building.Origin + new int2(x, z);
                        int candidate = math.csum(math.abs(cell - source));
                        if (!navigationGrid.TryGetCost(cell, out int cost) || cost == 0) continue;
                        bool full = navigationGrid.IsFullPassage(cell);
                        if ((bestFull && !full) || (bestFull == full && candidate >= distance) ||
                            !navigationGrid.Connected(source, cell)) continue;
                        destination = cell;
                        distance = candidate;
                        bestFull = full;
                    }
            }
            LastDepotSearchMilliseconds = MillisecondsSince(started);
            return distance != int.MaxValue;
        }

        private bool IsDeliveryCell(int owner, int2 cell)
        {
            if (!physicalDepots) return cell.Equals(DepotCell);
            if (!navigationGrid.TryGetCost(cell, out int cost) || cost == 0) return false;
            var manager = World.EntityManager;
            foreach (Entity entity in buildings)
            {
                var building = manager.GetComponentData<Building>(entity);
                var definition = BuildingDefinition.Get(building.Kind);
                if (building.Owner != owner || building.State != ConstructionState.Completed ||
                    !definition.ReceivesResources) continue;
                int2 offset = cell - building.Origin;
                bool edgeX = (offset.x == -1 || offset.x == definition.Size.x) &&
                    offset.y >= 0 && offset.y < definition.Size.y;
                bool edgeZ = (offset.y == -1 || offset.y == definition.Size.y) &&
                    offset.x >= 0 && offset.x < definition.Size.x;
                if (edgeX || edgeZ) return true;
            }
            return false;
        }

        public PlacementIssue ValidateBuilding(BuildingKind kind, int2 origin, int owner)
        {
            long started = Stopwatch.GetTimestamp();
            var result = CheckBuilding(BuildingDefinition.Get(kind), origin, owner, true);
            PlacementMilliseconds = MillisecondsSince(started);
            return result;
        }

        private PlacementIssue CheckBuilding(BuildingDefinition definition, int2 origin, int owner, bool payment)
        {
            if (navigationGrid == null || !EconomyEnabled || owner < 0 ||
                math.any(origin < navigationGrid.Min) ||
                math.any(origin > navigationGrid.Min + navigationGrid.Size - definition.Size))
                return PlacementIssue.Bounds;

            PlacementIssue issue = PlacementIssue.None;
            for (int z = 0; z < definition.Size.y; z++)
                for (int x = 0; x < definition.Size.x; x++)
                    issue |= navigationGrid.GetPlacementIssue(origin + new int2(x, z));
            if (!TryBuildingAccess(origin, definition.Size, out _)) issue |= PlacementIssue.Access;
            if (payment && (GetStock(owner, NaturalResource.Tree) < definition.Wood ||
                GetStock(owner, NaturalResource.Stone) < definition.Stone)) issue |= PlacementIssue.Cost;
            if ((issue & ~PlacementIssue.Cost) != PlacementIssue.None) return issue;

            CompleteJobs();
            using var poses = units.ToComponentDataArray<LogicalPose>(Allocator.Temp);
            foreach (var pose in poses)
                if (math.all(pose.Position.xz >= origin) && math.all(pose.Position.xz < origin + definition.Size))
                    return issue | PlacementIssue.Unit;
            return issue;
        }

        private bool TryBuildingAccess(int2 origin, int2 size, out int2 access, int2? source = null)
        {
            for (int z = -1; z <= size.y; z++)
                for (int x = -1; x <= size.x; x++)
                {
                    bool edgeX = x == -1 || x == size.x;
                    bool edgeZ = z == -1 || z == size.y;
                    if (edgeX == edgeZ) continue;
                    access = origin + new int2(x, z);
                    if (navigationGrid.TryGetCost(access, out int cost) && cost > 0 &&
                        (!source.HasValue || navigationGrid.Connected(source.Value, access))) return true;
                }
            access = default;
            return false;
        }

        public Entity PlaceBuilding(BuildingKind kind, int2 origin, int owner, bool preservePassages = false)
        {
            return CreateBuilding(kind, origin, owner, false, preservePassages);
        }

        private Entity CreateBuilding(BuildingKind kind, int2 origin, int owner, bool starting, bool preservePassages)
        {
            var definition = BuildingDefinition.Get(kind);
            if (CheckBuilding(definition, origin, owner, !starting) != PlacementIssue.None) return Entity.Null;
            if (preservePassages && !navigationGrid.PreservesPassages(origin, definition.Size)) return Entity.Null;
            var manager = World.EntityManager;
            Entity entity = manager.CreateEntity(typeof(Building), typeof(LogicalEntityId));
            manager.SetComponentData(entity, new LogicalEntityId { Value = nextId++ });
            manager.SetComponentData(entity, new Building
            {
                Kind = kind, Origin = origin, Owner = owner, Health = definition.Health,
                State = starting ? ConstructionState.Completed : ConstructionState.Planned,
                Work = starting ? definition.Work : 0
            });
            buildings.Add(entity);
            if (!starting) ChangeConstructionStock(owner, definition, -1);
            UpdateBuildingFootprint(origin, definition.Size, true);
            return entity;
        }

        private void ChangeConstructionStock(int owner, BuildingDefinition definition, int sign)
        {
            stock[(owner, NaturalResource.Tree)] = GetStock(owner, NaturalResource.Tree) + sign * definition.Wood;
            stock[(owner, NaturalResource.Stone)] = GetStock(owner, NaturalResource.Stone) + sign * definition.Stone;
        }

        private void UpdateBuildingFootprint(int2 origin, int2 size, bool occupied)
        {
            long started = Stopwatch.GetTimestamp();
            LastInvalidatedFields = 0;
            navigationGrid.SetStructure(origin, size, occupied);
            depotRevision++;
            PublishCrowdWalkability();
            InvalidateEnvironmentalFields();
            FootprintMilliseconds = MillisecondsSince(started);
        }

        public bool CancelBuilding(Entity entity, int owner)
        {
            CompleteJobs();
            var manager = World.EntityManager;
            if (!manager.Exists(entity) || !manager.HasComponent<Building>(entity)) return false;
            var building = manager.GetComponentData<Building>(entity);
            if (building.Owner != owner || building.State is ConstructionState.Completed or ConstructionState.Destroyed) return false;
            if (building.State == ConstructionState.Planned)
                ChangeConstructionStock(owner, BuildingDefinition.Get(building.Kind), 1);
            RemoveBuilding(manager, entity, building);
            return true;
        }

        public bool DamageBuilding(Entity entity, int damage)
        {
            if (damage <= 0) return false;
            CompleteJobs();
            var manager = World.EntityManager;
            if (!manager.Exists(entity) || !manager.HasComponent<Building>(entity)) return false;
            var building = manager.GetComponentData<Building>(entity);
            if (building.State == ConstructionState.Destroyed) return false;
            building.Health = math.max(0, building.Health - damage);
            if (building.Health == 0) RemoveBuilding(manager, entity, building);
            else manager.SetComponentData(entity, building);
            return true;
        }

        private void RemoveBuilding(EntityManager manager, Entity entity, Building building)
        {
            building.Health = 0;
            building.State = ConstructionState.Destroyed;
            manager.SetComponentData(entity, building);
            buildings.Remove(entity);
            UpdateBuildingFootprint(building.Origin, BuildingDefinition.Get(building.Kind).Size, false);
        }

        private UnitOrderResult OrderBuild(EntityManager manager, Entity target, int owner, NativeArray<Entity> entities)
        {
            if (!manager.Exists(target) || !manager.HasComponent<Building>(target)) return UnitOrderResult.InvalidTarget;
            var building = manager.GetComponentData<Building>(target);
            if (building.Owner != owner || building.State is ConstructionState.Completed or ConstructionState.Destroyed)
                return UnitOrderResult.InvalidTarget;
            int assigned = 0;
            var size = BuildingDefinition.Get(building.Kind).Size;
            foreach (Entity entity in entities)
            {
                if (manager.GetComponentData<UnitIdentity>(entity).Kind != UnitKind.Villager) continue;
                var cell = (int2)math.floor(manager.GetComponentData<LogicalPose>(entity).Position.xz);
                if (!TryBuildingAccess(building.Origin, size, out var destination, cell) ||
                    !AcquireField(destination, null, out int slot)) continue;
                CancelGather(manager, entity);
                CancelAttack(manager, entity);
                manager.GetBuffer<FormationWaypoint>(entity).Clear();
                SetMovementField(manager, entity, slot, false, destination);
                buildTargets[entity] = target;
                MarkManual(manager, entity);
                assigned++;
            }
            return assigned == 0 ? UnitOrderResult.Unreachable : UnitOrderResult.Accepted;
        }

        private void CancelBuild(Entity entity) => buildTargets.Remove(entity);

        internal void TickConstruction(float seconds)
        {
            if (buildTargets.Count == 0)
            {
                ConstructionMilliseconds = 0;
                return;
            }
            long started = Stopwatch.GetTimestamp();
            CompleteJobs();
            var manager = World.EntityManager;
            finishedBuilders.Clear();
            foreach (var job in buildTargets)
            {
                if (!manager.Exists(job.Key)) { finishedBuilders.Add(job.Key); continue; }
                if (!manager.Exists(job.Value) || !manager.HasComponent<Building>(job.Value))
                {
                    var idle = default(GatherWorker);
                    FinishWorker(manager, job.Key, ref idle, GatherPhase.Idle);
                    finishedBuilders.Add(job.Key);
                    continue;
                }
                var building = manager.GetComponentData<Building>(job.Value);
                var motion = manager.GetComponentData<UnitMotion>(job.Key);
                if (building.State is not (ConstructionState.Completed or ConstructionState.Destroyed) &&
                    motion.Activity is UnitActivity.WaitingForNavigation or UnitActivity.Unreachable)
                {
                    var cell = (int2)math.floor(manager.GetComponentData<LogicalPose>(job.Key).Position.xz);
                    if (TryBuildingAccess(building.Origin, BuildingDefinition.Get(building.Kind).Size, out var access, cell) &&
                        AcquireField(access, null, out int slot))
                    {
                        SetMovementField(manager, job.Key, slot, false, access);
                        continue;
                    }
                }
                if (building.State is ConstructionState.Completed or ConstructionState.Destroyed ||
                    motion.Activity is UnitActivity.WaitingForNavigation or UnitActivity.Unreachable)
                {
                    var idle = default(GatherWorker);
                    FinishWorker(manager, job.Key, ref idle, GatherPhase.Idle);
                    finishedBuilders.Add(job.Key);
                    continue;
                }
                if (motion.Field != 0 || motion.Activity == UnitActivity.WaitingForTerrain) continue;
                double2 point = manager.GetComponentData<LogicalPose>(job.Key).Position.xz;
                double2 goal = motion.InteractionCell + new double2(.5);
                if (math.distancesq(point, goal) > UnitMovementSystem.InteractionRadiusSquared ||
                    !ClearNavigationSegment(point, goal)) continue;
                int required = BuildingDefinition.Get(building.Kind).Work;
                building.Work = math.min(required, building.Work + seconds);
                building.State = building.Work >= required ? ConstructionState.Completed : ConstructionState.UnderConstruction;
                manager.SetComponentData(job.Value, building);
                if (building.State == ConstructionState.Completed)
                {
                    if (IsAutomatic(manager, job.Key)) AutonomousBuildings++;
                    if (BuildingDefinition.Get(building.Kind).ReceivesResources) depotRevision++;
                }
            }
            foreach (Entity entity in finishedBuilders) buildTargets.Remove(entity);
            ConstructionMilliseconds = MillisecondsSince(started);
        }
    }

    [DisableAutoCreation]
    [UpdateInGroup(typeof(FixedStepSimulationSystemGroup))]
    [UpdateAfter(typeof(UnitMovementSystem))]
    public partial class ConstructionSystem : SystemBase
    {
        internal UnitSession Session;
        protected override void OnUpdate() => Session.TickConstruction(SystemAPI.Time.DeltaTime);
    }
}
