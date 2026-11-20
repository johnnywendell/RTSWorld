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

        [TestCase(.25, true)]
        [TestCase(.85, false)]
        public void ParallelObstaclesClassifyWideAndPhysicallyNarrowCorridors(double radius, bool accessible)
        {
            var terrain = new ProceduralTerrainContract(new WorldDefinition(7));
            var lower = Resource(0, -1, NaturalResource.Stone);
            var upper = Resource(0, 1, NaturalResource.Stone);
            var min = new int2(-4);
            var max = new int2(4);
            var obstacles = new EnvironmentalObstacles(min, max, 32, .22, new[] { lower, upper });
            var grid = new NavigationGrid(terrain, min, max, 100, cell => cell.y != 0, obstacles: obstacles);
            using var session = new UnitSession(terrain, min, max);
            session.SetNavigationGrid(grid);
            Assert.That(session.SetEnvironmentalResourceRadius(lower.Key, radius));
            Assert.That(session.SetEnvironmentalResourceRadius(upper.Key, radius));
            Assert.That(2 - 2 * radius >= 2 * UnitDefinition.Soldier.OccupancyRadius, Is.EqualTo(accessible));
            Assert.That(NavigationSearch.TryFindPath(grid, new int2(-2, 0), new int2(2, 0), new List<int2>()),
                Is.EqualTo(accessible));
        }

        [Test]
        public void IsolatedTreeBlocksItsCellAndCanBeCircumvented()
        {
            var terrain = new ProceduralTerrainContract(new WorldDefinition(7));
            var min = new int2(-4);
            var max = new int2(4);
            var obstacles = new EnvironmentalObstacles(min, max, 32, .22,
                new[] { Resource(0, 0, NaturalResource.Tree) });
            var grid = new NavigationGrid(terrain, min, max, 100, obstacles: obstacles);
            var path = new List<int2>();
            Assert.That(NavigationSearch.TryFindPath(grid, new int2(-2, 0), int2.zero, path), Is.False);
            Assert.That(NavigationSearch.TryFindPath(grid, new int2(-2, 0), new int2(2, 0), path), Is.True);
            Assert.That(path, Has.No.Member(int2.zero));
        }

        [TestCase(NaturalResource.Tree, .22, true)]
        [TestCase(NaturalResource.Stone, .49, true)]
        public void AdjacentResourcesPreserveOnlyCorridorsThatFitTheGrid(
            NaturalResource neighbor, double radius, bool accessible)
        {
            var terrain = new ProceduralTerrainContract(new WorldDefinition(7, cellsPerChunk: 4));
            var tree = Resource(0, -1, NaturalResource.Tree);
            var upper = Resource(0, 1, neighbor);
            var min = new int2(-4);
            var max = new int2(4);
            var obstacles = new EnvironmentalObstacles(min, max, 4, .22, new[] { tree, upper });
            var grid = new NavigationGrid(terrain, min, max, 100,
                cell => cell.y != 0, obstacles: obstacles);
            using var session = new UnitSession(terrain, min, max);
            session.SetNavigationGrid(grid);
            Assert.That(session.SetEnvironmentalResourceRadius(upper.Key, radius), Is.True);

            var path = new List<int2>();
            Assert.That(NavigationSearch.TryFindPath(grid, new int2(-2, 0), new int2(2, 0), path),
                Is.EqualTo(accessible));
            if (!accessible)
            {
                Assert.That(session.SetEnvironmentalResourceAvailable(upper.Key, false), Is.True);
                Assert.That(NavigationSearch.TryFindPath(grid, new int2(-2, 0), new int2(2, 0), path), Is.True);
            }
        }

        [TestCase(false)]
        [TestCase(true)]
        public void SoldierCrossesTreeAndRockOnlyAlongTheSafeLane(bool avoidance)
        {
            var terrain = new ProceduralTerrainContract(new WorldDefinition(7, cellsPerChunk: 4));
            var tree = Resource(0, -1, NaturalResource.Tree);
            var rock = Resource(0, 1, NaturalResource.Stone);
            var min = new int2(-4);
            var max = new int2(4);
            var obstacles = new EnvironmentalObstacles(min, max, 4, .22, new[] { tree, rock });
            var grid = new NavigationGrid(terrain, min, max, 100, cell => cell.y != 0, obstacles: obstacles);
            using var session = new UnitSession(terrain, min, max, crowdEnabled: avoidance);
            session.SetNavigationGrid(grid);
            Assert.That(session.SetEnvironmentalResourceRadius(rock.Key, .49), Is.True);
            Assert.That(grid.GetPlacementIssue(int2.zero), Is.EqualTo(PlacementIssue.Obstacle));

            var origin = new int2(-2, 0);
            var goal = new int2(2, 0);
            var path = new List<int2>();
            Assert.That(NavigationSearch.TryFindPath(grid, origin, goal, path), Is.True);
            var field = NavigationSearch.BuildFlowField(grid, goal);
            for (int i = 0; i < path.Count - 1; i++)
            {
                Assert.That(field.TryGetDirection(path[i], out var direction), Is.True);
                Assert.That(path[i] + direction, Is.EqualTo(path[i + 1]));
            }

            var definition = UnitDefinition.Soldier;
            using var units = session.SpawnBatch(definition, 0, 1, new double3(-1.5, 0, .8), default);
            Assert.That(session.ApplyOrder(UnitOrder.Move(goal), 0, units), Is.EqualTo(UnitOrderResult.Accepted));
            var manager = session.World.EntityManager;
            var position = manager.GetComponentData<LogicalPose>(units[0]).Position.xz;
            for (int tick = 0; tick < 100; tick++)
            {
                session.Advance(session.FixedDeltaTime);
                session.CompleteJobs();
                var next = manager.GetComponentData<LogicalPose>(units[0]).Position.xz;
                foreach (var obstacle in obstacles.Entries)
                {
                    var delta = next - position;
                    double lengthSquared = math.lengthsq(delta);
                    double fraction = lengthSquared == 0 ? 0 :
                        math.saturate(math.dot(obstacle.Center - position, delta) / lengthSquared);
                    Assert.That(math.distance(position + delta * fraction, obstacle.Center),
                        Is.GreaterThanOrEqualTo(obstacle.Radius + definition.OccupancyRadius));
                }
                position = next;
            }
            Assert.That(position, Is.EqualTo(new double2(2.5, .5)));

            using var probe = session.SpawnBatch(definition, 0, 1, new double3(.5, 0, .5), default);
            session.SetIntention(probe[0], new double2(0, 3));
            session.Advance(session.FixedDeltaTime);
            session.CompleteJobs();
            Assert.That(manager.GetComponentData<LogicalPose>(probe[0]).Position.xz, Is.EqualTo(new double2(.5, .5)));

            // Radius change closes the lane even though the coarse placement mask stays blocked.
            Assert.That(session.SetEnvironmentalResourceRadius(rock.Key, .85), Is.True);
            Assert.That(field.IsCurrent, Is.False);
            Assert.That(NavigationSearch.TryFindPath(grid, origin, goal, path), Is.False);
            Assert.That(NavigationSearch.BuildFlowField(grid, goal).TryGetDirection(origin, out _), Is.False);
        }

        [Test]
        public void TangentCellsAreBlockedOnBothSidesOfNegativeChunkBoundary()
        {
            var terrain = new ProceduralTerrainContract(new WorldDefinition(7, cellsPerChunk: 4));
            var tree = Resource(-1, 0, NaturalResource.Tree);
            var min = new int2(-4);
            var max = new int2(4);
            var obstacles = new EnvironmentalObstacles(min, max, 4, 0, new[] { tree });
            var grid = new NavigationGrid(terrain, min, max, 100, obstacles: obstacles);
            using var session = new UnitSession(terrain, min, max);
            session.SetNavigationGrid(grid);

            Assert.That(session.SetEnvironmentalResourceRadius(tree.Key, .5), Is.True);
            foreach (int x in new[] { -2, 0 })
            {
                var cell = new int2(x, 0);
                Assert.That(grid.GetPlacementIssue(cell), Is.EqualTo(PlacementIssue.Obstacle));
                Assert.That(grid.TryGetCost(cell, out int cost), Is.True);
                Assert.That(cost, Is.Zero);
            }

            Assert.That(session.SetEnvironmentalResourceRadius(tree.Key, .49), Is.True);
            Assert.That(grid.GetPlacementIssue(new int2(-2, 0)), Is.EqualTo(PlacementIssue.None));
            Assert.That(grid.GetPlacementIssue(new int2(0, 0)), Is.EqualTo(PlacementIssue.None));
        }

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
            foreach (int x in new[] { -1, 2 })
            {
                Assert.That(grid.TryGetCost(new int2(x, 0), out int cost), Is.True);
                Assert.That(cost, Is.Zero);
            }
            // The neighboring cell still reserves coarse occupancy for placement.
            // A vertical lane may pass it without entering either obstacle's center cell.
            Assert.That(grid.GetPlacementIssue(int2.zero), Is.EqualTo(PlacementIssue.Obstacle));
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
