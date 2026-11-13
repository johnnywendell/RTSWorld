using System;
using System.Linq;
using NUnit.Framework;
using RTSWorld.Presentation.Streaming;
using RTSWorld.World.Chunks;
using UnityEngine;

namespace RTSWorld.Presentation.Tests
{
    public sealed class ChunkManagerTests
    {
        private GameObject root, observer;
        private Material material;
        private ChunkManager manager;

        [SetUp]
        public void SetUp()
        {
            root = new GameObject("Streaming test");
            observer = new GameObject("Observer");
            material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            manager = root.AddComponent<ChunkManager>();
            manager.Configure(observer.transform, material, cells: 8, maxPerFrame: 3, milliseconds: 50);
        }

        [TearDown]
        public void TearDown()
        {
            UnityEngine.Object.DestroyImmediate(root);
            UnityEngine.Object.DestroyImmediate(observer);
            UnityEngine.Object.DestroyImmediate(material);
        }

        private void Drain()
        {
            manager.Step();
            for (int i = 0; i < 100 && manager.PendingCount > 0; i++) manager.Step();
            Assert.That(manager.PendingCount, Is.EqualTo(0));
        }

        [Test]
        public void CountBudgetIsRespectedAndStationaryObserverDoesNotRegenerate()
        {
            manager.Step();
            Assert.That(manager.ActiveCount, Is.InRange(1, 3));
            Assert.That(manager.ActiveCount + manager.PendingCount, Is.EqualTo(9));
            Drain();
            var references = manager.ActiveCoordinates.ToDictionary(c => c, c => { manager.TryGetMesh(c, out var mesh); return mesh; });
            long generated = manager.TotalGenerated;
            observer.transform.position = new Vector3(1, 100, 1); // Same logical chunk; Y irrelevant.
            for (int i = 0; i < 10; i++) manager.Step();
            Assert.That(manager.TotalGenerated, Is.EqualTo(generated));
            foreach (var pair in references)
            { Assert.That(manager.TryGetMesh(pair.Key, out var mesh), Is.True); Assert.That(mesh, Is.SameAs(pair.Value)); }
        }

        [Test]
        public void TimeBudgetStopsAfterOneExpensiveChunk()
        {
            manager.Configure(observer.transform, material, cells: 256, maxPerFrame: 8, milliseconds: 0.1f);
            manager.Step();
            // Exercise the real clock with the largest supported geometry, not an artificial delay.
            Assert.That(manager.LastStepMilliseconds, Is.GreaterThanOrEqualTo(0.1));
            Assert.That(manager.LastGeneratedCount, Is.EqualTo(1));
            Assert.That(manager.PendingCount, Is.EqualTo(8));
        }

        [Test]
        public void TransitionPreservesOverlapAndReleasesOnlyOutgoingMeshes()
        {
            Drain();
            manager.TryGetMesh(new ChunkCoordinate(0, 0), out var kept);
            manager.TryGetMesh(new ChunkCoordinate(-1, 0), out var removed);
            observer.transform.position = new Vector3(16, 0, 0);
            Drain();
            Assert.That(manager.ActiveCount, Is.EqualTo(9));
            Assert.That(manager.TotalGenerated, Is.EqualTo(12));
            Assert.That(manager.TotalUnloaded, Is.EqualTo(3));
            Assert.That(removed == null, Is.True);
            manager.TryGetMesh(new ChunkCoordinate(0, 0), out var after);
            Assert.That(after, Is.SameAs(kept));
            Assert.That(root.transform.childCount, Is.EqualTo(9));
        }

        [Test]
        public void TeleportCancelsOldQueueWithoutDuplicates()
        {
            manager.Configure(observer.transform, material, cells: 8, maxPerFrame: 1);
            manager.Step();
            observer.transform.position = new Vector3(-1600, 0, 1600);
            Drain();
            Assert.That(manager.Center, Is.EqualTo(new ChunkCoordinate(-100, 100)));
            Assert.That(manager.ActiveCount, Is.EqualTo(9));
            Assert.That(manager.TotalGenerated, Is.EqualTo(10));
            Assert.That(manager.TotalUnloaded, Is.EqualTo(1));
            Assert.That(manager.ActiveCoordinates.Distinct().Count(), Is.EqualTo(9));
            foreach (var c in manager.ActiveCoordinates) Assert.That(c.X, Is.InRange(-101, -99));
        }

        [Test]
        public void RadiusAndSeedChangesReplaceOnlyWhatIsRequired()
        {
            Drain();
            manager.TryGetMesh(new ChunkCoordinate(0, 0), out var center);
            manager.LoadRadius = 0;
            Drain();
            Assert.That(manager.ActiveCount, Is.EqualTo(1));
            Assert.That(manager.TotalGenerated, Is.EqualTo(9));
            Assert.That(manager.TotalUnloaded, Is.EqualTo(8));
            manager.LoadRadius = 2;
            Drain();
            Assert.That(manager.ActiveCount, Is.EqualTo(25));
            manager.TryGetMesh(new ChunkCoordinate(0, 0), out var kept);
            Assert.That(kept, Is.SameAs(center));
            float before = kept.vertices[0].y;
            manager.Seed = 67890;
            Drain();
            Assert.That(center == null, Is.True);
            manager.TryGetMesh(new ChunkCoordinate(0, 0), out var replaced);
            Assert.That(replaced.vertices[0].y, Is.Not.EqualTo(before));
            Assert.That(root.GetComponentsInChildren<MeshFilter>().Length, Is.EqualTo(25));
        }

        [Test]
        public void DisableAndRepeatedTraversalDoNotKeepUnloadedResources()
        {
            for (int i = 0; i < 20; i++)
            {
                observer.transform.position = new Vector3(i * 64, 0, -i * 64);
                Drain();
                Assert.That(root.transform.childCount, Is.EqualTo(9));
                Assert.That(manager.TotalGenerated - manager.TotalUnloaded, Is.EqualTo(9));
            }
            var meshes = root.GetComponentsInChildren<MeshFilter>().Select(f => f.sharedMesh).ToArray();
            manager.enabled = false;
            Assert.That(manager.ActiveCount, Is.EqualTo(0));
            Assert.That(manager.PendingCount, Is.EqualTo(0));
            Assert.That(root.transform.childCount, Is.EqualTo(0));
            foreach (var mesh in meshes) Assert.That(mesh == null, Is.True);
        }

        [Test]
        public void LoadedNegativeChunksShareExactWorldEdges()
        {
            observer.transform.position = new Vector3(-1, 0, -1);
            Drain();
            Assert.That(manager.Center, Is.EqualTo(new ChunkCoordinate(-1, -1)));
            manager.TryGetMesh(new ChunkCoordinate(-1, -1), out var west);
            manager.TryGetMesh(new ChunkCoordinate(0, -1), out var east);
            var a = west.vertices.Where(v => v.x == 16).Select(v => v + new Vector3(-16, 0, -16)).Distinct().OrderBy(v => v.z);
            var b = east.vertices.Where(v => v.x == 0).Select(v => v + new Vector3(0, 0, -16)).Distinct().OrderBy(v => v.z);
            CollectionAssert.AreEqual(a, b);
        }

        [Test]
        public void ObserverConversionRespectsRootTransformAndHorizontalScale()
        {
            root.transform.position = new Vector3(100, 0, -200);
            root.transform.rotation = Quaternion.Euler(0, 90, 0);
            observer.transform.position = root.transform.TransformPoint(new Vector3(-1, 0, 17));
            Drain();
            Assert.That(manager.Center, Is.EqualTo(new ChunkCoordinate(-1, 1)));
        }

        [Test]
        public void InvalidConfigurationDoesNotDestroyValidPresentation()
        {
            Drain();
            Assert.Throws<ArgumentOutOfRangeException>(() => manager.Configure(observer.transform, material, maxPerFrame: 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => manager.Configure(observer.transform, material, milliseconds: float.NaN));
            Assert.That(manager.ActiveCount, Is.EqualTo(9));
        }
    }
}
