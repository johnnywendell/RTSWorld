using System.Collections.Generic;
using RTSWorld.Simulation;
using RTSWorld.World.Environment;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace RTSWorld.Units.Presentation
{
    public sealed partial class RtsInteractionDemo
    {
        private static readonly string[] CrowdScenarios =
        {
            "Duas unidades se cruzando", "Grupos em direções opostas", "Gargalo estreito", "Corredor largo",
            "Travessia de floresta", "Passagem próxima a rochas", "Contorno de água", "Soldados atacando",
            "Aldeões e depósito", "Congestionamento inicial", "Destino inalcançável", "Travessia de chunks"
        };
        private int crowdScenario;

        private void DrawCrowdScenarios()
        {
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Anterior", GUILayout.Width(68)))
                crowdScenario = (crowdScenario + CrowdScenarios.Length - 1) % CrowdScenarios.Length;
            GUILayout.Label(CrowdScenarios[crowdScenario]);
            if (GUILayout.Button("Próximo", GUILayout.Width(68)))
                crowdScenario = (crowdScenario + 1) % CrowdScenarios.Length;
            if (GUILayout.Button("Executar", GUILayout.Width(68)))
                feedback = RunCrowdScenario(crowdScenario) ? CrowdScenarios[crowdScenario] : "Cenário indisponível";
            GUILayout.EndHorizontal();
        }

        public bool RunCrowdScenario(int scenario)
        {
            if (!crowdRecoveryDemo || scenario < 0 || scenario >= CrowdScenarios.Length) return false;
            bool recovery = Session.RecoveryEnabled;
            Rebuild(population);
            Session.RecoveryEnabled = recovery;
            if (scenario is 0 or 1 or 2 or 3 or 9) return RunCorridorExercise(scenario);

            var manager = Session.World.EntityManager;
            using var all = query.ToEntityArray(Allocator.Temp);
            using var group = new NativeList<Entity>(Allocator.Temp);
            using var inactive = new NativeList<Entity>(Allocator.Temp);
            Entity enemy = Entity.Null;
            foreach (Entity unit in all)
            {
                var identity = manager.GetComponentData<UnitIdentity>(unit);
                if (identity.Owner != 0 && scenario == 7 && enemy == Entity.Null)
                    enemy = unit;
                else if (identity.Owner == 0 && group.Length < 5 &&
                    identity.Kind == (scenario == 8 ? UnitKind.Villager : UnitKind.Soldier))
                    group.Add(unit);
                else inactive.Add(unit);
            }
            if (group.Length == 0) return false;
            Session.DestroyBatch(inactive.AsArray());
            if (scenario == 7)
                return enemy != Entity.Null && Session.ApplyOrder(UnitOrder.Attack(enemy), 0, group.AsArray()) == UnitOrderResult.Accepted;

            int2 source = (int2)math.floor(manager.GetComponentData<LogicalPose>(group[0]).Position.xz);
            var field = NavigationSearch.BuildFlowField(navigation, source);
            var obstacles = navigation.Obstacles;
            if (scenario == 8)
            {
                if (!Session.EnablePhysicalDepots(Session.DepotCell, 0, preservePassages: true)) return false;
                Session.DestroyBatch(group.AsArray());
                group.Clear();
                source = Session.DepotCell;
                var depotField = NavigationSearch.BuildFlowField(navigation, source);
                using var cells = new NativeList<int2>(5, Allocator.Temp);
                for (int z = -3; z <= 3 && cells.Length < 5; z++)
                for (int x = -3; x <= 3 && cells.Length < 5; x++)
                {
                    int2 cell = source + new int2(x, z);
                    if (navigation.IsFullPassage(cell) && depotField.TryGetDirection(cell, out _)) cells.Add(cell);
                }
                if (cells.Length != 5) return false;
                Session.SpawnNavigable(UnitDefinition.Villager, 0, cells.AsArray());
                using var workers = query.ToEntityArray(Allocator.Temp);
                var candidates = new List<EnvironmentalObstacle>();
                foreach (var resource in obstacles.Entries)
                    if (resource.Available && resource.Radius > 0 && math.distancesq(resource.Center, (double2)source) < 40 * 40)
                        candidates.Add(resource);
                candidates.Sort((a, b) => math.distancesq(a.Center, (double2)source)
                    .CompareTo(math.distancesq(b.Center, (double2)source)));
                foreach (var resource in candidates)
                    if (Session.Gather(resource.Placement.Key, 0, workers) == UnitOrderResult.Accepted)
                        return true;
                return false;
            }

            int2 destination = source;
            double nearest = double.MaxValue;
            var sourceChunk = LogicalTerrain.Region(source.x, source.y);
            for (int z = navigation.Min.y; z < navigation.Min.y + navigation.Size.y; z++)
            for (int x = navigation.Min.x; x < navigation.Min.x + navigation.Size.x; x++)
            {
                int2 candidate = new(x, z);
                double squared = math.distancesq((double2)candidate, source);
                if (squared < 16 || squared >= nearest || !navigation.IsFullPassage(candidate))
                    continue;
                bool reachable = field.TryGetDirection(candidate, out _);
                if (reachable == (scenario == 10)) continue;
                bool fits = scenario switch
                {
                    4 => NearResource(candidate, NaturalResource.Tree),
                    5 => NearResource(candidate, NaturalResource.Stone),
                    6 => CrossesWater(source, candidate),
                    11 => !LogicalTerrain.Region(x, z).Equals(sourceChunk),
                    _ => true
                };
                if (!fits) continue;
                nearest = squared;
                destination = candidate;
            }
            if (destination.Equals(source)) return false;
            var result = Session.ApplyOrder(UnitOrder.Move(destination), 0, group.AsArray());
            return result == (scenario == 10 ? UnitOrderResult.Unreachable : UnitOrderResult.Accepted);

            bool NearResource(int2 candidate, NaturalResource kind)
            {
                // Resource placements are owned by their global cell, including across chunk seams.
                for (int z = -1; z <= 1; z++)
                for (int x = -1; x <= 1; x++)
                {
                    var key = new ResourceKey(streaming.Seed, LogicalTerrain.Environment.Version, candidate.x + x, candidate.y + z);
                    if (obstacles.TryGet(key, out var resource) && resource.Available && resource.Placement.Kind == kind)
                        return true;
                }
                return false;
            }

            bool CrossesWater(int2 start, int2 end)
            {
                int steps = math.cmax(math.abs(end - start));
                for (int i = 1; i < steps; i++)
                {
                    var sample = (int2)math.floor(math.lerp((double2)start + .5, (double2)end + .5, (double)i / steps));
                    if ((navigation.GetPlacementIssue(sample) & PlacementIssue.Water) != 0) return true;
                }
                return false;
            }
        }

        private bool RunCorridorExercise(int scenario)
        {
            int width = scenario is 1 or 3 ? 2 : 1;
            int2 center = new(navigationCenter.x, navigationCenter.y);
            int2 origin = default;
            double nearest = double.MaxValue;
            int2 min = navigation.Min;
            int2 max = min + navigation.Size;
            for (int z = min.y + width; z < max.y - width; z++)
            for (int x = min.x + 5; x < max.x - 6; x++)
            {
                int2 candidate = new(x, z);
                double squared = math.distancesq((double2)candidate, center);
                if (squared >= nearest || !IsClearPatch(candidate)) continue;
                nearest = squared;
                origin = candidate;
            }
            if (nearest == double.MaxValue) return false;
            using (var oldUnits = query.ToEntityArray(Allocator.Temp))
                Session.DestroyBatch(oldUnits);

            bool narrow = scenario == 2;
            var obstacles = navigation.Obstacles;
            ResourceKey stoneKey = default;
            if (narrow)
            {
                var treeKey = new ResourceKey(streaming.Seed, LogicalTerrain.Environment.Version, origin.x, origin.y - 1);
                stoneKey = new ResourceKey(streaming.Seed, LogicalTerrain.Environment.Version, origin.x, origin.y + 1);
                var placements = new List<ResourcePlacement>(obstacles.ResourceCount + 2);
                foreach (var entry in obstacles.Entries)
                    if (!entry.Placement.Key.Equals(treeKey) && !entry.Placement.Key.Equals(stoneKey))
                        placements.Add(entry.Placement);
                placements.Add(new ResourcePlacement(treeKey, NaturalResource.Tree, .5f, .5f, 0));
                placements.Add(new ResourcePlacement(stoneKey, NaturalResource.Stone, .5f, .5f, 0));
                obstacles = new EnvironmentalObstacles(min, max, navigation.CellsPerChunk, environmentalClearance, placements);
            }

            navigation = new NavigationGrid(LogicalTerrain, min, max, 2.25,
                cell => OutsideExercise(cell - origin), captureSurface: true, obstacles: obstacles);
            Session.SetNavigationGrid(navigation);
            if (narrow) Session.SetEnvironmentalResourceRadius(stoneKey, .49);

            var soldier = UnitDefinition.Soldier;
            if (scenario == 9)
            {
                soldier.OccupancyRadius = .49f;
                using var traveler = Session.SpawnBatch(soldier, 0, 1, At(-3.5, .5), default);
                using var blockers = Session.SpawnBatch(soldier, 0, 3, At(-.1263914561, -.0026628317), default, .28);
                using var neighbor = Session.SpawnBatch(soldier, 0, 1, At(.1236085439, -.2785120456), default);
                return Session.SetDestination(traveler, origin + new int2(4, 0));
            }

            int count = scenario == 1 ? 6 : scenario == 3 ? 4 : 1;
            using var left = Session.SpawnBatch(soldier, 0, count, At(-1.5, .5), default, .75);
            using var right = Session.SpawnBatch(soldier, 1, count, At(2.5, .5), default, .75);
            return Session.ApplyOrder(UnitOrder.Move(origin + new int2(3, 0)), 0, left) == UnitOrderResult.Accepted &&
                Session.ApplyOrder(UnitOrder.Move(origin + new int2(-3, 0)), 1, right) == UnitOrderResult.Accepted;

            bool IsClearPatch(int2 candidate)
            {
                for (int z = -width; z <= width; z++)
                for (int x = -5; x <= 5; x++)
                    if (navigation.GetPlacementIssue(candidate + new int2(x, z)) != PlacementIssue.None)
                        return false;
                return true;
            }

            bool OutsideExercise(int2 local) => math.abs(local.x) > 5 ||
                (narrow ? local.y != 0 && !(local.y == 1 && math.abs(local.x) == 2) : math.abs(local.y) > width);

            double3 At(double x, double z) => new(origin.x + x, 0, origin.y + z);
        }
    }
}
