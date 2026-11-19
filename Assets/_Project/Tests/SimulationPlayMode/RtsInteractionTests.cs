#if UNITY_EDITOR
using System.Collections;
using NUnit.Framework;
using RTSWorld.Presentation.Cameras;
using RTSWorld.Presentation.Streaming;
using RTSWorld.Simulation;
using RTSWorld.Units.Presentation;
using RTSWorld.World.Chunks;
using Unity.Mathematics;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace RTSWorld.Tests
{
    public sealed class RtsInteractionTests
    {
        [UnityTest]
        public IEnumerator DemoSelectsMovesAndStopsWithCameraAndVisualStreaming()
        {
            var scene = EditorSceneManager.LoadSceneInPlayMode("Assets/_Project/Scenes/RTSInteractionDemo.unity",
                new LoadSceneParameters(LoadSceneMode.Additive));
            AsyncOperation unload = null;
            Unity.Entities.World world = null;
            try
            {
                yield return null;
                RtsInteractionDemo demo = null;
                ChunkManager stream = null;
                foreach (var root in scene.GetRootGameObjects())
                {
                    if (root.TryGetComponent(out RtsInteractionDemo found)) demo = found;
                    if (root.TryGetComponent(out ChunkManager chunks)) stream = chunks;
                }

                Assert.That(demo, Is.Not.Null);
                Assert.That(stream, Is.Not.Null);
                var rig = demo.GetComponent<RtsCameraController>();
                var camera = rig.OutputCamera;
                Assert.That(demo.Session.Count, Is.EqualTo(100));
                world = demo.Session.World;
                var manager = world.EntityManager;
                Unity.Entities.Entity member;
                using (var spawned = demo.Session.SpawnBatch(UnitDefinition.Soldier, 0, 1,
                    new double3(.5, 0, -8.5), default))
                    member = spawned[0];

                yield return null;
                Vector2 ScreenPoint() => camera.WorldToScreenPoint(
                    demo.WorldPosition(manager.GetComponentData<LogicalPose>(member).Position) + Vector3.up * .825f);

                Vector2 point = ScreenPoint();
                demo.Select(point, point, false, SelectionMode.Replace);
                Assert.That(demo.SelectionCount, Is.EqualTo(1));
                Assert.That(demo.Issue(UnitOrder.Move(new int2(12, -9))), Is.EqualTo(UnitOrderResult.Accepted));

                double2 before = manager.GetComponentData<LogicalPose>(member).Position.xz;
                for (int i = 0; i < 10; i++)
                {
                    demo.Session.Advance(demo.Session.FixedDeltaTime);
                    demo.Session.CompleteJobs();
                }
                Assert.That(manager.GetComponentData<LogicalPose>(member).Position.xz, Is.Not.EqualTo(before));
                Assert.That(demo.Issue(UnitOrder.Stop()), Is.EqualTo(UnitOrderResult.Accepted));
                before = manager.GetComponentData<LogicalPose>(member).Position.xz;
                demo.Session.Advance(.1f);
                demo.Session.CompleteJobs();
                Assert.That(manager.GetComponentData<LogicalPose>(member).Position.xz, Is.EqualTo(before));

                rig.Advance(new RtsCameraInputFrame(Vector2.zero, zoomSteps: 1, rotation: 1), .15, camera.pixelHeight);
                yield return null;
                point = ScreenPoint();
                demo.Select(point - Vector2.one * 6, point + Vector2.one * 6, true, SelectionMode.Replace);
                Assert.That(demo.SelectionCount, Is.EqualTo(1));
                Assert.That(demo.Issue(UnitOrder.Move(new int2(40, -9))), Is.EqualTo(UnitOrderResult.Accepted));
                var oldChunk = new ChunkCoordinate(0, -1);
                for (int i = 0; i < 32 && !stream.TryGetMesh(oldChunk, out _); i++) stream.Step();
                Assert.That(stream.TryGetMesh(oldChunk, out var oldMesh), Is.True);
                long unloadedBefore = stream.TotalUnloaded;
                rig.Advance(new RtsCameraInputFrame(Vector2.right), 12, camera.pixelHeight);
                for (int i = 0; i < 180; i++)
                {
                    demo.Session.Advance(demo.Session.FixedDeltaTime);
                    demo.Session.CompleteJobs();
                }

                yield return null;
                Assert.That(stream.TotalUnloaded, Is.GreaterThan(unloadedBefore));
                Assert.That(oldMesh == null || !stream.TryGetMesh(oldChunk, out _), Is.True);
                Assert.That(demo.Session.Count, Is.EqualTo(101));
                Assert.That(manager.GetComponentData<LogicalPose>(member).Position.x, Is.GreaterThan(10));
                using (var doomed = new Unity.Collections.NativeArray<Unity.Entities.Entity>(new[] { member }, Unity.Collections.Allocator.Temp))
                    demo.Session.DestroyBatch(doomed);
                yield return null;
                Assert.That(demo.SelectionCount, Is.Zero);
            }
            finally
            {
                if (scene.IsValid() && scene.isLoaded) unload = SceneManager.UnloadSceneAsync(scene);
            }
            if (unload != null) yield return unload;
            Assert.That(world.IsCreated, Is.False);
        }
    }
}
#endif
