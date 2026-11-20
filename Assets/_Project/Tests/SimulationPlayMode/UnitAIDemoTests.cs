#if UNITY_EDITOR
using System;
using System.Collections;
using NUnit.Framework;
using RTSWorld.Presentation.Cameras;
using RTSWorld.Presentation.Streaming;
using RTSWorld.Simulation;
using RTSWorld.Units.Presentation;
using Unity.Entities;
using Unity.Mathematics;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace RTSWorld.Tests
{
    public sealed class UnitAIDemoTests
    {
        [UnityTest]
        public IEnumerator MeasuresRenderedPopulationsWithAndWithoutActiveAutonomy()
        {
            var scene = EditorSceneManager.LoadSceneInPlayMode("Assets/_Project/Scenes/UnitAIDemo.unity",
                new LoadSceneParameters(LoadSceneMode.Additive));
            AsyncOperation unload = null;
            try
            {
                yield return null;
                RtsInteractionDemo demo = null;
                foreach (var root in scene.GetRootGameObjects())
                    if (root.TryGetComponent(out RtsInteractionDemo interaction)) demo = interaction;
                Assert.That(demo, Is.Not.Null);

                foreach (int requested in new[] { 100, 500, 1000, 5000 })
                {
                    for (int phase = 0; phase < 2; phase++)
                    {
                        demo.Rebuild(requested);
                        var session = demo.Session;
                        var manager = session.World.EntityManager;
                        int active;
                        using (var query = manager.CreateEntityQuery(typeof(UnitAutonomy)))
                        {
                            if (phase == 0) manager.RemoveComponent<UnitAutonomy>(query);
                            active = query.CalculateEntityCount();
                        }
                        Assert.That(session.Count, Is.EqualTo(requested));
                        Assert.That(active, Is.EqualTo(phase == 0 ? 0 : requested - requested / 2));
                        for (int i = 0; i < 40; i++) yield return null;
                        const int frames = 120;
                        double duration = 0;
                        int completions = session.AutonomousDeliveries + session.AutonomousBuildings;
                        int assignments = session.AutonomyAssignments;
                        ulong tick = session.Tick;
                        for (int i = 0; i < frames; i++)
                        {
                            yield return null;
                            duration += Time.unscaledDeltaTime;
                        }
                        TestContext.WriteLine(FormattableString.Invariant(
                            $"ECS009_RENDER {requested},{active},{frames},{frames / duration:F2},{session.Tick - tick},{session.AutonomyAssignments - assignments},{session.AutonomousDeliveries + session.AutonomousBuildings - completions},{duration:F4}"));
                    }
                }
            }
            finally
            {
                if (scene.IsValid() && scene.isLoaded) unload = SceneManager.UnloadSceneAsync(scene);
            }
            if (unload != null) yield return unload;
        }

        [UnityTest]
        public IEnumerator AutonomousUnitsAndTasksSurviveVisualChunkUnload()
        {
            var scene = EditorSceneManager.LoadSceneInPlayMode("Assets/_Project/Scenes/UnitAIDemo.unity",
                new LoadSceneParameters(LoadSceneMode.Additive));
            AsyncOperation unload = null;
            try
            {
                yield return null;
                RtsInteractionDemo demo = null;
                ChunkManager stream = null;
                Transform focus = null;
                foreach (var root in scene.GetRootGameObjects())
                {
                    if (root.TryGetComponent(out RtsInteractionDemo interaction)) demo = interaction;
                    if (root.TryGetComponent(out ChunkManager chunks)) stream = chunks;
                    if (root.name == "Camera Focus") focus = root.transform;
                }

                Assert.That(demo, Is.Not.Null);
                Assert.That(stream, Is.Not.Null);
                Assert.That(focus, Is.Not.Null);
                var session = demo.Session;
                var manager = session.World.EntityManager;
                using var autonomous = manager.CreateEntityQuery(typeof(UnitAutonomy));
                Assert.That(autonomous.CalculateEntityCount(), Is.GreaterThan(0));
                Assert.That(session.UsesPhysicalDepots);
                Assert.That(session.Buildings.Count, Is.EqualTo(3));
                var site = session.Buildings[2];
                var building = manager.GetComponentData<Building>(site);
                var chunk = demo.LogicalTerrain.Region(building.Origin.x, building.Origin.y);
                demo.GetComponent<CameraStreamingObserver>().enabled = false;
                stream.LoadRadius = 0;
                focus.position = demo.WorldPosition(new double3(building.Origin.x + .5, 0, building.Origin.y + .5));
                stream.Step();
                Assert.That(stream.TryGetMesh(chunk, out _));
                focus.position += Vector3.right * 512;
                stream.Step();
                Assert.That(stream.TryGetMesh(chunk, out _), Is.False);

                ulong tick = session.Tick;
                const int frames = 120;
                double duration = 0;
                for (int i = 0; i < frames; i++)
                {
                    yield return null;
                    duration += Time.unscaledDeltaTime;
                }

                Assert.That(session.Tick, Is.GreaterThan(tick));
                Assert.That(session.AutonomyAssignments, Is.GreaterThan(0));
                Assert.That(manager.Exists(site));
                Assert.That(stream.TryGetMesh(chunk, out _), Is.False);
                TestContext.WriteLine($"ECS009 demo {frames / duration:F1} FPS, " +
                    $"{autonomous.CalculateEntityCount()}/{session.Count} unidades com IA, " +
                    $"{session.AutonomyAssignments} atribuições, {session.DeliveredTotal:F1} recursos entregues");
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
