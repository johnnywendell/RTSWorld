using System;
using System.Collections.Generic;
using RTSWorld.World.Chunks;
using RTSWorld.World.Environment;
using Unity.Mathematics;

namespace RTSWorld.Simulation
{
    public readonly struct EnvironmentalObstacle
    {
        public EnvironmentalObstacle(ResourcePlacement placement, ChunkCoordinate chunk, double radius, bool available)
        {
            Placement = placement;
            Chunk = chunk;
            Radius = radius;
            Available = available;
        }

        public ResourcePlacement Placement { get; }
        public ChunkCoordinate Chunk { get; }
        public double Radius { get; }
        public bool Available { get; }
        public double2 Center => new((double)Placement.Key.X + Placement.OffsetX, (double)Placement.Key.Z + Placement.OffsetZ);
    }

    /// <summary>Bounded catalogue and occupancy counts independent of visual residency.</summary>
    public sealed class EnvironmentalObstacles
    {
        private readonly Dictionary<ResourceKey, EnvironmentalObstacle> resources = new();
        internal const byte AlongX = 1, AlongZ = 2, FullCell = 4;
        internal const double LaneHalfWidth = .01;
        private const double MaximumAgentRadius = .49;
        private readonly int[] occupancy, horizontalObstructions, verticalObstructions;
        private readonly Dictionary<int, byte> previousPassages = new();
        public int2 Min { get; }
        public int2 Size { get; }
        public double Clearance { get; }
        internal IEnumerable<ResourceKey> ResourceKeys => resources.Keys;
        public int ResourceCount => resources.Count;
        public IEnumerable<EnvironmentalObstacle> Entries => resources.Values;

        public EnvironmentalObstacles(int2 min, int2 max, int cellsPerChunk, double clearance,
            IEnumerable<ResourcePlacement> placements)
        {
            if (math.any(max <= min) || math.any((double2)max - min > 256))
                throw new ArgumentOutOfRangeException(nameof(max));
            if (!math.isfinite(clearance) || clearance < 0 || clearance > 1)
                throw new ArgumentOutOfRangeException(nameof(clearance));
            Min = min;
            Size = max - min;
            Clearance = clearance;
            occupancy = new int[Size.x * Size.y];
            horizontalObstructions = new int[occupancy.Length];
            verticalObstructions = new int[occupancy.Length];
            foreach (var placement in placements)
            {
                double variation = 0.8 + EnvironmentNoise.Hash(placement.Key.Seed, placement.Key.X, placement.Key.Z, 31) * 0.4;
                double radius = placement.Kind switch
                {
                    NaturalResource.Tree => 0.13 * Math.Sqrt(2) * variation,
                    NaturalResource.Stone => 0.5 * Math.Sqrt(2) * variation,
                    _ => 0
                };
                var chunk = ChunkSelection.FromPosition(placement.Key.X, placement.Key.Z, cellsPerChunk);
                var entry = new EnvironmentalObstacle(placement, chunk, radius, true);
                if (resources.TryAdd(placement.Key, entry)) Rasterize(entry, 1, null);
            }
        }

        public static EnvironmentalObstacles Build(ProceduralTerrainContract terrain, int2 min, int2 max, double clearance)
        {
            int cells = terrain.Definition.CellsPerChunk;
            var first = terrain.Region(min.x - 2.0, min.y - 2.0);
            var last = terrain.Region(max.x + 1.0, max.y + 1.0);
            return new EnvironmentalObstacles(min, max, cells, clearance, Placements());

            IEnumerable<ResourcePlacement> Placements()
            {
                for (int z = first.Z; z <= last.Z; z++)
                    for (int x = first.X; x <= last.X; x++)
                        foreach (var placement in terrain.GetEnvironmentChunk(new ChunkCoordinate(x, z)).Resources)
                            yield return placement;
            }
        }

        public bool TryGet(ResourceKey key, out EnvironmentalObstacle resource) => resources.TryGetValue(key, out resource);

        internal bool IsBlocked(int index) => occupancy[index] != 0;

        internal byte Passage(int index) => occupancy[index] == 0 ? FullCell :
            (byte)((horizontalObstructions[index] == 0 ? AlongX : 0) |
                   (verticalObstructions[index] == 0 ? AlongZ : 0));

        private void CollectChanges(List<int> changed)
        {
            foreach (var pair in previousPassages)
                if (Passage(pair.Key) != pair.Value)
                    changed.Add(pair.Key);
            previousPassages.Clear();
        }

        internal bool SetAvailable(ResourceKey key, bool available, List<int> changed)
        {
            if (!resources.TryGetValue(key, out var old) || old.Available == available) return false;
            var updated = new EnvironmentalObstacle(old.Placement, old.Chunk, old.Radius, available);
            resources[key] = updated;
            Rasterize(updated, available ? 1 : -1, changed == null ? null : previousPassages);
            if (changed != null) CollectChanges(changed);
            return true;
        }

        internal bool SetRadius(ResourceKey key, double radius, List<int> changed)
        {
            if (!math.isfinite(radius) || radius < 0 || radius > 1)
                throw new ArgumentOutOfRangeException(nameof(radius));
            if (!resources.TryGetValue(key, out var old) || old.Radius == radius) return false;
            var updated = new EnvironmentalObstacle(old.Placement, old.Chunk, radius, old.Available);
            resources[key] = updated;
            if (old.Available)
            {
                var previous = changed == null ? null : previousPassages;
                Rasterize(old, -1, previous);
                Rasterize(updated, 1, previous);
                if (changed != null) CollectChanges(changed);
            }
            return true;
        }

        public void FindInteractionCells(NavigationGrid grid, ResourceKey key, double distance,
            List<int2> result, NavigationFlowField reachableFrom)
        {
            result.Clear();
            if (!resources.TryGetValue(key, out var resource) || !resource.Available ||
                !math.isfinite(distance) || distance <= 0 || distance > 256)
                return;

            double2 center = resource.Center;
            int2 first = math.max(Min, (int2)math.floor(center - distance));
            int2 last = math.min(Min + Size - 1, (int2)math.floor(center + distance));
            for (int z = first.y; z <= last.y; z++)
                for (int x = first.x; x <= last.x; x++)
                {
                    var cell = new int2(x, z);
                    if (math.distancesq((double2)cell + .5, center) <= distance * distance &&
                        grid.TryGetCost(cell, out int cost) && cost > 0 &&
                        reachableFrom.TryGetDirection(cell, out _))
                        result.Add(cell);
                }

            result.Sort((a, b) =>
            {
                int order = grid.IsFullPassage(b).CompareTo(grid.IsFullPassage(a));
                if (order != 0) return order;
                order = math.distancesq((double2)a + .5, center).CompareTo(math.distancesq((double2)b + .5, center));
                return order != 0 ? order : grid.Index(a).CompareTo(grid.Index(b));
            });
        }

        private void Rasterize(EnvironmentalObstacle obstacle, int delta, Dictionary<int, byte> previous)
        {
            if (obstacle.Radius <= 0) return;
            double cellRadius = obstacle.Radius + Clearance;
            double laneRadius = obstacle.Radius + math.max(Clearance, MaximumAgentRadius);
            double2 center = obstacle.Center;
            // Cells are closed squares for the tangency test, including on the lower side.
            int2 first = math.max(Min, (int2)math.ceil(center - laneRadius) - 1);
            int2 last = math.min(Min + Size - 1, (int2)math.floor(center + laneRadius));
            for (int z = first.y; z <= last.y; z++)
            for (int x = first.x; x <= last.x; x++)
            {
                double2 cell = new(x, z);
                var nearest = math.clamp(center, cell, cell + 1);
                double distance = math.distancesq(center, nearest);
                if (distance > laneRadius * laneRadius) continue;

                int index = x - Min.x + (z - Min.y) * Size.x;
                previous?.TryAdd(index, Passage(index));
                if (distance <= cellRadius * cellRadius) occupancy[index] += delta;

                var xLane = math.clamp(center, cell + new double2(0, .5 - LaneHalfWidth),
                    cell + new double2(1, .5 + LaneHalfWidth));
                var zLane = math.clamp(center, cell + new double2(.5 - LaneHalfWidth, 0),
                    cell + new double2(.5 + LaneHalfWidth, 1));
                if (math.distancesq(center, xLane) <= laneRadius * laneRadius)
                    horizontalObstructions[index] += delta;
                if (math.distancesq(center, zLane) <= laneRadius * laneRadius)
                    verticalObstructions[index] += delta;
            }
        }
    }
}
