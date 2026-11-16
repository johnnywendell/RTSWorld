using System.Linq;
using System.Collections.Generic;
using NUnit.Framework;
using RTSWorld.Presentation.Environment;
using RTSWorld.Presentation.Streaming;
using RTSWorld.World.Chunks;
using RTSWorld.World.Environment;
using RTSWorld.World.Generation;
using UnityEngine;

namespace RTSWorld.Presentation.Tests
{
    public sealed class EnvironmentStreamingTests
    {
        [Test]
        public void DiagnosticViewsPreserveOriginalHeightsAndExposeOnlyClippedWater()
        {
            var world = new WorldDefinition(7);
            var terrain = new TerrainGenerator().Generate(world, new ChunkCoordinate(-5, 11));
            var data = new EnvironmentGenerator(world, new EnvironmentDefinition(38, 3)).Generate(terrain);
            var original = EnvironmentMeshBuilder.Build(data, 2, 1.2f, EnvironmentGeometryView.OriginalTerrain);
            var water = EnvironmentMeshBuilder.Build(data, 2, 1.2f, EnvironmentGeometryView.WaterOnly);
            var combined = EnvironmentMeshBuilder.Build(data, 2, 1.2f);
            try
            {
                Assert.That(original.vertexCount, Is.EqualTo(32 * 32 * 6));
                foreach (var v in original.vertices)
                    Assert.That(v.y, Is.EqualTo(terrain[Mathf.RoundToInt(v.x / 2), Mathf.RoundToInt(v.z / 2)].Elevation * 1.2f));
                var color = new Color(0.08f, 0.43f, 0.69f);
                Assert.That(water.vertexCount, Is.GreaterThan(0));
                Assert.That(water.colors.All(c => c == color), Is.True);
                var allVertices = combined.vertices; var allColors = combined.colors;
                var expected = allVertices.Where((v, i) => allColors[i] == color).ToArray();
                CollectionAssert.AreEqual(expected, water.vertices);
                var report = EnvironmentGeometryDiagnostics.Capture(data, 2, 1.2f, EnvironmentGeometryView.WaterOnly);
                Assert.That(report.samples.Count, Is.EqualTo(33 * 33));
                Assert.That(report.triangles.Count * 3, Is.EqualTo(water.vertexCount));
                Assert.That(report.invalidIndices + report.zeroAreaTriangles + report.downwardTriangles, Is.Zero);
                Assert.That(report.samples[0].globalX, Is.EqualTo(-160));
                Assert.That(report.samples[0].globalZ, Is.EqualTo(352));
            }
            finally { Object.DestroyImmediate(original); Object.DestroyImmediate(water); Object.DestroyImmediate(combined); }
        }

        [TestCase(1, 1, false)]
        [TestCase(3, -4, false)]
        [TestCase(4, -3, false)]
        [TestCase(5, -3, true)]
        [TestCase(3, -5, true)]
        public void ClippedWaterMeshMatchesAtChunkBoundaries(int cx, int cz, bool north)
        {
            var world = new WorldDefinition(12345);
            var gen = new EnvironmentGenerator(world, new EnvironmentDefinition());
            var terrain = new TerrainGenerator();
            var a = EnvironmentMeshBuilder.Build(gen.Generate(terrain.Generate(world, new ChunkCoordinate(cx, cz))), 2, 1.2f);
            var b = EnvironmentMeshBuilder.Build(gen.Generate(terrain.Generate(world, new ChunkCoordinate(cx + (north ? 0 : 1), cz + (north ? 1 : 0)))), 2, 1.2f);
            try
            {
                (Vector3 a, Vector3 b)[] WetSegments(Mesh mesh, float boundary)
                {
                    var vertices = mesh.vertices; var colors = mesh.colors;
                    var segments = new List<(Vector3 a, Vector3 b)>();
                    float Along(Vector3 v) => north ? v.x : v.z;
                    for (int i = 0; i < vertices.Length; i += 3)
                    {
                        if (colors[i] != new Color(0.08f, 0.43f, 0.69f)) continue;
                        for (int e = 0; e < 3; e++)
                        {
                            var p = vertices[i + e]; var q = vertices[i + (e + 1) % 3];
                            if ((north ? p.z : p.x) != boundary || (north ? q.z : q.x) != boundary || Along(p) == Along(q)) continue;
                            segments.Add(Along(p) < Along(q) ? (p, q) : (q, p));
                        }
                    }
                    // A lake can touch the boundary at just one dry grid vertex. That
                    // zero-length tangency need not produce water in the neighbour;
                    // compare the positive-length water interface, not isolated points.
                    return segments.Distinct().OrderBy(e => Along(e.a)).ThenBy(e => Along(e.b)).ToArray();
                }
                var east = WetSegments(a, 64); var west = WetSegments(b, 0);
                Assert.That(east.Length, Is.GreaterThan(0), "The tested boundary must contain water segments.");
                Assert.That(east.Length, Is.EqualTo(west.Length));
                var offset = (north ? Vector3.forward : Vector3.right) * 64;
                for (int i = 0; i < east.Length; i++)
                {
                    Assert.That(Vector3.Distance(east[i].a, west[i].a + offset), Is.LessThan(0.0001f));
                    Assert.That(Vector3.Distance(east[i].b, west[i].b + offset), Is.LessThan(0.0001f));
                }
            }
            finally { Object.DestroyImmediate(a); Object.DestroyImmediate(b); }
        }

        [TestCase(3, -5, 12345)]
        [TestCase(5, -3, 12345)]
        [TestCase(1, 1, 67890)]
        public void VisibleSurfaceIsClosedAtShoreAndCoversEachCellOnce(int cx, int cz, int seed)
        {
            var world = new WorldDefinition(seed);
            var data = new EnvironmentGenerator(world, new EnvironmentDefinition()).Generate(
                new TerrainGenerator().Generate(world, new ChunkCoordinate(cx, cz)));
            var mesh = EnvironmentMeshBuilder.Build(data, 2, 1.2f);
            try
            {
                var vertices = mesh.vertices; var colors = mesh.colors; var normals = mesh.normals;
                var edges = new Dictionary<(Vector3, Vector3), int>();
                double area = 0; int wet = 0;
                bool Resource(Color color) => color == new Color(0.32f, 0.2f, 0.1f) ||
                    color == new Color(0.12f, 0.34f, 0.12f) || color == new Color(1, 0.67f, 0.08f) ||
                    color == new Color(0.48f, 0.51f, 0.55f);
                int Compare(Vector3 a, Vector3 b)
                {
                    int result = a.x.CompareTo(b.x);
                    if (result == 0) result = a.z.CompareTo(b.z);
                    return result == 0 ? a.y.CompareTo(b.y) : result;
                }
                void Edge(Vector3 a, Vector3 b)
                {
                    var key = Compare(a, b) < 0 ? (a, b) : (b, a);
                    edges.TryGetValue(key, out int count); edges[key] = count + 1;
                }
                for (int i = 0; i < vertices.Length; i += 3)
                {
                    if (Resource(colors[i])) continue;
                    var a = vertices[i]; var b = vertices[i + 1]; var c = vertices[i + 2];
                    double projected = ((double)b.z - a.z) * ((double)c.x - a.x) -
                        ((double)b.x - a.x) * ((double)c.z - a.z);
                    Assert.That(projected, Is.GreaterThan(0), "Degenerate or reversed surface triangle.");
                    Assert.That(normals[i].sqrMagnitude, Is.EqualTo(1).Within(0.00001));
                    area += projected * 0.5;
                    Edge(a, b); Edge(b, c); Edge(c, a);
                    if (colors[i] == new Color(0.08f, 0.43f, 0.69f)) wet++;
                }
                Assert.That(area, Is.EqualTo(64 * 64).Within(0.001), "Overlapping or missing projected surface.");
                foreach (var edge in edges)
                {
                    var a = edge.Key.Item1; var b = edge.Key.Item2;
                    bool boundary = (a.x == 0 && b.x == 0) || (a.x == 64 && b.x == 64) ||
                        (a.z == 0 && b.z == 0) || (a.z == 64 && b.z == 64);
                    Assert.That(edge.Value, Is.EqualTo(boundary ? 1 : 2), $"Open/non-manifold edge {a} -> {b}");
                }
                if (seed == 12345) Assert.That(wet, Is.GreaterThan(0), "Regression must exercise water.");
            }
            finally { Object.DestroyImmediate(mesh); }
        }

        [Test]
        public void SeedSevenRadiusTwoMeshesHaveNoVerticalWallsDegeneratesOrChunkCracks()
        {
            var world = new WorldDefinition(7);
            var generator = new EnvironmentGenerator(world, new EnvironmentDefinition());
            var terrain = new TerrainGenerator();
            var meshes = new Dictionary<ChunkCoordinate, Mesh>();
            var surfaceVertices = new Dictionary<ChunkCoordinate, Vector3[]>();
            int waterTriangles = 0;
            bool Resource(Color color) => color == new Color(0.32f, 0.2f, 0.1f) ||
                color == new Color(0.12f, 0.34f, 0.12f) || color == new Color(1, 0.67f, 0.08f) ||
                color == new Color(0.48f, 0.51f, 0.55f);
            try
            {
                for (int cz = -2; cz <= 2; cz++)
                for (int cx = -2; cx <= 2; cx++)
                {
                    var coord = new ChunkCoordinate(cx, cz);
                    var mesh = EnvironmentMeshBuilder.Build(generator.Generate(terrain.Generate(world, coord)), 2, 1.2f);
                    meshes.Add(coord, mesh);
                    var vertices = mesh.vertices; var colors = mesh.colors; var normals = mesh.normals;
                    Assert.That(mesh.triangles.All(i => i >= 0 && i < vertices.Length), Is.True);
                    double area = 0;
                    var visible = new List<Vector3>();
                    for (int i = 0; i < vertices.Length; i += 3)
                    {
                        Assert.That(Vector3.Cross(vertices[i + 1] - vertices[i], vertices[i + 2] - vertices[i]).sqrMagnitude,
                            Is.GreaterThan(0), $"Degenerate triangle in {coord} at {i}");
                        if (Resource(colors[i])) continue;
                        var a = vertices[i]; var b = vertices[i + 1]; var c = vertices[i + 2];
                        double projected = ((double)b.z - a.z) * ((double)c.x - a.x) -
                            ((double)b.x - a.x) * ((double)c.z - a.z);
                        Assert.That(projected, Is.GreaterThan(0), $"Invalid surface winding/area in {coord} at {i}");
                        Assert.That(normals[i].y, Is.GreaterThan(0.25f), $"Near-vertical surface in {coord} at {i}");
                        Assert.That(normals[i].sqrMagnitude, Is.EqualTo(1).Within(0.00001));
                        area += projected * 0.5;
                        visible.Add(a); visible.Add(b); visible.Add(c);
                        if (colors[i] == new Color(0.08f, 0.43f, 0.69f)) waterTriangles++;
                    }
                    Assert.That(area, Is.EqualTo(64 * 64).Within(0.001));
                    surfaceVertices.Add(coord, visible.Distinct().ToArray());
                }
                foreach (var pair in surfaceVertices)
                foreach (bool north in new[] { false, true })
                {
                    var neighbour = new ChunkCoordinate(pair.Key.X + (north ? 0 : 1), pair.Key.Z + (north ? 1 : 0));
                    if (!surfaceVertices.TryGetValue(neighbour, out var other)) continue;
                    Vector3[] Edge(Vector3[] values, float edge) => values.Where(v => (north ? v.z : v.x) == edge)
                        .OrderBy(v => north ? v.x : v.z).ThenBy(v => v.y).ToArray();
                    var a = Edge(pair.Value, 64); var b = Edge(other, 0);
                    Assert.That(a.Length, Is.EqualTo(b.Length), $"Boundary topology {pair.Key}/{neighbour}");
                    for (int i = 0; i < a.Length; i++)
                        Assert.That(Vector3.Distance(a[i], b[i] + (north ? Vector3.forward : Vector3.right) * 64), Is.LessThan(0.0001f));
                }
                Assert.That(meshes.Count, Is.EqualTo(25));
                Assert.That(waterTriangles, Is.GreaterThan(0));
            }
            finally { foreach (var mesh in meshes.Values) Object.DestroyImmediate(mesh); }
        }

        [Test]
        public void GroupedMeshContainsColoredTerrainWaterAndResources()
        {
            var world = new WorldDefinition(12345);
            var data = new EnvironmentGenerator(world, new EnvironmentDefinition()).Generate(new TerrainGenerator().Generate(world, new ChunkCoordinate(3, -5)));
            var mesh = EnvironmentMeshBuilder.Build(data, 2, 1.2f);
            try
            {
                Assert.That(mesh.vertexCount, Is.GreaterThanOrEqualTo(32 * 32 * 6));
                Assert.That(mesh.colors.Length, Is.EqualTo(mesh.vertexCount));
                Assert.That(mesh.triangles.All(i => i >= 0 && i < mesh.vertexCount), Is.True);
                Assert.That(data.Resources.Count, Is.GreaterThan(0));
                Assert.That(mesh.colors, Does.Contain(new Color(0.08f, 0.43f, 0.69f)));
                Assert.That(mesh.colors.Distinct().Count(), Is.GreaterThanOrEqualTo(2));
                Assert.That(mesh.normals.All(n => n.sqrMagnitude > 0.99f && n.sqrMagnitude < 1.01f), Is.True);
            }
            finally { Object.DestroyImmediate(mesh); }
        }

        [Test]
        public void StreamingOwnsOneMeshPerChunkAndReloadsEnvironmentOnRevision()
        {
            var root = new GameObject("Environment stream test"); var observer = new GameObject("Observer");
            var material = new Material(Shader.Find("RTSWorld/Environment Colors"));
            try
            {
                var layer = root.AddComponent<EnvironmentPresentation>(); layer.Configure(material);
                var manager = root.AddComponent<ChunkManager>();
                manager.Configure(observer.transform, material, cells: 8, radius: 1, maxPerFrame: 8, milliseconds: 50);
                manager.SetEnvironment(layer);
                void Drain() { for (int i = 0; i < 20; i++) { manager.Step(); if (manager.PendingCount == 0) return; } Assert.Fail("Queue not drained"); }
                Drain();
                Assert.That(root.transform.childCount, Is.EqualTo(9));
                var old = root.GetComponentsInChildren<MeshFilter>().Select(f => f.sharedMesh).ToArray();
                long generated = manager.TotalGenerated;
                Drain(); Assert.That(manager.TotalGenerated, Is.EqualTo(generated));
                layer.Configure(material, 55); Drain();
                foreach (var mesh in old) Assert.That(mesh == null, Is.True);
                Assert.That(root.GetComponentsInChildren<MeshRenderer>().Length, Is.EqualTo(9));
                var versionTwoMeshes = root.GetComponentsInChildren<MeshFilter>().Select(f => f.sharedMesh).ToArray();
                layer.Configure(material, 55, version: 1); Drain();
                Assert.That(layer.EnvironmentVersion, Is.EqualTo(1));
                foreach (var mesh in versionTwoMeshes) Assert.That(mesh == null, Is.True);
                layer.Configure(material, 55, version: 2); Drain();
                observer.transform.position = new Vector3(-64, 0, 64); Drain();
                Assert.That(root.GetComponentsInChildren<MeshFilter>().Length, Is.EqualTo(9));
                var remaining = root.GetComponentsInChildren<MeshFilter>().Select(f => f.sharedMesh).ToArray();
                manager.enabled = false;
                foreach (var mesh in remaining) Assert.That(mesh == null, Is.True);
                Assert.That(root.transform.childCount, Is.EqualTo(0));
            }
            finally { Object.DestroyImmediate(root); Object.DestroyImmediate(observer); Object.DestroyImmediate(material); }
        }
    }
}
