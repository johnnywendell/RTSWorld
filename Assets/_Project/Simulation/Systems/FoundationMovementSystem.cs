using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;

namespace RTSWorld.Simulation
{
    // Explicit creation only: the foundation never injects gameplay into the default World.
    [DisableAutoCreation]
    [UpdateInGroup(typeof(FixedStepSimulationSystemGroup))]
    public partial class FoundationMovementSystem : SystemBase
    {
        protected override void OnCreate()
        {
            RequireForUpdate<SimulationConfiguration>();
            RequireForUpdate<SimulationClock>();
        }

        protected override void OnUpdate()
        {
            var config = SystemAPI.GetSingleton<SimulationConfiguration>();
            var clock = SystemAPI.GetSingletonRW<SimulationClock>();
            clock.ValueRW.Tick++;
            Dependency = new MovementJob
            {
                DeltaTime = SystemAPI.Time.DeltaTime,
                CellsPerChunk = config.CellsPerChunk
            }.ScheduleParallel(Dependency);
        }

        [BurstCompile(CompileSynchronously = true)]
        public partial struct MovementJob : IJobEntity
        {
            public float DeltaTime;
            public int CellsPerChunk;
            private void Execute(ref LogicalPose pose, in MovementVelocity velocity,
                ref EntitySimulationState state, ref LogicalRegion region, in LogicalEntityId id)
            {
                pose.Position += velocity.Value * DeltaTime;
                region.Chunk = (int2)math.floor(pose.Position.xz / CellsPerChunk);
                state.Updates++;
            }
        }
    }
}
