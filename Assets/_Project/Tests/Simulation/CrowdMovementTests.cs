using System.Collections.Generic;
using System.Diagnostics;
using NUnit.Framework;
using Unity.Collections;
using RTSWorld.Simulation;
using RTSWorld.World.Generation;
using RTSWorld.World.Environment;
using Unity.Entities;
using Unity.Mathematics;

namespace RTSWorld.Tests
{
    public sealed class CrowdMovementTests
    {
        [TestCase(false, true)]
        [TestCase(true, true)]
        [TestCase(false, false)]
        public void OpposingSoldiersWaitForSafeRestrictedPassage(bool reversePriority, bool hasBays)
        {
            var terrain = new ProceduralTerrainContract(new WorldDefinition(7, cellsPerChunk: 4));
            var min = new int2(-6);
            var max = new int2(6);
            var tree = new ResourcePlacement(new ResourceKey(7, 4, 0, -1), NaturalResource.Tree, .5f, .5f, 0);
            var rock = new ResourcePlacement(new ResourceKey(7, 4, 0, 1), NaturalResource.Stone, .5f, .5f, 0);
            var obstacles = new EnvironmentalObstacles(min, max, 4, .22, new[] { tree, rock });
            var grid = new NavigationGrid(terrain, min, max, 100,
                cell => cell.y != 0 && !(hasBays && cell.y == 1 && math.abs(cell.x) == 2), obstacles: obstacles);
            using var session = new UnitSession(terrain, min, max, crowdEnabled: true);
            session.SetNavigationGrid(grid);
            Assert.That(session.SetEnvironmentalResourceRadius(rock.Key, .49), Is.True);

            var definition = UnitDefinition.Soldier;
            var left = new double3(-1.5, 0, .5);
            var right = new double3(2.5, 0, .5);
            using var first = session.SpawnBatch(definition, 0, 1, reversePriority ? right : left, default);
            using var second = session.SpawnBatch(definition, 0, 1, reversePriority ? left : right, default);
            var firstGoal = new int2(reversePriority ? -3 : 3, 0);
            var secondGoal = new int2(-firstGoal.x, 0);
            Assert.That(session.ApplyOrder(UnitOrder.Move(firstGoal), 0, first), Is.EqualTo(UnitOrderResult.Accepted));
            Assert.That(session.ApplyOrder(UnitOrder.Move(secondGoal), 0, second), Is.EqualTo(UnitOrderResult.Accepted));

            var manager = session.World.EntityManager;
            bool waitedInBay = false;
            double closest = double.MaxValue;
            for (int tick = 0; tick < 160; tick++)
            {
                Tick(session);
                var a = Position(session, first[0]);
                var b = Position(session, second[0]);
                closest = math.min(closest, math.distance(a, b));
                waitedInBay |= b.y >= 1 && manager.GetComponentData<UnitMotion>(second[0]).Progress ==
                    NavigationProgress.WaitingForPassage;
            }
            Assert.That(waitedInBay, Is.EqualTo(hasBays));
            var motion = manager.GetComponentData<UnitMotion>(second[0]);
            Assert.That(motion.PassageWaitSeconds, Is.GreaterThan(0));
            Assert.That(motion.RecoveryAttempts, Is.Zero);
            Assert.That(closest, Is.GreaterThanOrEqualTo(2 * definition.OccupancyRadius - 1e-6));
            if (hasBays)
            {
                Assert.That(motion.RouteDistanceTravelled, Is.GreaterThan(5));
                Assert.That(Position(session, first[0]), Is.EqualTo((double2)firstGoal + .5));
                Assert.That(Position(session, second[0]), Is.EqualTo((double2)secondGoal + .5));
            }
            else
            {
                Assert.That(motion.Progress, Is.EqualTo(NavigationProgress.WaitingForPassage));
                Assert.That(motion.NoProgressSeconds, Is.Zero);
            }
            TestContext.WriteLine(System.FormattableString.Invariant(
                $"PASSAGE closest={closest:F4} reversePriority={reversePriority} bays={hasBays}"));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void RoutedSoldierEscapesPersistentCongestionInPassableCorridor(bool recovery)
        {
            var terrain = new ProceduralTerrainContract(new WorldDefinition(7));
            var min = new int2(-8);
            var max = new int2(8);
            var grid = new NavigationGrid(terrain, min, max, 100, cell => cell.y < -1 || cell.y > 1);
            using var session = new UnitSession(terrain, min, max, crowdEnabled: true);
            session.SetNavigationGrid(grid);
            session.RecoveryEnabled = recovery;
            var definition = UnitDefinition.Soldier;
            definition.OccupancyRadius = .49f;
            using var traveler = session.SpawnBatch(definition, 0, 1, new double3(-3.5, 0, .5), default);
            using var group = session.SpawnBatch(definition, 0, 3,
                new double3(-.126391456102203, 0, -.002662831685814), default, .28);
            using var neighbor = session.SpawnBatch(definition, 0, 1,
                new double3(.123608543897797, 0, -.278512045637943), default);
            Assert.That(session.SetDestination(traveler, new int2(4, 0)), Is.True);

            var manager = session.World.EntityManager;
            bool noticedCongestion = false;
            int lastAttempt = -32;
            ushort attempts = 0;
            int arrival = 0;
            for (int tick = 1; tick <= 220; tick++)
            {
                Tick(session);
                var motion = manager.GetComponentData<UnitMotion>(traveler[0]);
                var pose = Position(session, traveler[0]);
                Assert.That(math.all(math.isfinite(pose)), Is.True);
                Assert.That(grid.TryGetCost((int2)math.floor(pose), out int cost) && cost > 0, Is.True);
                Assert.That(math.length(manager.GetComponentData<MovementVelocity>(traveler[0]).Value),
                    Is.LessThanOrEqualTo(3.00001));
                Assert.That(manager.GetComponentData<UnitMotion>(group[0]).Progress, Is.EqualTo(NavigationProgress.Idle));
                noticedCongestion |= motion.Progress is NavigationProgress.Congested or NavigationProgress.Deadlocked;
                if (motion.RecoveryAttempts != attempts)
                {
                    Assert.That(tick - lastAttempt, Is.GreaterThanOrEqualTo(31));
                    lastAttempt = tick;
                    attempts = motion.RecoveryAttempts;
                }
                if (motion.Progress == NavigationProgress.Arrived) { arrival = tick; break; }
            }
            Assert.That(noticedCongestion, Is.True);
            Assert.That(arrival > 0, Is.EqualTo(recovery));
            Assert.That(attempts > 0, Is.EqualTo(recovery));
            Assert.That(session.FieldBuilds, Is.EqualTo(1), "Desvios preservam o campo compartilhado");
            TestContext.WriteLine($"Recovery={recovery} arrivalTick={arrival} attempts={attempts}");
        }

        [Test]
        public void OpposingUnitsReduceOverlapAcrossNegativeCellBoundary()
        {
            double without = CompareOpposing(false);
            double with = CompareOpposing(true);
            Assert.That(with, Is.LessThan(without));
            TestContext.WriteLine(System.FormattableString.Invariant($"OVERLAP radius_depth_tick baseline={without:F4} crowd={with:F4}"));
        }

        private static double CompareOpposing(bool enabled)
        {
            using var session = CreateSession(enabled);
            using var left = session.SpawnBatch(UnitDefinition.Soldier, 0, 1, new double3(-2, 0, -.5), new double2(2, 0));
            using var right = session.SpawnBatch(UnitDefinition.Villager, 1, 1, new double3(0, 0, -.5), new double2(-2, 0));
            double overlap = 0;
            for (int tick = 0; tick < 30; tick++)
            {
                Tick(session);
                var a = Position(session, left[0]);
                var b = Position(session, right[0]);
                overlap += math.max(0, .52 - math.distance(a, b));
                Assert.That(math.all(math.isfinite(a)) && math.all(math.isfinite(b)), Is.True);
                var manager = session.World.EntityManager;
                Assert.That(math.length(manager.GetComponentData<MovementVelocity>(left[0]).Value), Is.LessThanOrEqualTo(3.00001));
                Assert.That(math.length(manager.GetComponentData<MovementVelocity>(right[0]).Value), Is.LessThanOrEqualTo(2.00001));
            }

            Assert.That(Position(session, left[0]).x, Is.GreaterThan(Position(session, right[0]).x));
            return overlap;
        }

        [Test]
        public void StationaryUnitKeepsPositionAndDeviationsRespectBlockedCells()
        {
            using var session = CreateSession(true, passage: true);
            using var stationary = session.SpawnBatch(UnitDefinition.Villager, 0, 1, new double3(-2.5, 0, 1.5), default);
            using var moving = session.SpawnBatch(UnitDefinition.Soldier, 0, 4, new double3(-5.5, 0, .5), default);
            Assert.That(session.SetDestination(moving, new int2(4, 1)), Is.True);

            bool crossed = false;
            for (int tick = 0; tick < 180; tick++)
            {
                Tick(session);
                foreach (var entity in moving)
                {
                    double2 position = Position(session, entity);
                    Assert.That((int)math.floor(position.x) != 0 || (int)math.floor(position.y) == 1, Is.True);
                    crossed |= position.x >= 1;
                }
            }
            Assert.That(crossed, Is.True);
            Assert.That(Position(session, stationary[0]), Is.EqualTo(new double2(-2.5, 1.5)));

            Assert.That(session.ApplyOrder(UnitOrder.Stop(), 0, moving), Is.EqualTo(UnitOrderResult.Accepted));
            var stopped = Position(session, moving[0]);
            Tick(session);
            Assert.That(Position(session, moving[0]), Is.EqualTo(stopped));
        }

        [Test]
        public void MobileTargetCanBePursuedBySeveralAttackers()
        {
            using var session = CreateSession(true);
            var targetDefinition = UnitDefinition.Villager;
            targetDefinition.Health = 400;
            using var target = session.SpawnBatch(targetDefinition, 1, 1, new double3(0, 0, .5), new double2(.35, 0));
            using var attackers = session.SpawnBatch(UnitDefinition.Soldier, 0, 4, new double3(-4, 0, 0), default);
            Assert.That(session.ApplyOrder(UnitOrder.Attack(target[0]), 0, attackers), Is.EqualTo(UnitOrderResult.Accepted));
            int peak = 0;
            for (int tick = 0; tick < 125 && session.World.EntityManager.Exists(target[0]); tick++)
            {
                Tick(session);
                peak = math.max(peak, session.InRangeUnits);
            }

            Assert.That(peak, Is.GreaterThan(1));
            Assert.That(session.FieldBuilds, Is.GreaterThan(0));
            Assert.That(session.FieldHits, Is.GreaterThan(0));
        }

        [Test]
        public void FlowAtlasReportsHitsDenialsAndReplacementWithoutChangingCapacity()
        {
            using var session = CreateSession(true);
            using var agents = session.SpawnBatch(UnitDefinition.Soldier, 0, 9, new double3(-6.5, 0, -4.5), default);
            for (int i = 0; i < 8; i++)
                Assert.That(session.SetDestination(agents.GetSubArray(i, 1), new int2(4, i - 4)), Is.True);
            Assert.That(session.SetDestination(agents.GetSubArray(8, 1), new int2(4, 4)), Is.False);
            Assert.That(session.SetDestination(agents.GetSubArray(8, 1), new int2(4, 3)), Is.True);
            session.SetIntention(agents[0], default);
            Assert.That(session.SetDestination(agents.GetSubArray(0, 1), new int2(4, 4)), Is.True);
            Assert.That(session.FieldCapacityDenials, Is.EqualTo(1));
            Assert.That(session.FieldHits, Is.EqualTo(1));
            Assert.That(session.FieldBuilds, Is.EqualTo(9));
            Assert.That(session.FieldReplacements, Is.EqualTo(1));
            TestContext.WriteLine(System.FormattableString.Invariant($"ATLAS builds={session.FieldBuilds} build_ms={session.FieldBuildMilliseconds:F4} hits={session.FieldHits} misses={session.FieldMisses} replaced={session.FieldReplacements} denied={session.FieldCapacityDenials}"));
        }

        [TestCase(100)]
        [TestCase(500)]
        [TestCase(1000)]
        [TestCase(5000)]
        public void ProfileOpposingFlowFieldsAtSupportedPopulations(int count)
        {
            Profile(count, false, false);
            Profile(count, true, false);
            Profile(count, true, true);
        }

        private static void Profile(int count, bool enabled, bool recovery)
        {
            var terrain = new ProceduralTerrainContract(new WorldDefinition(7, cellsPerChunk: 4));
            var min = new int2(-48);
            var max = new int2(48);
            var grid = new NavigationGrid(terrain, min, max, 100);
            using var session = new UnitSession(terrain, min, max, crowdEnabled: enabled);
            using var left = session.SpawnBatch(UnitDefinition.Soldier, 0, count / 2, new double3(-32, 0, -22), default, .66);
            using var right = session.SpawnBatch(UnitDefinition.Soldier, 1, count / 2, new double3(9, 0, -22), default, .66);
            session.SetNavigationGrid(grid);
            session.MeasureCrowdJobs = true;
            session.RecoveryEnabled = recovery;
            Assert.That(session.SetDestination(left, new int2(22, -8)), Is.True);
            Assert.That(session.SetDestination(right, new int2(-22, -8)), Is.True);
            var em = session.World.EntityManager;
            using var units = em.CreateEntityQuery(typeof(LogicalPose), typeof(UnitMotion), typeof(MovementVelocity));
            for (int i = 0; i < 16; i++) Tick(session);

            double spatial = 0, movement = 0, tickMs = 0;
            long allocated = 0;
            int overlap = 0, stalled = 0, congested = 0, atGoal = 0, firstGoal = 0;
            long attempts = 0;
            const int samples = 1200;
            var timer = new Stopwatch();
            for (int i = 1; i <= samples; i++)
            {
                long bytes = System.GC.GetAllocatedBytesForCurrentThread();
                timer.Restart();
                Tick(session);
                timer.Stop();
                allocated += System.GC.GetAllocatedBytesForCurrentThread() - bytes;
                tickMs += timer.Elapsed.TotalMilliseconds;
                spatial += session.SpatialMilliseconds;
                movement += session.MovementMilliseconds;
                if (i % 20 != 0) continue;
                using var poses = units.ToComponentDataArray<LogicalPose>(Allocator.Temp);
                using var velocities = units.ToComponentDataArray<MovementVelocity>(Allocator.Temp);
                using var motions = units.ToComponentDataArray<UnitMotion>(Allocator.Temp);
                overlap += CountOverlappingUnits(poses);
                stalled = 0;
                congested = 0;
                atGoal = 0;
                attempts = 0;
                for (int u = 0; u < poses.Length; u++)
                {
                    attempts += motions[u].RecoveryAttempts;
                    if (motions[u].IsCongested) congested++;
                    if (motions[u].Activity == UnitActivity.Idle)
                    {
                        int2 cell = (int2)math.floor(poses[u].Position.xz);
                        if ((cell.x == 22 || cell.x == -22) && cell.y == -8) atGoal++;
                        else if (math.lengthsq(velocities[u].Value) < .01) stalled++;
                    }
                    else if (math.lengthsq(velocities[u].Value) < .01) stalled++;
                }
                if (firstGoal == 0 && atGoal != 0) firstGoal = i + 16;
            }

            TestContext.WriteLine(System.FormattableString.Invariant(
                $"CROWD n={count} enabled={enabled} recovery={recovery} attempts={attempts} tick_ms={tickMs / samples:F4} index_ms={spatial / samples:F4} movement_ms={movement / samples:F4} bytes_tick={allocated / (double)samples:F2} build_ms={session.FieldBuildMilliseconds:F4} overlap_mean={overlap / (samples / 20.0):F1} stalled_final={stalled} congested_final={congested} arrived_final={atGoal} first_arrival_tick_sample={firstGoal}"));
        }

        private static int CountOverlappingUnits(NativeArray<LogicalPose> poses)
        {
            // Benchmark sampling outside the timed simulation. One query per occupied position.
            var cells = new Dictionary<int2, List<double2>>();
            foreach (var pose in poses)
            {
                var cell = (int2)math.floor(pose.Position.xz);
                if (!cells.TryGetValue(cell, out var entries)) cells.Add(cell, entries = new List<double2>());
                entries.Add(pose.Position.xz);
            }

            int count = 0;
            foreach (var pose in poses)
            {
                var point = pose.Position.xz;
                var cell = (int2)math.floor(point);
                bool overlapping = false;
                for (int z = -1; z <= 1 && !overlapping; z++)
                for (int x = -1; x <= 1 && !overlapping; x++)
                {
                    if (!cells.TryGetValue(cell + new int2(x, z), out var entries)) continue;
                    int exact = 0;
                    foreach (double2 other in entries)
                    {
                        double distance = math.distancesq(other, point);
                        if (distance >= .36 || (distance == 0 && ++exact == 1)) continue;
                        overlapping = true;
                        break;
                    }
                }
                if (overlapping) count++;
            }
            return count;
        }

        private static UnitSession CreateSession(bool crowd, bool passage = false)
        {
            var terrain = new ProceduralTerrainContract(new WorldDefinition(7, cellsPerChunk: 4));
            var min = new int2(-8);
            var max = new int2(8);
            var session = new UnitSession(terrain, min, max, crowdEnabled: crowd);
            session.SetNavigationGrid(new NavigationGrid(terrain, min, max, 100,
                cell => passage && cell.x == 0 && cell.y != 1));
            return session;
        }

        private static double2 Position(UnitSession session, Entity entity) =>
            session.World.EntityManager.GetComponentData<LogicalPose>(entity).Position.xz;

        private static void Tick(UnitSession session)
        {
            session.Advance(session.FixedDeltaTime);
            session.CompleteJobs();
        }
    }
}
