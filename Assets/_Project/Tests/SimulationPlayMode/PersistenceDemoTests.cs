#if UNITY_EDITOR
using System;
using System.Collections;
using System.Diagnostics;
using System.IO;
using System.Linq;
using NUnit.Framework;
using RTSWorld.Presentation.Cameras;
using RTSWorld.Presentation.Streaming;
using RTSWorld.Simulation;
using RTSWorld.Units.Presentation;
using RTSWorld.World.Environment;
using Unity.Mathematics;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.Profiling;

namespace RTSWorld.Tests
{
    public sealed class PersistenceDemoTests
    {
        [UnityTest]
        public IEnumerator StartingBuildingsPreserveSpawnConnectivityWithAndWithoutAvoidance()
        {
            var scene = EditorSceneManager.LoadSceneInPlayMode("Assets/_Project/Scenes/PersistenceDemo.unity",
                new LoadSceneParameters(LoadSceneMode.Additive));
            AsyncOperation unload = null;
            try
            {
                yield return null;
                var demo = scene.GetRootGameObjects().Select(root => root.GetComponent<RtsInteractionDemo>())
                    .First(item => item != null);
                foreach (bool crowd in new[] { true, false })
                {
                    var settings = new UnityEditor.SerializedObject(demo);
                    settings.FindProperty("crowdDemo").boolValue = crowd;
                    settings.ApplyModifiedPropertiesWithoutUndo();
                    demo.Rebuild(100);
                    var session = demo.Session;
                    Assert.That(session.CrowdEnabled, Is.EqualTo(crowd));
                    Assert.That(scene.GetRootGameObjects().Select(root => root.GetComponent<ChunkManager>()).First(item => item != null).Seed, Is.EqualTo(7));
                    Assert.That(session.Count, Is.EqualTo(100));
                    Assert.That(session.Buildings.Count, Is.EqualTo(3));
                    var manager = session.World.EntityManager;
                    foreach (var entity in session.Buildings)
                    {
                        var building = manager.GetComponentData<Building>(entity);
                        var size = BuildingDefinition.Get(building.Kind).Size;
                        for (int z = 0; z < size.y; z++)
                        for (int x = 0; x < size.x; x++)
                        {
                            var cell = building.Origin + new int2(x, z);
                            Assert.That(demo.Navigation.GetPlacementIssue(cell), Is.EqualTo(PlacementIssue.Building));
                            Assert.That(demo.Navigation.TryGetCost(cell, out int cost) && cost == 0, Is.True);
                        }
                        TestContext.WriteLine($"{building.Kind} {building.Origin} {building.State}");
                    }

                    var goal = new int2(-134, 351);
                    var field = NavigationSearch.BuildFlowField(demo.Navigation, goal);
                    using var query = manager.CreateEntityQuery(typeof(UnitIdentity));
                    using var entities = query.ToEntityArray(Unity.Collections.Allocator.Temp);
                    using var members = new Unity.Collections.NativeList<Unity.Entities.Entity>(50, Unity.Collections.Allocator.Temp);
                    foreach (var entity in entities)
                    {
                        var cell = (int2)math.floor(manager.GetComponentData<LogicalPose>(entity).Position.xz);
                        Assert.That(demo.Navigation.TryGetCost(cell, out int cost) && cost > 0, Is.True, cell.ToString());
                        Assert.That(field.TryGetDirection(cell, out _), Is.True, cell.ToString());
                        if (manager.GetComponentData<UnitIdentity>(entity).Owner == 0) members.Add(entity);
                    }
                    Assert.That(members.Length, Is.EqualTo(50));
                    Assert.That(session.ApplyOrder(UnitOrder.Move(goal), 0, members.AsArray()), Is.EqualTo(UnitOrderResult.Accepted));

                    var disconnected = false;
                    for (int z = 0; z < demo.Navigation.Size.y && !disconnected; z++)
                    for (int x = 0; x < demo.Navigation.Size.x && !disconnected; x++)
                    {
                        var cell = demo.Navigation.Min + new int2(x, z);
                        if (!demo.Navigation.TryGetCost(cell, out int cost) || cost == 0 ||
                            field.TryGetDirection(cell, out _)) continue;
                        Assert.That(session.ApplyOrder(UnitOrder.Move(cell), 0, members.AsArray()), Is.EqualTo(UnitOrderResult.Unreachable));
                        disconnected = true;
                    }
                    Assert.That(disconnected, Is.True);
                }
            }
            finally
            {
                if (scene.IsValid() && scene.isLoaded) unload = SceneManager.UnloadSceneAsync(scene);
            }
            if (unload != null) yield return unload;
        }

        [UnityTest]
        public IEnumerator SaveResetLoadRestoresEconomyConstructionAndStreaming()
        {
            var scene = EditorSceneManager.LoadSceneInPlayMode("Assets/_Project/Scenes/PersistenceDemo.unity",
                new LoadSceneParameters(LoadSceneMode.Additive));
            string path = Path.Combine(Application.dataPath, "..", "Library", "RTSWorld", Guid.NewGuid() + ".rts");
            AsyncOperation unload = null;
            try
            {
                yield return null;
                var roots = scene.GetRootGameObjects();
                var demo = roots.Select(root => root.GetComponent<RtsInteractionDemo>()).First(item => item != null);
                var stream = roots.Select(root => root.GetComponent<ChunkManager>()).First(item => item != null);
                var focus = Array.Find(roots, root => root.name == "Camera Focus").transform;
                demo.Rebuild(100);
                var original = demo.Session;
                // Stage the snapshot explicitly so changes to path length cannot make the
                // planned depot finish before the first delivery under test.
                SimulationSnapshot before;
                {
                    original.CompleteJobs();
                    var manager = original.World.EntityManager;
                    using var workersQuery = manager.CreateEntityQuery(typeof(UnitAutonomy));
                    using var workers = workersQuery.ToEntityArray(Unity.Collections.Allocator.Temp);
                    foreach (var entity in workers)
                    {
                        var ai = manager.GetComponentData<UnitAutonomy>(entity);
                        if (ai.Mode != AutonomyMode.Work) continue;
                        ai.AutoBuild = false;
                        manager.SetComponentData(entity, ai);
                    }
                    before = original.CaptureSnapshot();
                    for (int i = 0; i < 240 && before.Delivered == 0; i++)
                    {
                        original.Advance(.1f);
                        if (i % 5 == 0) before = original.CaptureSnapshot();
                    }
                    Assert.That(before.Delivered, Is.GreaterThan(0));
                    var site = original.Buildings.First(entity =>
                        manager.GetComponentData<Building>(entity).State == ConstructionState.Planned);
                    // Release existing routes before staging the concurrent build and delivery.
                    // Restarting autonomy preserves carried resources.
                    foreach (var entity in workers)
                    {
                        if (!manager.Exists(entity)) continue;
                        var ai = manager.GetComponentData<UnitAutonomy>(entity);
                        Assert.That(original.EnableAutonomy(entity, ai.Mode, autoBuild: true));
                    }

                    var order = UnitOrderResult.Unreachable;
                    foreach (var entity in workers)
                    {
                        if (!manager.Exists(entity) ||
                            manager.GetComponentData<UnitAutonomy>(entity).Mode != AutonomyMode.Work) continue;
                        using var builder = new Unity.Collections.NativeArray<Unity.Entities.Entity>(
                            new[] { entity }, Unity.Collections.Allocator.Temp);
                        order = original.ApplyOrder(UnitOrder.Build(site), 0, builder);
                        if (order == UnitOrderResult.Accepted) break;
                    }
                    Assert.That(order, Is.EqualTo(UnitOrderResult.Accepted));
                    for (int i = 0; i < 240; i++)
                    {
                        original.Advance(.05f);
                        before = original.CaptureSnapshot();
                        if (before.Buildings.Any(building => building.State == ConstructionState.UnderConstruction) &&
                            before.Units.Any(unit => unit.Cargo > 0)) break;
                    }
                }
                original.Paused = true;
                Assert.That(before.Buildings.Any(building => building.State == ConstructionState.UnderConstruction));
                Assert.That(before.Units.Any(unit => unit.Cargo > 0));
                Assert.That(before.Delivered, Is.GreaterThan(0));
                Assert.That(before.Buildings.Length, Is.EqualTo(3));
                Assert.That(before.Units.Any(unit => unit.Autonomous));
                var removed = before.Resources.First(resource => resource.Available &&
                    resource.X > before.Min.x && resource.X < before.Max.x &&
                    resource.Z > before.Min.y && resource.Z < before.Max.y);
                var key = new ResourceKey(before.Seed, before.EnvironmentVersion, removed.X, removed.Z);
                Assert.That(original.SetEnvironmentalResourceAvailable(key, false));
                var expected = original.CaptureSnapshot();
                demo.SaveSimulation(path);
                yield return new WaitUntil(() => !demo.PersistenceBusy);
                Assert.That(demo.PersistenceStatus, Is.EqualTo("Jogo salvo."));
                demo.Rebuild(500);
                Assert.That(original.World.IsCreated, Is.False);
                Assert.That(demo.Session.Count, Is.EqualTo(500));
                stream.Seed = expected.Seed + 1;
                for (int repetition = 0; repetition < 2; repetition++)
                {
                    var previous = demo.Session;
                    demo.LoadSimulation(path);
                    yield return new WaitUntil(() => !demo.PersistenceBusy);
                    Assert.That(demo.PersistenceStatus, Is.EqualTo("Jogo carregado."));
                    Assert.That(previous.World.IsCreated, Is.False);
                    var actual = demo.Session.CaptureSnapshot();
                    Assert.That(stream.Seed, Is.EqualTo(expected.Seed));
                    Assert.That(actual.Tick, Is.EqualTo(expected.Tick));
                    Assert.That(actual.Stock, Is.EqualTo(expected.Stock));
                    Assert.That(actual.Resources, Is.EqualTo(expected.Resources));
                    Assert.That(actual.Delivered, Is.EqualTo(expected.Delivered));
                    Assert.That(actual.Units.Sum(unit => unit.Cargo), Is.EqualTo(expected.Units.Sum(unit => unit.Cargo)).Within(1e-8));
                    Assert.That(actual.Units.Count(unit => unit.BuildTarget != 0),
                        Is.EqualTo(expected.Units.Count(unit => unit.BuildTarget != 0)));
                    Assert.That(actual.Buildings, Is.EqualTo(expected.Buildings));
                    Assert.That(actual.Units.Select(unit => unit.Id), Is.EquivalentTo(expected.Units.Select(unit => unit.Id)));
                    Assert.That(demo.Session.IsResourceAvailable(key), Is.False);
                    foreach (var building in actual.Buildings)
                        Assert.That(demo.Navigation.TryGetCost(building.Origin, out int cost) && cost == 0);
                }

                demo.GetComponent<CameraStreamingObserver>().enabled = false;
                stream.LoadRadius = 0;
                focus.position = demo.WorldPosition(new double3(key.X + .5, 0, key.Z + .5));
                var chunk = demo.LogicalTerrain.Region(key.X, key.Z);
                stream.Step();
                Assert.That(stream.TryGetMesh(chunk, out _));
                focus.position += Vector3.right * 1024;
                stream.Step();
                Assert.That(stream.TryGetMesh(chunk, out _), Is.False);
                focus.position -= Vector3.right * 1024;
                stream.Step();
                Assert.That(stream.TryGetMesh(chunk, out _));
                Assert.That(demo.Session.IsResourceAvailable(key), Is.False);
                int visible = demo.LogicalTerrain.GetEnvironmentChunk(chunk).Resources
                    .Count(resource => demo.Session.IsResourceAvailable(resource.Key));
                Assert.That(stream.ActiveResourceCount, Is.EqualTo(visible));

                File.WriteAllText(path, "incompleto");
                var active = demo.Session;
                demo.LoadSimulation(path);
                yield return new WaitUntil(() => !demo.PersistenceBusy);
                Assert.That(demo.PersistenceStatus, Does.StartWith("Falha"));
                Assert.That(demo.Session, Is.SameAs(active));
                active.Paused = false;
                for (int i = 0; i < 100; i++) active.Advance(.1f);
                Assert.That(active.Tick, Is.GreaterThan(expected.Tick));
                var continued = active.CaptureSnapshot();
                Assert.That(continued.Delivered, Is.GreaterThan(expected.Delivered));
                Assert.That(continued.Buildings.Sum(building => building.Work),
                    Is.GreaterThan(expected.Buildings.Sum(building => building.Work)));
            }
            finally
            {
                File.Delete(path);
                if (scene.IsValid() && scene.isLoaded) unload = SceneManager.UnloadSceneAsync(scene);
            }
            if (unload != null) yield return unload;
        }

        [UnityTest]
        public IEnumerator MeasuresPersistenceStagesAcrossPopulations()
        {
            var scene = EditorSceneManager.LoadSceneInPlayMode("Assets/_Project/Scenes/PersistenceDemo.unity",
                new LoadSceneParameters(LoadSceneMode.Additive));
            string path = Path.Combine(Application.dataPath, "..", "Library", "RTSWorld", Guid.NewGuid() + ".rts");
            AsyncOperation unload = null;
            try
            {
                yield return null;
                var demo = scene.GetRootGameObjects().Select(root => root.GetComponent<RtsInteractionDemo>())
                    .First(item => item != null);
                foreach (int population in new[] { 100, 1000, 5000 })
                {
                    demo.Rebuild(population);
                    var demonstration = demo.Session;
                    demonstration.Paused = true;
                    using var compact = population == 100 ? CreateCompactSession(demonstration) : null;
                    var session = compact ?? demonstration;
                    long bytesBefore = Profiler.GetMonoUsedSizeLong();
                    var timer = Stopwatch.StartNew();
                    var snapshot = session.CaptureSnapshot();
                    double capture = timer.Elapsed.TotalMilliseconds;
                    long captureBytes = Profiler.GetMonoUsedSizeLong() - bytesBefore;
                    var save = SimulationSaveFile.SaveAsync(path, snapshot);
                    yield return new WaitUntil(() => save.IsCompleted);
                    var write = save.GetAwaiter().GetResult();
                    var load = SimulationSaveFile.ReadAsync(path);
                    yield return new WaitUntil(() => load.IsCompleted);
                    var read = load.GetAwaiter().GetResult();
                    long nativeBefore = Profiler.GetTotalAllocatedMemoryLong();
                    bytesBefore = Profiler.GetMonoUsedSizeLong();
                    timer.Restart();
                    using var restored = UnitSession.RestoreSnapshot(read.Snapshot);
                    double restore = timer.Elapsed.TotalMilliseconds;
                    long restoreBytes = Profiler.GetMonoUsedSizeLong() - bytesBefore;
                    long nativeDelta = Profiler.GetTotalAllocatedMemoryLong() - nativeBefore;
                    Assert.That(restored.Count, Is.EqualTo(population));
                    TestContext.WriteLine(FormattableString.Invariant(
                        $"ECS010_PROFILE {population},{snapshot.Resources.Length},{capture:F3},{write.SerializationMs:F3},{write.WriteMs:F3},{read.ReadMs:F3},{read.DeserializationMs:F3},{restore:F3},{captureBytes},{restoreBytes},{nativeDelta},{new FileInfo(path).Length}"));
                }
            }
            finally
            {
                File.Delete(path);
                if (scene.IsValid() && scene.isLoaded) unload = SceneManager.UnloadSceneAsync(scene);
            }
            if (unload != null) yield return unload;
        }

        private static UnitSession CreateCompactSession(UnitSession demonstration)
        {
            var terrain = demonstration.Terrain;
            var center = (int2)((demonstration.Bounds.Min + demonstration.Bounds.Max) * .5);
            var min = center - 24;
            var max = center + 24;
            var session = new UnitSession(terrain, min, max, crowdEnabled: true);
            var obstacles = EnvironmentalObstacles.Build(terrain, min, max, .22);
            session.SetNavigationGrid(new NavigationGrid(terrain, min, max, 2.25, obstacles: obstacles));
            using var members = session.SpawnBatch(UnitDefinition.Villager, 0, 100,
                new double3(center.x, 0, center.y), default);
            return session;
        }
    }
}
#endif
