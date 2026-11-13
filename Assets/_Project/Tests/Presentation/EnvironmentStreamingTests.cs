using System.Linq;
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
        [TestCase(1, 1)]
        [TestCase(3, -4)]
        [TestCase(4, -3)]
        public void ClippedWaterMeshMatchesAtChunkBoundaries(int cx, int cz)
        {
            var world = new WorldDefinition(12345);
            var gen = new EnvironmentGenerator(world, new EnvironmentDefinition());
            var terrain = new TerrainGenerator();
            var a = EnvironmentMeshBuilder.Build(gen.Generate(terrain.Generate(world, new ChunkCoordinate(cx, cz))), 2, 1.2f);
            var b = EnvironmentMeshBuilder.Build(gen.Generate(terrain.Generate(world, new ChunkCoordinate(cx + 1, cz))), 2, 1.2f);
            try
            {
                Vector3[] Edge(Mesh mesh, float x)
                {
                    var vertices = mesh.vertices; var colors = mesh.colors;
                    return vertices.Where((v, i) => v.x == x && colors[i] == new Color(0.08f, 0.43f, 0.69f)).Distinct().OrderBy(v => v.z).ThenBy(v => v.y).ToArray();
                }
                var east = Edge(a, 64); var west = Edge(b, 0);
                Assert.That(east.Length, Is.EqualTo(west.Length));
                for (int i = 0; i < east.Length; i++) Assert.That(Vector3.Distance(east[i], west[i] + Vector3.right * 64), Is.LessThan(0.0001f));
            }
            finally { Object.DestroyImmediate(a); Object.DestroyImmediate(b); }
        }

        [Test]
        public void GroupedMeshContainsColoredTerrainWaterAndResources()
        {
            var world = new WorldDefinition(12345);
            var data = new EnvironmentGenerator(world, new EnvironmentDefinition(55)).Generate(new TerrainGenerator().Generate(world, new ChunkCoordinate(1, 1)));
            var mesh = EnvironmentMeshBuilder.Build(data, 2, 1.2f);
            try
            {
                Assert.That(mesh.vertexCount, Is.GreaterThanOrEqualTo(32 * 32 * 6));
                Assert.That(mesh.colors.Length, Is.EqualTo(mesh.vertexCount));
                Assert.That(mesh.triangles.All(i => i >= 0 && i < mesh.vertexCount), Is.True);
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
