using System;
using System.Linq;
using NUnit.Framework;
using RTSWorld.Presentation.Terrain;
using RTSWorld.World.Chunks;
using RTSWorld.World.Generation;
using UnityEngine;
using UnityEngine.Rendering;

namespace RTSWorld.Presentation.Tests
{
    public sealed class TerrainGeometryTests
    {
        private static TerrainChunkData Data(int x = 0, int z = 0, int cells = 8, int seed = 12345) =>
            new TerrainGenerator().Generate(new WorldDefinition(seed, cellsPerChunk: cells), new ChunkCoordinate(x, z));

        [TestCase(1)]
        [TestCase(32)]
        [TestCase(256)]
        public void TopologyHasValidUpwardFacesAndFlatUnitNormals(int cells)
        {
            var g = TerrainGeometry.Build(Data(cells: cells), 2, 0.8f);
            Assert.That(g.Vertices.Length, Is.EqualTo(cells * cells * 6));
            for (int i = 0; i < g.Indices.Length; i += 3)
            {
                Assert.That(g.Indices[i], Is.EqualTo(i));
                Assert.That(g.Indices[i + 2], Is.EqualTo(i + 2));
                Vector3 cross = Vector3.Cross(g.Vertices[i + 1] - g.Vertices[i], g.Vertices[i + 2] - g.Vertices[i]);
                Assert.That(cross.y, Is.GreaterThan(0));
                Assert.That(Vector3.Dot(cross.normalized, g.Normals[i]), Is.GreaterThan(0.9999f));
                Assert.That(g.Normals[i].magnitude, Is.EqualTo(1).Within(0.0001f));
                Assert.That(g.Normals[i + 1], Is.EqualTo(g.Normals[i]));
                Assert.That(g.Normals[i + 2], Is.EqualTo(g.Normals[i]));
            }
        }

        [Test]
        public void ScaleChangesGeometryWithoutChangingSourceData()
        {
            var data = Data();
            float before = data[0, 0].Elevation;
            var a = TerrainGeometry.Build(data, 1, 1);
            var b = TerrainGeometry.Build(data, 3, 2);
            for (int i = 0; i < a.Vertices.Length; i++)
            {
                Assert.That(b.Vertices[i].x, Is.EqualTo(a.Vertices[i].x * 3));
                Assert.That(b.Vertices[i].z, Is.EqualTo(a.Vertices[i].z * 3));
                Assert.That(b.Vertices[i].y, Is.EqualTo(a.Vertices[i].y * 2));
            }
            Assert.That(data[0, 0].Elevation, Is.EqualTo(before));
        }

        [TestCase(-1, -1)]
        [TestCase(0, 0)]
        [TestCase(-2, 3)]
        public void SharedEdgesHaveIdenticalWorldPositions(int x, int z)
        {
            const int n = 8;
            const float scale = 2;
            var a = TerrainGeometry.Build(Data(x, z), scale, 0.8f);
            var east = TerrainGeometry.Build(Data(x + 1, z), scale, 0.8f);
            var north = TerrainGeometry.Build(Data(x, z + 1), scale, 0.8f);
            var right = a.Vertices.Where(v => v.x == n * scale).Distinct().OrderBy(v => v.z).ToArray();
            var left = east.Vertices.Where(v => v.x == 0).Select(v => v + new Vector3(n * scale, 0, 0)).Distinct().OrderBy(v => v.z).ToArray();
            Assert.That(right.Length, Is.EqualTo(n + 1));
            CollectionAssert.AreEqual(right, left);
            var top = a.Vertices.Where(v => v.z == n * scale).Distinct().OrderBy(v => v.x).ToArray();
            var bottom = north.Vertices.Where(v => v.z == 0).Select(v => v + new Vector3(0, 0, n * scale)).Distinct().OrderBy(v => v.x).ToArray();
            CollectionAssert.AreEqual(top, bottom);
        }

        [Test]
        public void PartitioningDoesNotChangeFaceNormalsOrGeometry()
        {
            var whole = TerrainGeometry.Build(Data(cells: 16), 2, 0.8f);
            for (int cz = 0; cz < 2; cz++)
            for (int cx = 0; cx < 2; cx++)
            {
                var part = TerrainGeometry.Build(Data(cx, cz), 2, 0.8f);
                for (int z = 0; z < 8; z++)
                for (int x = 0; x < 8; x++)
                for (int v = 0; v < 6; v++)
                {
                    int local = (z * 8 + x) * 6 + v;
                    int global = ((cz * 8 + z) * 16 + cx * 8 + x) * 6 + v;
                    Assert.That(part.Vertices[local] + new Vector3(cx * 16, 0, cz * 16), Is.EqualTo(whole.Vertices[global]));
                    Assert.That(Vector3.Distance(part.Normals[local], whole.Normals[global]), Is.LessThan(0.00001f));
                }
            }
        }

        [TestCase(32, IndexFormat.UInt16)]
        [TestCase(256, IndexFormat.UInt32)]
        public void NativeMeshUsesCorrectIndicesAndBounds(int cells, IndexFormat format)
        {
            var mesh = TerrainMeshFactory.Create(TerrainGeometry.Build(Data(cells: cells), 2, 0.8f));
            try
            {
                Assert.That(mesh.indexFormat, Is.EqualTo(format));
                Assert.That(mesh.vertexCount, Is.EqualTo(cells * cells * 6));
                Assert.That(mesh.bounds.min.x, Is.EqualTo(0));
                Assert.That(mesh.bounds.max.x, Is.EqualTo(cells * 2));
                Assert.That(mesh.bounds.max.z, Is.EqualTo(cells * 2));
            }
            finally { UnityEngine.Object.DestroyImmediate(mesh); }
        }

        [TestCase(0f)]
        [TestCase(-1f)]
        [TestCase(float.NaN)]
        [TestCase(float.PositiveInfinity)]
        public void InvalidScalesAreRejected(float scale)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => TerrainGeometry.Build(Data(), scale, 1));
            Assert.Throws<ArgumentOutOfRangeException>(() => TerrainGeometry.Build(Data(), 1, scale));
        }

        [Test]
        public void DemoRebuildReplacesFourObjectsAndReleasesOwnedMeshes()
        {
            var root = new GameObject("Demo lifecycle test");
            var material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            try
            {
                var demo = root.AddComponent<TerrainDemo>();
                demo.Configure(12345, 8, 2, 0.8f, material);
                demo.Rebuild();
                Assert.That(demo.ChunkCount, Is.EqualTo(4));
                var first = root.GetComponentsInChildren<MeshFilter>().Select(f => f.sharedMesh).ToArray();
                float before = first[0].vertices[0].y;
                demo.Configure(99, 8, 2, 0.8f, material);
                demo.Rebuild();
                Assert.That(root.transform.childCount, Is.EqualTo(4));
                Assert.That(root.GetComponentsInChildren<MeshRenderer>().Length, Is.EqualTo(4));
                Assert.That(root.GetComponentsInChildren<MeshFilter>()[0].sharedMesh.vertices[0].y, Is.Not.EqualTo(before));
                foreach (var mesh in first) Assert.That(mesh == null, Is.True);
                demo.enabled = false;
                Assert.That(root.transform.childCount, Is.EqualTo(0));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
                UnityEngine.Object.DestroyImmediate(material);
            }
        }
    }
}
