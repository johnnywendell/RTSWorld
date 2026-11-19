using System.Collections.Generic;
using System.Diagnostics;
using NUnit.Framework;
using RTSWorld.Simulation;
using RTSWorld.World.Environment;
using RTSWorld.World.Generation;
using Unity.Mathematics;

namespace RTSWorld.Tests
{
    public sealed class NavigationMovementTests
    {
        [Test]
        public void GroupStopsBeyondBarrierAndUsesDistinctLocalOffsets()
        {
            var terrain = new ProceduralTerrainContract(new WorldDefinition(7, cellsPerChunk: 4));
            var grid = new NavigationGrid(terrain, new int2(-8), new int2(8), 100,
                cell => cell.x == 0 && cell.y != 2);
            using var session = new UnitSession(terrain, grid.Min, grid.Min + grid.Size);
            session.SetNavigationGrid(grid);
            using var units = session.SpawnBatch(UnitDefinition.Soldier, 0, 4, new double3(-4.5, 0, 1.5), default, .02);
            Assert.That(session.SetDestination(units, new int2(0, 0)), Is.False);
            Assert.That(session.SetDestination(units, new int2(4, 0)), Is.True);
            var manager = session.World.EntityManager;
            for (int tick = 0; tick < 160; tick++)
            {
                session.Advance(session.FixedDeltaTime);
                session.CompleteJobs();
                foreach (var unit in units)
                {
                    var pose = manager.GetComponentData<LogicalPose>(unit);
                    Assert.That(grid.TryGetCost((int2)math.floor(pose.Position.xz), out int cost) && cost > 0, Is.True);
                }
            }

            var positions = new HashSet<double2>();
            foreach (var unit in units)
            {
                var pose = manager.GetComponentData<LogicalPose>(unit);
                Assert.That((int2)math.floor(pose.Position.xz), Is.EqualTo(new int2(4, 0)));
                Assert.That(manager.GetComponentData<MovementVelocity>(unit).Value, Is.EqualTo(double3.zero));
                positions.Add(pose.Position.xz);
            }
            Assert.That(positions.Count, Is.GreaterThan(1));
        }

        [Test]
        public void ClassifiesWaterSlopeCostsAndMissingRegions()
        {
            var world = new WorldDefinition(7, cellsPerChunk: 4);
            var dry = new ProceduralTerrainContract(world);
            var wet = new ProceduralTerrainContract(world, new EnvironmentDefinition(seaLevel: 100));
            var cell = new int2(-1, 0);
            var terrainGrid = new NavigationGrid(dry, new int2(-8), new int2(8), 100,
                isRegionAvailable: region => region.X != -2);
            Assert.That(terrainGrid.TryGetCost(cell, out int cost), Is.True);
            Assert.That(cost, Is.GreaterThan(1));
            Assert.That(terrainGrid.TryGetCost(new int2(-5, 0), out _), Is.False);

            var cliffGrid = new NavigationGrid(dry, new int2(-1), new int2(1), 0);
            Assert.That(cliffGrid.TryGetCost(cell, out cost), Is.True);
            Assert.That(cost, Is.Zero);
            var waterGrid = new NavigationGrid(wet, new int2(-1), new int2(1), 100);
            Assert.That(waterGrid.TryGetCost(cell, out cost), Is.True);
            Assert.That(cost, Is.Zero);
        }

        [Test]
        public void GridRevisionAndTerrainSuspensionFreezeUntilCommandIsAvailable()
        {
            var terrain = new ProceduralTerrainContract(new WorldDefinition(7));
            var min = new int2(-4);
            var max = new int2(4);
            using var session = new UnitSession(terrain, min, max);
            session.SetNavigationGrid(new NavigationGrid(terrain, min, max, 100));
            using var units = session.SpawnBatch(UnitDefinition.Soldier, 0, 1, new double3(-1.5, 0, -.5), default);
            var unit = units[0];
            var manager = session.World.EntityManager;
            Assert.That(session.SetDestination(units, new int2(2, 0)), Is.True);
            session.Advance(.1f);
            session.CompleteJobs();
            var before = manager.GetComponentData<LogicalPose>(unit).Position;

            session.SetTerrainAvailable(false);
            session.Advance(.1f);
            session.CompleteJobs();
            Assert.That(manager.GetComponentData<UnitMotion>(unit).Activity, Is.EqualTo(UnitActivity.WaitingForTerrain));
            Assert.That(manager.GetComponentData<LogicalPose>(unit).Position, Is.EqualTo(before));

            session.SetNavigationGrid(new NavigationGrid(terrain, min, max, 100));
            session.SetTerrainAvailable(true);
            session.Advance(.1f);
            session.CompleteJobs();
            Assert.That(manager.GetComponentData<UnitMotion>(unit).Activity, Is.EqualTo(UnitActivity.WaitingForNavigation));
            Assert.That(manager.GetComponentData<LogicalPose>(unit).Position, Is.EqualTo(before));
            Assert.That(session.SetDestination(units, new int2(2, 0)), Is.True);
            session.Advance(.1f);
            session.CompleteJobs();
            Assert.That(manager.GetComponentData<LogicalPose>(unit).Position, Is.Not.EqualTo(before));
        }

        [TestCase(8)]
        [TestCase(100)]
        [TestCase(500)]
        [TestCase(5000)]
        public void ProfilesTerrainPathFieldAndFixedMovement(int count)
        {
            var terrain = new ProceduralTerrainContract(new WorldDefinition(7));
            var min = new int2(-64);
            var max = new int2(64);
            var goal = new int2(40, 30);
            var sw = Stopwatch.StartNew();
            var grid = new NavigationGrid(terrain, min, max, 100, cell => cell.x == 0 && math.abs(cell.y) > 5);
            double gridMs = sw.Elapsed.TotalMilliseconds;

            sw.Restart();
            var path = new List<int2>();
            Assert.That(NavigationSearch.TryFindPath(grid, new int2(-60, -30), goal, path), Is.True);
            double pathMs = sw.Elapsed.TotalMilliseconds;

            sw.Restart();
            var flow = NavigationSearch.BuildFlowField(grid, goal);
            double fieldMs = sw.Elapsed.TotalMilliseconds;
            Assert.That(flow.TryGetDirection(new int2(-60, -30), out _), Is.True);
            using var session = new UnitSession(terrain, min, max);
            session.SetNavigationGrid(grid);
            using var units = session.SpawnBatch(UnitDefinition.Soldier, 0, count,
                new double3(-60.5, 0, -30.5), default, .75);
            Assert.That(session.SetDestination(units, goal), Is.True);
            for (int i = 0; i < 16; i++)
            {
                session.Advance(session.FixedDeltaTime);
                session.CompleteJobs();
            }

            ulong tick = session.Tick;
            sw.Restart();
            for (int i = 0; i < 120; i++)
            {
                session.Advance(session.FixedDeltaTime);
                session.CompleteJobs();
            }
            double totalMs = sw.Elapsed.TotalMilliseconds;
            Assert.That(session.Tick - tick, Is.EqualTo(120ul));
            Assert.That(session.Count, Is.EqualTo(count));
            Assert.That(session.World.EntityManager.GetComponentData<UnitMotion>(units[0]).Activity, Is.EqualTo(UnitActivity.Moving));
            TestContext.WriteLine(Formattable(count, gridMs, pathMs, fieldMs, totalMs / 120));
        }

        private static string Formattable(int count, double grid, double path, double flow, double movement) =>
            System.FormattableString.Invariant($"NAV_PROFILE count={count} grid_ms={grid:F5} astar_ms={path:F5} field_ms={flow:F5} tick_mean_ms={movement:F5}");
    }
}
