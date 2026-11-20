using System.Diagnostics;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Profiling;

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

        internal const double InteractionRadiusSquared = 1.5625;
        public bool CrowdEnabled, HasCrowdGrid;
        public bool RecoveryEnabled = true;
        public bool MeasureCrowdJobs;
        internal double LastFixedElapsed { get; private set; }
        public double SpatialMilliseconds { get; private set; }
        public double MovementMilliseconds { get; private set; }
        public NativeArray<byte> CrowdWalkable;
        private EntityQuery crowdUnits;
        private NativeParallelMultiHashMap<int2, CrowdSample> crowd;
        private static readonly ProfilerMarker SpatialUpdate = new("RTSWorld.Crowd.SpatialUpdate");
        private static readonly ProfilerMarker SteeringSchedule = new("RTSWorld.Crowd.SteeringSchedule");

        protected override void OnCreate()
        {
            crowdUnits = GetEntityQuery(ComponentType.ReadOnly<LogicalPose>(), ComponentType.ReadOnly<UnitAttributes>(),
                ComponentType.ReadOnly<LogicalEntityId>(), ComponentType.ReadOnly<MovementVelocity>(),
                ComponentType.ReadOnly<UnitMotion>());
            crowd = new NativeParallelMultiHashMap<int2, CrowdSample>(1, Allocator.Persistent);
        }

        protected override void OnDestroy()
        {
            Dependency.Complete();
            crowd.Dispose();
        }

        protected override void OnUpdate()
        {
            LastFixedElapsed = SystemAPI.Time.ElapsedTime;
            SystemAPI.GetSingletonRW<SimulationClock>().ValueRW.Tick++;
            long started = MeasureCrowdJobs ? Stopwatch.GetTimestamp() : 0;
            if (CrowdEnabled && TerrainAvailable)
            {
                using var scope = SpatialUpdate.Auto();
                Dependency.Complete();
                crowd.Clear();
                int count = crowdUnits.CalculateEntityCount();
                if (count > crowd.Capacity) crowd.Capacity = count;
                Dependency = new IndexCrowdJob { Index = crowd, Revision = NavigationRevision }.Schedule(crowdUnits, Dependency);
            }

            if (MeasureCrowdJobs)
            {
                Dependency.Complete();
                SpatialMilliseconds = MillisecondsSince(started);
                started = Stopwatch.GetTimestamp();
            }

            using var steering = SteeringSchedule.Auto();
            Dependency = new MoveUnitsJob
            {
                Heights = Heights, GridMin = GridMin, GridWidth = GridWidth,
                NavigationNext = NavigationNext, NavigationRevision = NavigationRevision,
                TerrainAvailable = TerrainAvailable, Bounds = SystemAPI.GetSingleton<UnitBounds>(),
                CellsPerChunk = SystemAPI.GetSingleton<SimulationConfiguration>().CellsPerChunk,
                Crowd = crowd, CrowdEnabled = CrowdEnabled, HasCrowdGrid = HasCrowdGrid,
                RecoveryEnabled = RecoveryEnabled, CrowdWalkable = CrowdWalkable, DeltaTime = SystemAPI.Time.DeltaTime
            }.ScheduleParallel(Dependency);
            if (MeasureCrowdJobs)
            {
                Dependency.Complete();
                MovementMilliseconds = MillisecondsSince(started);
            }
        }

        private static double MillisecondsSince(long started) =>
            (Stopwatch.GetTimestamp() - started) * (1000.0 / Stopwatch.Frequency);

        public struct CrowdSample
        {
            public ulong Id;
            public double2 Position, Velocity;
            public float Radius;
            public int2 Destination;
            public bool Routed;
        }

        [BurstCompile(CompileSynchronously = true)]
        private partial struct IndexCrowdJob : IJobEntity
        {
            public NativeParallelMultiHashMap<int2, CrowdSample> Index;
            public uint Revision;

            private void Execute(in LogicalPose pose, in MovementVelocity velocity, in UnitAttributes attributes,
                in LogicalEntityId id, in UnitMotion motion)
            {
                if (attributes.OccupancyRadius <= 0) return;
                Index.Add((int2)math.floor(pose.Position.xz), new CrowdSample
                {
                    Id = id.Value, Position = pose.Position.xz, Velocity = velocity.Value.xz,
                    Radius = attributes.OccupancyRadius, Destination = motion.RouteDestination, Routed = motion.Field != 0 && motion.NavigationRevision == Revision
                });
            }
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

            [ReadOnly] public NativeParallelMultiHashMap<int2, CrowdSample> Crowd;
            [ReadOnly] public NativeArray<byte> CrowdWalkable;
            public bool CrowdEnabled, HasCrowdGrid, RecoveryEnabled;

            private void Execute(ref LogicalPose pose, ref UnitMotion motion, ref MovementVelocity velocity,
                ref EntitySimulationState state, ref LogicalRegion region, in UnitAttributes attributes, in LogicalEntityId identity, in DynamicBuffer<FormationWaypoint> formation)
            {
                state.Updates++;
                motion.NearbyUnits = 0;
                if (!TerrainAvailable)
                {
                    velocity.Value = double3.zero;
                    motion.Activity = UnitActivity.WaitingForTerrain;
                    motion.Progress = NavigationProgress.WaitingForTerrain;
                    return; // Hold the last authoritative pose; never fall to zero or consume intention.
                }
                double2 before = pose.Position.xz;
                double2 next;
                double2 target = default;
                int2 nextCell = default;
                bool hasWaypoint = false, finalCell = false, reached = false, restricted = false, aligning = false;
                if (motion.Field != 0)
                {
                    if (motion.NavigationRevision != NavigationRevision)
                    {
                        velocity.Value = double3.zero;
                        motion.Activity = UnitActivity.WaitingForNavigation;
                        motion.Progress = NavigationProgress.NoRoute;
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
                        motion.Progress = NavigationProgress.NoRoute;
                        return;
                    }

                    int cellIndex = local.x + local.y * width;
                    int successor = NavigationNext[(motion.Field - 1) * width * height + cellIndex];
                    if (successor < 0)
                    {
                        velocity.Value = double3.zero;
                        motion.Progress = NavigationProgress.NoRoute;
                        motion.Activity = successor == -2 ? UnitActivity.WaitingForNavigation : UnitActivity.Unreachable;
                        return;
                    }

                    // Each cardinal segment stays inside its current cell and the next one.
                    uint lane = math.hash(new uint2((uint)identity.Value, (uint)(identity.Value >> 32)));
                    double2 offset = new double2(lane & 3u, (lane >> 2) & 3u) * 0.16 + 0.26;
                    if (motion.HasFormation && motion.FormationStep < 0 &&
                        motion.FormationProbeIndex != cellIndex + 1)
                    {
                        motion.FormationProbeIndex = cellIndex + 1;
                        if (successor == cellIndex) motion.FormationStep = formation.Length - 1;
                        else
                        {
                            // Join the existing return leg at its first intersection with the shared field.
                            for (int i = 0; i < formation.Length; i++)
                            {
                                if (!formation[i].Cell.Equals(cellPosition)) continue;
                                motion.FormationStep = i;
                                break;
                            }
                        }
                    }
                    hasWaypoint = motion.HasFormation && motion.FormationStep >= 0;
                    int2 targetCell = hasWaypoint ? formation[motion.FormationStep].Cell
                        : GridMin + new int2(successor % width, successor / width);
                    nextCell = targetCell;
                    target = targetCell + (motion.HasFormation ? new double2(.5) : offset);
                    if (HasCrowdGrid)
                    {
                        byte sourcePassage = CellPassage(cellPosition);
                        byte destinationPassage = CellPassage(targetCell);
                        restricted = IsLane(sourcePassage) || IsLane(destinationPassage);
                        if (restricted)
                        {
                            target = (double2)targetCell + .5;
                            if (math.any(targetCell != cellPosition))
                            {
                                bool alongX = targetCell.x != cellPosition.x;
                                double transverse = alongX ? before.y : before.x;
                                double centered = alongX ? cellPosition.y + .5 : cellPosition.x + .5;
                                aligning = math.abs(transverse - centered) > 1e-9;
                                if (aligning)
                                    target = alongX ? new double2(before.x, centered) : new double2(centered, before.y);
                            }
                        }
                    }
                    double2 remaining = target - before;
                    double distance = math.length(remaining);
                    double travel = attributes.MaxSpeed * DeltaTime;
                    next = distance <= travel ? target : before + remaining * (travel / distance);
                    reached = !aligning && distance <= travel;
                    finalCell = successor == cellIndex;
                }
                else
                {
                    double2 desired = motion.DesiredVelocity;
                    double speed = math.length(desired);
                    if (speed > attributes.MaxSpeed) desired *= attributes.MaxSpeed / speed;
                    next = math.clamp(before + desired * DeltaTime, Bounds.Min, Bounds.Max);
                }
                double2 preferred = next;
                bool routed = motion.Field != 0;
                bool recovering = false;
                bool waitingForPassage = CrowdEnabled && RecoveryEnabled && routed && attributes.OccupancyRadius > 0 &&
                    (restricted || motion.YieldTo != 0) &&
                    CoordinatePassage(before, nextCell, attributes, identity.Value, ref motion, ref next);
                if (waitingForPassage)
                {
                    reached = false;
                }
                else if (CrowdEnabled && !restricted && attributes.OccupancyRadius > 0 && math.any(next != before))
                {
                    next = Steer(before, preferred, attributes, identity.Value,
                        RecoveryEnabled ? (byte)0 : motion.CongestionTicks,
                        motion.InteractionArrival || motion.HasFormation, out byte neighbors);
                    motion.NearbyUnits = neighbors;
                    if (RecoveryEnabled && routed && !finalCell && !motion.InteractionArrival)
                        recovering = TryRecover(before, preferred, attributes.MaxSpeed, ref motion, ref next);
                }
                else if (HasCrowdGrid && math.any(before != next) && !ClearSegment(before, next))
                {
                    next = before;
                }

                double2 intended = preferred - before;
                bool stalled = math.lengthsq(intended) > 1e-12 &&
                    math.dot(next - before, intended) < math.lengthsq(intended) * .25;
                motion.CongestionTicks = stalled && motion.NearbyUnits > 0
                    ? (byte)math.min(255, motion.CongestionTicks + 1)
                    : RecoveryEnabled ? (byte)math.max(0, motion.CongestionTicks - 2) : (byte)0;

                double2 interactionPoint = motion.InteractionCell + new double2(.5);
                bool interactionReached = !waitingForPassage && motion.Field != 0 && motion.InteractionArrival &&
                    math.distancesq(next, interactionPoint) <= InteractionRadiusSquared && ClearSegment(next, interactionPoint);
                bool sharedDestinationReached = !waitingForPassage && CrowdEnabled && !restricted && finalCell && !motion.HasFormation &&
                    math.all((int2)math.floor(next) == (int2)math.floor(target)) &&
                    math.distancesq(next, target) <= .04;
                bool intermediateReached = !waitingForPassage && CrowdEnabled && !restricted && hasWaypoint && motion.FormationStep > 0 &&
                    math.distancesq(next, target) <= .04;
                if (interactionReached || sharedDestinationReached) motion.Field = 0;
                else if (intermediateReached || (reached && math.distancesq(next, target) < 1e-12))
                {
                    if (hasWaypoint && motion.FormationStep > 0) motion.FormationStep--;
                    else if (hasWaypoint || finalCell) motion.Field = 0;
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
                if (routed && CrowdEnabled)
                {
                    motion.RouteDistanceTravelled += (float)math.length(next - before);
                    if (waitingForPassage) motion.PassageWaitSeconds += DeltaTime;
                    else if (stalled) motion.NoProgressSeconds += DeltaTime;
                    else motion.NoProgressSeconds = math.max(0, motion.NoProgressSeconds - 2 * DeltaTime);
                }
                motion.Progress = routed && motion.Field == 0 ? NavigationProgress.Arrived
                    : waitingForPassage ? NavigationProgress.WaitingForPassage
                    : recovering ? NavigationProgress.Recovering
                    : stalled && routed && motion.NearbyUnits > 0 && motion.NoProgressSeconds >= 1.2f
                        ? NavigationProgress.Deadlocked
                    : stalled && motion.NearbyUnits > 0 ? NavigationProgress.Congested
                    : stalled ? NavigationProgress.Blocked
                    : moving ? NavigationProgress.Advancing
                    : routed ? NavigationProgress.Blocked
                    : motion.Progress == NavigationProgress.Arrived ? NavigationProgress.Arrived
                    : NavigationProgress.Idle;
                if (moving) pose.Rotation = quaternion.LookRotationSafe((float3)math.normalizesafe(velocity.Value), math.up());
                region.Chunk = (int2)math.floor(next / CellsPerChunk);
            }

            // A straight restricted corridor has at most eight cells under local arbitration.
            private bool CoordinatePassage(double2 before, int2 target, UnitAttributes attributes, ulong id,
                ref UnitMotion motion, ref double2 next)
            {
                int2 cell = (int2)math.floor(before);
                if (motion.YieldTo != 0)
                {
                    int2 mouth = motion.YieldGate - motion.YieldStep;
                    CrowdSample exiting = default;
                    bool inspectedMouth = FindContender(mouth, motion.YieldStep, motion.YieldTo, ref exiting) &
                        FindContender(mouth - motion.YieldStep, motion.YieldStep, motion.YieldTo, ref exiting);
                    bool mouthInUse = !inspectedMouth || exiting.Id != 0 &&
                        math.dot(exiting.Position - ((double2)mouth + .5), -(double2)motion.YieldStep) <
                        attributes.OccupancyRadius + exiting.Radius;
                    bool approaching = FindOnApproach(motion.YieldGate, motion.YieldStep, motion.YieldTo,
                        out _, out bool inspectedApproach);
                    if (mouthInUse || approaching || !inspectedApproach)
                    {
                        double2 offset = (double2)motion.YieldBay + .5 - before;
                        double distance = math.length(offset);
                        double travel = attributes.MaxSpeed * DeltaTime;
                        double2 candidate = distance <= travel ? before + offset : before + offset * (travel / distance);
                        next = ClearSegment(before, candidate) && HasSpace(before, candidate, attributes, id)
                            ? candidate : before;
                        return true;
                    }
                    motion.YieldTo = 0;
                }

                if (IsLane(CellPassage(cell)))
                {
                    if (HasSpace(before, next, attributes, id)) return false;
                    next = before;
                    return true;
                }

                if (!IsLane(CellPassage(target))) return false;
                int2 step = target - cell;
                if (math.csum(math.abs(step)) != 1) return false;
                bool found = FindOnApproach(target, step, 0, out var opponent, out bool inspected);
                if (!inspected)
                {
                    next = before;
                    return true;
                }
                if (found && opponent.Id != id)
                {
                    if (opponent.Id < id || IsLane(CellPassage((int2)math.floor(opponent.Position))))
                    {
                        int2 side = new(-step.y, step.x);
                        int2 bay = cell + side;
                        if (CellPassage(bay) != EnvironmentalObstacles.FullCell) bay = cell - side;
                        if (CellPassage(bay) == EnvironmentalObstacles.FullCell)
                        {
                            motion.YieldTo = opponent.Id;
                            motion.YieldGate = target;
                            motion.YieldStep = step;
                            motion.YieldBay = bay;
                        }
                        next = before;
                        return true;
                    }

                    int2 oppositeMouth = target;
                    for (int distance = 0; distance < 8 && IsLane(CellPassage(oppositeMouth)); distance++)
                        oppositeMouth += step;
                    double opposingDistanceToMouth = math.dot(opponent.Position - ((double2)oppositeMouth + .5), (double2)step);
                    if (opposingDistanceToMouth < attributes.OccupancyRadius + opponent.Radius)
                    {
                        next = before;
                        return true;
                    }
                }
                if (HasSpace(before, next, attributes, id)) return false;
                next = before;
                return true;
            }

            private const int PassageSampleLimit = 32;

            private bool FindOnApproach(int2 gate, int2 step, ulong trackedId, out CrowdSample chosen, out bool inspected)
            {
                chosen = default;
                inspected = true;
                int2 cell = gate;
                for (int distance = 0; distance < 8 && IsLane(CellPassage(cell)); distance++, cell += step)
                    inspected &= FindContender(cell, step, trackedId, ref chosen);
                if (IsLane(CellPassage(cell)))
                {
                    inspected = false;
                    return false;
                }

                inspected &= FindContender(cell, step, trackedId, ref chosen);
                inspected &= FindContender(cell + step, step, trackedId, ref chosen);
                return chosen.Id != 0;
            }

            private bool FindContender(int2 cell, int2 step, ulong trackedId, ref CrowdSample chosen)
            {
                if (!Crowd.TryGetFirstValue(cell, out var sample, out var iterator)) return true;
                int inspected = 0;
                do
                {
                    if (++inspected > PassageSampleLimit) return false;
                    if (trackedId != 0 ? sample.Id != trackedId :
                        !sample.Routed || math.dot((double2)(sample.Destination - cell), (double2)step) >= 0)
                        continue;
                    if (chosen.Id == 0 || sample.Id < chosen.Id) chosen = sample;
                } while (Crowd.TryGetNextValue(out sample, ref iterator));
                return true;
            }

            private bool HasSpace(double2 before, double2 next, UnitAttributes attributes, ulong id)
            {
                var first = (int2)math.floor(math.min(before, next) - 1);
                var last = (int2)math.floor(math.max(before, next) + 1);
                double2 travel = next - before;
                double length = math.lengthsq(travel);
                if (length <= 1e-20) return true;
                for (int z = first.y; z <= last.y; z++)
                for (int x = first.x; x <= last.x; x++)
                {
                    if (!Crowd.TryGetFirstValue(new int2(x, z), out var sample, out var iterator)) continue;
                    int inspected = 0;
                    do
                    {
                        if (++inspected > PassageSampleLimit) return false;
                        if (sample.Id == id) continue;
                        double radius = attributes.OccupancyRadius + sample.Radius;
                        double2 offset = sample.Position - before;
                        double along = math.dot(offset, travel);
                        if (along <= 0) continue;
                        double closest = math.lengthsq(offset - travel * math.saturate(along / length));
                        if (closest < radius * radius) return false;
                    } while (Crowd.TryGetNextValue(out sample, ref iterator));
                }
                return true;
            }

            private bool TryRecover(double2 before, double2 preferred, double speed, ref UnitMotion motion, ref double2 next)
            {
                if (motion.RecoveryCooldown > 0) motion.RecoveryCooldown--;
                if (motion.RecoveryTicks == 0)
                {
                    if (motion.RecoveryCooldown > 0 || motion.CongestionTicks < 12 || motion.NearbyUnits == 0)
                        return false;
                    motion.RecoveryTicks = 8;
                    motion.RecoverySide = motion.RecoverySide == 0 ? (sbyte)1 : (sbyte)-motion.RecoverySide;
                    if (motion.RecoveryAttempts < ushort.MaxValue) motion.RecoveryAttempts++;
                }

                motion.RecoveryTicks--;
                if (motion.RecoveryTicks == 0) motion.RecoveryCooldown = 24;
                double2 forward = math.normalizesafe(preferred - before);
                double2 right = new(forward.y, -forward.x);
                double2 candidate = before + (forward * .25 + right * (.5 * motion.RecoverySide)) * (speed * DeltaTime);
                if (!ClearSegment(before, candidate)) return false;
                next = candidate;
                return true;
            }

            private double2 Steer(double2 before, double2 preferred, UnitAttributes attributes, ulong id, byte congestionTicks, bool followRoute, out byte neighbors)
            {
                double2 desired = (preferred - before) / DeltaTime;
                double2 heading = math.normalizesafe(desired);
                double2 right = new(heading.y, -heading.x);
                double2 correction = default;
                double slowdown = 1;
                int nearby = 0;
                int2 cell = (int2)math.floor(before);
                double searchRadius = attributes.OccupancyRadius + .49 + .48;
                int2 first = (int2)math.floor(before - searchRadius);
                int2 last = (int2)math.floor(before + searchRadius);
                for (int z = first.y; z <= last.y; z++)
                for (int x = first.x; x <= last.x; x++)
                {
                    if (!Crowd.TryGetFirstValue(new int2(x, z), out var other, out var iterator)) continue;
                    int remaining = 24;
                    do
                    {
                        if (other.Id == id) continue;
                        double2 offset = other.Position - before;
                        double radius = attributes.OccupancyRadius + other.Radius;
                        double proximity = radius + .48;
                        double distanceSquared = math.lengthsq(offset);
                        if (distanceSquared >= proximity * proximity) continue;
                        nearby++;
                        double distance = math.sqrt(distanceSquared);
                        if (distance < radius)
                        {
                            double2 away = distance > 1e-9 ? -offset / distance
                                : (id < other.Id ? new double2(-1, 0) : new double2(1, 0));
                            correction += away * (4 * (radius - distance));
                        }

                        if (math.dot(offset, heading) <= 0) continue;
                        double2 relative = desired - other.Velocity;
                        double closing = math.dot(relative, offset);
                        if (closing <= 0) continue;
                        double horizon = math.min(0.25, closing / math.max(1e-9, math.lengthsq(relative)));
                        if (math.lengthsq(offset - relative * horizon) >= radius * radius) continue;
                        double lateral = math.dot(offset, right);
                        correction += right * (lateral > 0.08 ? -1 : 1) * attributes.MaxSpeed * .55;
                        slowdown = math.min(slowdown, math.clamp((distance - radius) / .35, .05, 1));
                    } while (--remaining > 0 && Crowd.TryGetNextValue(out other, ref iterator));
                }

                neighbors = (byte)math.min(255, nearby);
                double speed = attributes.MaxSpeed;
                double2 steered = desired * slowdown + correction;
                double length = math.length(steered);
                if (length > speed) steered *= speed / length;
                double2 candidate = math.clamp(before + steered * DeltaTime, Bounds.Min, Bounds.Max);
                // Keep reserved waypoints and interaction legs on their route while making room for neighbors.
                if (followRoute)
                {
                    candidate = math.lerp(preferred, candidate, .35);
                    int2 destinationCell = (int2)math.floor(candidate);
                    if (math.any(destinationCell != cell) && math.any(destinationCell != (int2)math.floor(preferred)))
                        candidate = preferred;
                    return ClearSegment(before, candidate) ? candidate
                        : ClearSegment(before, preferred) ? preferred : before;
                }

                // When separation repeatedly stalls progress, yield sideways in alternating fixed-tick windows.
                if (congestionTicks >= 8 && math.dot(candidate - before, heading) < speed * DeltaTime * .15)
                {
                    double side = ((congestionTicks / 8) & 1) == 0 ? 1 : -1;
                    double2 yield = math.clamp(before + right * (side * speed * DeltaTime * .45), Bounds.Min, Bounds.Max);
                    if (ClearSegment(before, yield)) return yield;
                }
                if (math.any(candidate != before) && ClearSegment(before, candidate)) return candidate;
                double2 reduced = math.lerp(before, preferred, slowdown);
                return ClearSegment(before, reduced) ? reduced : before;
            }

            private bool ClearSegment(double2 from, double2 to)
            {
                if (!HasCrowdGrid) return true;
                int2 cell = (int2)math.floor(from);
                int2 end = (int2)math.floor(to);
                double2 delta = to - from;
                int2 step = (int2)math.sign(delta);
                double2 crossing = new(
                    step.x == 0 ? double.PositiveInfinity : (cell.x + (step.x > 0 ? 1 : 0) - from.x) / delta.x,
                    step.y == 0 ? double.PositiveInfinity : (cell.y + (step.y > 0 ? 1 : 0) - from.y) / delta.y);
                double2 entry = from;
                while (math.any(cell != end))
                {
                    double x = cell.x == end.x ? double.PositiveInfinity : crossing.x;
                    double z = cell.y == end.y ? double.PositiveInfinity : crossing.y;
                    double2 exit = from + delta * math.min(x, z);
                    if (!ContainsSegment(cell, entry, exit)) return false;
                    if (math.abs(x - z) <= 1e-10 &&
                        (!ContainsSegment(cell + new int2(step.x, 0), exit, exit) ||
                         !ContainsSegment(cell + new int2(0, step.y), exit, exit)))
                        return false;
                    if (x <= z) { cell.x += step.x; crossing.x += math.abs(1 / delta.x); }
                    if (z <= x) { cell.y += step.y; crossing.y += math.abs(1 / delta.y); }
                    entry = exit;
                }
                return ContainsSegment(cell, entry, to);
            }

            private static bool IsLane(byte passage) => passage != 0 && passage != EnvironmentalObstacles.FullCell;

            private bool ContainsSegment(int2 cell, double2 from, double2 to)
            {
                byte passage = CellPassage(cell);
                if (passage == EnvironmentalObstacles.FullCell) return true;
                double2 center = (double2)cell + .5;
                double2 deviation = math.max(math.abs(from - center), math.abs(to - center));
                return ((passage & EnvironmentalObstacles.AlongX) != 0 && deviation.y <= EnvironmentalObstacles.LaneHalfWidth) ||
                       ((passage & EnvironmentalObstacles.AlongZ) != 0 && deviation.x <= EnvironmentalObstacles.LaneHalfWidth);
            }

            private byte CellPassage(int2 cell)
            {
                int2 local = cell - GridMin;
                int width = GridWidth - 1;
                if (math.any(local < 0) || local.x >= width || local.y >= CrowdWalkable.Length / width) return 0;
                return CrowdWalkable[local.x + local.y * width];
            }
        }
    }
}
