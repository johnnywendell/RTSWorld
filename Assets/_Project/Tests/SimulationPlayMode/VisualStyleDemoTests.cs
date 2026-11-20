#if UNITY_EDITOR
using System;
using System.Collections;
using System.IO;
using System.Linq;
using NUnit.Framework;
using RTSWorld.Simulation;
using RTSWorld.Presentation.Cameras;
using RTSWorld.Presentation.Streaming;
using RTSWorld.Units.Presentation;
using RTSWorld.World.Environment;
using Unity.Collections;
using Unity.Mathematics;
using Unity.Profiling;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace RTSWorld.Tests
{
    public sealed class VisualStyleDemoTests
    {
        private const string ScenePath = "Assets/_Project/Scenes/VisualStyleDemo.unity";

        [UnityTest]
        public IEnumerator VisualCoverageAndFoundationsSurviveCameraMotion()
        {
            var scene = EditorSceneManager.LoadSceneInPlayMode(ScenePath, new LoadSceneParameters(LoadSceneMode.Additive));
            AsyncOperation unload = null;
            try
            {
                yield return null;
                var demo = Component<RtsInteractionDemo>();
                var streaming = Component<ChunkManager>();
                var rig = Component<RtsCameraController>();
                var observer = Component<CameraStreamingObserver>();
                observer.Sync();
                Assert.That(observer.LoadingFirstView, Is.True);
                Assert.That(rig.OutputCamera.cullingMask, Is.Zero);
                yield return Drain();
                Assert.That(rig.OutputCamera.cullingMask, Is.Not.Zero);
                Assert.That(demo.VisualStatistics.ResidentBuildings, Is.EqualTo(2));
                Assert.That(demo.VisualStatistics.EnvironmentNear, Is.Positive);
                var depot = demo.Session.World.EntityManager.GetComponentData<Building>(demo.Session.Buildings[0]);
                var original = streaming.Center;
                var first = Placements();
                var originalArt = streaming.GetEnvironmentArt(original).ToArray();
                Assert.That(originalArt.Any(p => p.Decorative));
                Assert.That(demo.VisualStatistics.GroundInstances, Is.Positive);
                Assert.That(first.Any(x => x.Kind == NaturalResource.Tree));
                Assert.That(first.Any(x => x.Kind == NaturalResource.Stone));
                VerifyPlacementSupport(first);

                Assert.That(demo.TryGetVisualBuildingSupport(depot.Kind, depot.Origin, out double low, out double high));
                Assert.That(high - low, Is.GreaterThan(2));
                var size = BuildingDefinition.Get(depot.Kind).Size;
                for (int z = 0; z <= size.y; z++)
                for (int x = 0; x <= size.x; x++)
                {
                    Assert.That(demo.Session.TryGetTerrainElevation(depot.Origin.x + x, depot.Origin.y + z, out double ground));
                    Assert.That(demo.Navigation.TryGetSurface(depot.Origin.x + x, depot.Origin.y + z, out double surface));
                    Assert.That(surface, Is.EqualTo(ground).Within(1e-5), "The accepted depot footprint must be dry.");
                    Assert.That(ground, Is.InRange(low, high));
                }

                for (int i = 0; i < 20; i++)
                {
                    rig.Advance(new RtsCameraInputFrame(Vector2.right * .2f), .05, rig.OutputCamera.pixelHeight);
                    yield return null;
                }
                yield return Drain();
                Vector3 previousView = rig.OutputCamera.transform.position;
                double distantRequestStarted = Time.realtimeSinceStartupAsDouble;
                long generatedBeforePan = streaming.TotalGenerated;
                rig.Advance(new RtsCameraInputFrame(Vector2.right), 10, rig.OutputCamera.pixelHeight);
                rig.Motion.Stop();
                yield return null;
                Assert.That(observer.WaitingForCoverage, Is.True);
                Assert.That(rig.OutputCamera.transform.position, Is.EqualTo(previousView));
                Assert.That(streaming.TryGetMesh(original, out _), Is.True);
                yield return Drain();
                double distantRequestMs = (Time.realtimeSinceStartupAsDouble - distantRequestStarted) * 1000;
                long generatedDuringPan = streaming.TotalGenerated - generatedBeforePan;
                Assert.That(streaming.TryGetMesh(original, out _), Is.False);
                Assert.That(demo.VisualStatistics.ResidentBuildings, Is.Zero);
                Assert.That(demo.Session.Count, Is.EqualTo(10));
                Assert.That(demo.Session.Buildings.Count, Is.EqualTo(2));

                // Reverse a new request immediately and return to the original region.
                rig.Advance(new RtsCameraInputFrame(Vector2.up), 8, rig.OutputCamera.pixelHeight);
                rig.Motion.Stop();
                yield return null;
                rig.ResetView();
                yield return Drain();
                Assert.That(streaming.Center, Is.EqualTo(original));
                var restored = Placements();
                CollectionAssert.AreEqual(first, restored);
                CollectionAssert.AreEqual(originalArt, streaming.GetEnvironmentArt(original).ToArray());
                Assert.That(demo.VisualStatistics.ResidentBuildings, Is.EqualTo(2));

                rig.Advance(new RtsCameraInputFrame(Vector2.zero, zoomSteps: -6), 2, rig.OutputCamera.pixelHeight);
                yield return Drain();
                Assert.That(demo.VisualStatistics.EnvironmentMid, Is.Positive);
                Assert.That(demo.VisualStatistics.GroundInstances, Is.Zero);
                rig.Advance(new RtsCameraInputFrame(Vector2.zero, zoomSteps: -8), 2, rig.OutputCamera.pixelHeight);
                yield return Drain();
                Assert.That(demo.VisualStatistics.EnvironmentFar, Is.Positive);
                rig.ResetView();
                yield return Drain();
                Assert.That(demo.VisualStatistics.EnvironmentNear, Is.Positive);

                Assert.That(demo.Session.DamageBuilding(demo.Session.Buildings[0], depot.Health), Is.True);
                yield return null;
                Assert.That(demo.VisualStatistics.ResidentBuildings, Is.EqualTo(1));
                TestContext.WriteLine(FormattableString.Invariant(
                    $"VIS_MOTION peak_stream_ms={streaming.PeakStepMilliseconds:F4} jump_to_ready_ms={distantRequestMs:F1} jump_chunks={generatedDuringPan} generated={streaming.TotalGenerated} unloaded={streaming.TotalUnloaded}"));

                // Seed only the test save's treasury to exercise normal placement and build orders.
                var snapshot = demo.Session.CaptureSnapshot();
                snapshot.Stock = new[]
                {
                    new SavedStock { Kind = NaturalResource.Tree, Amount = 100 },
                    new SavedStock { Kind = NaturalResource.Stone, Amount = 100 }
                };
                string save = Path.Combine(Application.dataPath, "../Library/RTSWorld", Guid.NewGuid() + ".rts");
                try
                {
                    var write = SimulationSaveFile.SaveAsync(save, snapshot);
                    yield return new WaitUntil(() => write.IsCompleted);
                    write.GetAwaiter().GetResult();
                    demo.LoadSimulation(save);
                    yield return new WaitUntil(() => !demo.PersistenceBusy);
                    Assert.That(demo.PersistenceStatus, Is.EqualTo("Jogo carregado."));
                    yield return Drain();
                    CollectionAssert.AreEqual(originalArt, streaming.GetEnvironmentArt(original).ToArray());
                    var session = demo.Session;
                    var manager = session.World.EntityManager;
                    var site = Unity.Entities.Entity.Null;
                    using (var query = manager.CreateEntityQuery(typeof(UnitIdentity)))
                    using (var workers = query.ToEntityArray(Allocator.Temp))
                    {
                        for (int z = -5; z <= 5 && site == Unity.Entities.Entity.Null; z++)
                        for (int x = -5; x <= 5 && site == Unity.Entities.Entity.Null; x++)
                        {
                            var cell = depot.Origin + new int2(x, z);
                            site = session.PlaceBuilding(BuildingKind.Depot, cell, 0);
                            if (site != Unity.Entities.Entity.Null &&
                                session.ApplyOrder(UnitOrder.Build(site), 0, workers) != UnitOrderResult.Accepted)
                            {
                                session.CancelBuilding(site, 0);
                                site = Unity.Entities.Entity.Null;
                            }
                        }
                    }
                    Assert.That(site, Is.Not.EqualTo(Unity.Entities.Entity.Null));
                    var planned = manager.GetComponentData<Building>(site);
                    Assert.That(planned.State, Is.EqualTo(ConstructionState.Planned));
                    Assert.That(demo.TryGetVisualBuildingSupport(planned.Kind, planned.Origin, out double plannedLow, out double plannedHigh));
                    yield return null;
                    Assert.That(demo.VisualStatistics.ResidentBuildings, Is.EqualTo(2));
                    for (int i = 0; i < 1200 && manager.GetComponentData<Building>(site).State != ConstructionState.Completed; i++)
                        session.Advance(session.FixedDeltaTime);
                    session.CompleteJobs();
                    yield return null;
                    Assert.That(manager.GetComponentData<Building>(site).State, Is.EqualTo(ConstructionState.Completed));
                    Assert.That(demo.TryGetVisualBuildingSupport(planned.Kind, planned.Origin, out double finishedLow, out double finishedHigh));
                    Assert.That(finishedLow, Is.EqualTo(plannedLow));
                    Assert.That(finishedHigh, Is.EqualTo(plannedHigh));
                    Assert.That(demo.VisualStatistics.ResidentBuildings, Is.EqualTo(2));
                }
                finally { File.Delete(save); }

                T Component<T>() where T : Component =>
                    scene.GetRootGameObjects().Select(o => o.GetComponentInChildren<T>()).First(o => o != null);

                IEnumerator Drain()
                {
                    yield return null;
                    for (int i = 0; i < 400 && (streaming.PendingCount != 0 || observer.WaitingForCoverage); i++)
                        yield return null;
                    Assert.That(streaming.PendingCount, Is.Zero);
                    Assert.That(observer.WaitingForCoverage, Is.False);
                    yield return null;
                }

                ResourcePlacement[] Placements()
                {
                    Assert.That(streaming.TryGetResourceInstances(original, out _, out var instances));
                    var copy = instances.ToArray();
                    Assert.That(copy.Select(p => p.Key).Distinct().Count(), Is.EqualTo(copy.Length));
                    return copy;
                }

                void VerifyPlacementSupport(ResourcePlacement[] instances)
                {
                    foreach (var placement in instances)
                    {
                        var p = placement.Key;
                        Assert.That(demo.Session.TryGetTerrainElevation(p.X + (double)placement.OffsetX,
                            p.Z + (double)placement.OffsetZ, out double height));
                        Assert.That(placement.Elevation, Is.EqualTo(height).Within(1e-5));
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
        public IEnumerator VisualStyleReadsSimulationAcrossCommandsBackendsAndLoad()
        {
            var scene = EditorSceneManager.LoadSceneInPlayMode(ScenePath, new LoadSceneParameters(LoadSceneMode.Additive));
            AsyncOperation unload = null;
            string save = Path.Combine(Application.dataPath, "../Library/RTSWorld", Guid.NewGuid() + ".rts");
            try
            {
                yield return null;
                var demo = scene.GetRootGameObjects().Select(o => o.GetComponentInChildren<RtsInteractionDemo>()).First(o => o != null);
                var camera = scene.GetRootGameObjects().Select(o => o.GetComponentInChildren<Camera>()).First(o => o != null);
                var session = demo.Session;
                Assert.That(session.Count, Is.EqualTo(10));
                Assert.That(session.Buildings.Count, Is.EqualTo(2));
                for (int i = 0; i < 30 && (demo.VisualResourceCount == 0 || demo.VisualStatistics.ResidentBuildings < 2); i++)
                    yield return null;
                Assert.That(demo.VisualResourceCount, Is.GreaterThan(3));
                Assert.That(demo.VisualStatistics.ResidentBuildings, Is.EqualTo(2));
                var depot = session.World.EntityManager.GetComponentData<Building>(session.Buildings[0]);
                Assert.That(demo.TryGetVisualBuildingSupport(depot.Kind, depot.Origin, out double initialLow, out double initialHigh));
                demo.VisualLod = false;
                demo.VisualCulling = false;
                yield return null;
                Assert.That(demo.VisualStatistics.Animated, Is.EqualTo(10));
                Assert.That(demo.VisualStatistics.Walking, Is.Zero);
                Assert.That(demo.VisualStatistics.ActiveObjects, Is.Zero);

                var manager = session.World.EntityManager;
                var selected = Unity.Entities.Entity.Null;
                using (var query = manager.CreateEntityQuery(typeof(UnitIdentity), typeof(LogicalPose)))
                using (var entities = query.ToEntityArray(Allocator.Temp))
                {
                    Assert.That(entities.Count(e => manager.GetComponentData<UnitIdentity>(e).Kind == UnitKind.Villager), Is.EqualTo(5));
                    foreach (var entity in entities)
                    {
                        var pose = manager.GetComponentData<LogicalPose>(entity);
                        var point = camera.WorldToScreenPoint(demo.WorldPosition(pose.Position) + Vector3.up * .6f);
                        demo.Select(point, point, false, SelectionMode.Replace);
                        if (demo.SelectionCount == 1) { selected = entity; break; }
                    }
                }
                Assert.That(selected, Is.Not.EqualTo(Unity.Entities.Entity.Null));
                var source = (int2)math.floor(manager.GetComponentData<LogicalPose>(selected).Position.xz);
                var field = NavigationSearch.BuildFlowField(demo.Navigation, source);
                var goal = source;
                for (int x = -5; x <= 5 && goal.Equals(source); x++)
                for (int z = -5; z <= 5 && goal.Equals(source); z++)
                    if (x * x + z * z > 8 && field.TryGetDirection(source + new int2(x, z), out _))
                        goal = source + new int2(x,z);
                Assert.That(goal, Is.Not.EqualTo(source));
                Assert.That(demo.Issue(UnitOrder.Move(goal)), Is.EqualTo(UnitOrderResult.Accepted));
                session.Advance(session.FixedDeltaTime);
                yield return null;
                Assert.That(demo.VisualStatistics.Walking, Is.GreaterThan(0));

                // Selection may resolve a closer blocker along the same ray. Confirm movement via the scene summary.
                demo.VisualMode = VisualBackend.Hidden;
                for (int i = 0; i < 9; i++) session.Advance(session.FixedDeltaTime);
                session.CompleteJobs();
                yield return null;
                Assert.That(demo.MovingCount, Is.GreaterThan(0));
                Assert.That(demo.VisualStatistics.Animated, Is.Zero);
                Assert.That(demo.Issue(UnitOrder.Stop()), Is.EqualTo(UnitOrderResult.Accepted));
                session.Advance(.1f);
                yield return null;
                Assert.That(demo.MovingCount, Is.Zero);

                demo.VisualMode = VisualBackend.MeshRenderer;
                yield return null;
                Assert.That(demo.VisualStatistics.ActiveObjects, Is.EqualTo(10));
                var renderers = demo.GetComponentsInChildren<MeshRenderer>();
                Assert.That(renderers.Select(r => r.sharedMaterial).Distinct().Count(), Is.EqualTo(1));
                var filters = demo.GetComponentsInChildren<MeshFilter>();
                Assert.That(filters.Count(filter => filter.sharedMesh.name == "Aldeão"), Is.EqualTo(5));
                Assert.That(filters.Count(filter => filter.sharedMesh.name == "Soldado"), Is.EqualTo(5));
                using (var query = manager.CreateEntityQuery(typeof(LogicalPose)))
                using (var poses = query.ToComponentDataArray<LogicalPose>(Allocator.Temp))
                {
                    foreach (var filter in filters)
                    {
                        bool matched = false;
                        foreach (var pose in poses)
                        {
                            if ((filter.transform.position - demo.WorldPosition(pose.Position)).sqrMagnitude > 1e-6f) continue;
                            float4 q = pose.Rotation.value;
                            matched |= Quaternion.Angle(filter.transform.rotation, new Quaternion(q.x, q.y, q.z, q.w)) < .1f;
                        }
                        Assert.That(matched, Is.True);
                    }
                }
                demo.SaveSimulation(save);
                yield return new WaitUntil(() => !demo.PersistenceBusy);
                Assert.That(demo.PersistenceStatus, Is.EqualTo("Jogo salvo."));
                demo.Rebuild(100);
                demo.LoadSimulation(save);
                yield return new WaitUntil(() => !demo.PersistenceBusy);
                yield return null;
                Assert.That(demo.PersistenceStatus, Is.EqualTo("Jogo carregado."));
                Assert.That(demo.Session.Count, Is.EqualTo(10));
                Assert.That(demo.VisualStatistics.ActiveObjects, Is.EqualTo(10));
                Assert.That(demo.GetComponentsInChildren<MeshRenderer>().Length, Is.EqualTo(10));
                for (int i = 0; i < 30 && demo.VisualStatistics.ResidentBuildings < 2; i++) yield return null;
                Assert.That(demo.VisualStatistics.ResidentBuildings, Is.EqualTo(2));
                Assert.That(demo.TryGetVisualBuildingSupport(depot.Kind, depot.Origin, out double loadedLow, out double loadedHigh));
                Assert.That(loadedLow, Is.EqualTo(initialLow));
                Assert.That(loadedHigh, Is.EqualTo(initialHigh));

                using (var query = demo.Session.World.EntityManager.CreateEntityQuery(typeof(UnitIdentity)))
                using (var units = query.ToEntityArray(Allocator.Temp))
                    demo.Session.DestroyBatch(units.GetSubArray(0, 1));
                yield return null;
                Assert.That(demo.Session.Count, Is.EqualTo(9));
                Assert.That(demo.VisualStatistics.ActiveObjects, Is.EqualTo(9));
                Assert.That(demo.GetComponentsInChildren<MeshRenderer>().Length, Is.EqualTo(9));
                demo.VisualAnimation = false;
                yield return null;
                Assert.That(demo.VisualStatistics.Animated, Is.Zero);

                demo.VisualMode = VisualBackend.Instanced;
                demo.VisualLod = true;
                var target = RenderTexture.GetTemporary(128, 128, 24);
                try
                {
                    camera.targetTexture = target;
                    yield return null;
                    Assert.That(demo.VisualStatistics.Far, Is.EqualTo(9));
                    Assert.That(demo.VisualStatistics.Shadowed, Is.Zero);
                    Assert.That(demo.GetComponentsInChildren<MeshRenderer>().Length, Is.Zero);
                }
                finally
                {
                    camera.targetTexture = null;
                    RenderTexture.ReleaseTemporary(target);
                }
            }
            finally
            {
                File.Delete(save);
                if (scene.IsValid() && scene.isLoaded) unload = SceneManager.UnloadSceneAsync(scene);
            }
            if (unload != null) yield return unload;
        }

        [UnityTest]
        public IEnumerator ProfilesThreeBackendsAtFivePopulations()
        {
            var scene = EditorSceneManager.LoadSceneInPlayMode(ScenePath, new LoadSceneParameters(LoadSceneMode.Additive));
            AsyncOperation unload = null;
            try
            {
                yield return null;
                var demo = scene.GetRootGameObjects().Select(o => o.GetComponentInChildren<RtsInteractionDemo>()).First(o => o != null);
                var camera = scene.GetRootGameObjects().Select(o => o.GetComponentInChildren<Camera>()).First(o => o != null);
                var streaming = scene.GetRootGameObjects().Select(o => o.GetComponentInChildren<ChunkManager>()).First(o => o != null);
                var rig = scene.GetRootGameObjects().Select(o => o.GetComponentInChildren<RtsCameraController>()).First(o => o != null);
                demo.VisualShadows = demo.VisualLod = demo.VisualCulling = false;
                TestContext.WriteLine($"VIS_CONFIG {camera.pixelWidth}x{camera.pixelHeight} quality={QualitySettings.names[QualitySettings.GetQualityLevel()]}");
                using var gc = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC Allocated In Frame", 1);
                using var total = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "Total Used Memory", 1);
                using var calls = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Draw Calls Count", 1);
                using var batches = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Batches Count", 1);
                using var passes = ProfilerRecorder.StartNew(ProfilerCategory.Render, "SetPass Calls Count", 1);
                using var triangles = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Triangles Count", 1);
                using var vertices = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Vertices Count", 1);
                using var main = ProfilerRecorder.StartNew(ProfilerCategory.Internal, "Main Thread", 1);
                using var render = ProfilerRecorder.StartNew(ProfilerCategory.Render, "CPU Render Thread Frame Time", 1);
                using var cameraRender = ProfilerRecorder.StartNew(ProfilerCategory.Render,
                    $"UniversalRenderPipeline.RenderSingleCameraInternal: {camera.name}", 1);
                using var gpu = ProfilerRecorder.StartNew(ProfilerCategory.Render, "GPU Frame Time", 1);
                using var instancedBatches = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Instanced Batches Count", 1);
                var recorders = new[] { gc, total, calls, batches, passes, triangles, vertices, main, render, cameraRender, gpu, instancedBatches };

                foreach (int count in new[] { 10, 100, 500, 1000, 5000 })
                {
                    demo.Rebuild(count);
                    var configurations = new[]
                    {
                        (VisualBackend.Legacy, false, false, 0), (VisualBackend.Instanced, false, false, 0),
                        (VisualBackend.MeshRenderer, false, false, 0), (VisualBackend.Instanced, true, true, 0),
                        (VisualBackend.Instanced, true, false, 0), (VisualBackend.Instanced, true, false, -6),
                        (VisualBackend.Instanced, true, false, -14)
                    };
                    foreach (var (mode, defaults, shadows, zoomSteps) in configurations.Take(count == 5000 ? 7 : 5))
                    {
                        if (zoomSteps != 0)
                        {
                            rig.ResetView();
                            rig.Advance(new RtsCameraInputFrame(Vector2.zero, zoomSteps: zoomSteps), 2, camera.pixelHeight);
                        }
                        demo.VisualMode = mode;
                        demo.VisualLod = demo.VisualCulling = defaults;
                        demo.VisualShadows = shadows;
                        for (int i = 0; i < 400 && (i < 12 || streaming.PendingCount > 0); i++)
                            yield return null;
                        Assert.That(streaming.PendingCount, Is.Zero);
                        double seconds = 0, visual = 0, culled = 0, pose = 0, lod = 0, transforms = 0, submit = 0, streamMs = 0;
                        const int samples = 120;
                        var sums = new long[recorders.Length];
                        for (int i = 0; i < samples; i++)
                        {
                            yield return null;
                            seconds += Time.unscaledDeltaTime;
                            visual += demo.PresentationMilliseconds;
                            streamMs += streaming.LastStepMilliseconds;
                            var s = demo.VisualStatistics;
                            culled += s.CullMilliseconds;
                            pose += s.PoseMilliseconds;
                            lod += s.LodMilliseconds;
                            transforms += s.TransformMilliseconds;
                            submit += s.SubmitMilliseconds;
                            for (int j = 0; j < recorders.Length; j++)
                                if (recorders[j].Valid) sums[j] += recorders[j].LastValue;
                        }
                        var stats = demo.VisualStatistics;
                        Assert.That(demo.Session.Count, Is.EqualTo(count));
                        if (defaults) Assert.That(stats.Animated + stats.Far, Is.EqualTo(stats.Visible));
                        TestContext.WriteLine(FormattableString.Invariant(
                            $"VIS_PROFILE {count},{mode},{defaults},{stats.Visible},{count-stats.Visible},{stats.Animated},{stats.Shadowed},{stats.Far},{stats.ActiveObjects},{stats.Submissions},{samples/seconds:F2},{visual/samples:F4},{culled/samples:F4},{pose/samples:F4},{lod/samples:F4},{transforms/samples:F4},{submit/samples:F4},{Sample(0)},{Sample(1)},{Sample(2)},{Sample(3)},{Sample(4)},{Sample(5)},{Sample(6)},{Sample(7,1e-6)},{Sample(8,1e-6)},{Sample(9,1e-6)},{Sample(10,1e-6)},{Sample(11)}"));
                        TestContext.WriteLine(FormattableString.Invariant(
                            $"VIS_TERRAIN {count},{mode},{defaults},{shadows},{rig.Motion.Zoom:F0},{streaming.ActiveCount},{demo.VisualResourceCount},{stats.EnvironmentNear},{stats.EnvironmentMid},{stats.EnvironmentFar},{streamMs/samples:F4},{streaming.PeakStepMilliseconds:F4}"));

                        string Sample(int i, double scale = 1) => recorders[i].Valid && sums[i] > 0
                            ? (sums[i] * scale / samples).ToString("F3", System.Globalization.CultureInfo.InvariantCulture)
                            : "NOT_RUN";
                    }
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
