using System.Collections.Generic;
using NUnit.Framework;
using RTSWorld.Simulation;
using RTSWorld.World.Environment;
using RTSWorld.World.Generation;
using Unity.Mathematics;

namespace RTSWorld.Tests
{
    public sealed class UnitOrdersTests
    {
        [Test]
        public void GroupCrossesChunksAndStopsAtDistinctSpacedCells()
        {
            var terrain = new ProceduralTerrainContract(new WorldDefinition(7, cellsPerChunk: 4));
            var grid = new NavigationGrid(terrain, new int2(-12), new int2(12), 100,
                cell => cell.x == 0 && cell.y != 2);
            using var session = new UnitSession(terrain, grid.Min, grid.Min + grid.Size);
            session.SetNavigationGrid(grid);
            using var members = session.SpawnBatch(UnitDefinition.Soldier, 0, 9, new double3(-5.5, 0, .5), default);
            Assert.That(session.ApplyOrder(UnitOrder.Move(new int2(5, 2), 2), 0, members), Is.EqualTo(UnitOrderResult.Accepted));

            var manager = session.World.EntityManager;
            for (int tick = 0; tick < 220; tick++)
            {
                Tick(session);
                foreach (var member in members)
                {
                    var cell = (int2)math.floor(manager.GetComponentData<LogicalPose>(member).Position.xz);
                    Assert.That(grid.TryGetCost(cell, out int cost) && cost > 0, Is.True);
                }
            }

            var positions = new HashSet<double2>();
            foreach (var member in members)
            {
                var position = manager.GetComponentData<LogicalPose>(member).Position.xz;
                Assert.That(position.x, Is.GreaterThan(0));
                Assert.That(manager.GetComponentData<MovementVelocity>(member).Value, Is.EqualTo(double3.zero));
                foreach (var other in positions) Assert.That(math.distance(position, other), Is.GreaterThanOrEqualTo(2));
                positions.Add(position);
            }
            Assert.That(positions.Count, Is.EqualTo(9));
            Assert.That(session.ApplyOrder(UnitOrder.Stop(), 0, members), Is.EqualTo(UnitOrderResult.Accepted));
            Tick(session);
            foreach (var member in members)
                Assert.That(positions.Contains(manager.GetComponentData<LogicalPose>(member).Position.xz), Is.True);
        }

        [Test]
        public void InvalidOrderPreservesMoveAndStopFreezesReplacement()
        {
            var terrain = new ProceduralTerrainContract(new WorldDefinition(7));
            var min = new int2(-12); var max = new int2(12);
            using var session = new UnitSession(terrain, min, max);
            session.SetNavigationGrid(new NavigationGrid(terrain, min, max, 100,
                cell => cell.x == 0 || cell.Equals(new int2(-1, 0))));
            using var members = session.SpawnBatch(UnitDefinition.Soldier, 0, 1, new double3(-5.5, 0, .5), default);
            var entity = members[0];
            var manager = session.World.EntityManager;
            double2 Position() => manager.GetComponentData<LogicalPose>(entity).Position.xz;

            Assert.That(session.ApplyOrder(UnitOrder.Move(new int2(-2, 0)), 0, members), Is.EqualTo(UnitOrderResult.Accepted));
            Tick(session);
            var before = Position();
            Assert.That(session.ApplyOrder(UnitOrder.Move(new int2(-1, 0)), 0, members), Is.EqualTo(UnitOrderResult.InvalidDestination));
            Assert.That(session.ApplyOrder(UnitOrder.Move(new int2(12, 0)), 0, members), Is.EqualTo(UnitOrderResult.InvalidDestination));
            Assert.That(session.ApplyOrder(UnitOrder.Move(new int2(2, 0)), 0, members), Is.EqualTo(UnitOrderResult.Unreachable));
            Tick(session);
            Assert.That(Position().x, Is.GreaterThan(before.x));

            Assert.That(session.ApplyOrder(UnitOrder.Move(new int2(-9, 0)), 0, members), Is.EqualTo(UnitOrderResult.Accepted));
            before = Position();
            Tick(session);
            Assert.That(Position().x, Is.LessThan(before.x));

            Assert.That(session.ApplyOrder(UnitOrder.Stop(), 0, members), Is.EqualTo(UnitOrderResult.Accepted));
            before = Position();
            Tick(session);
            Assert.That(Position(), Is.EqualTo(before));
            Assert.That(manager.GetComponentData<MovementVelocity>(entity).Value, Is.EqualTo(double3.zero));

            session.DestroyBatch(members);
            Assert.That(session.ApplyOrder(UnitOrder.Stop(), 0, members), Is.EqualTo(UnitOrderResult.InvalidUnit));
        }

        [Test]
        public void ReplacementCanReclaimItsOwnFieldAtCapacity()
        {
            var terrain = new ProceduralTerrainContract(new WorldDefinition(7));
            var min = new int2(-12); var max = new int2(12);
            using var session = new UnitSession(terrain, min, max);
            session.SetNavigationGrid(new NavigationGrid(terrain, min, max, 100));
            using var members = session.SpawnBatch(UnitDefinition.Villager, 0, 9, new double3(-6.5, 0, -.5), default);
            for (int i = 0; i < 8; i++)
                Assert.That(session.ApplyOrder(UnitOrder.Move(new int2(i, 6)), 0, members.GetSubArray(i, 1)),
                    Is.EqualTo(UnitOrderResult.Accepted));

            var destination = UnitOrder.Move(new int2(-4, 5));
            Assert.That(session.ApplyOrder(destination, 0, members.GetSubArray(8, 1)), Is.EqualTo(UnitOrderResult.CacheBusy));
            Assert.That(session.ApplyOrder(destination, 0, members.GetSubArray(0, 1)), Is.EqualTo(UnitOrderResult.Accepted));
        }

        [Test]
        public void PickingSnapshotMatchesProceduralTrianglesAndIncludesTheBoundary()
        {
            var terrain = new ProceduralTerrainContract(new WorldDefinition(7));
            using var session = new UnitSession(terrain, new int2(-2), new int2(2));
            foreach (var point in new[] { new double2(-1.75, -.7), new double2(-.2, -.3), new double2(2, 2) })
            {
                Assert.That(session.TryGetTerrainElevation(point.x, point.y, out double height), Is.True);
                Assert.That(height, Is.EqualTo(terrain.Elevation(point.x, point.y)).Within(1e-10));
            }
            Assert.That(session.TryGetTerrainElevation(2.01, 0, out _), Is.False);
        }

        [Test]
        public void WaterRejectsMove()
        {
            var terrain = new ProceduralTerrainContract(new WorldDefinition(7), new EnvironmentDefinition(100));
            using var session = new UnitSession(terrain, new int2(-2), new int2(2));
            session.SetNavigationGrid(new NavigationGrid(terrain, new int2(-2), new int2(2), 100));
            using var member = session.SpawnBatch(UnitDefinition.Villager, 0, 1, new double3(-1.5, 0, .5), default);
            Assert.That(session.ApplyOrder(UnitOrder.Move(int2.zero), 0, member), Is.EqualTo(UnitOrderResult.InvalidDestination));
        }

        [TestCase(100)]
        [TestCase(500)]
        [TestCase(1000)]
        [TestCase(5000)]
        public void ProfilesColdAndCachedGroupOrders(int count)
        {
            var terrain = new ProceduralTerrainContract(new WorldDefinition(7));
            var min = new int2(-64); var max = new int2(64);
            using var session = new UnitSession(terrain, min, max);
            session.SetNavigationGrid(new NavigationGrid(terrain, min, max, 100));
            using var members = session.SpawnBatch(UnitDefinition.Villager, 0, count, new double3(-60.5, 0, -60.5), default, .75);
            var order = UnitOrder.Move(new int2(20, 20));
            Assert.That(session.ApplyOrder(order, 0, members), Is.EqualTo(UnitOrderResult.Accepted));
            double cold = session.OrderNavigationMilliseconds;
            Assert.That(session.ApplyOrder(order, 0, members), Is.EqualTo(UnitOrderResult.Accepted));
            TestContext.WriteLine(System.FormattableString.Invariant(
                $"RTS_ORDER count={count} cold_ms={cold:F5} cached_ms={session.OrderNavigationMilliseconds:F5} distribution_ms={session.OrderDistributionMilliseconds:F5} application_ms={session.OrderApplicationMilliseconds:F5}"));
        }

        private static void Tick(UnitSession session)
        {
            session.Advance(session.FixedDeltaTime);
            session.CompleteJobs();
        }
    }
}
