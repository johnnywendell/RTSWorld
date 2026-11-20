using System;
using System.Collections.Generic;
using Unity.Collections;
using RTSWorld.World.Environment;
using Unity.Core;
using Unity.Entities;
using Unity.Mathematics;

namespace RTSWorld.Simulation
{
    /// <summary>Logical unit lifetime. No renderer, camera, collider or visual residency dependency.</summary>
    public sealed partial class UnitSession : IDisposable
    {
        public Unity.Entities.World World { get; }
        public float FixedDeltaTime { get; }
        public bool Paused { get; set; }
        public UnitBounds Bounds { get; }
        public ulong Tick => World.EntityManager.GetComponentData<SimulationClock>(settings).Tick;
        public int Count => units.CalculateEntityCount();
        private readonly FixedStepSimulationSystemGroup group;
        private readonly UnitMovementSystem movement;
        private readonly Entity settings;
        private readonly EntityArchetype archetype;
        private EntityQuery units;
        private NativeArray<double> heights;
        private ulong nextId = 1;
        private readonly ProceduralTerrainContract proceduralTerrain;
        private double elapsed;
        private bool terrainAvailable;
        private readonly int width;
        private const int NavigationCapacity = 8;
        private readonly Dictionary<int2, int> navigationSlots = new();
        private readonly int2[] fieldDestinations = new int2[NavigationCapacity];
        private readonly NavigationFlowField[] sharedFields = new NavigationFlowField[NavigationCapacity];
        private NavigationGrid navigationGrid;
        private uint navigationRevision;
        private NativeArray<int> navigationNext;
        private NativeArray<byte> crowdWalkable;
        private readonly bool crowdEnabled;

        public bool CrowdEnabled => crowdEnabled;
        public bool RecoveryEnabled
        {
            get => movement.RecoveryEnabled;
            set => movement.RecoveryEnabled = value;
        }
        public bool MeasureCrowdJobs { get => movement.MeasureCrowdJobs; set => movement.MeasureCrowdJobs = value; }
        public double SpatialMilliseconds => movement.SpatialMilliseconds;
        public double MovementMilliseconds => movement.MovementMilliseconds;

        public long FieldHits { get; private set; }
        public long FieldMisses { get; private set; }
        public long FieldReplacements { get; private set; }
        public long FieldCapacityDenials { get; private set; }
        public long FieldBuilds { get; private set; }
        public double FieldBuildMilliseconds { get; private set; }
        private int NavigationArea => (width - 1) * ((int)(Bounds.Max.y - Bounds.Min.y));

        public UnitSession(ProceduralTerrainContract terrain, int2 min, int2 max, float fixedDeltaTime = 0.05f, bool crowdEnabled = false)
            : this(terrain, min, max, fixedDeltaTime, crowdEnabled, false) { }

        private UnitSession(ProceduralTerrainContract terrain, int2 min, int2 max, float fixedDeltaTime,
            bool crowdEnabled, bool hasAlreadyTicked)
        {
            if (math.any(max <= min) || math.any(max - (double2)min > 256) ||
                math.any(math.abs((double2)min) > 1000000) || math.any(math.abs((double2)max) > 1000000))
                throw new ArgumentOutOfRangeException(nameof(max), "Finite integer area, at most 256 cells per axis, within +/-1000000.");
            if (!math.isfinite(fixedDeltaTime) || fixedDeltaTime < 0.001f || fixedDeltaTime > 1)
                throw new ArgumentOutOfRangeException(nameof(fixedDeltaTime));
            FixedDeltaTime = fixedDeltaTime;
            proceduralTerrain = terrain;
            this.crowdEnabled = crowdEnabled;
            Bounds = new UnitBounds { Min = min, Max = max };
            width = max.x - min.x + 1;
            heights = new NativeArray<double>(width * (max.y - min.y + 1), Allocator.Persistent);
            try
            {
                if (terrain != null)
                    for (int z = min.y; z <= max.y; z++)
                        for (int x = min.x; x <= max.x; x++)
                            heights[x-min.x + (z-min.y)*width] = terrain.SampleVertex(x,z,out _);
                terrainAvailable = terrain != null;
                World = new Unity.Entities.World("RTSWorld Units");
                var em = World.EntityManager;
                settings = em.CreateEntity(typeof(SimulationConfiguration), typeof(SimulationClock), typeof(UnitBounds));
                em.SetComponentData(settings, new SimulationConfiguration { FixedDeltaTime = fixedDeltaTime,
                    CellsPerChunk = terrain?.Definition.CellsPerChunk ?? 32 });
                em.SetComponentData(settings, Bounds);
                archetype = em.CreateArchetype(typeof(LogicalEntityId), typeof(LogicalPose), typeof(LogicalRegion),
                    typeof(MovementVelocity), typeof(EntitySimulationState), typeof(UnitIdentity), typeof(UnitHealth),
                    typeof(UnitAttributes), typeof(UnitMotion), typeof(FormationWaypoint), typeof(GatherWorker), typeof(UnitCombat));
                units = em.CreateEntityQuery(typeof(UnitIdentity), typeof(LogicalPose), typeof(UnitMotion));
                group = World.GetOrCreateSystemManaged<FixedStepSimulationSystemGroup>();
                group.Timestep = fixedDeltaTime;
                // Initialize the rate manager at t=0 before installing simulation systems.
                if (hasAlreadyTicked) group.Update();
                movement = World.GetOrCreateSystemManaged<UnitMovementSystem>();
                movement.Heights = heights; movement.GridMin = min; movement.GridWidth = width;
                movement.TerrainAvailable = terrainAvailable;
                navigationNext = new NativeArray<int>(1, Allocator.Persistent);
                movement.NavigationNext = navigationNext;
                movement.CrowdEnabled = crowdEnabled;
                crowdWalkable = new NativeArray<byte>(NavigationArea, Allocator.Persistent);
                movement.CrowdWalkable = crowdWalkable;
                group.AddSystemToUpdateList(movement);
                var combat = World.GetOrCreateSystemManaged<UnitCombatSystem>();
                combat.Session = this;
                group.AddSystemToUpdateList(combat);
                var construction = World.GetOrCreateSystemManaged<ConstructionSystem>();
                construction.Session = this;
                group.AddSystemToUpdateList(construction);
                autonomousUnits = em.CreateEntityQuery(typeof(UnitAutonomy));
                var autonomy = World.GetOrCreateSystemManaged<UnitAutonomySystem>();
                autonomy.Session = this;
                group.AddSystemToUpdateList(autonomy);
                group.SortSystems();
            }
            catch
            {
                if (World != null && World.IsCreated) World.Dispose();
                if (navigationNext.IsCreated) navigationNext.Dispose();
                if (crowdWalkable.IsCreated) crowdWalkable.Dispose();
                heights.Dispose();
                throw;
            }
        }

        public void SetTerrainAvailable(bool available)
        {
            CompleteJobs();
            // A missing snapshot cannot be made valid by flipping availability.
            if (available && !terrainAvailable) throw new InvalidOperationException("No terrain snapshot was supplied.");
            movement.TerrainAvailable = available;
        }

        public NativeArray<Entity> SpawnBatch(UnitDefinition definition, int owner, int count,
            double3 origin, double2 desiredVelocity, double spacing = 1.5)
        {
            definition.Validate();
            if (owner < 0 || count < 1 || count > 100000 || !math.all(math.isfinite(origin)) ||
                !math.all(math.isfinite(desiredVelocity)) || !math.isfinite(spacing) || spacing <= 0)
                throw new ArgumentOutOfRangeException(nameof(count));
            int columns = (int)math.ceil(math.sqrt(count));
            double2 last = origin.xz + new double2(math.min(columns, count)-1, (count-1)/columns)*spacing;
            if (math.any(origin.xz < Bounds.Min) || math.any(last > Bounds.Max))
                throw new ArgumentOutOfRangeException(nameof(origin), "Spawn grid must fit logical bounds.");
            if ((ulong)count > ulong.MaxValue-nextId) throw new InvalidOperationException("Identity range exhausted.");
            CompleteJobs();
            var entities = new NativeArray<Entity>(count, Allocator.Temp);
            var em = World.EntityManager;
            em.CreateEntity(archetype, entities);
            int cells = em.GetComponentData<SimulationConfiguration>(settings).CellsPerChunk;
            for (int i=0; i<count; i++)
            {
                double3 p = origin + new double3(i%columns*spacing, 0, i/columns*spacing);
                InitializeUnit(em, entities[i], definition, owner, p, desiredVelocity, cells);
            }
            return entities;
        }

        public void SpawnNavigable(UnitDefinition definition, int owner, NativeArray<int2> cells)
        {
            definition.Validate();
            if (owner < 0 || (ulong)cells.Length > ulong.MaxValue - nextId)
                throw new ArgumentOutOfRangeException(nameof(owner));
            if (cells.Length == 0) return;
            foreach (int2 cell in cells)
                if (navigationGrid == null || !navigationGrid.TryGetCost(cell, out int cost) || cost == 0)
                    throw new ArgumentException("Spawn cells must be traversable.", nameof(cells));

            CompleteJobs();
            using var entities = new NativeArray<Entity>(cells.Length, Allocator.Temp);
            var manager = World.EntityManager;
            manager.CreateEntity(archetype, entities);
            for (int i = 0; i < cells.Length; i++)
                InitializeUnit(manager, entities[i], definition, owner,
                    new double3(cells[i].x + .5, 0, cells[i].y + .5), default, navigationGrid.CellsPerChunk);
        }

        private void InitializeUnit(EntityManager em, Entity e, UnitDefinition definition, int owner,
            double3 position, double2 desiredVelocity, int cellsPerChunk)
        {
            if (movement.TerrainAvailable) position.y = Sample(position.xz);
            em.SetComponentData(e, new LogicalEntityId { Value = nextId++ });
            em.SetComponentData(e, new LogicalPose { Position = position, Rotation = quaternion.identity });
            em.SetComponentData(e, new LogicalRegion { Chunk = (int2)math.floor(position.xz / cellsPerChunk) });
            em.SetComponentData(e, new UnitIdentity { Kind = definition.Kind, Owner = owner });
            em.SetComponentData(e, new UnitCombat { Interval = definition.AttackInterval > 0 ? definition.AttackInterval : 1 });
            em.SetComponentData(e, new UnitHealth { Current = definition.Health, Maximum = definition.Health });
            em.SetComponentData(e, new UnitAttributes { MaxSpeed = definition.Speed, CarryCapacity = definition.CarryCapacity,
                Attack = definition.Attack, Defense = definition.Defense, Range = definition.Range, OccupancyRadius = definition.OccupancyRadius });
            em.SetComponentData(e, new UnitMotion { DesiredVelocity = desiredVelocity, Activity = movement.TerrainAvailable
                ? UnitActivity.Idle : UnitActivity.WaitingForTerrain });
        }

        /// <summary>Reads the immutable logical terrain snapshot without synchronizing movement jobs.</summary>
        public bool TryGetTerrainElevation(double x, double z, out double elevation)
        {
            var position = new double2(x, z);
            if (!terrainAvailable || !heights.IsCreated || !math.all(math.isfinite(position)) ||
                math.any(position < Bounds.Min) || math.any(position > Bounds.Max))
            {
                elevation = 0;
                return false;
            }

            elevation = Sample(position);
            return true;
        }

        private double Sample(double2 p)
        {
            int2 cell = math.min((int2)math.floor(p), (int2)Bounds.Max-1);
            double2 uv = p-cell;
            int i = cell.x-(int)Bounds.Min.x + (cell.y-(int)Bounds.Min.y)*width;
            double a=heights[i], b=heights[i+1], c=heights[i+width], d=heights[i+width+1];
            return uv.x+uv.y<=1 ? a+(b-a)*uv.x+(c-a)*uv.y : d+(c-d)*(1-uv.x)+(b-d)*(1-uv.y);
        }
        /// <summary>Publishes a new revision. Reissue destination commands after replacing the grid.</summary>
        public void SetNavigationGrid(NavigationGrid grid)
        {
            if (grid != null && (math.any((double2)grid.Min != Bounds.Min) ||
                math.any((double2)grid.Size != Bounds.Max - Bounds.Min)))
                throw new ArgumentException("Navigation and terrain must cover the same logical area.", nameof(grid));
            if (ReferenceEquals(navigationGrid, grid)) return;
            CompleteJobs();
            if (grid != null && navigationNext.Length == 1)
            {
                var replacement = new NativeArray<int>(NavigationArea * NavigationCapacity, Allocator.Persistent);
                navigationNext.Dispose();
                navigationNext = replacement;
                movement.NavigationNext = navigationNext;
            }
            navigationGrid = grid;
            PublishCrowdWalkability();
            navigationSlots.Clear();
            Array.Clear(sharedFields, 0, sharedFields.Length);
            movement.NavigationRevision = navigationRevision = checked(navigationRevision + 1);
        }

        /// <summary>Assigns one immutable field to a batch. Returns false for missing or blocked destinations.</summary>
        public bool SetDestination(NativeArray<Entity> entities, int2 destination)
        {
            if (navigationGrid == null || !navigationGrid.TryGetCost(destination, out int cost) || cost == 0)
                return false;

            CompleteJobs();
            var manager = World.EntityManager;
            foreach (var entity in entities)
                if (!manager.Exists(entity) || !manager.HasComponent<UnitIdentity>(entity))
                    throw new ArgumentException("All handles must be live units in this session.", nameof(entities));

            if (!AcquireField(destination, null, out int slot)) return false;

            foreach (var entity in entities)
            {
                SetMovementField(manager, entity, slot, false);
                MarkManual(manager, entity);
                CancelBuild(entity);
                CancelGather(manager, entity);
                CancelAttack(manager, entity);
            }
            return true;
        }

        private bool AcquireField(int2 destination, NavigationFlowField prepared, out int slot,
            HashSet<Entity> replacing = null)
        {
            if (navigationSlots.TryGetValue(destination, out slot))
            {
                FieldHits++;
                return true;
            }

            FieldMisses++;
            if (navigationSlots.Count == NavigationCapacity)
            {
                // Fields referenced only by the incoming batch can be replaced atomically.
                int used = 0;
                using var motions = units.ToComponentDataArray<UnitMotion>(Allocator.Temp);
                using var liveUnits = units.ToEntityArray(Allocator.Temp);
                for (int i = 0; i < motions.Length; i++)
                {
                    var motion = motions[i];
                    if (motion.Field != 0 && motion.NavigationRevision == navigationRevision &&
                        (replacing == null || !replacing.Contains(liveUnits[i])))
                        used |= 1 << (motion.Field - 1);
                }
                if (used == (1 << NavigationCapacity) - 1)
                {
                    FieldCapacityDenials++;
                    return false;
                }

                int2 evicted = default;
                foreach (var entry in navigationSlots)
                {
                    if ((used & (1 << entry.Value)) != 0) continue;
                    evicted = entry.Key;
                    slot = entry.Value;
                    break;
                }
                navigationSlots.Remove(evicted);
                FieldReplacements++;
            }
            else
            {
                slot = Array.FindIndex(sharedFields, field => field == null);
            }

            sharedFields[slot] = prepared ?? BuildMeasuredField(destination);
            sharedFields[slot].CopyTo(navigationNext, slot * NavigationArea);
            navigationSlots.Add(destination, slot);
            fieldDestinations[slot] = destination;

            return true;
        }

        public bool TryGetActiveField(UnitMotion motion, out NavigationFlowField field, out int2 destination)
        {
            field = null;
            destination = default;
            if (motion.Field <= 0 || motion.Field > sharedFields.Length ||
                motion.NavigationRevision != navigationRevision) return false;

            int slot = motion.Field - 1;
            field = sharedFields[slot];
            destination = fieldDestinations[slot];
            return field != null && field.IsCurrent;
        }

        private NavigationFlowField BuildMeasuredField(int2 destination)
        {
            long start = System.Diagnostics.Stopwatch.GetTimestamp();
            var field = NavigationSearch.BuildFlowField(navigationGrid, destination);
            FieldBuilds++;
            FieldBuildMilliseconds += (System.Diagnostics.Stopwatch.GetTimestamp() - start) *
                (1000.0 / System.Diagnostics.Stopwatch.Frequency);
            return field;
        }

        private void PublishCrowdWalkability()
        {
            if (!crowdEnabled && Buildings.Count == 0 && navigationGrid?.Obstacles == null && !movement.HasCrowdGrid) return;
            movement.HasCrowdGrid = navigationGrid != null;
            if (navigationGrid == null) return;
            for (int i = 0; i < crowdWalkable.Length; i++)
                crowdWalkable[i] = navigationGrid.Passage(i);
        }

        public int LastInvalidatedFields { get; private set; }

        /// <summary>Applies a logical resource availability change at a job synchronization boundary.</summary>
        public bool SetEnvironmentalResourceAvailable(ResourceKey key, bool available)
        {
            LastInvalidatedFields = 0;
            if (navigationGrid?.Obstacles == null) return false;
            CompleteJobs();
            if (!navigationGrid.ChangeResourceAvailability(key, available)) return false;

            PublishCrowdWalkability();
            InvalidateEnvironmentalFields();
            return true;
        }

        public bool SetEnvironmentalResourceRadius(ResourceKey key, double radius)
        {
            LastInvalidatedFields = 0;
            if (navigationGrid?.Obstacles == null) return false;
            CompleteJobs();
            if (!navigationGrid.ChangeResourceRadius(key, radius)) return false;
            PublishCrowdWalkability();
            InvalidateEnvironmentalFields();
            return true;
        }

        private void InvalidateEnvironmentalFields()
        {
            int affected = 0;
            foreach (var item in navigationSlots)
                if (!sharedFields[item.Value].IsCurrent)
                    affected |= 1 << item.Value;
            if (affected == 0) return;

            var manager = World.EntityManager;
            using var handles = units.ToEntityArray(Allocator.Temp);
            foreach (var entity in handles)
            {
                var motion = manager.GetComponentData<UnitMotion>(entity);
                if (motion.Field == 0 || motion.NavigationRevision != navigationRevision ||
                    (affected & (1 << (motion.Field - 1))) == 0) continue;
                motion.NavigationRevision = 0;
                manager.SetComponentData(entity, motion);
            }

            for (int i = 0; i < sharedFields.Length; i++)
            {
                if ((affected & (1 << i)) == 0) continue;
                int2 destination = default;
                foreach (var item in navigationSlots)
                    if (item.Value == i) { destination = item.Key; break; }
                navigationSlots.Remove(destination);
                sharedFields[i] = null;
                LastInvalidatedFields++;
            }

        }

        private void SetMovementField(EntityManager manager, Entity entity, int slot, bool formation, int2? interactionCell = null)
        {
            var motion = manager.GetComponentData<UnitMotion>(entity);
            motion.Field = slot + 1;
            motion.RouteDestination = fieldDestinations[slot];
            motion.NavigationRevision = navigationRevision;
            motion.DesiredVelocity = default;
            motion.HasFormation = formation;
            motion.InteractionArrival = interactionCell.HasValue;
            motion.InteractionCell = interactionCell.GetValueOrDefault();
            motion.CongestionTicks = 0;
            motion.RecoveryTicks = motion.RecoveryCooldown = 0;
            motion.RecoveryAttempts = 0;
            motion.RouteDistanceTravelled = motion.NoProgressSeconds = motion.PassageWaitSeconds = 0;
            motion.RecoverySide = 0;
            motion.YieldTo = 0;
            motion.Progress = NavigationProgress.Advancing;
            motion.FormationStep = -1;
            motion.FormationProbeIndex = 0;
            manager.SetComponentData(entity, motion);
        }

        public void SetIntention(Entity unit, double2 velocity)
        {
            if (!math.all(math.isfinite(velocity))) throw new ArgumentOutOfRangeException(nameof(velocity));
            CompleteJobs();
            var motion = World.EntityManager.GetComponentData<UnitMotion>(unit);
            motion.Field = 0;
            motion.HasFormation = false;
            World.EntityManager.GetBuffer<FormationWaypoint>(unit).Clear();
            motion.DesiredVelocity = velocity;
            motion.YieldTo = 0;
            motion.CongestionTicks = motion.RecoveryTicks = motion.RecoveryCooldown = 0;
            motion.Progress = math.any(velocity != 0) ? NavigationProgress.Advancing : NavigationProgress.Idle;
            World.EntityManager.SetComponentData(unit, motion);
            MarkManual(World.EntityManager, unit);
            CancelBuild(unit);
            CancelGather(World.EntityManager, unit);
            CancelAttack(World.EntityManager, unit);
        }
        public void DestroyBatch(NativeArray<Entity> entities)
        {
            CompleteJobs();
            foreach (var entity in entities)
                if (!World.EntityManager.Exists(entity) || !World.EntityManager.HasComponent<UnitIdentity>(entity))
                    throw new ArgumentException("All handles must be live units in this session.");
            if (!EconomyEnabled)
            {
                World.EntityManager.DestroyEntity(entities);
                PruneAttackers(World.EntityManager);
                return;
            }

            commandMembers.Clear();
            double cargo = 0;
            foreach (var entity in entities)
                if (commandMembers.Add(entity)) cargo += World.EntityManager.GetComponentData<GatherWorker>(entity).Cargo;
            World.EntityManager.DestroyEntity(entities);
            LostCargo += cargo;
            PruneAttackers(World.EntityManager);
        }
        public void Advance(float frameDeltaTime)
        {
            if (!math.isfinite(frameDeltaTime) || frameDeltaTime < 0) throw new ArgumentOutOfRangeException(nameof(frameDeltaTime));
            if (Paused) return; // No accumulated paused time and no tick-zero initialization while paused.
            elapsed += frameDeltaTime;
            World.SetTime(new TimeData(elapsed, frameDeltaTime));
            group.Update();
        }
        public void CompleteJobs() => World.EntityManager.CompleteAllTrackedJobs();
        public void Dispose()
        {
            if (World != null && World.IsCreated) { CompleteJobs(); World.Dispose(); }
            if (heights.IsCreated) heights.Dispose();
            if (navigationNext.IsCreated) navigationNext.Dispose();
            if (crowdWalkable.IsCreated) crowdWalkable.Dispose();
        }
    }
}
