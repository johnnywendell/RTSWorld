using System;
using NUnit.Framework;
using RTSWorld.Presentation.Cameras;
using RTSWorld.Presentation.Environment;
using RTSWorld.Presentation.Streaming;
using RTSWorld.World.Environment;
using RTSWorld.World.Generation;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using Object = UnityEngine.Object;

namespace RTSWorld.Presentation.Tests
{
    public sealed class RtsCameraTests
    {
        private static RtsCameraMotion Route(int fps)
        {
            var motion = new RtsCameraMotion(new RtsCameraSettings(), yaw: 0);
            for (int i = 0; i < fps * 2; i++) motion.Step(new RtsCameraInputFrame(Vector2.up, zoomSteps: i == 0 ? 1 : 0), 1.0 / fps, 600);
            for (int i = 0; i < fps; i++) motion.Step(default, 1.0 / fps, 600);
            return motion;
        }
        [TestCase(30)] [TestCase(60)] [TestCase(144)]
        public void AcceleratingCoastingAndZoomingAgreeAcrossFrameRates(int fps)
        {
            var expected = Route(240); var actual = Route(fps);
            Assert.That(actual.FocusZ, Is.EqualTo(expected.FocusZ).Within(0.00001));
            Assert.That(actual.FocusX, Is.EqualTo(0).Within(0.00001));
            Assert.That(actual.Zoom, Is.EqualTo(expected.Zoom).Within(0.00001));
            Assert.That(actual.VelocityZ, Is.LessThan(0.001));
        }
        [TestCase(1, 0)] [TestCase(-1, 0)] [TestCase(0, 1)] [TestCase(0, -1)]
        public void MovementUsesTheLogicalGroundPlane(int x, int z)
        {
            var motion = new RtsCameraMotion(new RtsCameraSettings(), yaw: 0);
            motion.Step(new RtsCameraInputFrame(new Vector2(x, z)), 1, 600);
            Assert.That(motion.FocusX, Is.EqualTo(x * 26.25125798).Within(0.0001));
            Assert.That(motion.FocusZ, Is.EqualTo(z * 26.25125798).Within(0.0001));
        }
        [Test]
        public void LongFramesAndDiagonalInputDoNotChangeSpeed()
        {
            var settings = new RtsCameraSettings();
            var longFrame = new RtsCameraMotion(settings, yaw: 0); var many = new RtsCameraMotion(settings, yaw: 0);
            longFrame.Step(new RtsCameraInputFrame(Vector2.right), 3, 600);
            for (int i = 0; i < 180; i++) many.Step(new RtsCameraInputFrame(Vector2.right), 1.0 / 60, 600);
            Assert.That(longFrame.FocusX, Is.EqualTo(many.FocusX).Within(0.00001));
            var diagonal = new RtsCameraMotion(settings, yaw: 0);
            diagonal.Step(new RtsCameraInputFrame(Vector2.one), 3, 600);
            Assert.That(Math.Sqrt(diagonal.FocusX * diagonal.FocusX + diagonal.FocusZ * diagonal.FocusZ), Is.EqualTo(longFrame.FocusX).Within(0.00001));
        }
        [Test]
        public void DragUsesPixelTravelOnceRatherThanMultiplyingItByDeltaTime()
        {
            RtsCameraMotion Drag(int fps)
            {
                var model = new RtsCameraMotion(new RtsCameraSettings(), yaw: 0);
                for (int i = 0; i < fps; i++) model.Step(new RtsCameraInputFrame(Vector2.zero,
                    new Vector2(120f / fps, 60f / fps), dragging: true), 1.0 / fps, 600);
                return model;
            }
            var a = Drag(30); var b = Drag(120);
            Assert.That(a.FocusX, Is.EqualTo(-9.6).Within(0.00001));
            Assert.That(a.FocusX, Is.EqualTo(b.FocusX).Within(0.00001));
            Assert.That(a.FocusZ, Is.EqualTo(b.FocusZ).Within(0.00001));
            Assert.That(a.VelocityX, Is.Zero);
        }
        [Test]
        public void ZoomOrientationAndPrecisionGuardRespectTheirLimits()
        {
            var settings = new RtsCameraSettings { CoordinateLimit = 10 };
            var motion = new RtsCameraMotion(settings, yaw: 359);
            motion.Step(new RtsCameraInputFrame(Vector2.right, zoomSteps: 1000, rotation: 1), 1, 600);
            Assert.That(motion.Zoom, Is.InRange(settings.MinimumZoom, settings.MaximumZoom));
            Assert.That(motion.TargetZoom, Is.EqualTo(settings.MinimumZoom));
            Assert.That(motion.Yaw, Is.EqualTo(89).Within(0.001));
            Assert.That(Math.Abs(motion.FocusX), Is.LessThanOrEqualTo(10));
            Assert.That(Math.Abs(motion.FocusZ), Is.LessThanOrEqualTo(10));
            motion.Step(new RtsCameraInputFrame(Vector2.zero, zoomSteps: -1000), 10, 600);
            Assert.That(motion.Zoom, Is.EqualTo(settings.MaximumZoom).Within(0.00001));
            settings.EnableRotation = false;
            motion.Step(new RtsCameraInputFrame(Vector2.zero, rotation: 1), 1, 600);
            Assert.That(motion.Yaw, Is.EqualTo(89).Within(0.001));
            settings.Inclination = 0; Assert.Throws<ArgumentOutOfRangeException>(() => settings.Validate());
        }
        [TestCase(20, 12)] [TestCase(55, 24)] [TestCase(80, 48)]
        public void FrustumCoversTerrainBoundsAtEveryZoomAndInclination(float pitch, float zoom)
        {
            var settings = new RtsCameraSettings { Inclination = pitch };
            var motion = new RtsCameraMotion(settings, zoom: zoom);
            var pose = RtsCameraPose.Calculate(motion, settings, 2, 120, -3, 121.2f, 0.3f);
            var bottom = pose.PositionLocal + pose.RotationLocal * new Vector3(0, -pose.OrthographicSize, 0.3f);
            Assert.That(bottom.y, Is.GreaterThan(121.2f + settings.TerrainClearance - 0.0001f));
            var farTop = pose.PositionLocal + pose.RotationLocal * new Vector3(0, pose.OrthographicSize, pose.FarClip);
            Assert.That(farTop.y, Is.LessThan(-3), "Far plane must retain low ground behind a high focus at low pitch.");
            var forward = pose.RotationLocal * Vector3.forward;
            Assert.That(Vector3.Cross((pose.FocusLocal - pose.PositionLocal).normalized, forward).magnitude, Is.LessThan(0.00001));
        }
        [Test]
        public void DevicesDriveWasdEdgesMiddleDragScrollAndUiFocusGates()
        {
            var keyboard = InputSystem.AddDevice<Keyboard>(); var mouse = InputSystem.AddDevice<Mouse>();
            var go = new GameObject("RTS input test"); var reader = go.AddComponent<RtsCameraInput>();
            var settings = new RtsCameraSettings(); var viewport = new Vector2(1000, 1000);
            try
            {
                void Devices(KeyboardState keys, MouseState pointer)
                { InputSystem.QueueStateEvent(keyboard, keys); InputSystem.QueueStateEvent(mouse, pointer); InputSystem.Update(); }
                Devices(new KeyboardState(Key.W, Key.D, Key.E), new MouseState { position = new Vector2(500, 500) });
                var frame = reader.Read(settings, true, viewport);
                Assert.That(frame.Pan.x, Is.EqualTo(0.7071067).Within(0.00001));
                Assert.That(frame.Pan.y, Is.EqualTo(frame.Pan.x)); Assert.That(frame.Rotation, Is.EqualTo(1));
                Devices(new KeyboardState(), new MouseState { position = new Vector2(999, 500), scroll = new Vector2(0, 120) });
                frame = reader.Read(settings, true, viewport);
                Assert.That(frame.Pan, Is.EqualTo(Vector2.right)); Assert.That(frame.ZoomSteps, Is.EqualTo(1));
                settings.EnableEdgePan = false; Assert.That(reader.Read(settings, true, viewport).Pan, Is.EqualTo(Vector2.zero));
                Devices(new KeyboardState(), new MouseState { position = new Vector2(500, 500), delta = new Vector2(20, 10) }.WithButton(MouseButton.Middle));
                Assert.That(reader.Read(settings, true, viewport).DragPixels, Is.EqualTo(Vector2.zero));
                Devices(new KeyboardState(), new MouseState { position = new Vector2(520, 510), delta = new Vector2(20, 10) }.WithButton(MouseButton.Middle));
                frame = reader.Read(settings, true, viewport); Assert.That(frame.Dragging, Is.True); Assert.That(frame.DragPixels, Is.EqualTo(new Vector2(20, 10)));
                reader.PointerBlocked = true; Assert.That(reader.Read(settings, true, viewport).Dragging, Is.False);
                reader.PointerBlocked = false; reader.ExcludedGuiRect = new Rect(0, 0, 200, 200); settings.EnableEdgePan = true;
                Devices(new KeyboardState(), new MouseState { position = new Vector2(5, 995), scroll = new Vector2(0, 120) });
                frame = reader.Read(settings, true, viewport); Assert.That(frame.Pan, Is.EqualTo(Vector2.zero)); Assert.That(frame.ZoomSteps, Is.Zero);
                Devices(new KeyboardState(Key.W), new MouseState { position = new Vector2(500, 500) });
                reader.KeyboardBlocked = true; Assert.That(reader.Read(settings, true, viewport).Pan, Is.EqualTo(Vector2.zero));
                reader.KeyboardBlocked = false; Assert.That(reader.Read(settings, false, viewport).Pan, Is.EqualTo(Vector2.zero));
                reader.InputEnabled = false; Assert.That(reader.Read(settings, true, viewport).Pan, Is.EqualTo(Vector2.zero));
            }
            finally { Object.DestroyImmediate(go); InputSystem.RemoveDevice(mouse); InputSystem.RemoveDevice(keyboard); }
        }
        [Test]
        public void TranslatedRotatedUniformFrameKeepsFocusAndNearPlaneCorrect()
        {
            var root = new GameObject("Frame test"); var focus = new GameObject("Focus");
            var cameraObject = new GameObject("Camera"); var rig = new GameObject("Rig");
            var material = new Material(Shader.Find("RTSWorld/Environment Colors"));
            try
            {
                root.transform.SetPositionAndRotation(new Vector3(80, 10, -40), Quaternion.Euler(0, 90, 0));
                root.transform.localScale = Vector3.one * 2;
                var manager = root.AddComponent<ChunkManager>(); manager.Configure(focus.transform, material);
                var layer = root.AddComponent<EnvironmentPresentation>(); layer.Configure(material); manager.SetEnvironment(layer);
                var surface = root.AddComponent<WorldSurfaceQuery>(); surface.Configure(manager);
                var camera = cameraObject.AddComponent<UnityEngine.Camera>(); camera.aspect = 1.6f;
                var reader = rig.AddComponent<RtsCameraInput>(); var controller = rig.AddComponent<RtsCameraController>();
                controller.Configure(camera, reader, surface, x: 35, z: -33);
                var bridge = rig.AddComponent<CameraStreamingObserver>(); bridge.Configure(controller, manager, focus.transform);
                manager.Step(); Assert.That(manager.Center, Is.EqualTo(new RTSWorld.World.Chunks.ChunkCoordinate(1, -2)));
                var center = camera.WorldToViewportPoint(focus.transform.position);
                Assert.That(center.x, Is.EqualTo(0.5).Within(0.0001)); Assert.That(center.y, Is.EqualTo(0.5).Within(0.0001));
                var corners = new Vector3[4];
                camera.CalculateFrustumCorners(new Rect(0, 0, 1, 1), camera.nearClipPlane, UnityEngine.Camera.MonoOrStereoscopicEye.Mono, corners);
                foreach (var corner in corners)
                    Assert.That(camera.transform.TransformPoint(corner).y, Is.GreaterThan(10 + (surface.MaximumHeight + controller.Settings.TerrainClearance) * 2 - 0.001));
                root.transform.localScale = new Vector3(1, 2, 1);
                Assert.Throws<InvalidOperationException>(() => controller.ApplyPose());
            }
            finally { Object.DestroyImmediate(rig); Object.DestroyImmediate(root); Object.DestroyImmediate(focus); Object.DestroyImmediate(cameraObject); Object.DestroyImmediate(material); }
        }

        [TestCase(1)] [TestCase(2)] [TestCase(3)] [TestCase(4)]
        public void SurfaceQueryMatchesTheRenderedTriangleWithoutLoadingChunks(int version)
        {
            var root = new GameObject("Query test"); var observer = new GameObject("Observer");
            var material = new Material(Shader.Find("RTSWorld/Environment Colors"));
            try
            {
                var manager = root.AddComponent<ChunkManager>(); manager.Configure(observer.transform, material);
                var layer = root.AddComponent<EnvironmentPresentation>(); layer.Configure(material, 38, version); manager.SetEnvironment(layer);
                var query = root.AddComponent<WorldSurfaceQuery>(); query.Configure(manager);
                var gen = new EnvironmentGenerator(new WorldDefinition(12345), new EnvironmentDefinition(38, version));
                var a = gen.Sample(160, -73); var b = gen.Sample(161, -73); var c = gen.Sample(160, -72);
                double ground = a.Elevation + (b.Elevation - a.Elevation) * 0.2 + (c.Elevation - a.Elevation) * 0.3;
                double water = a.WaterLevel + (b.WaterLevel - a.WaterLevel) * 0.2 + (c.WaterLevel - a.WaterLevel) * 0.3;
                Assert.That(query.HeightAt(160.2, -72.7), Is.EqualTo(Math.Max(ground, water) * 1.2).Within(0.0001));
                Assert.That(manager.ActiveCount, Is.Zero);
                layer.Configure(material, 100, version);
                Assert.That(query.HeightAt(160.2, -72.7), Is.EqualTo(120).Within(0.0001));
            }
            finally { Object.DestroyImmediate(root); Object.DestroyImmediate(observer); Object.DestroyImmediate(material); }
        }
    }
}
