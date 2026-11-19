#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using RTSWorld.Presentation.Cameras;
using RTSWorld.Presentation.Streaming;
using RTSWorld.Simulation;
using RTSWorld.Units.Presentation;
using RTSWorld.World.Environment;
using Unity.Collections;
using Unity.Mathematics;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace RTSWorld.Tests
{
    public sealed class EconomyDemoTests
    {
        [UnityTest]
        public IEnumerator EconomyContinuesOffscreenAndExhaustedTreeStaysAbsentAfterReload()
        {
            var scene = EditorSceneManager.LoadSceneInPlayMode("Assets/_Project/Scenes/EconomyDemo.unity",
                new LoadSceneParameters(LoadSceneMode.Additive));
            AsyncOperation unload = null;
            try
            {
                yield return null;
                yield return null;
                RtsInteractionDemo demo = null;
                ChunkManager stream = null;
                Transform observer = null;
                foreach (var root in scene.GetRootGameObjects())
                {
                    if (root.TryGetComponent(out RtsInteractionDemo interaction)) demo = interaction;
                    if (root.TryGetComponent(out ChunkManager chunks)) stream = chunks;
                    if (root.name == "Camera Focus") observer = root.transform;
                }

                Assert.That(demo, Is.Not.Null);
                Assert.That(stream, Is.Not.Null);
                Assert.That(observer, Is.Not.Null);
                Assert.That(demo.Session.EconomyEnabled, Is.True);
                var session = demo.Session;
                demo.GetComponent<CameraStreamingObserver>().enabled = false;
                stream.LoadRadius = 0;
                var depot = (double2)session.DepotCell + .5;
                var field = NavigationSearch.BuildFlowField(demo.Navigation, session.DepotCell);
                var approaches = new List<int2>();
                ResourcePlacement target = default;
                double nearest = double.PositiveInfinity;
                var originChunk = demo.LogicalTerrain.Region(depot.x, depot.y);
                var data = demo.LogicalTerrain.GetEnvironmentChunk(originChunk);
                foreach (var resource in data.Resources)
                {
                    if (resource.Kind != NaturalResource.Tree) continue;
                    double distance = math.distancesq(depot, new double2((double)resource.Key.X + resource.OffsetX,
                        (double)resource.Key.Z + resource.OffsetZ));
                    if (distance >= nearest) continue;
                    demo.Navigation.Obstacles.FindInteractionCells(demo.Navigation, resource.Key, 3.5, approaches, field);
                    if (approaches.Count == 0) continue;
                    nearest = distance;
                    target = resource;
                }

                Assert.That(double.IsInfinity(nearest), Is.False);
                observer.position = demo.WorldPosition(new double3(target.Key.X + .5, 0, target.Key.Z + .5));
                Vector3 originalObserver = observer.position;
                stream.Step();
                Assert.That(stream.TryGetMesh(originChunk, out var initialMesh), Is.True);
                int initialCount = stream.ActiveResourceCount;
                int initialVertices = initialMesh.vertexCount;
                using var batch = session.SpawnBatch(UnitDefinition.Villager, 0, 10, new double3(depot.x, 0, depot.y), default, .03);
                Assert.That(session.Gather(target.Key, 0, batch), Is.EqualTo(UnitOrderResult.Accepted));
                Assert.That(session.TryGetResource(target.Key, out var initial), Is.True);
                for (int i = 0; i < 600; i++)
                {
                    Tick(session);
                    session.TryGetResource(target.Key, out var state);
                    if (state.Remaining < initial.Remaining) break;
                }

                Assert.That(session.TryGetResource(target.Key, out var partial), Is.True);
                Assert.That(partial.Remaining, Is.InRange(.01, initial.Initial - .01));
                observer.position += Vector3.right * 512;
                stream.Step();
                Assert.That(stream.TryGetMesh(originChunk, out _), Is.False);
                for (int i = 0; i < 3600 && session.DeliveredTotal < initial.Initial - 1e-8; i++) Tick(session);
                Assert.That(session.DeliveredTotal, Is.EqualTo(initial.Initial).Within(1e-7));
                Assert.That(session.GetStock(0, NaturalResource.Tree), Is.EqualTo(initial.Initial).Within(1e-7));

                observer.position = originalObserver;
                stream.Step();
                Assert.That(stream.TryGetMesh(originChunk, out var refreshed), Is.True);
                Assert.That(stream.ActiveResourceCount, Is.EqualTo(initialCount - 1));
                Assert.That(refreshed.vertexCount, Is.LessThan(initialVertices));
                Assert.That(session.TryGetResource(target.Key, out var depleted) && depleted.Remaining == 0, Is.True);
                Assert.That(demo.Navigation.Obstacles.TryGet(target.Key, out var obstacle) && !obstacle.Available, Is.True);

                demo.Rebuild(100); // Population controls explicitly start a fresh scenario.
                stream.Step();
                Assert.That(demo.Session.DeliveredTotal, Is.Zero);
                Assert.That(stream.ActiveResourceCount, Is.EqualTo(initialCount));
            }
            finally
            {
                if (scene.IsValid() && scene.isLoaded) unload = SceneManager.UnloadSceneAsync(scene);
            }

            if (unload != null) yield return unload;
        }

        private static void Tick(UnitSession session)
        {
            session.Advance(session.FixedDeltaTime);
            session.CompleteJobs();
        }
    }
}
#endif
