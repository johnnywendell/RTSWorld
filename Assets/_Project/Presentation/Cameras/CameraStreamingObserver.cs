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
        }
        private void Update() { if (Application.isPlaying) Sync(); }
    }
}
