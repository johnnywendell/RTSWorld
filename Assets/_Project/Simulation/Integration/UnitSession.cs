using System;
using Unity.Collections;
using Unity.Core;
using Unity.Entities;
using Unity.Mathematics;

namespace RTSWorld.Simulation
{
    /// <summary>Logical unit lifetime. No renderer, camera, collider or visual residency dependency.</summary>
    public sealed class UnitSession : IDisposable
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
        private double elapsed;
        private bool terrainAvailable;
        private readonly int width;

        public UnitSession(ProceduralTerrainContract terrain, int2 min, int2 max, float fixedDeltaTime = 0.05f)
        {
            if (math.any(max <= min) || math.any(max - (double2)min > 256) ||
                math.any(math.abs((double2)min) > 1000000) || math.any(math.abs((double2)max) > 1000000))
                throw new ArgumentOutOfRangeException(nameof(max), "Finite integer area, at most 256 cells per axis, within +/-1000000.");
            if (!math.isfinite(fixedDeltaTime) || fixedDeltaTime < 0.001f || fixedDeltaTime > 1)
                throw new ArgumentOutOfRangeException(nameof(fixedDeltaTime));
            FixedDeltaTime = fixedDeltaTime;
            Bounds = new UnitBounds { Min = min, Max = max };
            width = max.x - min.x + 1;
            heights = new NativeArray<double>(width * (max.y - min.y + 1), Allocator.Persistent);
            try
            {
                if (terrain != null)
                    for (int z = min.y; z <= max.y; z++)
                        for (int x = min.x; x <= max.x; x++)
                            heights[x-min.x + (z-min.y)*width] = terrain.Elevation(x,z);
                terrainAvailable = terrain != null;
                World = new Unity.Entities.World("RTSWorld Units");
                var em = World.EntityManager;
                settings = em.CreateEntity(typeof(SimulationConfiguration), typeof(SimulationClock), typeof(UnitBounds));
                em.SetComponentData(settings, new SimulationConfiguration { FixedDeltaTime = fixedDeltaTime,
                    CellsPerChunk = terrain?.Definition.CellsPerChunk ?? 32 });
                em.SetComponentData(settings, Bounds);
                archetype = em.CreateArchetype(typeof(LogicalEntityId), typeof(LogicalPose), typeof(LogicalRegion),
                    typeof(MovementVelocity), typeof(EntitySimulationState), typeof(UnitIdentity), typeof(UnitHealth),
                    typeof(UnitAttributes), typeof(UnitMotion));
                units = em.CreateEntityQuery(typeof(UnitIdentity), typeof(LogicalPose));
                group = World.GetOrCreateSystemManaged<FixedStepSimulationSystemGroup>();
                group.Timestep = fixedDeltaTime;
                movement = World.GetOrCreateSystemManaged<UnitMovementSystem>();
                movement.Heights = heights; movement.GridMin = min; movement.GridWidth = width;
                movement.TerrainAvailable = terrainAvailable;
                group.AddSystemToUpdateList(movement);
                group.SortSystems();
            }
            catch { if (World != null && World.IsCreated) World.Dispose(); heights.Dispose(); throw; }
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
                if (movement.TerrainAvailable) p.y = Sample(p.xz);
                Entity e = entities[i];
                em.SetComponentData(e, new LogicalEntityId { Value = nextId++ });
                em.SetComponentData(e, new LogicalPose { Position = p, Rotation = quaternion.identity });
                em.SetComponentData(e, new LogicalRegion { Chunk = (int2)math.floor(p.xz/cells) });
                em.SetComponentData(e, new UnitIdentity { Kind = definition.Kind, Owner = owner });
                em.SetComponentData(e, new UnitHealth { Current = definition.Health, Maximum = definition.Health });
                em.SetComponentData(e, new UnitAttributes { MaxSpeed=definition.Speed, CarryCapacity=definition.CarryCapacity,
                    Attack=definition.Attack, Defense=definition.Defense, Range=definition.Range });
                em.SetComponentData(e, new UnitMotion { DesiredVelocity=desiredVelocity, Activity=movement.TerrainAvailable
                    ? UnitActivity.Idle : UnitActivity.WaitingForTerrain });
            }
            return entities; // Caller owns Temp handle; entities belong to this session.
        }

        private double Sample(double2 p)
        {
            int2 cell = math.min((int2)math.floor(p), (int2)Bounds.Max-1);
            double2 uv = p-cell;
            int i = cell.x-(int)Bounds.Min.x + (cell.y-(int)Bounds.Min.y)*width;
            double a=heights[i], b=heights[i+1], c=heights[i+width], d=heights[i+width+1];
            return uv.x+uv.y<=1 ? a+(b-a)*uv.x+(c-a)*uv.y : d+(c-d)*(1-uv.x)+(b-d)*(1-uv.y);
        }
        public void SetIntention(Entity unit, double2 velocity)
        {
            if (!math.all(math.isfinite(velocity))) throw new ArgumentOutOfRangeException(nameof(velocity));
            CompleteJobs();
            var motion = World.EntityManager.GetComponentData<UnitMotion>(unit);
            motion.DesiredVelocity = velocity;
            World.EntityManager.SetComponentData(unit, motion);
        }
        public void DestroyBatch(NativeArray<Entity> entities)
        {
            CompleteJobs();
            foreach (var entity in entities)
                if (!World.EntityManager.Exists(entity) || !World.EntityManager.HasComponent<UnitIdentity>(entity))
                    throw new ArgumentException("All handles must be live units in this session.");
            World.EntityManager.DestroyEntity(entities);
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
        }
    }
}
