#if UNITY_EDITOR
using System.Collections;
using NUnit.Framework;
using RTSWorld.Units.Presentation;
using RTSWorld.Presentation.Streaming;
using RTSWorld.Simulation;
using RTSWorld.World.Chunks;
using Unity.Collections;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace RTSWorld.Tests
{
    public sealed class NavigationDemoTests
    {
        [UnityTest]
        public IEnumerator NavigationSceneRunsBothGroupsAndReleasesItsWorld()
        {
            Scene scene = EditorSceneManager.LoadSceneInPlayMode("Assets/_Project/Scenes/NavigationDemo.unity",
                new LoadSceneParameters(LoadSceneMode.Additive));
            var streaming = new GameObject("Navigation visual streaming");
            var observer = new GameObject("Navigation visual observer");
            var material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            Unity.Entities.World navigationWorld = null;
            AsyncOperation unload = null;
            try
            {
                yield return null;
                UnitDemo demo = null;
                foreach (var root in scene.GetRootGameObjects())
                    if (root.TryGetComponent(out demo)) break;
                Assert.That(demo, Is.Not.Null);
                Assert.That(demo.DestinationsAccepted, Is.True);
                Assert.That(demo.Session.Count, Is.EqualTo(1000));
                var world = demo.Session.World;
                var manager = streaming.AddComponent<ChunkManager>();
                observer.transform.position = new Vector3(-31.5f, 0, -58.5f);
                manager.Configure(observer.transform, material, worldSeed: 7, cells: 32, horizontal: 1, vertical: 1, radius: 0, maxPerFrame: 1);
                manager.Step();
                Assert.That(manager.TryGetMesh(new ChunkCoordinate(-1, -2), out var original), Is.True);

                var query = world.EntityManager.CreateEntityQuery(typeof(UnitIdentity), typeof(LogicalPose));
                using var before = query.ToComponentDataArray<LogicalPose>(Allocator.Persistent);
                observer.transform.position = new Vector3(32, 0, 32);
                manager.Step();
                yield return null;
                Assert.That(original == null, Is.True);
                Assert.That(manager.TotalUnloaded, Is.GreaterThan(0));
                ulong tick = demo.Session.Tick;
                demo.Session.Advance(.1f);
                demo.Session.CompleteJobs();
                Assert.That(demo.Session.Tick, Is.GreaterThan(tick));
                using (var after = query.ToComponentDataArray<LogicalPose>(Allocator.Temp))
                {
                    Assert.That(after.Length, Is.EqualTo(before.Length));
                    Assert.That(after[0].Position, Is.Not.EqualTo(before[0].Position));
                }
                demo.SetVisible(false);
                demo.Rebuild(5000);
                Assert.That(world.IsCreated, Is.False);
                Assert.That(demo.DestinationsAccepted, Is.True);
                Assert.That(demo.Session.Count, Is.EqualTo(5000));
                navigationWorld = demo.Session.World;
            }
            finally
            {
                Object.DestroyImmediate(streaming);
                Object.DestroyImmediate(observer);
                Object.DestroyImmediate(material);
                if (scene.IsValid() && scene.isLoaded) unload = SceneManager.UnloadSceneAsync(scene);
            }
            if (unload != null) yield return unload;
            Assert.That(navigationWorld.IsCreated, Is.False);
        }
    }
}
#endif
