using System;
using Unity.Core;
using Unity.Entities;
using Unity.Mathematics;

namespace RTSWorld.Simulation
{
    /// <summary>Explicit lifetime for a standalone authoritative simulation; no camera or visual chunks.</summary>
    public sealed class FoundationSession : IDisposable
    {
        public Unity.Entities.World World { get; }
        public FixedStepSimulationSystemGroup FixedGroup { get; }
        private readonly Entity settings;
        private double elapsed;
        public int Count { get { using (var query = World.EntityManager.CreateEntityQuery(typeof(LogicalEntityId))) return query.CalculateEntityCount(); } }
        public ulong Tick => World.EntityManager.GetComponentData<SimulationClock>(settings).Tick;
        public float FixedDeltaTime { get; }

        public FoundationSession(float fixedDeltaTime = 0.05f, int cellsPerChunk = 32)
        {
            if (float.IsNaN(fixedDeltaTime) || fixedDeltaTime < 0.001f || fixedDeltaTime > 1)
                throw new ArgumentOutOfRangeException(nameof(fixedDeltaTime));
            if (cellsPerChunk < 1 || cellsPerChunk > 256) throw new ArgumentOutOfRangeException(nameof(cellsPerChunk));
            FixedDeltaTime = fixedDeltaTime;
            World = new Unity.Entities.World("RTSWorld Foundation");
            settings = World.EntityManager.CreateEntity(typeof(SimulationConfiguration), typeof(SimulationClock));
            World.EntityManager.SetComponentData(settings, new SimulationConfiguration
                { FixedDeltaTime = fixedDeltaTime, CellsPerChunk = cellsPerChunk });
            FixedGroup = World.GetOrCreateSystemManaged<FixedStepSimulationSystemGroup>();
            FixedGroup.Timestep = fixedDeltaTime;
            FixedGroup.AddSystemToUpdateList(World.GetOrCreateSystemManaged<FoundationMovementSystem>());
            FixedGroup.SortSystems();
        }

        public Entity Spawn(ulong id, double3 position, double3 velocity)
        {
            if (!math.all(math.isfinite(position)) || !math.all(math.isfinite(velocity)))
                throw new ArgumentOutOfRangeException(nameof(position));
            var manager = World.EntityManager;
            var config = manager.GetComponentData<SimulationConfiguration>(settings);
            double2 chunk = math.floor(position.xz / config.CellsPerChunk);
            if (math.any(chunk < int.MinValue) || math.any(chunk > int.MaxValue))
                throw new ArgumentOutOfRangeException(nameof(position));
            using (var query = manager.CreateEntityQuery(typeof(LogicalEntityId)))
            using (var ids = query.ToComponentDataArray<LogicalEntityId>(Unity.Collections.Allocator.Temp))
                foreach (var existing in ids)
                    if (existing.Value == id) throw new ArgumentException("Logical id already exists.", nameof(id));
            var entity = manager.CreateEntity(typeof(LogicalEntityId), typeof(LogicalPose),
                typeof(MovementVelocity), typeof(EntitySimulationState), typeof(LogicalRegion));
            manager.SetComponentData(entity, new LogicalEntityId { Value = id });
            manager.SetComponentData(entity, new LogicalPose { Position = position, Rotation = quaternion.identity });
            manager.SetComponentData(entity, new MovementVelocity { Value = velocity });
            manager.SetComponentData(entity, new LogicalRegion { Chunk = (int2)chunk });
            return entity;
        }

        public void Advance(float frameDeltaTime)
        {
            if (float.IsNaN(frameDeltaTime) || float.IsInfinity(frameDeltaTime) || frameDeltaTime < 0)
                throw new ArgumentOutOfRangeException(nameof(frameDeltaTime));
            // Explicit frame clock. FixedStep group owns accumulation and catch-up, including tick zero.
            elapsed += frameDeltaTime;
            World.SetTime(new TimeData(elapsed, frameDeltaTime));
            FixedGroup.Update();
        }

        public void CompleteJobs() => World.EntityManager.CompleteAllTrackedJobs();
        public void Dispose()
        {
            if (World.IsCreated) { CompleteJobs(); World.Dispose(); }
        }
    }
}
