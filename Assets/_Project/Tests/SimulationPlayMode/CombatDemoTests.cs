#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using RTSWorld.Presentation.Cameras;
using RTSWorld.Presentation.Streaming;
using RTSWorld.Simulation;
using RTSWorld.Units.Presentation;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace RTSWorld.Tests
{
    public sealed class CombatDemoTests
    {
        private const string ScenePath = "Assets/_Project/Scenes/CombatDemo.unity";

        [UnityTest]
        public IEnumerator SelectAttackStopAndResumeWhileTheProceduralWorldStreams()
        {
            var scene = EditorSceneManager.LoadSceneInPlayMode(ScenePath, new LoadSceneParameters(LoadSceneMode.Additive));
            AsyncOperation unload = null;
            try
            {
                yield return null;
                var (demo, stream) = FindDemo(scene);
                Assert.That(demo.Session.Count, Is.EqualTo(100));
                demo.Rebuild(2);
                var session = demo.Session;
                Assert.That(session.EconomyEnabled, Is.True);
                var manager = session.World.EntityManager;
                var camera = demo.GetComponent<RtsCameraController>().OutputCamera;
                Assert.That(demo.Navigation.Obstacles, Is.Not.Null);
                var start = session.DepotCell;
                var goal = ReachableTestDestination(demo, start);

                Entity attacker = Spawn(session, UnitDefinition.Soldier, 0, start);
                Entity target = Spawn(session, UnitDefinition.Soldier, 1, goal);
                yield return null;
                var origin = manager.GetComponentData<LogicalPose>(attacker).Position;
                Vector2 point = ScreenPosition(attacker);
                demo.Select(point, point, false, SelectionMode.Replace);
                Assert.That(demo.SelectionCount, Is.EqualTo(1));
                Assert.That(demo.MoveAt(ScreenPosition(target)), Is.EqualTo(UnitOrderResult.Accepted));
                Assert.That(manager.GetComponentData<UnitCombat>(attacker).Target, Is.EqualTo(target));
                Tick(session, 5);
                Assert.That(manager.GetComponentData<LogicalPose>(attacker).Position, Is.Not.EqualTo(origin));
                Assert.That(demo.Issue(UnitOrder.Stop()), Is.EqualTo(UnitOrderResult.Accepted));
                var stopped = manager.GetComponentData<LogicalPose>(attacker).Position;
                Tick(session, 6);
                Assert.That(manager.GetComponentData<LogicalPose>(attacker).Position, Is.EqualTo(stopped));

                Assert.That(demo.MoveAt(ScreenPosition(target)), Is.EqualTo(UnitOrderResult.Accepted));
                stream.LoadRadius = 0;
                var observer = demo.GetComponent<CameraStreamingObserver>();
                observer.enabled = false;
                var focus = Array.Find(scene.GetRootGameObjects(), item => item.name == "Camera Focus");
                Assert.That(focus, Is.Not.Null);
                var targetPose = manager.GetComponentData<LogicalPose>(target).Position;
                var chunk = demo.LogicalTerrain.Region(targetPose.x, targetPose.z);
                focus.transform.position = demo.WorldPosition(targetPose);
                stream.Step();
                Assert.That(stream.TryGetMesh(chunk, out _), Is.True);
                focus.transform.position += Vector3.right * 512;
                stream.Step();
                Assert.That(stream.TryGetMesh(chunk, out _), Is.False);

                bool hit = false;
                for (int i = 0; i < 450 && manager.Exists(target); i++)
                {
                    Tick(session, 1);
                    var pose = manager.GetComponentData<LogicalPose>(attacker).Position;
                    Assert.That(demo.Navigation.TryGetCost((int2)math.floor(pose.xz), out int cost) && cost > 0, Is.True);
                    if (!manager.Exists(target)) break;
                    var health = manager.GetComponentData<UnitHealth>(target);
                    if (health.Current >= health.Maximum) continue;
                    if (!hit)
                    {
                        Assert.That(math.distance(pose.xz, targetPose.xz), Is.LessThanOrEqualTo(1.5));
                        Assert.That(manager.GetComponentData<MovementVelocity>(attacker).Value, Is.EqualTo(double3.zero));
                        Assert.That(manager.GetComponentData<UnitCombat>(target).HitUntil, Is.GreaterThan(session.CombatTime));
                    }
                    hit = true;
                }
                Assert.That(hit, Is.True);
                Assert.That(manager.Exists(target), Is.False);
                Assert.That(session.DefeatedUnits, Is.EqualTo(1));
                Assert.That(manager.GetComponentData<UnitCombat>(attacker).Target, Is.EqualTo(Entity.Null));
                Assert.That(demo.Issue(UnitOrder.Move(start)), Is.EqualTo(UnitOrderResult.Accepted));
                Tick(session, 4);
                Assert.That(manager.GetComponentData<UnitMotion>(attacker).Activity, Is.EqualTo(UnitActivity.Moving));

                var here = manager.GetComponentData<LogicalPose>(attacker).Position;
                var lethal = UnitDefinition.Soldier;
                lethal.Attack = 100;
                using (var enemy = session.SpawnBatch(lethal, 1, 1, here, default))
                    Assert.That(session.ApplyOrder(UnitOrder.Attack(attacker), 1, enemy), Is.EqualTo(UnitOrderResult.Accepted));
                Tick(session, 1);
                yield return null;
                Assert.That(demo.SelectionCount, Is.Zero);
                Assert.That(manager.Exists(attacker), Is.False);

                Vector2 ScreenPosition(Entity entity) => camera.WorldToScreenPoint(
                    demo.WorldPosition(manager.GetComponentData<LogicalPose>(entity).Position) + Vector3.up * .825f);
            }
            finally
            {
                if (scene.IsValid() && scene.isLoaded) unload = SceneManager.UnloadSceneAsync(scene);
            }
            if (unload != null) yield return unload;
        }

        [UnityTest]
        public IEnumerator ProfilesIntegratedBattlesAtFourPopulations()
        {
            var scene = EditorSceneManager.LoadSceneInPlayMode(ScenePath, new LoadSceneParameters(LoadSceneMode.Additive));
            AsyncOperation unload = null;
            try
            {
                yield return null;
                var (demo, stream) = FindDemo(scene);
                var camera = demo.GetComponent<RtsCameraController>();
                foreach (int count in new[] { 100, 500, 1000, 5000 })
                {
                    camera.ResetView();
                    demo.Rebuild(count);
                    var session = demo.Session;
                    var manager = session.World.EntityManager;
                    using var query = manager.CreateEntityQuery(typeof(UnitIdentity), typeof(LogicalPose));
                    var allies = new List<Entity>();
                    var enemies = new List<Entity>();
                    // Copy before yielding. Temp containers cannot live across frames.
                    using (var handles = query.ToEntityArray(Allocator.Temp))
                    {
                        foreach (var entity in handles)
                        {
                            var identity = manager.GetComponentData<UnitIdentity>(entity);
                            if (identity.Kind != UnitKind.Soldier) continue;
                            (identity.Owner == 0 ? allies : enemies).Add(entity);
                        }
                    }
                    Assert.That(session.Count, Is.EqualTo(count));
                    Assert.That(allies.Count, Is.GreaterThan(0));
                    Assert.That(enemies.Count, Is.GreaterThan(0));
                    Entity allyTarget = allies[0], enemyTarget = enemies[0];
                    using (var attackers = new NativeArray<Entity>(allies.ToArray(), Allocator.Temp))
                        Assert.That(session.ApplyOrder(UnitOrder.Attack(enemyTarget), 0, attackers), Is.EqualTo(UnitOrderResult.Accepted));
                    using (var attackers = new NativeArray<Entity>(enemies.ToArray(), Allocator.Temp))
                        Assert.That(session.ApplyOrder(UnitOrder.Attack(allyTarget), 1, attackers), Is.EqualTo(UnitOrderResult.Accepted));

                    int ordered = session.EngagedUnits;
                    var total = new double[8];
                    var peak = new double[8];
                    double begin = Time.realtimeSinceStartupAsDouble;
                    int fighting = 0, simulationSamples = 0, frames = 0;
                    ulong observedTick = session.Tick;
                    for (int frame = 0; frame < 720 && observedTick < 120; frame++)
                    {
                        if (frame == 20)
                            camera.Advance(new RtsCameraInputFrame(Vector2.right), 2, camera.OutputCamera.pixelHeight);
                        yield return null;
                        frames++;
                        Sample(0, demo.PresentationMilliseconds);
                        Sample(1, demo.SimulationMilliseconds);
                        Sample(2, stream.LastStepMilliseconds);
                        if (observedTick == session.Tick) continue;
                        observedTick = session.Tick;
                        simulationSamples++;
                        Sample(3, session.TargetMilliseconds);
                        Sample(4, session.PursuitMilliseconds);
                        Sample(5, session.StrikeMilliseconds);
                        Sample(6, session.DamageMilliseconds);
                        Sample(7, session.DeathMilliseconds);
                        fighting = math.max(fighting, session.InRangeUnits);
                    }

                    Assert.That(simulationSamples, Is.GreaterThan(0));
                    Assert.That(fighting, Is.GreaterThan(0));
                    Report(FormattableString.Invariant(
                        $"COMBAT_PROFILE total={count} ordered={ordered} in_range_peak={fighting} deaths={session.DefeatedUnits} ticks={observedTick} tick_samples={simulationSamples} fps={frames / (Time.realtimeSinceStartupAsDouble - begin):F2} visual_avg_ms={total[0] / frames:F4} sim_frame_avg_ms={total[1] / frames:F4} stream_avg_ms={total[2] / frames:F4} stream_peak_ms={peak[2]:F4} target_tick_avg_ms={total[3] / simulationSamples:F4} pursuit_tick_avg_ms={total[4] / simulationSamples:F4} pursuit_peak_ms={peak[4]:F4} attack_tick_avg_ms={total[5] / simulationSamples:F4} damage_tick_avg_ms={total[6] / simulationSamples:F4} death_tick_avg_ms={total[7] / simulationSamples:F4} death_peak_ms={peak[7]:F4}"));

                    void Sample(int channel, double value)
                    {
                        total[channel] += value;
                        peak[channel] = math.max(peak[channel], value);
                    }
                }
            }
            finally
            {
                if (scene.IsValid() && scene.isLoaded) unload = SceneManager.UnloadSceneAsync(scene);
            }
            if (unload != null) yield return unload;
        }

        private static void Report(string message)
        {
            TestContext.WriteLine(message);
            UnityEngine.Debug.Log(message);
        }

        private static (RtsInteractionDemo, ChunkManager) FindDemo(Scene scene)
        {
            RtsInteractionDemo demo = null;
            ChunkManager stream = null;
            foreach (var root in scene.GetRootGameObjects())
            {
                if (root.TryGetComponent(out RtsInteractionDemo interaction)) demo = interaction;
                if (root.TryGetComponent(out ChunkManager chunks)) stream = chunks;
            }
            Assert.That(demo, Is.Not.Null);
            Assert.That(stream, Is.Not.Null);
            return (demo, stream);
        }

        private static int2 ReachableTestDestination(RtsInteractionDemo demo, int2 start)
        {
            var field = NavigationSearch.BuildFlowField(demo.Navigation, start);
            var camera = demo.GetComponent<RtsCameraController>().OutputCamera;
            for (int z = -7; z <= 7; z++)
                for (int x = -7; x <= 7; x++)
                {
                    var candidate = start + new int2(x, z);
                    if (x * x + z * z < 36 || !field.TryGetDirection(candidate, out _)) continue;
                    double2 center = (double2)candidate + .5;
                    demo.Session.TryGetTerrainElevation(center.x, center.y, out double height);
                    var viewport = camera.WorldToViewportPoint(demo.WorldPosition(new double3(center.x, height, center.y)));
                    if (viewport.x > .18f && viewport.x < .85f && viewport.y > .18f && viewport.y < .85f)
                        return candidate;
                }
            Assert.Fail("Nenhuma célula de teste visível e alcançável a partir do depósito.");
            return default;
        }

        private static Entity Spawn(UnitSession session, UnitDefinition definition, int owner, int2 cell)
        {
            using var entities = session.SpawnBatch(definition, owner, 1, new double3(cell.x + .5, 0, cell.y + .5), default);
            return entities[0];
        }

        private static void Tick(UnitSession session, int count)
        {
            for (int i = 0; i < count; i++)
            {
                session.Advance(session.FixedDeltaTime);
                session.CompleteJobs();
            }
        }
    }
}
#endif
