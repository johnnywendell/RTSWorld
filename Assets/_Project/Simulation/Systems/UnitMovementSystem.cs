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
        public NativeArray<int> NavigationNext;
        public uint NavigationRevision;

        protected override void OnUpdate()
        {
            SystemAPI.GetSingletonRW<SimulationClock>().ValueRW.Tick++;
            Dependency = new MoveUnitsJob
            {
                Heights = Heights, GridMin = GridMin, GridWidth = GridWidth,
                NavigationNext = NavigationNext, NavigationRevision = NavigationRevision,
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
            [ReadOnly] public NativeArray<int> NavigationNext;
            public uint NavigationRevision;

            private void Execute(ref LogicalPose pose, ref UnitMotion motion, ref MovementVelocity velocity,
                ref EntitySimulationState state, ref LogicalRegion region, in UnitAttributes attributes, in LogicalEntityId identity, in DynamicBuffer<FormationWaypoint> formation)
            {
                state.Updates++;
                if (!TerrainAvailable)
                {
                    velocity.Value = double3.zero;
                    motion.Activity = UnitActivity.WaitingForTerrain;
                    return; // Hold the last authoritative pose; never fall to zero or consume intention.
                }
                double2 before = pose.Position.xz;
                double2 next;
                if (motion.Field != 0)
                {
                    if (motion.NavigationRevision != NavigationRevision)
                    {
                        velocity.Value = double3.zero;
                        motion.Activity = UnitActivity.WaitingForNavigation;
                        return;
                    }

                    int2 cellPosition = (int2)math.floor(before);
                    int2 local = cellPosition - GridMin;
                    int width = GridWidth - 1;
                    int height = (int)(Bounds.Max.y - Bounds.Min.y);
                    if (math.any(local < 0) || local.x >= width || local.y >= height)
                    {
                        velocity.Value = double3.zero;
                        motion.Activity = UnitActivity.WaitingForNavigation;
                        return;
                    }

                    int cellIndex = local.x + local.y * width;
                    int successor = NavigationNext[(motion.Field - 1) * width * height + cellIndex];
                    if (successor < 0)
                    {
                        velocity.Value = double3.zero;
                        motion.Activity = successor == -2 ? UnitActivity.WaitingForNavigation : UnitActivity.Unreachable;
                        return;
                    }

                    // Each cardinal segment stays inside its current cell and the next one.
                    uint lane = math.hash(new uint2((uint)identity.Value, (uint)(identity.Value >> 32)));
                    double2 offset = new double2(lane & 3u, (lane >> 2) & 3u) * 0.16 + 0.26;
                    bool localRoute = motion.HasFormation && (motion.FormationStep >= 0 || successor == cellIndex);
                    if (localRoute && motion.FormationStep < 0) motion.FormationStep = formation.Length - 1;
                    bool hasWaypoint = localRoute && motion.FormationStep >= 0;
                    int2 targetCell = hasWaypoint ? formation[motion.FormationStep].Cell
                        : GridMin + new int2(successor % width, successor / width);
                    double2 target = targetCell + (motion.HasFormation ? new double2(.5) : offset);
                    double2 remaining = target - before;
                    double distance = math.length(remaining);
                    double travel = attributes.MaxSpeed * DeltaTime;
                    next = distance <= travel ? target : before + remaining * (travel / distance);
                    if (distance <= travel)
                    {
                        if (hasWaypoint)
                        {
                            if (motion.FormationStep == 0) motion.Field = 0;
                            else motion.FormationStep--;
                        }
                        else if (successor == cellIndex) motion.Field = 0;
                    }
                }
                else
                {
                    double2 desired = motion.DesiredVelocity;
                    double speed = math.length(desired);
                    if (speed > attributes.MaxSpeed) desired *= attributes.MaxSpeed / speed;
                    next = math.clamp(before + desired * DeltaTime, Bounds.Min, Bounds.Max);
                }
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
