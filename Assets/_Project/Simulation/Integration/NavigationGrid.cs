using System;
using System.Collections.Generic;
using RTSWorld.World.Environment;
using Unity.Mathematics;
using RTSWorld.World.Chunks;

namespace RTSWorld.Simulation
{
    /// <summary>Immutable costs for a bounded window of global terrain cells. Zero means blocked.</summary>
    public sealed class NavigationGrid
    {
        private readonly int[] costs;
        private readonly double[] surfaceGround, surfaceWater;
        private readonly int[] obstacleChange;
        private readonly List<int> changedCells = new();
        internal int ObstacleRevision { get; private set; }
        public EnvironmentalObstacles Obstacles { get; }

        public int2 Min { get; }
        public int2 Size { get; }
        public int CellsPerChunk { get; }

        public NavigationGrid(ProceduralTerrainContract terrain, int2 min, int2 max,
            double maximumSlope, Func<int2, bool> isObstacle = null,
            Func<ChunkCoordinate, bool> isRegionAvailable = null, bool captureSurface = false, EnvironmentalObstacles obstacles = null)
        {
            if (terrain == null) throw new ArgumentNullException(nameof(terrain));
            if (math.any(max <= min) || math.any((double2)max - min > 256))
                throw new ArgumentOutOfRangeException(nameof(max));
            if (!math.isfinite(maximumSlope) || maximumSlope < 0)
                throw new ArgumentOutOfRangeException(nameof(maximumSlope));

            CellsPerChunk = terrain.Definition.CellsPerChunk;
            Min = min;
            Size = max - min;
            costs = new int[Size.x * Size.y];
            if (obstacles != null)
            {
                if (!obstacles.Min.Equals(Min) || !obstacles.Size.Equals(Size))
                    throw new ArgumentException("Obstacle bounds must match the navigation bounds.", nameof(obstacles));
                Obstacles = obstacles;
                obstacleChange = new int[costs.Length];
            }

            int stride = Size.x + 1;
            var heights = new double[stride * (Size.y + 1)];
            var water = new bool[heights.Length];
            if (captureSurface)
            {
                surfaceGround = heights;
                surfaceWater = new double[heights.Length];
            }
            for (int z = 0; z <= Size.y; z++)
                for (int x = 0; x <= Size.x; x++)
                {
                    int vertex = x + z * stride;
                    heights[vertex] = terrain.SampleVertex((long)min.x + x, (long)min.y + z, out double level);
                    water[vertex] = level > heights[vertex];
                    if (captureSurface) surfaceWater[vertex] = level;
                }

            for (int z = 0; z < Size.y; z++)
                for (int x = 0; x < Size.x; x++)
                {
                    int2 cell = min + new int2(x, z);
                    if (isRegionAvailable != null && !isRegionAvailable(terrain.Region(cell.x, cell.y)))
                    {
                        costs[x + z * Size.x] = -1;
                        continue;
                    }

                    int a = x + z * stride;
                    int b = a + 1, c = a + stride, d = c + 1;
                    if (water[a] || water[b] || water[c] || water[d] ||
                        (isObstacle != null && isObstacle(cell)))
                        continue;

                    double slope = math.max(
                        math.length(new double2(heights[b] - heights[a], heights[c] - heights[a])),
                        math.length(new double2(heights[d] - heights[c], heights[d] - heights[b])));
                    if (slope <= maximumSlope)
                        costs[x + z * Size.x] = 1 + (int)math.min(254, math.ceil(slope * 10));
                }
        }

        /// <summary>Samples the top visible surface from the same triangles used for navigation.</summary>
        public bool TryGetSurface(double x, double z, out double height)
        {
            var point = new double2(x, z);
            var max = Min + Size;
            if (surfaceGround == null || !math.all(math.isfinite(point)) ||
                math.any(point < Min) || math.any(point > max))
            {
                height = 0;
                return false;
            }

            int2 cell = math.min((int2)math.floor(point), max - 1);
            double2 uv = point - cell;
            int stride = Size.x + 1;
            int a = cell.x - Min.x + (cell.y - Min.y) * stride;
            height = math.max(Interpolate(surfaceGround), Interpolate(surfaceWater));
            return true;

            double Interpolate(double[] values) => uv.x + uv.y <= 1
                ? values[a] + (values[a + 1] - values[a]) * uv.x + (values[a + stride] - values[a]) * uv.y
                : values[a + stride + 1] + (values[a + stride] - values[a + stride + 1]) * (1 - uv.x)
                    + (values[a + 1] - values[a + stride + 1]) * (1 - uv.y);
        }

        public bool TryGetCost(int2 cell, out int cost)
        {
            if (math.any(cell < Min) || math.any(cell >= Min + Size))
            {
                cost = 0;
                return false;
            }

            int index = Index(cell);
            int value = costs[index];
            if (value > 0 && Obstacles != null && Obstacles.IsBlocked(index)) value = 0;
            cost = math.max(0, value);
            return value >= 0;
        }

        internal bool ChangeResourceAvailability(ResourceKey key, bool available)
        {
            changedCells.Clear();
            if (Obstacles == null || !Obstacles.SetAvailable(key, available, changedCells)) return false;
            RecordObstacleRevision();
            return true;
        }

        internal bool ChangeResourceRadius(ResourceKey key, double radius)
        {
            changedCells.Clear();
            if (Obstacles == null || !Obstacles.SetRadius(key, radius, changedCells)) return false;
            RecordObstacleRevision();
            return true;
        }

        private void RecordObstacleRevision()
        {
            if (changedCells.Count == 0) return;
            int revision = checked(++ObstacleRevision);
            foreach (int index in changedCells)
                if (costs[index] > 0) obstacleChange[index] = revision;
        }

        internal bool AffectsField(int previousRevision, int[] successors)
        {
            if (previousRevision == ObstacleRevision) return false;
            for (int i = 0; i < obstacleChange.Length; i++)
            {
                if (obstacleChange[i] <= previousRevision) continue;
                if (successors[i] >= 0) return true;
                int x = i % Size.x;
                if ((x > 0 && successors[i - 1] >= 0) ||
                    (x + 1 < Size.x && successors[i + 1] >= 0) ||
                    (i >= Size.x && successors[i - Size.x] >= 0) ||
                    (i + Size.x < successors.Length && successors[i + Size.x] >= 0))
                    return true;
            }
            return false;
        }

        internal int Index(int2 cell)
        {
            int2 offset = cell - Min;
            return offset.x + offset.y * Size.x;
        }

        internal int2 Cell(int index) => Min + new int2(index % Size.x, index / Size.x);
    }
}
