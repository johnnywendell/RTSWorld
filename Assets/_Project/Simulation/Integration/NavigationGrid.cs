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
        private readonly bool[] structures;
        private readonly PlacementIssue[] terrainIssues;
        private static readonly int2[] CardinalNeighbors = { new(1, 0), new(-1, 0), new(0, 1), new(0, -1) };
        private int[] components;
        private int connectedRevision = -1;
        private readonly List<int> changedCells = new();
        internal int ObstacleRevision { get; private set; }
        public EnvironmentalObstacles Obstacles { get; }

        public int2 Min { get; }
        public int2 Size { get; }
        public int CellsPerChunk { get; }
        internal double MaximumSlope { get; }
        internal bool CapturesSurface => surfaceGround != null;
        internal bool HasCustomConstraints { get; }

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
            MaximumSlope = maximumSlope;
            HasCustomConstraints = isObstacle != null || isRegionAvailable != null;
            Min = min;
            Size = max - min;
            costs = new int[Size.x * Size.y];
            structures = new bool[costs.Length];
            terrainIssues = new PlacementIssue[costs.Length];
            obstacleChange = new int[costs.Length];
            if (obstacles != null)
            {
                if (!obstacles.Min.Equals(Min) || !obstacles.Size.Equals(Size))
                    throw new ArgumentException("Obstacle bounds must match the navigation bounds.", nameof(obstacles));
                Obstacles = obstacles;
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
                    int index = Index(cell);
                    if (water[a] || water[b] || water[c] || water[d])
                        terrainIssues[index] |= PlacementIssue.Water;
                    if (isObstacle != null && isObstacle(cell))
                        terrainIssues[index] |= PlacementIssue.Obstacle;

                    double slope = math.max(
                        math.length(new double2(heights[b] - heights[a], heights[c] - heights[a])),
                        math.length(new double2(heights[d] - heights[c], heights[d] - heights[b])));
                    if (slope > maximumSlope) terrainIssues[index] |= PlacementIssue.Slope;
                    if (terrainIssues[index] == PlacementIssue.None)
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
            if (value > 0 && Passage(index) == 0) value = 0;
            cost = math.max(0, value);
            return value >= 0;
        }

        public bool IsFullPassage(int2 cell) => math.all(cell >= Min) && math.all(cell < Min + Size) &&
            Passage(Index(cell)) == EnvironmentalObstacles.FullCell;

        internal byte Passage(int index) => costs[index] <= 0 || structures[index] ? (byte)0 :
            Obstacles?.Passage(index) ?? EnvironmentalObstacles.FullCell;

        internal bool CanCross(int2 from, int2 to)
        {
            if (!TryGetCost(to, out int cost) || cost == 0) return false;
            byte axis = from.x != to.x ? EnvironmentalObstacles.AlongX : EnvironmentalObstacles.AlongZ;
            byte allowed = (byte)(axis | EnvironmentalObstacles.FullCell);
            return (Passage(Index(from)) & allowed) != 0 && (Passage(Index(to)) & allowed) != 0;
        }

        // A legal, open footprint connects all its open perimeter neighbors before reservation.
        // If they can still reach one another around it, no external route loses connectivity.
        internal bool PreservesPassages(int2 origin, int2 size)
        {
            var exits = new HashSet<int>();
            for (int x = 0; x < size.x; x++)
            {
                AddExit(origin + new int2(x, -1));
                AddExit(origin + new int2(x, size.y));
            }
            for (int z = 0; z < size.y; z++)
            {
                AddExit(origin + new int2(-1, z));
                AddExit(origin + new int2(size.x, z));
            }
            if (exits.Count == 0) return false;

            var frontier = new Queue<int>();
            foreach (int exit in exits)
            {
                frontier.Enqueue(exit);
                break;
            }
            var visited = new bool[costs.Length];
            visited[frontier.Peek()] = true;
            while (frontier.TryDequeue(out int index))
            {
                exits.Remove(index);
                if (exits.Count == 0) return true;
                foreach (var offset in CardinalNeighbors)
                {
                    int2 cell = Cell(index) + offset;
                    int2 relative = cell - origin;
                    if (math.all(relative >= 0) && math.all(relative < size)) continue;
                    if (!CanCross(Cell(index), cell)) continue;
                    int next = Index(cell);
                    if (visited[next]) continue;
                    visited[next] = true;
                    frontier.Enqueue(next);
                }
            }
            return false;

            void AddExit(int2 cell)
            {
                int2 nearest = math.clamp(cell, origin, origin + size - 1);
                if (CanCross(nearest, cell)) exits.Add(Index(cell));
            }
        }

        internal bool Connected(int2 source, int2 destination)
        {
            if (!TryGetCost(source, out int start) || start == 0 ||
                !TryGetCost(destination, out int end) || end == 0) return false;
            if (connectedRevision != ObstacleRevision)
            {
                components ??= new int[costs.Length];
                Array.Clear(components, 0, components.Length);
                var frontier = new Queue<int>();
                for (int root = 0; root < costs.Length; root++)
                {
                    if (components[root] != 0 || !TryGetCost(Cell(root), out int cost) || cost == 0) continue;
                    frontier.Enqueue(root);
                    components[root] = root + 1;
                    while (frontier.TryDequeue(out int index))
                    {
                        foreach (var offset in CardinalNeighbors)
                        {
                            int2 cell = Cell(index) + offset;
                            if (!CanCross(Cell(index), cell)) continue;
                            int next = Index(cell);
                            if (components[next] != 0) continue;
                            components[next] = root + 1;
                            frontier.Enqueue(next);
                        }
                    }
                }
                connectedRevision = ObstacleRevision;
            }
            return components[Index(source)] == components[Index(destination)];
        }

        public PlacementIssue GetPlacementIssue(int2 cell)
        {
            if (math.any(cell < Min) || math.any(cell >= Min + Size))
                return PlacementIssue.Bounds;
            int index = Index(cell);
            if (costs[index] < 0) return PlacementIssue.Bounds;
            var issue = terrainIssues[index];
            if (structures[index]) issue |= PlacementIssue.Building;
            if (Obstacles != null && Obstacles.IsBlocked(index)) issue |= PlacementIssue.Obstacle;
            return issue;
        }

        internal void SetStructure(int2 origin, int2 size, bool occupied)
        {
            changedCells.Clear();
            for (int z = 0; z < size.y; z++)
                for (int x = 0; x < size.x; x++)
                {
                    int index = Index(origin + new int2(x, z));
                    structures[index] = occupied;
                    changedCells.Add(index);
                }
            RecordObstacleRevision();
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
