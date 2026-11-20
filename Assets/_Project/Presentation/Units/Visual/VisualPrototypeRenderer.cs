using System;
using RTSWorld.Simulation;
using UnityEngine;
using UnityEngine.Rendering;

namespace RTSWorld.Units.Presentation
{
    internal readonly struct VisualUnitSample
    {
        internal readonly Vector3 Feet;
        internal readonly Quaternion Facing;
        internal readonly VisualArchetype Kind;
        internal readonly int Owner;
        internal readonly ulong Id;
        internal readonly bool Walking;

        internal VisualUnitSample(Vector3 feet, Quaternion facing, UnitIdentity identity, ulong id, bool walking)
        {
            Feet = feet;
            Facing = facing;
            Kind = identity.Kind == UnitKind.Soldier ? VisualArchetype.Soldier : VisualArchetype.Villager;
            Owner = identity.Owner;
            Id = id;
            Walking = walking;
        }
    }

    public struct VisualFrameStatistics
    {
        public int Visible, Animated, Walking, Shadowed, Far, Submissions, Vertices, Triangles, ActiveObjects;
        public int EnvironmentNear, EnvironmentMid, EnvironmentFar, GroundInstances;
        public int ResidentBuildings;
        public double CullMilliseconds, LodMilliseconds, PoseMilliseconds, TransformMilliseconds, SubmitMilliseconds;
    }

    internal sealed class VisualPrototypeRenderer : IDisposable
    {
        private const int BatchSize = 511;
        private readonly Transform parent;
        private readonly VisualPrototypeLibrary library;
        private readonly Plane[] planes = new Plane[6];
        private readonly Matrix4x4[][] near = CreateBuckets();
        private readonly Matrix4x4[][] unshadowed = CreateBuckets();
        private readonly int[,] counts = new int[2, VisualPrototypeLibrary.Foundation + 1];
        private readonly System.Collections.Generic.List<(MeshFilter Filter, MeshRenderer Renderer)> objects = new();
        private VisualFrameStatistics stats;
        internal VisualFrameStatistics Statistics => stats;
        private VisualBackend backend;
        private Camera view;
        private bool culling, lod, animation, shadows;
        private float time;

        internal VisualPrototypeRenderer(Transform parent, Material material)
        {
            this.parent = parent;
            library = new VisualPrototypeLibrary(material);
        }

        private static Matrix4x4[][] CreateBuckets() => new Matrix4x4[VisualPrototypeLibrary.Foundation + 1][];

        internal void Begin(Camera camera, VisualBackend mode, bool culling, bool lod, bool animation, bool shadows, float time)
        {
            stats = default;
            view = camera;
            backend = mode;
            this.culling = culling;
            this.lod = lod;
            this.animation = animation;
            this.shadows = shadows;
            this.time = time;
            GeometryUtility.CalculateFrustumPlanes(camera, planes);
        }

        internal void Unit(in VisualUnitSample sample)
        {
            long stamp = System.Diagnostics.Stopwatch.GetTimestamp();
            var bounds = new Bounds(sample.Feet + Vector3.up * .89f, new Vector3(1.2f, 2.1f, 1.2f));
            bool visible = GeometryUtility.TestPlanesAABB(planes, bounds);
            stats.CullMilliseconds += Elapsed(stamp);
            if (visible) stats.Visible++;
            if (backend is VisualBackend.Legacy or VisualBackend.Hidden || (culling && !visible)) return;

            stamp = System.Diagnostics.Stopwatch.GetTimestamp();
            bool far = lod && ProjectedHeight(sample.Feet, 1.65f) < 22;
            stats.LodMilliseconds += Elapsed(stamp);
            stamp = System.Diagnostics.Stopwatch.GetTimestamp();
            int phase = 0;
            if (far) { phase = VisualPrototypeLibrary.Poses; stats.Far++; }
            else if (animation)
            {
                phase = sample.Walking
                    ? 2 + Mathf.FloorToInt(Mathf.Repeat(time * 7.5f + (sample.Id % 9) * .6f, 4))
                    : Mathf.FloorToInt(Mathf.Repeat(time * 1.28f + (sample.Id % 5) * .3f, 2));
                stats.Animated++;
                if (sample.Walking) stats.Walking++;
            }
            int slot = VisualPrototypeLibrary.UnitMesh(sample.Kind, sample.Owner, phase);
            bool cast = shadows && !far;
            if (cast) stats.Shadowed++;
            stats.PoseMilliseconds += Elapsed(stamp);
            Submit(slot, sample.Feet, sample.Facing, Vector3.one, cast, backend == VisualBackend.MeshRenderer);
        }

        internal float ProjectedHeight(Vector3 feet, float height)
        {
            float halfHeight = view.orthographicSize;
            if (!view.orthographic)
            {
                float depth = Vector3.Dot(feet - view.transform.position, view.transform.forward);
                halfHeight = Mathf.Max(1, depth) * Mathf.Tan(view.fieldOfView * Mathf.Deg2Rad * .5f);
            }
            return view.pixelHeight * height / (2 * halfHeight);
        }

        internal bool Includes(Bounds bounds) => !culling || GeometryUtility.TestPlanesAABB(planes, bounds);
        internal void RecordResidentBuilding() => stats.ResidentBuildings++;

        internal void EnvironmentBatch(int kind, int lod, Matrix4x4[] matrices)
        {
            bool ground = VisualPrototypeLibrary.IsGroundCover(kind);
            if (ground) stats.GroundInstances += matrices.Length;
            if (lod == 0) stats.EnvironmentNear += matrices.Length;
            else if (lod == 1) stats.EnvironmentMid += matrices.Length;
            else stats.EnvironmentFar += matrices.Length;
            int slot = VisualPrototypeLibrary.EnvironmentMesh(kind, lod);
            int group = shadows && lod == 0 && !ground ? 0 : 1;
            var buffer = (group == 0 ? near : unshadowed)[slot] ??= new Matrix4x4[BatchSize];
            var mesh = library.Meshes[slot];
            stats.Vertices += mesh.vertexCount * matrices.Length;
            stats.Triangles += (int)mesh.GetIndexCount(0) / 3 * matrices.Length;
            long start = System.Diagnostics.Stopwatch.GetTimestamp();
            for (int offset = 0; offset < matrices.Length;)
            {
                int count = Math.Min(matrices.Length - offset, BatchSize - counts[group, slot]);
                Array.Copy(matrices, offset, buffer, counts[group, slot], count);
                offset += count;
                counts[group, slot] += count;
                if (counts[group, slot] == BatchSize) Flush(group, slot);
            }
            stats.SubmitMilliseconds += Elapsed(start);
        }

        internal void Prop(int slot, Vector3 origin, Quaternion rotation, Vector3 scale)
        {
            if (backend == VisualBackend.Legacy) return;
            var bounds = new Bounds(origin + Vector3.up * (scale.y * 1.4f), scale * 12);
            if (culling && !GeometryUtility.TestPlanesAABB(planes, bounds)) return;
            Submit(slot, origin, rotation, scale, shadows, false);
        }

        private void Submit(int slot, Vector3 origin, Quaternion rotation, Vector3 scale, bool cast, bool objectBackend)
        {
            long stamp = System.Diagnostics.Stopwatch.GetTimestamp();
            var mesh = library.Meshes[slot];
            stats.Vertices += mesh.vertexCount;
            stats.Triangles += (int)mesh.GetIndexCount(0) / 3;
            if (objectBackend)
            {
                int index = stats.ActiveObjects++;
                if (index == objects.Count)
                {
                    var instance = new GameObject("Protótipo visual", typeof(MeshFilter), typeof(MeshRenderer));
                    instance.transform.SetParent(parent, false);
                    var meshRenderer = instance.GetComponent<MeshRenderer>();
                    meshRenderer.sharedMaterial = library.Material;
                    meshRenderer.lightProbeUsage = LightProbeUsage.Off;
                    objects.Add((instance.GetComponent<MeshFilter>(), meshRenderer));
                }
                var (filter, renderer) = objects[index];
                filter.sharedMesh = mesh;
                long transformStart = System.Diagnostics.Stopwatch.GetTimestamp();
                filter.transform.SetPositionAndRotation(origin, rotation);
                stats.TransformMilliseconds += Elapsed(transformStart);
                renderer.shadowCastingMode = cast ? ShadowCastingMode.On : ShadowCastingMode.Off;
                if (!filter.gameObject.activeSelf) filter.gameObject.SetActive(true);
            }
            else
            {
                int group = cast ? 0 : 1;
                var matrices = cast ? near : unshadowed;
                long transformStart = System.Diagnostics.Stopwatch.GetTimestamp();
                var buffer = matrices[slot] ??= new Matrix4x4[BatchSize];
                buffer[counts[group, slot]++] = Matrix4x4.TRS(origin, rotation, scale);
                stats.TransformMilliseconds += Elapsed(transformStart);
                if (counts[group, slot] == BatchSize) Flush(group, slot);
            }
            stats.SubmitMilliseconds += Elapsed(stamp);
        }

        internal void End()
        {
            long stamp = System.Diagnostics.Stopwatch.GetTimestamp();
            for (int i = stats.ActiveObjects; i < objects.Count; i++)
            {
                var instance = objects[i].Filter.gameObject;
                if (!instance.activeSelf) break;
                instance.SetActive(false);
            }
            for (int group = 0; group < 2; group++)
            for (int slot = 0; slot < library.Meshes.Length; slot++)
                if (counts[group, slot] != 0) Flush(group, slot);
            stats.SubmitMilliseconds += Elapsed(stamp);
        }

        private void Flush(int group, int slot)
        {
            var parameters = new RenderParams(library.Material)
            {
                camera = view,
                shadowCastingMode = group == 0 ? ShadowCastingMode.On : ShadowCastingMode.Off,
                lightProbeUsage = LightProbeUsage.Off,
                receiveShadows = true
            };
            Graphics.RenderMeshInstanced(parameters, library.Meshes[slot], 0,
                (group == 0 ? near : unshadowed)[slot], counts[group, slot]);
            stats.Submissions++;
            counts[group, slot] = 0;
        }

        private static double Elapsed(long start) => (System.Diagnostics.Stopwatch.GetTimestamp() - start) *
            (1000.0 / System.Diagnostics.Stopwatch.Frequency);

        public void Dispose()
        {
            foreach (var pair in objects) UnityEngine.Object.Destroy(pair.Filter.gameObject);
            objects.Clear();
            library.Dispose();
        }
    }
}
