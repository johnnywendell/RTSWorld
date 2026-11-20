using System;
using System.Collections.Generic;
using Unity.Mathematics;
using Unity.Collections;

namespace RTSWorld.Simulation
{
    public static class NavigationSearch
    {
        private static readonly int2[] Neighbors =
        {
            new int2(1, 0), new int2(0, 1), new int2(-1, 0), new int2(0, -1)
        };

        public static bool TryFindPath(NavigationGrid grid, int2 start, int2 destination, List<int2> path)
        {
            if (path == null) throw new ArgumentNullException(nameof(path));
            path.Clear();
            if (!IsOpen(grid, start) || !IsOpen(grid, destination))
                return false;

            int startIndex = grid.Index(start), goalIndex = grid.Index(destination);
            var next = Search(grid, startIndex, goalIndex, false);
            if (next[goalIndex] < 0)
                return false;

            for (int index = goalIndex; index != startIndex; index = next[index])
                path.Add(grid.Cell(index));
            path.Add(start);
            path.Reverse();
            return true;
        }

        public static NavigationFlowField BuildFlowField(NavigationGrid grid, int2 destination)
        {
            if (!IsOpen(grid, destination))
                throw new ArgumentException("Destination requires a traversable cell.", nameof(destination));

            int[] next = Search(grid, grid.Index(destination), -1, true);
            for (int i = 0; i < next.Length; i++)
                if (!grid.TryGetCost(grid.Cell(i), out _)) next[i] = -2;
            return new NavigationFlowField(grid, next);
        }

        private static bool IsOpen(NavigationGrid grid, int2 cell) =>
            grid.TryGetCost(cell, out int cost) && cost > 0;

        private static int[] Search(NavigationGrid grid, int start, int goal, bool reverse)
        {
            int count = grid.Size.x * grid.Size.y;
            var distance = new int[count];
            var previous = new int[count];
            Array.Fill(distance, int.MaxValue);
            Array.Fill(previous, -1);
            distance[start] = 0;
            previous[start] = start;

            // Each priority may contain multiple cells. Index ordering breaks ties deterministically.
            var frontier = new SortedSet<(int priority, int index)> { (0, start) };
            while (frontier.Count > 0)
            {
                var entry = frontier.Min;
                frontier.Remove(entry);
                if (entry.index == goal)
                    break;

                int2 cell = grid.Cell(entry.index);
                grid.TryGetCost(cell, out int currentCost);
                foreach (int2 offset in Neighbors)
                {
                    int2 neighbor = cell + offset;
                    if (!grid.CanCross(cell, neighbor))
                        continue;

                    grid.TryGetCost(neighbor, out int neighborCost);
                    int index = grid.Index(neighbor);
                    int candidate = distance[entry.index] + (reverse ? currentCost : neighborCost);
                    if (candidate >= distance[index])
                        continue;

                    int estimate = reverse ? 0 : Manhattan(neighbor, grid.Cell(goal));
                    if (distance[index] != int.MaxValue)
                        frontier.Remove((distance[index] + estimate, index));

                    distance[index] = candidate;
                    previous[index] = entry.index;
                    frontier.Add((candidate + estimate, index));
                }
            }

            return previous;
        }

        private static int Manhattan(int2 from, int2 to)
        {
            int2 delta = math.abs(from - to);
            return delta.x + delta.y;
        }
    }

    /// <summary>Shares recent fields within a grid revision. Replace the grid to invalidate them.</summary>
    public sealed class NavigationFlowCache
    {
        private readonly int capacity;
        private readonly Dictionary<int2, NavigationFlowField> fields = new();
        private readonly Queue<int2> order = new();
        private NavigationGrid grid;

        public NavigationFlowCache(NavigationGrid grid, int capacity = 8)
        {
            if (capacity < 1) throw new ArgumentOutOfRangeException(nameof(capacity));
            this.capacity = capacity;
            this.grid = grid ?? throw new ArgumentNullException(nameof(grid));
        }

        public void ReplaceGrid(NavigationGrid replacement)
        {
            if (replacement == null) throw new ArgumentNullException(nameof(replacement));
            if (ReferenceEquals(grid, replacement)) return;

            grid = replacement;
            fields.Clear();
            order.Clear();
        }

        public NavigationFlowField GetField(int2 destination)
        {
            if (fields.TryGetValue(destination, out var field))
            {
                if (field.IsCurrent) return field;
                field = NavigationSearch.BuildFlowField(grid, destination);
                fields[destination] = field;
                return field;
            }

            field = NavigationSearch.BuildFlowField(grid, destination);
            if (fields.Count == capacity)
                fields.Remove(order.Dequeue());

            fields.Add(destination, field);
            order.Enqueue(destination);
            return field;
        }
    }

    public sealed class NavigationFlowField
    {
        private readonly NavigationGrid grid;
        private readonly int[] next;
        private int revision;
        private bool invalid;
        public bool IsCurrent
        {
            get
            {
                if (!invalid && revision != grid.ObstacleRevision)
                {
                    invalid = grid.AffectsField(revision, next);
                    revision = grid.ObstacleRevision;
                }
                return !invalid;
            }
        }

        internal NavigationFlowField(NavigationGrid grid, int[] next)
        {
            this.grid = grid;
            this.next = next;
            revision = grid.ObstacleRevision;
        }

        internal void CopyTo(NativeArray<int> destination, int offset) =>
            NativeArray<int>.Copy(next, 0, destination, offset, next.Length);

        /// <summary>Reads integrated path cost without retaining a second array for every field.</summary>
        public bool TryGetIntegrationCost(int2 cell, int maxSteps, out int cost)
        {
            cost = 0;
            if (!IsCurrent || !grid.TryGetCost(cell, out int entryCost) || entryCost == 0)
                return false;

            int index = grid.Index(cell);
            for (int steps = 0; steps <= maxSteps; steps++)
            {
                int successor = next[index];
                if (successor == index) return true;
                if (successor < 0 || steps == maxSteps) break;
                grid.TryGetCost(grid.Cell(successor), out entryCost);
                cost += entryCost;
                index = successor;
            }
            cost = 0;
            return false;
        }

        public bool TryGetDirection(int2 cell, out int2 direction)
        {
            direction = default;
            if (!IsCurrent || !grid.TryGetCost(cell, out int cost) || cost == 0)
                return false;

            int index = next[grid.Index(cell)];
            if (index < 0)
                return false;

            direction = grid.Cell(index) - cell;
            return true;
        }
    }
}
