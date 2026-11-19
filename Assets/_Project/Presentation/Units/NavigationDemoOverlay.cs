using System;
using System.Collections.Generic;
using RTSWorld.Simulation;
using Unity.Mathematics;
using UnityEngine;

namespace RTSWorld.Units.Presentation
{
    internal sealed class NavigationDemoOverlay : IDisposable
    {
        private readonly List<(Mesh mesh, Material material)> layers = new();
        private readonly ProceduralTerrainContract terrain;

        public NavigationDemoOverlay(ProceduralTerrainContract terrain, NavigationGrid grid, int2 first, int2 second)
        {
            this.terrain = terrain;
            var blocked = new List<Vector3>();
            var cheap = new List<Vector3>();
            var expensive = new List<Vector3>();
            var missing = new List<Vector3>();
            var links = new List<Vector3>();
            var routes = new List<Vector3>();
            var arrows = new List<Vector3>();
            var markers = new List<Vector3>();
            var goal = grid.TryGetCost(first, out int goalCost) && goalCost > 0
                ? NavigationSearch.BuildFlowField(grid, first) : null;

            for (int z = grid.Min.y; z < grid.Min.y + grid.Size.y; z++)
                for (int x = grid.Min.x; x < grid.Min.x + grid.Size.x; x++)
                {
                    var cell = new int2(x, z);
                    double2 center = (double2)cell + .5;
                    if (!grid.TryGetCost(cell, out int cost))
                    {
                        if (x % 3 == 0 && z % 3 == 0) Cross(missing, center, .35, .45f);
                        continue;
                    }

                    if (cost == 0)
                    {
                        Cross(blocked, center, .42, .55f);
                        continue;
                    }

                    if (x % 4 == 0 && z % 4 == 0)
                    {
                        Cross(cost <= 5 ? cheap : expensive, center, .2, .25f);
                        if (goal != null && goal.TryGetDirection(cell, out int2 direction) && math.any(direction != 0))
                        {
                            double2 tip = center + (double2)direction * 1.25;
                            double2 side = new double2(-direction.y, direction.x);
                            Line(arrows, center, tip, .7f);
                            Line(arrows, tip, tip - (double2)direction * .28 + side * .2, .7f);
                            Line(arrows, tip, tip - (double2)direction * .28 - side * .2, .7f);
                        }
                    }

                    if (x % grid.CellsPerChunk == 0 && grid.TryGetCost(cell - new int2(1, 0), out int west) && west > 0)
                        Line(links, new double2(x, z), new double2(x, z + 1), .4f);
                    if (z % grid.CellsPerChunk == 0 && grid.TryGetCost(cell - new int2(0, 1), out int south) && south > 0)
                        Line(links, new double2(x, z), new double2(x + 1, z), .4f);
                }

            Route(grid, new int2(-32, -59), first, routes, markers);
            Route(grid, new int2(1, -59), second, routes, markers);
            Add(blocked, new Color(1, .17f, .12f));
            Add(cheap, Color.green);
            Add(expensive, Color.yellow);
            Add(missing, Color.gray);
            Add(links, new Color(.8f, .4f, 1));
            Add(routes, Color.white);
            Add(arrows, Color.cyan);
            Add(markers, new Color(1, .6f, .1f));
        }

        private void Route(NavigationGrid grid, int2 start, int2 goal, List<Vector3> routes, List<Vector3> markers)
        {
            if (grid.TryGetCost(goal, out _)) Cross(markers, (double2)goal + .5, 2, .85f);
            var path = new List<int2>();
            if (!NavigationSearch.TryFindPath(grid, start, goal, path)) return;
            for (int i = 1; i < path.Count; i++)
                Line(routes, (double2)path[i - 1] + .5, (double2)path[i] + .5, .85f);
        }

        private void Cross(List<Vector3> vertices, double2 center, double radius, float height)
        {
            Line(vertices, center - new double2(radius), center + new double2(radius), height);
            Line(vertices, center - new double2(radius, -radius), center + new double2(radius, -radius), height);
        }

        private void Line(List<Vector3> vertices, double2 from, double2 to, float height)
        {
            vertices.Add(Point(from, height));
            vertices.Add(Point(to, height));
        }

        private Vector3 Point(double2 point, float height) =>
            new Vector3((float)point.x, (float)terrain.Elevation(point.x, point.y) + height, (float)point.y);

        private void Add(List<Vector3> vertices, Color color)
        {
            var mesh = new Mesh { name = "Navigation diagnostic lines" };
            mesh.SetVertices(vertices);
            var indices = new int[vertices.Count];
            for (int i = 0; i < indices.Length; i++) indices[i] = i;
            mesh.SetIndices(indices, MeshTopology.Lines, 0);
            var material = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            material.SetColor("_BaseColor", color);
            layers.Add((mesh, material));
        }

        public void Draw(bool diagnostics)
        {
            int count = diagnostics ? layers.Count : 1;
            for (int i = 0; i < count; i++)
            {
                var layer = layers[i];
                if (layer.mesh.vertexCount > 0)
                    Graphics.RenderMesh(new RenderParams(layer.material), layer.mesh, 0, Matrix4x4.identity);
            }
        }

        public void Dispose()
        {
            foreach (var layer in layers)
            {
                UnityEngine.Object.Destroy(layer.mesh);
                UnityEngine.Object.Destroy(layer.material);
            }
        }
    }
}
