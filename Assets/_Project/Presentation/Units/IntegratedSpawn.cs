using System.Collections.Generic;
using RTSWorld.Simulation;
using Unity.Collections;
using Unity.Mathematics;

namespace RTSWorld.Units.Presentation
{
    internal static class IntegratedSpawn
    {
        public static int Place(UnitSession session, NavigationGrid grid, int2 center, int count, bool combat = false)
        {
            var anchor = center;
            var nearest = double.PositiveInfinity;
            int2 max = grid.Min + grid.Size;
            for (int z = grid.Min.y; z < max.y; z++)
                for (int x = grid.Min.x; x < max.x; x++)
                {
                    var cell = new int2(x, z);
                    double distance = math.lengthsq((double2)cell - center);
                    if (distance < nearest && grid.TryGetCost(cell, out int cost) && cost > 0)
                    {
                        anchor = cell;
                        nearest = distance;
                    }
                }

            if (double.IsInfinity(nearest)) return 0;
            var flow = NavigationSearch.BuildFlowField(grid, anchor);
            var candidates = new List<int2>();
            for (int z = grid.Min.y; z < max.y; z++)
                for (int x = grid.Min.x; x < max.x; x++)
                {
                    var cell = new int2(x, z);
                    if (flow.TryGetDirection(cell, out _)) candidates.Add(cell);
                }

            int total = math.min(count, candidates.Count);
            int enemies = combat ? total / 2 : 0;
            int villagers = combat ? (total - enemies) / 5 : (total + 1) / 2;
            PlaceGroup(UnitDefinition.Villager, anchor - new int2(12, 0), villagers);
            PlaceGroup(UnitDefinition.Soldier, anchor + new int2(12, 0), total - villagers - enemies);
            if (combat) PlaceGroup(UnitDefinition.Soldier, anchor + new int2(20, 8), enemies, 1);
            return total;

            void PlaceGroup(UnitDefinition kind, int2 origin, int size, int owner = 0)
            {
                candidates.Sort((left, right) =>
                {
                    int distance = math.lengthsq((double2)left - origin).CompareTo(math.lengthsq((double2)right - origin));
                    if (distance != 0) return distance;
                    return left.y != right.y ? left.y.CompareTo(right.y) : left.x.CompareTo(right.x);
                });
                using var storage = new NativeArray<int2>(size, Allocator.Temp);
                var cells = storage;
                for (int i = 0; i < size; i++) cells[i] = candidates[i];
                session.SpawnNavigable(kind, owner, cells);
                candidates.RemoveRange(0, size);
            }
        }
    }
}
