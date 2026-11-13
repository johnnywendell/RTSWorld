using System;
using System.Collections.Generic;
using System.Diagnostics;
using RTSWorld.Presentation.Terrain;
using RTSWorld.World.Chunks;
using RTSWorld.World.Generation;
using UnityEngine;
using RTSWorld.Presentation.Environment;
using Object = UnityEngine.Object;

namespace RTSWorld.Presentation.Streaming
{
    /// <summary>Local visual residency only. No simulation state or cache of unloaded chunks.</summary>
    [ExecuteAlways]
    public sealed class ChunkManager : MonoBehaviour
    {
        [SerializeField] private Transform observer;
        [SerializeField] private Material terrainMaterial;
        [SerializeField] private EnvironmentPresentation environment;
        [SerializeField] private int seed = 12345;
        [SerializeField, Range(1, 256)] private int cellsPerChunk = 32;
        [SerializeField, Range(0.001f, 10000f)] private float horizontalScale = 2;
        [SerializeField, Range(0.001f, 10000f)] private float verticalScale = 1.2f;
        [SerializeField, Range(0, ChunkSelection.MaximumRadius)] private int loadRadius = 1;
        [SerializeField, Range(1, 8)] private int maxChunksPerFrame = 1;
        [SerializeField, Range(0.1f, 50)] private float generationBudgetMilliseconds = 4;

        private sealed class View
        {
            public GameObject Object;
            public Mesh Mesh;
        }

        private readonly Dictionary<ChunkCoordinate, View> active = new Dictionary<ChunkCoordinate, View>();
        private readonly Queue<ChunkCoordinate> pending = new Queue<ChunkCoordinate>();
        private readonly HashSet<ChunkCoordinate> desired = new HashSet<ChunkCoordinate>();
        private readonly List<ChunkCoordinate> ordered = new List<ChunkCoordinate>();
        private readonly List<ChunkCoordinate> removal = new List<ChunkCoordinate>();
        private readonly TerrainGenerator generator = new TerrainGenerator();
        private WorldDefinition definition;
        private float appliedHorizontal, appliedVertical;
        private Material appliedMaterial;
        private int selectedRadius = -1;
        private EnvironmentPresentation appliedEnvironment;
        private int appliedEnvironmentRevision;
        public float HorizontalScale => horizontalScale;
        public void SetEnvironment(EnvironmentPresentation layer) => environment = layer;

        public int Seed { get => seed; set => seed = value; }
        public int LoadRadius { get => loadRadius; set { ChunkSelection.ValidateRadius(value); loadRadius = value; } }
        public int ActiveCount => active.Count;
        public int PendingCount => pending.Count;
        public ChunkCoordinate Center { get; private set; }
        public long TotalGenerated { get; private set; }
        public long TotalUnloaded { get; private set; }
        public int LastGeneratedCount { get; private set; }
        public double LastStepMilliseconds { get; private set; }
        public double PeakStepMilliseconds { get; private set; }
        public IEnumerable<ChunkCoordinate> ActiveCoordinates => active.Keys;

        public bool TryGetMesh(ChunkCoordinate coordinate, out Mesh mesh)
        {
            bool found = active.TryGetValue(coordinate, out var view);
            mesh = found ? view.Mesh : null;
            return found;
        }

        public void Configure(Transform target, Material material, int worldSeed = 12345, int cells = 32,
            float horizontal = 2, float vertical = 1.2f, int radius = 1, int maxPerFrame = 1, float milliseconds = 4)
        {
            Validate(cells, horizontal, vertical, radius, maxPerFrame, milliseconds);
            if (target == null || material == null) throw new ArgumentNullException("Observer and material are required.");
            observer = target; terrainMaterial = material; seed = worldSeed; cellsPerChunk = cells;
            horizontalScale = horizontal; verticalScale = vertical; loadRadius = radius;
            maxChunksPerFrame = maxPerFrame; generationBudgetMilliseconds = milliseconds;
        }

        private static void Validate(int cells, float horizontal, float vertical, int radius, int max, float ms)
        {
            if (cells < 1 || cells > 256) throw new ArgumentOutOfRangeException(nameof(cells));
            TerrainGeometry.ValidateScale(horizontal, nameof(horizontal));
            TerrainGeometry.ValidateScale(vertical, nameof(vertical));
            ChunkSelection.ValidateRadius(radius);
            if (max < 1 || max > 8) throw new ArgumentOutOfRangeException(nameof(max));
            if (float.IsNaN(ms) || float.IsInfinity(ms) || ms < 0.1f || ms > 50)
                throw new ArgumentOutOfRangeException(nameof(ms));
        }

        private void OnEnable()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.update += EditorTick;
#endif
        }

#if UNITY_EDITOR
        private void EditorTick()
        {
            if (this == null || !isActiveAndEnabled || UnityEditor.EditorApplication.isPlayingOrWillChangePlaymode) return;
            Advance();
            if (LastGeneratedCount > 0) UnityEditor.SceneView.RepaintAll();
        }
#endif

        private void Update()
        {
            if (Application.isPlaying) Advance();
        }

        private void Advance()
        {
            if (observer == null || terrainMaterial == null) { Clear(); return; }
            try { Step(); }
            catch (Exception exception)
            {
                UnityEngine.Debug.LogException(exception, this);
                enabled = false; // Do not retry an invalid inspector configuration every frame.
            }
        }

        private void OnDisable()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.update -= EditorTick;
#endif
            Clear();
        }

        /// <summary>One scheduling tick. Also callable explicitly for deterministic tests/tools.</summary>
        public void Step()
        {
            Validate(cellsPerChunk, horizontalScale, verticalScale, loadRadius, maxChunksPerFrame, generationBudgetMilliseconds);
            if (observer == null || terrainMaterial == null) throw new InvalidOperationException("Assign observer and material.");
            long started = Stopwatch.GetTimestamp();
            LastGeneratedCount = 0;
            Vector3 local = transform.InverseTransformPoint(observer.position);
            var center = ChunkSelection.FromPosition((double)local.x / horizontalScale,
                (double)local.z / horizontalScale, cellsPerChunk);
            var layer = environment != null && environment.isActiveAndEnabled ? environment : null;
            if (layer != null) layer.Validate();
            if (definition == null || definition.Seed != seed || definition.CellsPerChunk != cellsPerChunk ||
                appliedHorizontal != horizontalScale || appliedVertical != verticalScale || appliedMaterial != terrainMaterial ||
                appliedEnvironment != layer || (layer != null && appliedEnvironmentRevision != layer.Revision))
            {
                Clear();
                definition = new WorldDefinition(seed, cellsPerChunk: cellsPerChunk);
                appliedHorizontal = horizontalScale; appliedVertical = verticalScale; appliedMaterial = terrainMaterial;
                appliedEnvironment = layer; appliedEnvironmentRevision = layer != null ? layer.Revision : 0;
            }
            if (selectedRadius != loadRadius || !Center.Equals(center)) Select(center);

            // A chunk is atomic. Always allow one, then stop at either count or elapsed budget.
            while (pending.Count > 0 && LastGeneratedCount < maxChunksPerFrame)
            {
                if (LastGeneratedCount > 0 && Elapsed(started) >= generationBudgetMilliseconds) break;
                var coordinate = pending.Peek();
                Create(coordinate);
                pending.Dequeue();
                LastGeneratedCount++;
                TotalGenerated++;
            }
            LastStepMilliseconds = Elapsed(started);
            PeakStepMilliseconds = Math.Max(PeakStepMilliseconds, LastStepMilliseconds);
        }

        private static double Elapsed(long started) =>
            (Stopwatch.GetTimestamp() - started) * 1000.0 / Stopwatch.Frequency;

        private void Select(ChunkCoordinate center)
        {
            Center = center;
            selectedRadius = loadRadius;
            ChunkSelection.Fill(center, loadRadius, ordered);
            desired.Clear();
            foreach (var coordinate in ordered) desired.Add(coordinate);
            removal.Clear();
            foreach (var pair in active) if (!desired.Contains(pair.Key)) removal.Add(pair.Key);
            foreach (var coordinate in removal)
            {
                ReleaseView(active[coordinate]);
                active.Remove(coordinate);
                TotalUnloaded++;
            }
            removal.Clear();
            pending.Clear(); // Discard obsolete requests on teleport/radius changes, not a growing history.
            foreach (var coordinate in ordered) if (!active.ContainsKey(coordinate)) pending.Enqueue(coordinate);
        }

        private void Create(ChunkCoordinate coordinate)
        {
            // Geometry and immutable source data are temporary; only native visual resources stay resident.
            var data = generator.Generate(definition, coordinate);
            var mesh = appliedEnvironment != null ? appliedEnvironment.Build(data, horizontalScale, verticalScale) :
                TerrainMeshFactory.Create(TerrainGeometry.Build(data, horizontalScale, verticalScale));
            mesh.name = $"Stream chunk {coordinate}";
            mesh.hideFlags = HideFlags.DontSave;
            GameObject chunk = null;
            try
            {
                chunk = new GameObject($"Chunk {coordinate.X},{coordinate.Z}") { hideFlags = HideFlags.DontSave };
                chunk.transform.SetParent(transform, false);
                var origin = coordinate.GetSampleCoordinate(cellsPerChunk, 0, 0);
                chunk.transform.localPosition = new Vector3((float)(origin.X * (double)horizontalScale), 0,
                    (float)(origin.Z * (double)horizontalScale));
                chunk.AddComponent<MeshFilter>().sharedMesh = mesh;
                chunk.AddComponent<MeshRenderer>().sharedMaterial = appliedEnvironment != null ? appliedEnvironment.Material : terrainMaterial;
                active.Add(coordinate, new View { Object = chunk, Mesh = mesh });
            }
            catch
            {
                if (chunk != null) { chunk.SetActive(false); Release(chunk); }
                Release(mesh);
                throw;
            }
        }

        public void Clear()
        {
            foreach (var view in active.Values) { ReleaseView(view); TotalUnloaded++; }
            active.Clear(); pending.Clear(); desired.Clear(); ordered.Clear(); removal.Clear();
            definition = null; appliedMaterial = null; selectedRadius = -1;
            appliedEnvironment = null;
            LastGeneratedCount = 0;
        }

        private static void ReleaseView(View view)
        {
            if (view.Object != null)
            {
                view.Object.SetActive(false);
                // Remove the renderer's reference before delayed native destruction in Play Mode.
                view.Object.GetComponent<MeshFilter>().sharedMesh = null;
                Release(view.Object);
            }
            if (view.Mesh != null) Release(view.Mesh);
        }

        private static void Release(Object value)
        {
            if (Application.isPlaying) Destroy(value);
            else DestroyImmediate(value);
        }
    }
}
