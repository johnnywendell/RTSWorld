using Unity.Entities;
using Unity.Mathematics;

namespace RTSWorld.Simulation
{
    public struct LogicalEntityId : IComponentData { public ulong Value; }
    // LocalTransform is float/local/rendering oriented; this pose is global logical data.
    public struct LogicalPose : IComponentData
    {
        public double3 Position;
        public quaternion Rotation;
    }
    public struct MovementVelocity : IComponentData { public double3 Value; }
    public struct EntitySimulationState : IComponentData { public ulong Updates; }
    public struct LogicalRegion : IComponentData { public int2 Chunk; }
    public struct SimulationConfiguration : IComponentData
    {
        public float FixedDeltaTime;
        public int CellsPerChunk;
    }
    public struct SimulationClock : IComponentData { public ulong Tick; }
}
