using System;
using UnityEngine;

namespace RTSWorld.Presentation.Cameras
{
    [Serializable]
    public sealed class RtsCameraSettings
    {
        [Min(0.1f)] public float MovementSpeed = 30;
        [Tooltip("Resposta da velocidade por segundo; maior acelera mais rápido."), Min(0.1f)] public float Acceleration = 8;
        [Tooltip("Resposta ao soltar a entrada, por segundo."), Min(0.1f)] public float Deceleration = 12;
        [Min(0.01f)] public float MouseSensitivity = 1;
        [Min(0.01f)] public float ZoomSpeed = 4;
        [Min(0.1f)] public float MinimumZoom = 12;
        [Min(0.1f)] public float MaximumZoom = 48;
        [Min(0.1f)] public float Smoothing = 10;
        [Range(20, 80)] public float Inclination = 55;
        public bool EnableRotation = true;
        [Min(0.1f)] public float RotationSpeed = 90;
        public bool EnableEdgePan = true;
        [Range(1, 100)] public float EdgePixels = 12;
        [Tooltip("Guarda da apresentação; não limita o mundo autoritativo. Sem floating origin."), Min(1)]
        public float CoordinateLimit = 32768;
        [Min(0.1f)] public float TerrainClearance = 3;

        public void Validate()
        {
            void Positive(float value, string name)
            { if (float.IsNaN(value) || float.IsInfinity(value) || value <= 0) throw new ArgumentOutOfRangeException(name); }
            Positive(MovementSpeed, nameof(MovementSpeed)); Positive(Acceleration, nameof(Acceleration));
            Positive(Deceleration, nameof(Deceleration)); Positive(MouseSensitivity, nameof(MouseSensitivity));
            Positive(ZoomSpeed, nameof(ZoomSpeed)); Positive(MinimumZoom, nameof(MinimumZoom));
            Positive(MaximumZoom, nameof(MaximumZoom)); Positive(Smoothing, nameof(Smoothing));
            Positive(Inclination, nameof(Inclination)); Positive(RotationSpeed, nameof(RotationSpeed));
            Positive(EdgePixels, nameof(EdgePixels)); Positive(CoordinateLimit, nameof(CoordinateLimit));
            Positive(TerrainClearance, nameof(TerrainClearance));
            if (MaximumZoom < MinimumZoom || Inclination < 20 || Inclination > 80)
                throw new ArgumentException("Zoom limits or inclination are invalid.");
        }
    }

    public readonly struct RtsCameraInputFrame
    {
        public readonly Vector2 Pan, DragPixels;
        public readonly float ZoomSteps, Rotation;
        public readonly bool Dragging;
        public RtsCameraInputFrame(Vector2 pan, Vector2 dragPixels = default, float zoomSteps = 0, float rotation = 0, bool dragging = false)
        { Pan = pan; DragPixels = dragPixels; ZoomSteps = zoomSteps; Rotation = rotation; Dragging = dragging; }
    }

    /// <summary>Presentation motion in logical X/Z. No devices, scene objects or generation.</summary>
    public sealed class RtsCameraMotion
    {
        private readonly RtsCameraSettings settings;
        private double zoom, targetZoom, yaw;
        public double FocusX { get; private set; }
        public double FocusZ { get; private set; }
        public double VelocityX { get; private set; }
        public double VelocityZ { get; private set; }
        public float Zoom => (float)zoom;
        public float TargetZoom => (float)targetZoom;
        public float Yaw => (float)yaw;
        public RtsCameraMotion(RtsCameraSettings settings, double x = 0, double z = 0, float zoom = 24, float yaw = 45)
        {
            this.settings = settings ?? throw new ArgumentNullException(nameof(settings)); settings.Validate();
            if (double.IsNaN(x) || double.IsInfinity(x) || double.IsNaN(z) || double.IsInfinity(z) ||
                float.IsNaN(zoom) || float.IsInfinity(zoom) || float.IsNaN(yaw) || float.IsInfinity(yaw)) throw new ArgumentException("Nonfinite pose.");
            FocusX = Clamp(x, -settings.CoordinateLimit, settings.CoordinateLimit);
            FocusZ = Clamp(z, -settings.CoordinateLimit, settings.CoordinateLimit);
            this.zoom = targetZoom = Clamp(zoom, settings.MinimumZoom, settings.MaximumZoom);
            this.yaw = (yaw % 360 + 360) % 360;
        }
        private static double Clamp(double value, double min, double max) => Math.Max(min, Math.Min(max, value));
        public void Stop() { VelocityX = 0; VelocityZ = 0; }
        public void Step(RtsCameraInputFrame input, double seconds, float viewportHeight)
        {
            settings.Validate();
            if (double.IsNaN(seconds) || double.IsInfinity(seconds) || seconds < 0) throw new ArgumentOutOfRangeException(nameof(seconds));
            if (seconds == 0) return;
            targetZoom = Clamp(targetZoom - input.ZoomSteps * settings.ZoomSpeed, settings.MinimumZoom, settings.MaximumZoom);
            zoom = Clamp(zoom + (targetZoom - zoom) * (1 - Math.Exp(-settings.Smoothing * seconds)), settings.MinimumZoom, settings.MaximumZoom);
            double rotation = settings.EnableRotation ? input.Rotation * settings.RotationSpeed * seconds : 0;
            double heading = (yaw + rotation * 0.5) * Math.PI / 180;
            double sin = Math.Sin(heading), cos = Math.Cos(heading);
            yaw = ((yaw + rotation) % 360 + 360) % 360;
            double dx, dz;
            if (input.Dragging)
            {
                Stop();
                double pixel = 2 * zoom / Math.Max(1, viewportHeight) * settings.MouseSensitivity;
                double forward = input.DragPixels.y / Math.Sin(settings.Inclination * Math.PI / 180);
                dx = -pixel * (cos * input.DragPixels.x + sin * forward);
                dz = -pixel * (-sin * input.DragPixels.x + cos * forward);
            }
            else
            {
                var pan = Vector2.ClampMagnitude(input.Pan, 1);
                double tx = (cos * pan.x + sin * pan.y) * settings.MovementSpeed;
                double tz = (-sin * pan.x + cos * pan.y) * settings.MovementSpeed;
                double rate = pan.sqrMagnitude > 0 ? settings.Acceleration : settings.Deceleration;
                double decay = Math.Exp(-rate * seconds), integral = (1 - decay) / rate;
                // Exact integration of dv/dt = rate*(target-v) for a constant command.
                dx = tx * seconds + (VelocityX - tx) * integral;
                dz = tz * seconds + (VelocityZ - tz) * integral;
                VelocityX = tx + (VelocityX - tx) * decay; VelocityZ = tz + (VelocityZ - tz) * decay;
                if (pan.sqrMagnitude == 0 && Math.Abs(VelocityX) + Math.Abs(VelocityZ) < 1e-8) Stop();
            }
            double limit = settings.CoordinateLimit;
            FocusX = Clamp(FocusX + dx, -limit, limit); FocusZ = Clamp(FocusZ + dz, -limit, limit);
            if (Math.Abs(FocusX) == limit) VelocityX = 0;
            if (Math.Abs(FocusZ) == limit) VelocityZ = 0;
        }
    }
}
