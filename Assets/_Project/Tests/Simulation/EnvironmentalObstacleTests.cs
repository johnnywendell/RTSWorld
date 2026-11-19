using System.Collections.Generic;
using System.Diagnostics;
using NUnit.Framework;
using RTSWorld.Simulation;
using RTSWorld.World.Chunks;
using RTSWorld.World.Environment;
using RTSWorld.World.Generation;
using Unity.Mathematics;

namespace RTSWorld.Tests
{
    public sealed class EnvironmentalObstacleTests
    {
        private static ResourcePlacement Resource(int x, int z, NaturalResource kind, float offset = .5f) =>
            new(new ResourceKey(7, 4, x, z), kind, offset, .5f, 0);

        [Test]
        public void OccupancyAndInteractionCrossNegativeChunkBoundary()
        {
            var terrain = new ProceduralTerrainContract(new WorldDefinition(7, cellsPerChunk: 4));
            var tree = Resource(-1, 0, NaturalResource.Tree, .75f);
            var stone = Resource(2, 0, NaturalResource.Stone);
            var gold = Resource(-3, 0, NaturalResource.Gold);
            var min = new int2(-6); var max = new int2(6);
            var obstacles = new EnvironmentalObstacles(min, max, 4, .22, new[] { tree, stone, gold, tree });
            var grid = new NavigationGrid(terrain, min, max, 100, obstacles: obstacles);
            Assert.That(obstacles.ResourceCount, Is.EqualTo(3));
            Assert.That(obstacles.TryGet(tree.Key, out var obstacle), Is.True);
            Assert.That(obstacle.Chunk, Is.EqualTo(new ChunkCoordinate(-1, 0)));
            foreach (int x in new[] { -1, 0, 2 })
            {
                Assert.That(grid.TryGetCost(new int2(x, 0), out int cost), Is.True);
                Assert.That(cost, Is.Zero);
            }
            Assert.That(grid.TryGetCost(new int2(-3, 0), out int goldCost) && goldCost > 0, Is.True);
            var field = NavigationSearch.BuildFlowField(grid, new int2(-4, 0));
            var points = new List<int2>();
            obstacles.FindInteractionCells(grid, tree.Key, 2, points, field);
            Assert.That(points, Is.Not.Empty);
            Assert.That(points, Has.No.Member(new int2(-1, 0)));
            obstacles.FindInteractionCells(grid, stone.Key, .1, points, field);
            Assert.That(points, Is.Empty);
        }

        [Test]
        public void RemovingOneNeighbourPreservesOverlapAndUnrelatedField()
        {
            var terrain = new ProceduralTerrainContract(new WorldDefinition(7));
            var min = new int2(-8); var max = new int2(8);
            var first = Resource(-4, 0, NaturalResource.Stone, .75f);
            var second = Resource(-3, 0, NaturalResource.Stone, .25f);
            var obstacles = new EnvironmentalObstacles(min, max, 32, .22, new[] { first, second });
            var grid = new NavigationGrid(terrain, min, max, 100, cell => cell.x == 0, obstacles: obstacles);
            using var session = new UnitSession(terrain, min, max);
            session.SetNavigationGrid(grid);
            var cache = new NavigationFlowCache(grid);
            var leftTarget = new int2(-6, 2);
            var rightTarget = new int2(4, 2);
            var leftField = cache.GetField(leftTarget);
            var rightField = cache.GetField(rightTarget);
            using var left = session.SpawnBatch(UnitDefinition.Soldier, 0, 1, new double3(-6.5, 0, -2.5), default);
            using var right = session.SpawnBatch(UnitDefinition.Soldier, 0, 1, new double3(3.5, 0, -2.5), default);
            Assert.That(session.SetDestination(left, leftTarget), Is.True);
            Assert.That(session.SetDestination(right, rightTarget), Is.True);
            var timer = Stopwatch.StartNew();
            Assert.That(session.SetEnvironmentalResourceAvailable(first.Key, false), Is.True);
            TestContext.WriteLine($"Obstacle change and selective invalidation in {timer.Elapsed.TotalMilliseconds:F5} ms");
            Assert.That(session.LastInvalidatedFields, Is.EqualTo(1));
            Assert.That(grid.TryGetCost(new int2(-3, 0), out int overlap) && overlap == 0, Is.True);
            Assert.That(leftField.IsCurrent, Is.False);
            Assert.That(cache.GetField(leftTarget), Is.Not.SameAs(leftField));
            Assert.That(cache.GetField(rightTarget), Is.SameAs(rightField));
            session.Advance(.1f);
            session.CompleteJobs();
            var manager = session.World.EntityManager;
            Assert.That(manager.GetComponentData<UnitMotion>(left[0]).Activity, Is.EqualTo(UnitActivity.WaitingForNavigation));
            Assert.That(manager.GetComponentData<UnitMotion>(right[0]).Activity, Is.EqualTo(UnitActivity.Moving));
            Assert.That(session.SetEnvironmentalResourceAvailable(second.Key, false), Is.True);
            Assert.That(grid.TryGetCost(new int2(-3, 0), out int opened) && opened > 0, Is.True);
            Assert.That(session.SetEnvironmentalResourceAvailable(second.Key, true), Is.True);
            Assert.That(grid.TryGetCost(new int2(-3, 0), out int added) && added == 0, Is.True);
            Assert.That(session.SetEnvironmentalResourceRadius(second.Key, 0), Is.True);
            Assert.That(grid.TryGetCost(new int2(-3, 0), out int resized) && resized > 0, Is.True);
            Assert.That(cache.GetField(rightTarget), Is.SameAs(rightField));
        }

        [Test]
        public void FlowAndAStarDetourAroundRockAndTreeThroughNarrowOpening()
        {
            var terrain = new ProceduralTerrainContract(new WorldDefinition(7, cellsPerChunk: 4));
            var min = new int2(-8); var max = new int2(8);
            var tree = Resource(0, 0, NaturalResource.Tree);
            var stone = Resource(0, 2, NaturalResource.Stone);
            var obstacles = new EnvironmentalObstacles(min, max, 4, .22, new[] { tree, stone });
            var grid = new NavigationGrid(terrain, min, max, 100, cell => cell.x == 0 && cell.y != 0 && cell.y != 2 && cell.y != 5,
                obstacles: obstacles);
            int2 start = new(-6, 0), goal = new(6, 0);
            var path = new List<int2>();
            Assert.That(NavigationSearch.TryFindPath(grid, start, goal, path), Is.True);
            Assert.That(path, Does.Contain(new int2(0, 5)));
            Assert.That(NavigationSearch.TryFindPath(grid, start, new int2(0, 0), path), Is.False);

            using var session = new UnitSession(terrain, min, max);
            session.SetNavigationGrid(grid);
            using var members = session.SpawnBatch(UnitDefinition.Soldier, 0, 1, new double3(-5.5, 0, .5), default);
            Assert.That(session.ApplyOrder(UnitOrder.Move(goal), 0, members), Is.EqualTo(UnitOrderResult.Accepted));
            bool usedGap = false;
            var manager = session.World.EntityManager;
            for (int i = 0; i < 300; i++)
            {
                session.Advance(.05f);
                session.CompleteJobs();
                var pose = manager.GetComponentData<LogicalPose>(members[0]);
                var cell = (int2)math.floor(pose.Position.xz);
                usedGap |= cell.Equals(new int2(0, 5));
                Assert.That(grid.TryGetCost(cell, out int cost) && cost > 0, Is.True);
            }
            Assert.That(usedGap, Is.True);
            Assert.That(manager.GetComponentData<UnitMotion>(members[0]).Activity, Is.EqualTo(UnitActivity.Idle));
            Assert.That((int2)math.floor(manager.GetComponentData<LogicalPose>(members[0]).Position.xz), Is.EqualTo(goal));
        }
    }
}
