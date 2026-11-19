#if UNITY_EDITOR
using System.Collections;
using NUnit.Framework;
using RTSWorld.Presentation.Cameras;
using RTSWorld.Presentation.Streaming;
using RTSWorld.Simulation;
using RTSWorld.Units.Presentation;
using Unity.Collections;
using Unity.Mathematics;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace RTSWorld.Tests
{
    public sealed class IntegratedWorldDemoTests
    {
        [UnityTest]
        public IEnumerator EnvironmentalUnitsMoveAcrossChunksWhileTheVisualWorldReloads()
        {
            Scene scene = EditorSceneManager.LoadSceneInPlayMode("Assets/_Project/Scenes/RTSWorldIntegratedDemo.unity",
                new LoadSceneParameters(LoadSceneMode.Additive));
            AsyncOperation unload = null;
            try
            {
                yield return null;
                RtsInteractionDemo demo = null;
                ChunkManager stream = null;
                foreach (var root in scene.GetRootGameObjects())
                {
                    if (root.TryGetComponent(out RtsInteractionDemo interaction)) demo = interaction;
                    if (root.TryGetComponent(out ChunkManager chunks)) stream = chunks;
                }

                Assert.That(demo, Is.Not.Null);
                Assert.That(stream.ActiveEnvironment, Is.Not.Null);
                Assert.That(demo.LogicalTerrain.Definition.Seed, Is.EqualTo(stream.Seed));
                Assert.That(demo.Session.Count, Is.EqualTo(100));
                var rig = demo.GetComponent<RtsCameraController>();
                var bridge = demo.GetComponent<CameraStreamingObserver>();
                bridge.Sync();
                bridge.enabled = false;
                var observer = scene.GetRootGameObjects()[0].transform;
                foreach (var root in scene.GetRootGameObjects())
                    if (root.name == "Camera Focus") observer = root.transform;
                Vector3 saved = rig.FocusWorld;
                observer.position = saved;
                stream.LoadRadius = 0;
                stream.Step();
                var initialChunk = stream.Center;
                Assert.That(stream.TryGetMesh(initialChunk, out var beforeMesh), Is.True);
                Vector3[] vertices = beforeMesh.vertices;
                Color[] colors = beforeMesh.colors;
                int resources = demo.LogicalTerrain.GetEnvironmentChunk(initialChunk).Resources.Count;
                Assert.That(stream.ActiveResourceCount, Is.EqualTo(resources));
                Assert.That(resources, Is.GreaterThan(0));

                var session = demo.Session;
                var manager = session.World.EntityManager;
                using var unitQuery = manager.CreateEntityQuery(typeof(UnitIdentity), typeof(LogicalPose));
                using (var poses = unitQuery.ToComponentDataArray<LogicalPose>(Allocator.Temp))
                    foreach (var pose in poses)
                    {
                        var cell = (int2)math.floor(pose.Position.xz);
                        Assert.That(demo.Navigation.TryGetCost(cell, out int cost) && cost > 0, Is.True);
                        Assert.That(pose.Position.y, Is.EqualTo(demo.LogicalTerrain.Elevation(pose.Position.x, pose.Position.z)).Within(.00001));
                    }

                // The presenter publishes the selectable snapshot in LateUpdate.
                yield return null;
                var camera = rig.OutputCamera;
                demo.Select(camera.pixelRect.min, camera.pixelRect.max, true, SelectionMode.Replace);
                Assert.That(demo.SelectionCount, Is.GreaterThan(0));
                Assert.That(demo.Issue(UnitOrder.Move(new int2(-152, 351))), Is.EqualTo(UnitOrderResult.Accepted));
                using var startingPoses = unitQuery.ToComponentDataArray<LogicalPose>(Allocator.Temp);
                observer.position += Vector3.right * 512;
                stream.Step();
                Assert.That(stream.TryGetMesh(initialChunk, out _), Is.False);

                bool crossed = false;
                for (int batch = 0; batch < 25; batch++)
                {
                    for (int tick = 0; tick < 24; tick++)
                    {
                        session.Advance(session.FixedDeltaTime);
                        session.CompleteJobs();
                    }

                    using var current = unitQuery.ToComponentDataArray<LogicalPose>(Allocator.Temp);
                    for (int i = 0; i < current.Length; i++)
                    {
                        double2 position = current[i].Position.xz;
                        Assert.That(demo.Navigation.TryGetCost((int2)math.floor(position), out int cost) && cost > 0, Is.True);
                        var start = startingPoses[i].Position;
                        crossed |= !demo.LogicalTerrain.Region(start.x, start.z).Equals(
                            demo.LogicalTerrain.Region(position.x, position.y));
                    }
                }

                Assert.That(crossed, Is.True);
                observer.position = saved;
                stream.Step();
                Assert.That(stream.TryGetMesh(initialChunk, out var afterMesh), Is.True);
                Assert.That(afterMesh, Is.Not.SameAs(beforeMesh));
                CollectionAssert.AreEqual(vertices, afterMesh.vertices);
                CollectionAssert.AreEqual(colors, afterMesh.colors);
                Assert.That(stream.ActiveCount, Is.EqualTo(1));
                Assert.That(stream.ActiveResourceCount, Is.EqualTo(resources));
                Assert.That(session.Count, Is.EqualTo(100));
                Assert.That(demo.Issue(UnitOrder.Stop()), Is.EqualTo(UnitOrderResult.Accepted));

                using (var poses = unitQuery.ToComponentDataArray<LogicalPose>(Allocator.Temp))
                    foreach (var pose in poses)
                    {
                        Assert.That(demo.Navigation.TryGetCost((int2)math.floor(pose.Position.xz), out int cost) && cost > 0, Is.True);
                        Assert.That(pose.Position.y, Is.EqualTo(demo.LogicalTerrain.Elevation(pose.Position.x, pose.Position.z)).Within(.00001));
                    }
            }
            finally
            {
                if (scene.IsValid() && scene.isLoaded) unload = SceneManager.UnloadSceneAsync(scene);
            }
            if (unload != null) yield return unload;
        }
    }
}
#endif
