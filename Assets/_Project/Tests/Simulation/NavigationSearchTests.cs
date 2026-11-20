using System.Collections.Generic;
using NUnit.Framework;
using RTSWorld.Simulation;
using RTSWorld.World.Generation;
using Unity.Mathematics;

namespace RTSWorld.Tests.Simulation
{
    public sealed class NavigationSearchTests
    {
        private static readonly int2 Start = new int2(-6, 0);
        private static readonly int2 Goal = new int2(6, 0);
        private static readonly int2 Gap = new int2(0, 2);

        [Test]
        public void DryShoreRouteDoesNotCrossWater()
        {
            var terrain = new ProceduralTerrainContract(new WorldDefinition(7),
                new RTSWorld.World.Environment.EnvironmentDefinition());
            var min = new int2(-248, 256);
            var max = new int2(-56, 448);
            var grid = new NavigationGrid(terrain, min, max, 2.25);
            var path = new List<int2>();
            for (int z = min.y; z < max.y - 1; z++)
            for (int x = min.x; x < max.x - 1; x++)
            {
                var start = new int2(x, z);
                var goal = start + new int2(1, 0);
                var water = start + new int2(0, 1);
                if (grid.GetPlacementIssue(start) != PlacementIssue.None ||
                    grid.GetPlacementIssue(goal) != PlacementIssue.None ||
                    !grid.GetPlacementIssue(water).HasFlag(PlacementIssue.Water)) continue;
                Assert.That(NavigationSearch.TryFindPath(grid, start, goal, path), Is.True);
                Assert.That(path, Is.EqualTo(new[] { start, goal }));
                Assert.That(NavigationSearch.TryFindPath(grid, start, water, path), Is.False);
                return;
            }
            Assert.Fail("Nenhum par de células secas junto à margem na seed de teste.");
        }

        [Test]
        public void FullyBlockedRegionHasNoPath()
        {
            var terrain = new ProceduralTerrainContract(new WorldDefinition(7));
            var grid = new NavigationGrid(terrain, new int2(-4), new int2(4), 100, _ => true);
            Assert.That(NavigationSearch.TryFindPath(grid, int2.zero, new int2(1, 0), new List<int2>()), Is.False);
        }

        [Test]
        public void PathAndFlowCrossChunkBoundariesThroughTheOnlyOpening()
        {
            var grid = CreateGrid(true);
            var path = new List<int2>();
            Assert.That(NavigationSearch.TryFindPath(grid, Start, Goal, path), Is.True);
            CollectionAssert.Contains(path, Gap);
            Assert.That(path[0], Is.EqualTo(Start));
            Assert.That(path[path.Count - 1], Is.EqualTo(Goal));

            int pathCost = 0;
            for (int i = 1; i < path.Count; i++)
            {
                Assert.That(grid.TryGetCost(path[i], out int cost), Is.True);
                pathCost += cost;
            }

            var flow = NavigationSearch.BuildFlowField(grid, Goal);
            int flowCost = 0;
            int2 cell = Start;
            int steps = 0;
            for (; !cell.Equals(Goal) && steps < 128; steps++)
            {
                Assert.That(flow.TryGetDirection(cell, out var direction), Is.True);
                Assert.That(math.csum(math.abs(direction)), Is.EqualTo(1));
                cell += direction;
                Assert.That(grid.TryGetCost(cell, out int cost) && cost > 0, Is.True);
                flowCost += cost;
            }

            Assert.That(flowCost, Is.EqualTo(pathCost));
            Assert.That(flow.TryGetIntegrationCost(Start, path.Count - 1, out int integrated), Is.True);
            Assert.That(integrated, Is.EqualTo(pathCost));
            Assert.That(flow.TryGetIntegrationCost(Start, path.Count - 2, out _), Is.False);
            Assert.That(flow.TryGetIntegrationCost(Goal, 0, out integrated), Is.True);
            Assert.That(integrated, Is.Zero);
            Assert.That(cell, Is.EqualTo(Goal));
            Assert.That(flow.TryGetDirection(Goal, out var stop), Is.True);
            Assert.That(stop, Is.EqualTo(int2.zero));
        }

        [Test]
        public void CacheSharesFieldsAndInvalidatesWhenGridChanges()
        {
            var cache = new NavigationFlowCache(CreateGrid(true), 1);
            var initial = cache.GetField(Goal);
            Assert.That(cache.GetField(Goal), Is.SameAs(initial));
            cache.GetField(Start);
            Assert.That(cache.GetField(Goal), Is.Not.SameAs(initial));

            var replacement = CreateGrid(false);
            cache.ReplaceGrid(replacement);
            Assert.That(cache.GetField(Goal).TryGetDirection(Start, out _), Is.False);
            Assert.That(NavigationSearch.TryFindPath(replacement, Start, Goal, new List<int2>()), Is.False);
            Assert.That(replacement.TryGetCost(new int2(8, 0), out _), Is.False);
        }

        private static NavigationGrid CreateGrid(bool open) =>
            new NavigationGrid(new ProceduralTerrainContract(new WorldDefinition(7, cellsPerChunk: 4)),
                new int2(-8, -4), new int2(8, 4), 1000, cell => cell.x == 0 && (!open || !cell.Equals(Gap)));
    }
}
