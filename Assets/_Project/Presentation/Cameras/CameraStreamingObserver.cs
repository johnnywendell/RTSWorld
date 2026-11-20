using System;
using RTSWorld.Presentation.Streaming;
using RTSWorld.World.Chunks;
using UnityEngine;

namespace RTSWorld.Presentation.Cameras
{
    /// <summary>Only the visual residency bridge knows the ChunkManager; the motion model does not.</summary>
    [DefaultExecutionOrder(-100)]
    public sealed class CameraStreamingObserver : MonoBehaviour
    {
        [SerializeField] private RtsCameraController controller;
        [SerializeField] private ChunkManager manager;
        [SerializeField] private Transform observer;
        [SerializeField] private bool fitVisibleArea = true;
        [SerializeField, Range(0, 2)] private int extraRadius;
        public bool CoverageLimited { get; private set; }
        public int RequiredRadius { get; private set; }
        public bool HoldUncoveredView
        {
            get => holdUncoveredView;
            set
            {
                if (holdUncoveredView == value) return;
                holdUncoveredView = value;
                if (!value) ResetDisplayGuard();
            }
        }
        public bool WaitingForCoverage { get; private set; }
        public bool LoadingFirstView => WaitingForCoverage && !hasDisplayedView;
        private readonly Plane[] coveragePlanes = new Plane[6];
        private bool hasDisplayedView;
        private Vector3 displayedPosition;
        private Quaternion displayedRotation;
        private float displayedZoom, displayedFar;
        private int displayedResidencyVersion;
        private bool holdUncoveredView, maskSaved;
        private int displayMask;
        public void Configure(RtsCameraController cameraController, ChunkManager stream, Transform target, bool fit = true)
        {
            if (cameraController == null || stream == null || target == null) throw new ArgumentNullException();
            controller = cameraController; manager = stream; observer = target; fitVisibleArea = fit; Sync();
        }
        public void Sync()
        {
            if (controller == null || manager == null || observer == null || controller.Motion == null) return;
            observer.position = controller.FocusWorld;
            if (!fitVisibleArea) return;
            var surface = controller.Surface;
            float pitch = controller.Settings.Inclination * Mathf.Deg2Rad;
            float width = controller.Motion.Zoom * controller.OutputCamera.aspect;
            float height = controller.Motion.Zoom / Mathf.Sin(pitch);
            float elevationSpan = Mathf.Max(Mathf.Abs(surface.MaximumHeight - controller.FocusLocal.y),
                Mathf.Abs(surface.MinimumHeight - controller.FocusLocal.y)) / (surface.HorizontalScale * Mathf.Tan(pitch));
            float yaw = controller.Motion.Yaw * Mathf.Deg2Rad;
            float sin = Mathf.Abs(Mathf.Sin(yaw)), cos = Mathf.Abs(Mathf.Cos(yaw));
            float extent = Mathf.Max(cos * width + sin * (height + elevationSpan), sin * width + cos * (height + elevationSpan));
            RequiredRadius = Mathf.Max(1, Mathf.CeilToInt(extent / manager.CellsPerChunk) + extraRadius);
            CoverageLimited = RequiredRadius > ChunkSelection.MaximumRadius;
            int radius = Mathf.Min(RequiredRadius, ChunkSelection.MaximumRadius);
            if (manager.LoadRadius != radius) manager.LoadRadius = radius;
            if (Application.isPlaying && HoldUncoveredView && controller.enabled)
                GuardView(radius);
        }

        private void GuardView(int radius)
        {
            var camera = controller.OutputCamera;
            var center = ChunkSelection.FromPosition(controller.Motion.FocusX, controller.Motion.FocusZ, manager.CellsPerChunk);
            if (displayedResidencyVersion != manager.ResidencyVersion) hasDisplayedView = false;
            bool covered = manager.IsSelectionReady(center, radius);
            if (!covered) covered = HasViewportCoverage(camera, center, radius);
            WaitingForCoverage = !covered;
            if (LoadingFirstView)
            {
                if (!maskSaved)
                {
                    displayMask = camera.cullingMask;
                    maskSaved = true;
                }
                camera.cullingMask = 0;
                return;
            }
            RestoreMask();
            if (covered)
            {
                hasDisplayedView = true;
                displayedResidencyVersion = manager.ResidencyVersion;
                displayedPosition = camera.transform.position;
                displayedRotation = camera.transform.rotation;
                displayedZoom = camera.orthographicSize;
                displayedFar = camera.farClipPlane;
                manager.RetainDisplayedWindow(center, radius);
            }
            else if (hasDisplayedView)
            {
                camera.transform.SetPositionAndRotation(displayedPosition, displayedRotation);
                camera.orthographicSize = displayedZoom;
                camera.farClipPlane = displayedFar;
            }
        }

        private bool HasViewportCoverage(Camera camera, ChunkCoordinate center, int radius)
        {
            GeometryUtility.CalculateFrustumPlanes(camera, coveragePlanes);
            var surface = controller.Surface;
            float width = manager.CellsPerChunk * manager.HorizontalScale;
            float low = surface.MinimumHeight;
            float high = surface.MaximumHeight;
            var frame = surface.ReferenceFrame;
            // The sphere enclosing the horizontal square covers rotated chunk roots as well.
            float diameter = width * Mathf.Sqrt(2);
            var size = new Vector3(diameter, high - low, diameter) * frame.lossyScale.x;
            for (int z = -radius; z <= radius; z++)
            for (int x = -radius; x <= radius; x++)
            {
                var coordinate = new ChunkCoordinate(center.X + x, center.Z + z);
                if (manager.TryGetMesh(coordinate, out _)) continue;
                var local = new Vector3((coordinate.X + .5f) * width, (low + high) * .5f, (coordinate.Z + .5f) * width);
                if (GeometryUtility.TestPlanesAABB(coveragePlanes, new Bounds(frame.TransformPoint(local), size)))
                    return false;
            }
            return true;
        }

        private void RestoreMask()
        {
            if (!maskSaved) return;
            if (controller != null && controller.OutputCamera != null) controller.OutputCamera.cullingMask = displayMask;
            maskSaved = false;
        }

        private void ResetDisplayGuard()
        {
            RestoreMask();
            if (manager != null) manager.RetainDisplayedWindow(default, -1);
            hasDisplayedView = WaitingForCoverage = false;
        }

        private void OnDisable()
        {
            if (HoldUncoveredView) ResetDisplayGuard();
        }
        private void Update() { if (Application.isPlaying) Sync(); }
    }
}
