using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace RTSWorld.Simulation
{
    [DisableAutoCreation]
    [UpdateInGroup(typeof(FixedStepSimulationSystemGroup))]
    public partial class UnitMovementSystem : SystemBase
    {
        // Owned by UnitSession. Immutable between CompleteJobs and the next update.
        public NativeArray<double> Heights;
        public int2 GridMin;
        public int GridWidth;
        public bool TerrainAvailable;

        protected override void OnUpdate()
        {
            SystemAPI.GetSingletonRW<SimulationClock>().ValueRW.Tick++;
            Dependency = new MoveUnitsJob
            {
                Heights = Heights, GridMin = GridMin, GridWidth = GridWidth,
                TerrainAvailable = TerrainAvailable, Bounds = SystemAPI.GetSingleton<UnitBounds>(),
                CellsPerChunk = SystemAPI.GetSingleton<SimulationConfiguration>().CellsPerChunk,
                DeltaTime = SystemAPI.Time.DeltaTime
            }.ScheduleParallel(Dependency);
        }

        [BurstCompile(CompileSynchronously = true)]
        public partial struct MoveUnitsJob : IJobEntity
        {
            [ReadOnly] public NativeArray<double> Heights;
            public int2 GridMin;
            public int GridWidth, CellsPerChunk;
            public bool TerrainAvailable;
            public UnitBounds Bounds;
            public float DeltaTime;

            private void Execute(ref LogicalPose pose, ref UnitMotion motion, ref MovementVelocity velocity,
                ref EntitySimulationState state, ref LogicalRegion region, in UnitAttributes attributes)
            {
                state.Updates++;
                if (!TerrainAvailable)
                {
                    velocity.Value = double3.zero;
                    motion.Activity = UnitActivity.WaitingForTerrain;
                    return; // Hold the last authoritative pose; never fall to zero or consume intention.
                }
                double2 desired = motion.DesiredVelocity;
                double speed = math.length(desired);
                if (speed > attributes.MaxSpeed) desired *= attributes.MaxSpeed / speed;
                double2 before = pose.Position.xz;
                double2 next = math.clamp(before + desired * DeltaTime, Bounds.Min, Bounds.Max);
                velocity.Value = new double3((next.x - before.x) / DeltaTime, 0, (next.y - before.y) / DeltaTime);
                pose.Position.xz = next;
                // Same triangle diagonal as ProceduralTerrainContract; last grid vertex is a legal boundary.
                int2 cell = math.min((int2)math.floor(next), (int2)Bounds.Max - 1);
                double2 uv = next - cell;
                int index = cell.x - GridMin.x + (cell.y - GridMin.y) * GridWidth;
                double a = Heights[index], b = Heights[index + 1];
                double c = Heights[index + GridWidth], d = Heights[index + GridWidth + 1];
                pose.Position.y = uv.x + uv.y <= 1 ? a + (b-a)*uv.x + (c-a)*uv.y
                    : d + (c-d)*(1-uv.x) + (b-d)*(1-uv.y);
                bool moving = math.lengthsq(velocity.Value.xz) > 1e-20;
                motion.Activity = moving ? UnitActivity.Moving : UnitActivity.Idle;
                if (moving) pose.Rotation = quaternion.LookRotationSafe((float3)math.normalizesafe(velocity.Value), math.up());
                region.Chunk = (int2)math.floor(next / CellsPerChunk);
            }
        }
    }
}
