#if UNITY_EDITOR
using System.Collections;
using NUnit.Framework;
using RTSWorld.Presentation.Cameras;
using RTSWorld.Presentation.Streaming;
using RTSWorld.Units.Presentation;
using Unity.Profiling;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace RTSWorld.Tests
{
    public sealed class CrowdMovementDemoTests
    {
        [UnityTest]
        public IEnumerator CrowdRecoverySceneReproducesAllTwelveScenarios()
        {
            const string path = "Assets/_Project/Scenes/CrowdRecoveryDemo.unity";
            var scene = EditorSceneManager.LoadSceneInPlayMode(path, new LoadSceneParameters(LoadSceneMode.Additive));
            AsyncOperation unload = null;
            try
            {
                yield return null;
                RtsInteractionDemo demo = null;
                foreach (var root in scene.GetRootGameObjects())
                {
                    demo = root.GetComponentInChildren<RtsInteractionDemo>();
                    if (demo != null) break;
                }
                Assert.That(demo, Is.Not.Null);
                for (int scenario = 0; scenario < 12; scenario++)
                {
                    Assert.That(demo.RunCrowdScenario(scenario), Is.True, $"Cenário {scenario + 1}");
                    var session = demo.Session;
                    var manager = session.World.EntityManager;
                    using var units = manager.CreateEntityQuery(typeof(RTSWorld.Simulation.UnitMotion),
                        typeof(RTSWorld.Simulation.LogicalRegion));
                    using var entities = units.ToEntityArray(Unity.Collections.Allocator.Temp);
                    using var initial = units.ToComponentDataArray<RTSWorld.Simulation.LogicalRegion>(Unity.Collections.Allocator.Temp);
                    for (int tick = 0; tick < 800; tick++) session.Advance(session.FixedDeltaTime);
                    session.CompleteJobs();
                    using var motions = units.ToComponentDataArray<RTSWorld.Simulation.UnitMotion>(Unity.Collections.Allocator.Temp);
                    int arrived = 0;
                    bool waited = false;
                    bool crossedChunk = false;
                    for (int i = 0; i < entities.Length; i++)
                        if (manager.Exists(entities[i]))
                            crossedChunk |= !initial[i].Chunk.Equals(
                                manager.GetComponentData<RTSWorld.Simulation.LogicalRegion>(entities[i]).Chunk);
                    foreach (var motion in motions)
                    {
                        if (motion.Progress == RTSWorld.Simulation.NavigationProgress.Arrived) arrived++;
                        waited |= motion.PassageWaitSeconds > 0;
                    }
                    if (scenario is 0 or 1 or 2 or 3 or 4 or 5 or 6 or 11)
                        Assert.That(arrived, Is.EqualTo(session.Count), $"Chegadas no cenário {scenario + 1}");
                    if (scenario == 2) Assert.That(waited, Is.True);
                    if (scenario == 7) Assert.That(session.DefeatedUnits, Is.EqualTo(1));
                    if (scenario == 8) Assert.That(session.DeliveredTotal, Is.GreaterThan(0));
                    if (scenario == 9) Assert.That(arrived, Is.EqualTo(1));
                    if (scenario == 10) Assert.That(arrived, Is.Zero);
                    if (scenario == 11) Assert.That(crossedChunk, Is.True);
                }
            }
            finally
            {
                if (scene.IsValid() && scene.isLoaded) unload = SceneManager.UnloadSceneAsync(scene);
            }
            if (unload != null) yield return unload;
        }

        [UnityTest]
        public IEnumerator CrowdRecoverySceneRunsOpposingGroupsWithLiveRecoveryControls()
        {
            const string path = "Assets/_Project/Scenes/CrowdRecoveryDemo.unity";
            var scene = EditorSceneManager.LoadSceneInPlayMode(path, new LoadSceneParameters(LoadSceneMode.Additive));
            AsyncOperation unload = null;
            try
            {
                yield return null;
                RtsInteractionDemo demo = null;
                foreach (var root in scene.GetRootGameObjects())
                {
                    demo = root.GetComponentInChildren<RtsInteractionDemo>();
                    if (demo != null) break;
                }
                Assert.That(demo, Is.Not.Null);
                Assert.That(demo.Session.Count, Is.EqualTo(100));
                Assert.That(demo.Session.RecoveryEnabled && demo.Session.CrowdEnabled, Is.True);
                Assert.That(demo.OrderCrowd(false), Is.True);
                for (int tick = 0; tick < 24; tick++) demo.Session.Advance(demo.Session.FixedDeltaTime);
                demo.Session.CompleteJobs();
                Assert.That(demo.Session.FieldBuilds, Is.GreaterThanOrEqualTo(2));
                demo.Session.RecoveryEnabled = false;
                Assert.That(demo.OrderCrowd(true), Is.True);

                foreach (int count in new[] { 100, 500, 1000, 5000 })
                foreach (bool enabled in new[] { false, true })
                {
                    demo.Rebuild(count);
                    demo.Session.RecoveryEnabled = enabled;
                    Assert.That(demo.Session.Count, Is.EqualTo(count));
                    Assert.That(demo.OrderCrowd(false), Is.True);
                    for (int frame = 0; frame < 12; frame++) yield return null;

                    using var allocations = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC Allocated In Frame", 1);
                    const int frames = 30;
                    double seconds = 0;
                    long bytes = 0;
                    for (int frame = 0; frame < frames; frame++)
                    {
                        yield return null;
                        seconds += Time.unscaledDeltaTime;
                        if (allocations.Valid) bytes += allocations.LastValue;
                    }
                    Assert.That(seconds, Is.GreaterThan(0));
                    string averageBytes = allocations.Valid ? (bytes / frames).ToString() : "unavailable";
                    TestContext.WriteLine(System.FormattableString.Invariant(
                        $"RECOVERY_SCENE n={count} enabled={enabled} fps={frames / seconds:F1} bytes_frame={averageBytes}"));
                }
            }
            finally
            {
                if (scene.IsValid() && scene.isLoaded) unload = SceneManager.UnloadSceneAsync(scene);
            }
            if (unload != null) yield return unload;
        }

        [UnityTest]
        public IEnumerator CrowdSceneRunsOwnSessionWithCombatEconomyAndSharedNavigation()
        {
            const string path = "Assets/_Project/Scenes/CrowdMovementDemo.unity";
            var scene = EditorSceneManager.LoadSceneInPlayMode(path, new LoadSceneParameters(LoadSceneMode.Additive));
            AsyncOperation unload = null;
            try
            {
                yield return null;
                RtsInteractionDemo demo = null;
                foreach (var root in scene.GetRootGameObjects())
                {
                    demo = root.GetComponentInChildren<RtsInteractionDemo>();
                    if (demo != null) break;
                }
                Assert.That(demo, Is.Not.Null);
                Assert.That(demo.Session.Count, Is.EqualTo(100));
                Assert.That(demo.Session.CrowdEnabled && demo.Session.EconomyEnabled, Is.True);
                Assert.That(demo.OrderCrowd(false), Is.True);
                for (int tick = 0; tick < 25; tick++) demo.Session.Advance(demo.Session.FixedDeltaTime);
                demo.Session.CompleteJobs();
                Assert.That(demo.Session.FieldBuilds, Is.GreaterThanOrEqualTo(2));
                Assert.That(demo.OrderCrowd(true), Is.True);
                var before = demo.Session.Tick;
                yield return null;
                demo.Session.Advance(demo.Session.FixedDeltaTime);
                Assert.That(demo.Session.Tick, Is.GreaterThan(before));

                var streaming = demo.GetComponent<ChunkManager>();
                if (streaming == null)
                    foreach (var root in scene.GetRootGameObjects())
                        if ((streaming = root.GetComponentInChildren<ChunkManager>()) != null) break;
                Assert.That(streaming, Is.Not.Null);
                int oldRadius = streaming.LoadRadius;
                streaming.LoadRadius = 0;
                var observer = demo.GetComponent<CameraStreamingObserver>();
                observer.enabled = false;
                var focus = System.Array.Find(scene.GetRootGameObjects(), item => item.name == "Camera Focus");
                Assert.That(focus, Is.Not.Null);
                var previousFocus = focus.transform.position;
                var depot = demo.Session.DepotCell;
                var chunk = demo.LogicalTerrain.Region(depot.x + .5, depot.y + .5);
                focus.transform.position = demo.WorldPosition(new Unity.Mathematics.double3(depot.x + .5, 0, depot.y + .5));
                streaming.Step();
                Assert.That(streaming.TryGetMesh(chunk, out _), Is.True);
                focus.transform.position += Vector3.right * 512;
                streaming.Step();
                Assert.That(streaming.TryGetMesh(chunk, out _), Is.False);
                demo.Session.Advance(demo.Session.FixedDeltaTime);
                demo.Session.CompleteJobs();
                Assert.That(demo.Session.Count, Is.EqualTo(100));

                focus.transform.position = previousFocus;
                streaming.LoadRadius = oldRadius;
                observer.enabled = true;

                foreach (int count in new[] { 100, 500, 1000, 5000 })
                {
                    demo.Rebuild(count);
                    Assert.That(demo.Session.Count, Is.EqualTo(count));
                    Assert.That(demo.OrderCrowd(false), Is.True);
                    for (int i = 0; i < 12; i++) yield return null;
                    using var allocation = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC Allocated In Frame", 1);
                    const int samples = 30;
                    double seconds = 0;
                    long bytes = 0;
                    for (int i = 0; i < samples; i++)
                    {
                        yield return null;
                        seconds += Time.unscaledDeltaTime;
                        if (allocation.Valid) bytes += allocation.LastValue;
                    }

                    string allocationResult = allocation.Valid ? (bytes / samples).ToString() : "unavailable";
                    TestContext.WriteLine(System.FormattableString.Invariant(
                        $"CROWD_SCENE n={count} frames={samples} fps={samples / seconds:F1} gc_bytes_frame={allocationResult}"));
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
