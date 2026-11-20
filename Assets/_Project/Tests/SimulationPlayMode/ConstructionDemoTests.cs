#if UNITY_EDITOR
using System.Collections;
using NUnit.Framework;
using RTSWorld.Presentation.Cameras;
using RTSWorld.Presentation.Streaming;
using RTSWorld.Simulation;
using RTSWorld.Units.Presentation;
using Unity.Mathematics;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace RTSWorld.Tests
{
    public sealed class ConstructionDemoTests
    {
        [UnityTest]
        public IEnumerator BuildingsStayAuthoritativeAfterTheirChunkUnloads()
        {
            const string path = "Assets/_Project/Scenes/ConstructionDemo.unity";
            var scene = EditorSceneManager.LoadSceneInPlayMode(path, new LoadSceneParameters(LoadSceneMode.Additive));
            AsyncOperation unload = null;
            try
            {
                yield return null;
                RtsInteractionDemo demo = null;
                Transform focus = null;
                ChunkManager stream = null;
                foreach (var root in scene.GetRootGameObjects())
                {
                    if (root.TryGetComponent(out RtsInteractionDemo interaction)) demo = interaction;
                    if (root.TryGetComponent(out ChunkManager chunks)) stream = chunks;
                    if (root.name == "Camera Focus") focus = root.transform;
                }
                Assert.That(demo, Is.Not.Null);
                Assert.That(focus, Is.Not.Null);
                var session = demo.Session;
                Assert.That(session.UsesPhysicalDepots);
                Assert.That(session.Buildings.Count, Is.EqualTo(2));
                var entity = session.Buildings[0];
                var manager = session.World.EntityManager;
                var depot = manager.GetComponentData<Building>(entity);
                Assert.That(depot.Kind, Is.EqualTo(BuildingKind.Depot));
                Assert.That(depot.State, Is.EqualTo(ConstructionState.Completed));
                Assert.That(demo.Navigation.TryGetCost(depot.Origin, out int cost) && cost == 0);

                Assert.That(stream, Is.Not.Null);
                demo.GetComponent<CameraStreamingObserver>().enabled = false;
                stream.LoadRadius = 0;
                var chunk = demo.LogicalTerrain.Region(depot.Origin.x, depot.Origin.y);
                focus.position = demo.WorldPosition(new double3(depot.Origin.x + .5, 0, depot.Origin.y + .5));
                stream.Step();
                Assert.That(stream.TryGetMesh(chunk, out _), Is.True);
                focus.position += Vector3.right * 512;
                stream.Step();
                Assert.That(stream.TryGetMesh(chunk, out _), Is.False);
                Assert.That(manager.GetComponentData<Building>(entity).Health, Is.EqualTo(depot.Health));
                Assert.That(demo.Navigation.TryGetCost(depot.Origin, out cost) && cost == 0);
                const int samples = 120;
                for (int i = 0; i < 45; i++) yield return null;
                double withBuildings = 0;
                for (int i = 0; i < samples; i++)
                {
                    yield return null;
                    withBuildings += Time.unscaledDeltaTime;
                }
                Assert.That(session.DamageBuilding(entity, depot.Health), Is.True);
                Assert.That(demo.Navigation.TryGetCost(depot.Origin, out cost) && cost > 0);
                var center = session.Buildings[0];
                Assert.That(session.DamageBuilding(center, manager.GetComponentData<Building>(center).Health), Is.True);
                for (int i = 0; i < 45; i++) yield return null;

                double withoutBuildings = 0;
                for (int i = 0; i < samples; i++)
                {
                    yield return null;
                    withoutBuildings += Time.unscaledDeltaTime;
                }
                TestContext.WriteLine($"ConstructionDemo: {samples} frames por fase, " +
                    $"{samples / withBuildings:F1} FPS com 2 estruturas, {samples / withoutBuildings:F1} sem estruturas");
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
