using System.Collections.Generic;
using RTSWorld.Simulation;
using RTSWorld.Presentation.Cameras;
using RTSWorld.Presentation.Environment;
using RTSWorld.World.Environment;
using RTSWorld.World.Chunks;
using Unity.Mathematics;
using UnityEngine;

namespace RTSWorld.Units.Presentation
{
    public sealed partial class RtsInteractionDemo
    {
        [SerializeField] private bool visualStyleDemo;
        [SerializeField] private bool environmentArt;
        [SerializeField] private Material visualPaletteMaterial;
        [SerializeField] private Material visualMarkerMaterial;
        [SerializeField] private bool environmentBenchmark;
        private int groundDensity = 2;
        public bool GroundCover { get; set; } = true;
        public int GroundDensity
        {
            get => groundDensity;
            set
            {
                value = Mathf.Clamp(value, 1, 3);
                if (groundDensity == value) return;
                groundDensity = value;
                environmentChunks.Clear();
            }
        }
        public bool EnvironmentArt
        {
            get => environmentArt;
            set
            {
                if (environmentArt == value) return;
                environmentArt = value;
                if (visualStyleDemo && Session != null) SetDemoResourceFilter();
            }
        }
        [SerializeField] private UnityEngine.Rendering.RenderPipelineAsset visualPipeline;
        private UnityEngine.Rendering.RenderPipelineAsset previousPipeline;
        private VisualBackend visualBackend = VisualBackend.Instanced;
        private VisualPrototypeRenderer prototypes;
        private CameraStreamingObserver visualObserver;
        private sealed class EnvironmentChunk
        {
            internal Mesh Mesh;
            internal Matrix4x4[][] Groups;
            internal int Lod = -1;
        }

        private readonly Dictionary<ChunkCoordinate, EnvironmentChunk> environmentChunks = new();
        private readonly List<ChunkCoordinate> retiredEnvironment = new();
        private int visualResourceCount;
        public bool VisualTerrainDiagnostics { get; set; }
        public bool VisualCulling { get; set; } = true;
        public bool VisualLod { get; set; } = true;
        public bool VisualAnimation { get; set; } = true;
        public bool VisualShadows { get; set; } = true;
        public VisualFrameStatistics VisualStatistics => prototypes?.Statistics ?? default;
        public int VisualResourceCount => visualResourceCount;
        public VisualBackend VisualMode
        {
            get => visualBackend;
            set
            {
                if (visualBackend == value) return;
                visualBackend = value;
                if (visualStyleDemo && Session != null) SetDemoResourceFilter();
            }
        }

        private bool Themed => visualStyleDemo && visualBackend != VisualBackend.Legacy;

        private void InitializeVisualStyle()
        {
            if (!visualStyleDemo) return;
            if (visualPipeline != null)
            {
                previousPipeline = QualitySettings.renderPipeline;
                QualitySettings.renderPipeline = visualPipeline;
            }
            prototypes = new VisualPrototypeRenderer(transform, visualPaletteMaterial);
            visualObserver = rig.GetComponent<CameraStreamingObserver>();
            if (visualObserver != null) visualObserver.HoldUncoveredView = true;
        }

        private void SetDemoResourceFilter()
        {
            var layer = streaming.ActiveEnvironment;
            if (layer == null) return;
            layer.SetInstanceTreesAndRocks(Themed);
            layer.SetArtProfile(Themed && environmentArt);
            layer.SetResourceFilter(ResourceVisible);
        }

        private void BeginVisualStyle()
        {
            prototypes?.Begin(View, visualBackend, VisualCulling, VisualLod, VisualAnimation, VisualShadows, Time.unscaledTime);
        }

        private void DrawStyledEnvironment()
        {
            if (!visualStyleDemo) return;
            visualResourceCount = 0;
            retiredEnvironment.Clear();
            foreach (var pair in environmentChunks)
                if (!Themed || !streaming.TryGetResourceInstances(pair.Key, out var mesh, out _) || pair.Value.Mesh != mesh)
                    retiredEnvironment.Add(pair.Key);
            foreach (var coordinate in retiredEnvironment) environmentChunks.Remove(coordinate);
            if (!Themed) return;

            foreach (var coordinate in streaming.ActiveCoordinates)
            {
                if (!streaming.TryGetResourceInstances(coordinate, out var mesh, out var placements))
                    continue;
                visualResourceCount += placements.Length;
                var art = streaming.GetEnvironmentArt(coordinate);
                if (art.IsEmpty && placements.IsEmpty) continue;
                var chunkOrigin = coordinate.GetSampleCoordinate(streaming.CellsPerChunk, 0, 0);
                Vector3 center = WorldPosition(new double3(chunkOrigin.X, 0, chunkOrigin.Z))
                    + streaming.transform.TransformVector(mesh.bounds.center);
                // Allow canopy and trunk height beyond the terrain mesh before rejecting a whole chunk.
                var bounds = new Bounds(center, mesh.bounds.size + new Vector3(10, 18, 10));
                if (!prototypes.Includes(bounds)) continue;

                if (!environmentChunks.TryGetValue(coordinate, out var chunk) || chunk.Mesh != mesh)
                    environmentChunks[coordinate] = chunk = BuildEnvironmentChunk(mesh, placements, art);
                int lod = 0;
                if (VisualLod)
                {
                    chunk.Lod = SelectEnvironmentLod(prototypes.ProjectedHeight(center, 3f * streaming.HorizontalScale / 2), chunk.Lod);
                    lod = chunk.Lod;
                }
                else chunk.Lod = -1;
                for (int kind = 0; kind < chunk.Groups.Length; kind++)
                {
                    var instances = chunk.Groups[kind];
                    if (instances == null) continue;
                    bool ground = VisualPrototypeLibrary.IsGroundCover(kind);
                    if (ground && (!GroundCover || lod != 0)) continue;
                    prototypes.EnvironmentBatch(kind, lod, instances);
                }
            }
        }

        private EnvironmentChunk BuildEnvironmentChunk(Mesh mesh, System.ReadOnlySpan<ResourcePlacement> placements,
            System.ReadOnlySpan<EnvironmentArtPlacement> art)
        {
            var builders = new List<Matrix4x4>[VisualPrototypeLibrary.EnvironmentKinds];
            if (!art.IsEmpty)
            {
                foreach (var placement in art)
                {
                    if (placement.Decorative && EnvironmentArtDistribution.Roll(placement.Cell, 135) > groundDensity / 3f) continue;
                    Add(VisualPrototypeLibrary.ArtKind(placement), placement.Cell, placement.OffsetX, placement.OffsetZ,
                        placement.Elevation, placement.Turn, new Vector3(placement.Width, placement.Height, placement.Width) *
                            (streaming.HorizontalScale * .5f));
                }
            }
            else foreach (var placement in placements)
            {
                var key = placement.Key;
                uint variation = math.hash(new int2((int)key.X, (int)key.Z));
                bool tree = placement.Kind == NaturalResource.Tree;
                int kind = tree ? (int)(variation % 3) : 3;
                double size = .8 + EnvironmentNoise.Hash(key.Seed, key.X, key.Z, 31) * .4;
                float scale = (float)(size * streaming.HorizontalScale * System.Math.Sqrt(2) * (tree ? .13 / .18 : .5 / .79));
                Add(kind, key, placement.OffsetX, placement.OffsetZ, placement.Elevation,
                    variation % 360, Vector3.one * scale);
            }
            var groups = new Matrix4x4[builders.Length][];
            for (int i = 0; i < groups.Length; i++) groups[i] = builders[i]?.ToArray();
            return new EnvironmentChunk { Mesh = mesh, Groups = groups };

            void Add(int kind, ResourceKey key, float x, float z, float elevation, float turn, Vector3 scale)
            {
                var origin = WorldPosition(new double3(key.X + (double)x, elevation, key.Z + (double)z));
                var rotation = streaming.transform.rotation * Quaternion.Euler(0, turn, 0);
                (builders[kind] ??= new List<Matrix4x4>()).Add(
                    Matrix4x4.TRS(origin, rotation, Vector3.Scale(streaming.transform.lossyScale, scale)));
            }
        }

        public static int SelectEnvironmentLod(float pixels, int previous)
        {
            const float near = 56, mid = 26, margin = .12f;
            float nearThreshold = near * (previous < 0 ? 1 : previous == 0 ? 1 - margin : 1 + margin);
            float midThreshold = mid * (previous < 0 ? 1 : previous <= 1 ? 1 - margin : 1 + margin);
            return pixels >= nearThreshold ? 0 : pixels >= midThreshold ? 1 : 2;
        }

        private void StyledUnit(LogicalPose pose, UnitIdentity identity, ulong id, bool walking, Vector3 feet)
        {
            if (prototypes == null) return;
            float4 q = pose.Rotation.value;
            var orientation = streaming.transform.rotation * new Quaternion(q.x, q.y, q.z, q.w);
            prototypes.Unit(new VisualUnitSample(feet, orientation, identity, id, walking));
        }

        public bool TryGetVisualBuildingSupport(BuildingKind kind, int2 origin, out double low, out double high)
            => TrySampleFoundation(origin, BuildingDefinition.Get(kind).Size, Session.TryGetTerrainElevation, out low, out high);

        public delegate bool GroundHeight(double x, double z, out double elevation);

        public static bool TrySampleFoundation(int2 origin, int2 size, GroundHeight ground, out double low, out double high)
        {
            low = double.PositiveInfinity;
            high = double.NegativeInfinity;
            // A triangulated height field reaches its extrema at grid vertices. Include interior samples.
            for (int z = 0; z <= size.y; z++)
            for (int x = 0; x <= size.x; x++)
            {
                if (!ground(origin.x + x, origin.y + z, out double elevation)) return false;
                low = math.min(low, elevation);
                high = math.max(high, elevation);
            }
            return true;
        }

        private bool StyledBuilding(Building building, ref Bounds bounds)
        {
            if (!Themed) return false;
            var size = BuildingDefinition.Get(building.Kind).Size;
            var first = LogicalTerrain.Region(building.Origin.x, building.Origin.y);
            var last = LogicalTerrain.Region(building.Origin.x + size.x - 1, building.Origin.y + size.y - 1);
            for (int z = first.Z; z <= last.Z; z++)
            for (int x = first.X; x <= last.X; x++)
                if (!streaming.TryGetMesh(new ChunkCoordinate(x, z), out _)) return true;

            if (!TryGetVisualBuildingSupport(building.Kind, building.Origin, out double low, out double high)) return true;
            prototypes.RecordResidentBuilding();
            double2 center = building.Origin + (double2)size * .5;
            var bottom = WorldPosition(new double3(center.x, low, center.y));
            var top = WorldPosition(new double3(center.x, high, center.y));
            var rotation = streaming.transform.rotation;
            var footprint = Vector3.Scale(streaming.transform.lossyScale,
                new Vector3(size.x * streaming.HorizontalScale, 1, size.y * streaming.HorizontalScale));
            footprint.y = Vector3.Distance(top, bottom);
            if (footprint.y > 0)
                prototypes.Prop(VisualPrototypeLibrary.Foundation, bottom, rotation, footprint);

            bounds.center = top + Vector3.up * bounds.extents.y;
            if (building.State != ConstructionState.Completed || building.Kind == BuildingKind.Workshop)
                return false;

            int slot = building.Kind == BuildingKind.Depot ? VisualPrototypeLibrary.Depot : VisualPrototypeLibrary.Hall;
            prototypes.Prop(slot, top, rotation, streaming.transform.lossyScale * (streaming.HorizontalScale / 2));
            return true;
        }

        private void DrawVisualTerrainGizmos()
        {
            if (!VisualTerrainDiagnostics || Session == null) return;
            var previous = Gizmos.matrix;
            Gizmos.matrix = streaming.transform.localToWorldMatrix;
            int cells = streaming.CellsPerChunk;
            float width = cells * streaming.HorizontalScale;
            foreach (var coordinate in streaming.ActiveCoordinates)
            {
                if (!streaming.TryGetMesh(coordinate, out var mesh)) continue;
                var bounds = mesh.bounds;
                var center = new Vector3((coordinate.X + .5f) * width, bounds.max.y, (coordinate.Z + .5f) * width);
                int lod = environmentChunks.TryGetValue(coordinate, out var chunk) ? chunk.Lod : -1;
                Gizmos.color = coordinate.Equals(streaming.Center) ? Color.yellow
                    : lod == 0 ? Color.green : lod == 1 ? Color.cyan : Color.gray;
                Gizmos.DrawWireCube(center, new Vector3(width, 0, width));
            }
            Gizmos.matrix = previous;

            var manager = Session.World.EntityManager;
            foreach (var entity in Session.Buildings)
            {
                var building = manager.GetComponentData<Building>(entity);
                if (!TryGetVisualBuildingSupport(building.Kind, building.Origin, out double low, out double high)) continue;
                var size = BuildingDefinition.Get(building.Kind).Size;
                double2 center = building.Origin + (double2)size * .5;
                Gizmos.color = Color.magenta;
                Gizmos.DrawSphere(WorldPosition(new double3(center.x, high, center.y)), .15f);
                for (int z = 0; z <= size.y; z++)
                for (int x = 0; x <= size.x; x++)
                {
                    double sx = building.Origin.x + x, sz = building.Origin.y + z;
                    if (!Session.TryGetTerrainElevation(sx, sz, out double ground)) continue;
                    Gizmos.color = ground == low ? Color.yellow : Color.cyan;
                    Gizmos.DrawLine(WorldPosition(new double3(sx, ground, sz)), WorldPosition(new double3(sx, high, sz)));
                }
            }
        }

        private void DrawVisualControls()
        {
            if (!visualStyleDemo) return;
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Blocos")) VisualMode = VisualBackend.Legacy;
            if (GUILayout.Button("Instâncias")) VisualMode = VisualBackend.Instanced;
            if (GUILayout.Button("MeshRenderer")) VisualMode = VisualBackend.MeshRenderer;
            if (GUILayout.Button("Ocultar unidades")) VisualMode = VisualBackend.Hidden;
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            VisualAnimation = GUILayout.Toggle(VisualAnimation, "Animação");
            VisualLod = GUILayout.Toggle(VisualLod, "LOD");
            VisualCulling = GUILayout.Toggle(VisualCulling, "Culling");
            VisualShadows = GUILayout.Toggle(VisualShadows, "Sombras");
            if (GUILayout.Button("10")) Rebuild(10);
            GUILayout.EndHorizontal();
            GUILayout.BeginHorizontal();
            EnvironmentArt = GUILayout.Toggle(EnvironmentArt, "Ambiente VISUAL-002");
            GroundCover = GUILayout.Toggle(GroundCover, "Rasteira");
            if (environmentBenchmark)
            {
                if (GUILayout.Button("Baixa")) GroundDensity = 1;
                if (GUILayout.Button("Média")) GroundDensity = 2;
                if (GUILayout.Button("Alta")) GroundDensity = 3;
            }
            GUILayout.EndHorizontal();
            if (environmentBenchmark)
            {
                GUILayout.BeginHorizontal();
                if (GUILayout.Button("Vista próxima")) rig.ResetView();
                if (GUILayout.Button("Vista distante"))
                {
                    rig.ResetView();
                    rig.Advance(new RtsCameraInputFrame(Vector2.zero, zoomSteps: -14), 2, View.pixelHeight);
                }
                if (GUILayout.Button("Pan rápido"))
                {
                    rig.Advance(new RtsCameraInputFrame(Vector2.right), 10, View.pixelHeight);
                    rig.Motion.Stop();
                }
                GUILayout.EndHorizontal();
            }
            VisualTerrainDiagnostics = GUILayout.Toggle(VisualTerrainDiagnostics, "Chunks, apoios e LOD");
            var stats = VisualStatistics;
            if (VisualTerrainDiagnostics)
                GUILayout.Label($"Chunks {streaming.ActiveCount} (+{streaming.PendingCount}) · Centro {streaming.Center} · raio {streaming.LoadRadius}" +
                    (visualObserver != null && visualObserver.WaitingForCoverage ? " · aguardando cobertura" : "") +
                    $"\nÁrvores e rochas LOD 0/1/2: {stats.EnvironmentNear}/{stats.EnvironmentMid}/{stats.EnvironmentFar} · câmera {Vector3.Distance(View.transform.position, rig.FocusWorld):F1} m");
            GUILayout.Label($"{VisualMode} · {stats.Visible}/{Session.Count} na câmera · {stats.Animated} animadas · {stats.Shadowed} sombras · {stats.Far} LOD");
        }
    }
}
