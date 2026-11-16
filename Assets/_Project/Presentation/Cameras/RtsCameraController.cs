using System;
using UnityEngine;

namespace RTSWorld.Presentation.Cameras
{
    public readonly struct RtsCameraPose
    {
        public readonly Vector3 FocusLocal, PositionLocal;
        public readonly Quaternion RotationLocal;
        public readonly float OrthographicSize, FarClip;
        private RtsCameraPose(Vector3 focus, Vector3 position, Quaternion rotation, float size, float far)
        { FocusLocal = focus; PositionLocal = position; RotationLocal = rotation; OrthographicSize = size; FarClip = far; }
        public static RtsCameraPose Calculate(RtsCameraMotion motion, RtsCameraSettings settings,
            float horizontal, float focusHeight, float minHeight, float maxHeight, float nearClip, float rootScale = 1)
        {
            settings.Validate();
            float size = motion.Zoom * horizontal;
            float sin = Mathf.Sin(settings.Inclination * Mathf.Deg2Rad), cos = Mathf.Cos(settings.Inclination * Mathf.Deg2Rad);
            // The lowest point of an orthographic near plane is eyeY-size*cos(pitch)-near*sin(pitch).
            // A conservative field bound guarantees clearance even over hills in unloaded chunks.
            float margin = settings.TerrainClearance + Mathf.Max(0.001f, Mathf.Abs(maxHeight) * 0.000001f);
            float distance = Mathf.Max(2 * size, (maxHeight + margin - focusHeight + size * cos) / sin + nearClip / rootScale);
            var focus = new Vector3((float)(motion.FocusX * horizontal), focusHeight, (float)(motion.FocusZ * horizontal));
            var rotation = Quaternion.Euler(settings.Inclination, motion.Yaw, 0);
            // The upper far-plane corner must reach the lowest possible ground even
            // at the minimum pitch; a fixed multiple of height range is insufficient.
            float far = distance + (Mathf.Max(0, focusHeight - minHeight) + size * cos) / sin + margin;
            return new RtsCameraPose(focus, focus + rotation * (Vector3.back * distance), rotation,
                size * rootScale, far * rootScale);
        }
    }

    [DefaultExecutionOrder(-200)]
    public sealed class RtsCameraController : MonoBehaviour
    {
        [SerializeField] private UnityEngine.Camera outputCamera;
        [SerializeField] private RtsCameraInput input;
        [SerializeField] private WorldSurfaceQuery surface;
        [SerializeField] private RtsCameraSettings settings = new RtsCameraSettings();
        [SerializeField] private double initialFocusX, initialFocusZ;
        [SerializeField] private float initialZoom = 24, initialYaw = 45;
        public RtsCameraMotion Motion { get; private set; }
        public RtsCameraSettings Settings => settings;
        public ICameraSurface Surface => surface;
        public UnityEngine.Camera OutputCamera => outputCamera;
        public Vector3 FocusLocal { get; private set; }
        public Vector3 FocusWorld => surface.ReferenceFrame.TransformPoint(FocusLocal);
        public void Configure(UnityEngine.Camera camera, RtsCameraInput reader, WorldSurfaceQuery query,
            RtsCameraSettings configuration = null, double x = 0, double z = 0, float zoom = 24, float yaw = 45)
        {
            if (camera == null || reader == null || query == null) throw new ArgumentNullException("Camera, input and surface are required.");
            outputCamera = camera; input = reader; surface = query; settings = configuration ?? new RtsCameraSettings();
            initialFocusX = x; initialFocusZ = z; initialZoom = zoom; initialYaw = yaw; ResetView();
        }
        public void ResetView()
        { Motion = new RtsCameraMotion(settings, initialFocusX, initialFocusZ, initialZoom, initialYaw); ApplyPose(); }
        private void OnEnable()
        {
            if (outputCamera == null || input == null || surface == null) return;
            if (Motion == null) ResetView(); else { Motion.Stop(); ApplyPose(); }
        }
        private void OnDisable() => Motion?.Stop();
        private void OnValidate()
        {
            if (settings == null) settings = new RtsCameraSettings();
            settings.Inclination = Mathf.Clamp(settings.Inclination, 20, 80);
            settings.MinimumZoom = Mathf.Max(0.1f, settings.MinimumZoom);
            settings.MaximumZoom = Mathf.Max(settings.MinimumZoom, settings.MaximumZoom);
        }
        public void Advance(RtsCameraInputFrame frame, double seconds, float viewportHeight)
        {
            if (Motion == null) ResetView();
            Motion.Step(frame, seconds, viewportHeight); ApplyPose();
        }
        public void ApplyPose()
        {
            if (Motion == null || surface == null || outputCamera == null) return;
            var reference = surface.ReferenceFrame; var scale = reference.lossyScale;
            if (scale.x <= 0 || Mathf.Abs(scale.x - scale.y) > 0.0001f || Mathf.Abs(scale.x - scale.z) > 0.0001f ||
                Vector3.Dot(reference.up, Vector3.up) < 0.9999f)
                throw new InvalidOperationException("Camera reference frame must be upright with uniform positive scale.");
            float ground = surface.HeightAt(Motion.FocusX, Motion.FocusZ);
            var pose = RtsCameraPose.Calculate(Motion, settings, surface.HorizontalScale, ground,
                surface.MinimumHeight, surface.MaximumHeight, outputCamera.nearClipPlane, scale.x);
            FocusLocal = pose.FocusLocal;
            outputCamera.orthographic = true; outputCamera.orthographicSize = pose.OrthographicSize;
            outputCamera.farClipPlane = Mathf.Max(outputCamera.nearClipPlane + 1, pose.FarClip);
            outputCamera.transform.SetPositionAndRotation(reference.TransformPoint(pose.PositionLocal), reference.rotation * pose.RotationLocal);
        }
        private void Update()
        {
            if (!Application.isPlaying || input == null || surface == null || outputCamera == null) return;
            try { Advance(input.Read(settings, Application.isFocused, new Vector2(Screen.width, Screen.height)), Time.unscaledDeltaTime, outputCamera.pixelHeight); }
            catch (Exception e) { Debug.LogException(e, this); enabled = false; }
        }
    }
}
