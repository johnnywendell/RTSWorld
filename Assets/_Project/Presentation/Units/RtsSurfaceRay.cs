using RTSWorld.Presentation.Cameras;
using UnityEngine;

namespace RTSWorld.Units.Presentation
{
    public static class RtsSurfaceRay
    {
        // Test every terrain triangle boundary, then refine the first surface crossing.
        public static bool TryHit(ICameraSurface surface, Ray ray, float limit, out float distance, out Vector2 logical,
            System.Func<double, double, float> heightAt = null)
        {
            logical = default;
            distance = 0;
            var frame = surface.ReferenceFrame;
            Vector3 origin = frame.InverseTransformPoint(ray.origin);
            Vector3 direction = frame.InverseTransformVector(ray.direction);
            if (direction.y >= -.00001f) return false;

            float start = Mathf.Max(0, (surface.MaximumHeight - origin.y) / direction.y);
            float end = Mathf.Min(limit, (surface.MinimumHeight - origin.y) / direction.y);
            if (start > end) return false;
            float logicalScale = surface.HorizontalScale;
            var localStep = new Vector2(direction.x, direction.z) / logicalScale;
            var localStart = new Vector2(origin.x + direction.x * start, origin.z + direction.z * start) / logicalScale;
            var x = new CellBoundary(localStart.x, localStep.x, start);
            var z = new CellBoundary(localStart.y, localStep.y, start);
            var diagonal = new CellBoundary(localStart.x + localStart.y, localStep.x + localStep.y, start);
            float previous = start;
            float current = start;
            for (;;)
            {
                if (Below(current))
                {
                    for (int i = 0; i < 10; i++)
                    {
                        float middle = (previous + current) * .5f;
                        if (Below(middle)) current = middle;
                        else previous = middle;
                    }

                    distance = current;
                    var hit = origin + direction * distance;
                    logical = new Vector2(hit.x, hit.z) / logicalScale;
                    return true;
                }

                if (current >= end) return false;
                previous = current;
                x.AdvancePast(current);
                z.AdvancePast(current);
                diagonal.AdvancePast(current);
                current = Mathf.Min(end, Mathf.Min(x.Time, Mathf.Min(z.Time, diagonal.Time)));
            }

            bool Below(float t)
            {
                var point = origin + direction * t;
                double x = point.x / logicalScale;
                double z = point.z / logicalScale;
                return point.y <= (heightAt != null ? heightAt(x, z) : surface.HeightAt(x, z));
            }
        }

        private struct CellBoundary
        {
            private readonly float period;
            public float Time { get; private set; }

            public CellBoundary(float coordinate, float velocity, float start)
            {
                if (Mathf.Abs(velocity) < .000001f)
                {
                    period = Time = float.PositiveInfinity;
                    return;
                }

                float boundary = velocity > 0 ? Mathf.Floor(coordinate) + 1 : Mathf.Ceil(coordinate) - 1;
                period = 1 / Mathf.Abs(velocity);
                Time = start + (boundary - coordinate) / velocity;
            }

            public void AdvancePast(float time)
            {
                if (Time <= time) Time += period;
            }
        }
    }
}
