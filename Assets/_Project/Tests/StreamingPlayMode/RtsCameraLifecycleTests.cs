using System.Collections;
using System.Linq;
using NUnit.Framework;
using RTSWorld.Presentation.Cameras;
using RTSWorld.Presentation.Environment;
using RTSWorld.Presentation.Streaming;
using RTSWorld.World.Chunks;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;

namespace RTSWorld.Streaming.PlayMode.Tests
{
    public sealed class RtsCameraLifecycleTests
    {
        [UnityTest]
        public IEnumerator DevicesMoveTheFocusAndStreamWithoutIdleRegenerationOrTerrainPenetration()
        {
            int baseline = Resources.FindObjectsOfTypeAll<Mesh>().Count(m => m.name.StartsWith("Stream chunk "));
            var root = new GameObject("Camera stream"); var focus = new GameObject("Focus");
            var rig = new GameObject("Camera rig"); var cameraObject = new GameObject("Camera output");
            var camera = cameraObject.AddComponent<UnityEngine.Camera>(); camera.aspect = 1.6f;
            var material = new Material(Shader.Find("RTSWorld/Environment Colors"));
            var keyboard = InputSystem.AddDevice<Keyboard>(); var mouse = InputSystem.AddDevice<Mouse>();
            try
            {
                var layer = root.AddComponent<EnvironmentPresentation>(); layer.Configure(material, 38, 3);
                var manager = root.AddComponent<ChunkManager>(); manager.Configure(focus.transform, material, radius: 2, maxPerFrame: 8, milliseconds: 50);
                manager.SetEnvironment(layer);
                var surface = root.AddComponent<WorldSurfaceQuery>(); surface.Configure(manager);
                var reader = rig.AddComponent<RtsCameraInput>(); var controller = rig.AddComponent<RtsCameraController>();
                controller.Configure(camera, reader, surface); controller.enabled = false;
                var bridge = rig.AddComponent<CameraStreamingObserver>(); bridge.Configure(controller, manager, focus.transform); bridge.enabled = false;
                var corners = new Vector3[4];
                void SafePose()
                {
                    camera.CalculateFrustumCorners(new Rect(0, 0, 1, 1), camera.nearClipPlane, UnityEngine.Camera.MonoOrStereoscopicEye.Mono, corners);
                    foreach (var corner in corners)
                        Assert.That(camera.transform.TransformPoint(corner).y, Is.GreaterThanOrEqualTo(surface.MaximumHeight + controller.Settings.TerrainClearance - 0.001));
                    var center = camera.WorldToViewportPoint(focus.transform.position);
                    Assert.That(center.x, Is.EqualTo(0.5).Within(0.0001)); Assert.That(center.y, Is.EqualTo(0.5).Within(0.0001));
                    Assert.That(bridge.CoverageLimited, Is.False);
                }
                IEnumerator Drain()
                {
                    int guard = 0;
                    do { manager.Step(); yield return null; Assert.That(++guard, Is.LessThan(100)); }
                    while (manager.PendingCount != 0);
                    int count = (manager.LoadRadius * 2 + 1) * (manager.LoadRadius * 2 + 1);
                    Assert.That(manager.ActiveCount, Is.EqualTo(count));
                    Assert.That(root.GetComponentsInChildren<MeshFilter>().Length, Is.EqualTo(count)); SafePose();
                }
                void Input(KeyboardState keys, MouseState pointer)
                {
                    InputSystem.QueueStateEvent(keyboard, keys); InputSystem.QueueStateEvent(mouse, pointer); InputSystem.Update();
                    controller.Advance(reader.Read(controller.Settings, true, new Vector2(1000, 600)), 1.0 / 60, 600); bridge.Sync();
                }
                yield return Drain();
                long generated = manager.TotalGenerated;
                for (int i = 0; i < 120; i++)
                {
                    Input(new KeyboardState(Key.W), new MouseState { position = new Vector2(500, 300) });
                    if (i % 20 == 0) SafePose();
                    yield return null;
                }
                yield return Drain();
                var expected = ChunkSelection.FromPosition(controller.Motion.FocusX, controller.Motion.FocusZ, 32);
                Assert.That(manager.Center, Is.EqualTo(expected)); Assert.That(manager.Center, Is.Not.EqualTo(new ChunkCoordinate(0, 0)));
                Assert.That(manager.TotalGenerated, Is.GreaterThan(generated)); Assert.That(manager.TotalUnloaded, Is.GreaterThan(0));
                var start = new Vector2((float)controller.Motion.FocusX, (float)controller.Motion.FocusZ);
                for (int i = 0; i < 12; i++)
                    Input(new KeyboardState(), new MouseState { position = new Vector2(500 + i * 8, 300 + i * 4), delta = new Vector2(8, 4) }.WithButton(MouseButton.Middle));
                Assert.That(Vector2.Distance(start, new Vector2((float)controller.Motion.FocusX, (float)controller.Motion.FocusZ)), Is.GreaterThan(1));
                float zoom = controller.Motion.TargetZoom;
                Input(new KeyboardState(Key.E), new MouseState { position = new Vector2(500, 300), scroll = new Vector2(0, 120) });
                Assert.That(controller.Motion.TargetZoom, Is.LessThan(zoom));
                for (int i = 0; i < 90; i++) { Input(new KeyboardState(), new MouseState { position = new Vector2(500, 300) }); yield return null; }
                yield return Drain(); generated = manager.TotalGenerated;
                var resident = root.GetComponentsInChildren<MeshFilter>().Select(f => f.sharedMesh).ToArray();
                for (int i = 0; i < 10; i++) yield return null;
                Assert.That(manager.TotalGenerated, Is.EqualTo(generated));
                CollectionAssert.AreEquivalent(resident, root.GetComponentsInChildren<MeshFilter>().Select(f => f.sharedMesh).ToArray());
                manager.Seed = 7; layer.Configure(material, 55, 3);
                controller.Advance(default, 1.0 / 60, 600); bridge.Sync(); yield return Drain();
                foreach (var old in resident) Assert.That(old == null, Is.True); SafePose();
                manager.enabled = false; yield return null;
                Assert.That(Resources.FindObjectsOfTypeAll<Mesh>().Count(m => m.name.StartsWith("Stream chunk ")), Is.EqualTo(baseline));
            }
            finally
            {
                InputSystem.RemoveDevice(mouse); InputSystem.RemoveDevice(keyboard);
                Object.Destroy(root); Object.Destroy(focus); Object.Destroy(rig); Object.Destroy(cameraObject); Object.Destroy(material);
            }
        }
    }
}
