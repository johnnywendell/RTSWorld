using System.Collections.Generic;
using RTSWorld.World.Chunks;
using RTSWorld.World.Generation;
using UnityEngine;

namespace RTSWorld.Presentation.Terrain
{
    /// <summary>Fixed 2x2 demonstration, not a streaming manager. Rebuild is explicit.</summary>
    [ExecuteAlways]
    public sealed class TerrainDemo : MonoBehaviour
    {
        [SerializeField] private int seed = 12345;
        [SerializeField, Range(1, 256)] private int cellsPerChunk = 32;
        [SerializeField, Range(0.001f, 10000f)] private float horizontalScale = 2f;
        [SerializeField, Range(0.001f, 10000f)] private float verticalScale = 0.8f;
        [SerializeField] private Material terrainMaterial;
        private readonly List<GameObject> chunks = new List<GameObject>(4);
        private readonly List<Mesh> meshes = new List<Mesh>(4);

        public int Seed => seed;
        public int ChunkCount => chunks.Count;

        public void Configure(int newSeed, int cells, float horizontal, float vertical, Material material)
        {
            // Validate before changing the existing presentation.
            _ = new WorldDefinition(newSeed, cellsPerChunk: cells);
            TerrainGeometry.ValidateScale(horizontal, nameof(horizontal));
            TerrainGeometry.ValidateScale(vertical, nameof(vertical));
            seed = newSeed; cellsPerChunk = cells;
            horizontalScale = horizontal; verticalScale = vertical; terrainMaterial = material;
        }

        private void OnEnable() { if (terrainMaterial != null) Rebuild(); }
        private void OnDisable() => Clear();

        [ContextMenu("Rebuild Terrain")]
        public void Rebuild()
        {
            var definition = new WorldDefinition(seed, cellsPerChunk: cellsPerChunk);
            TerrainGeometry.ValidateScale(horizontalScale, nameof(horizontalScale));
            TerrainGeometry.ValidateScale(verticalScale, nameof(verticalScale));
            if (terrainMaterial == null) throw new System.InvalidOperationException("Assign a URP terrain material first.");
            Clear();
            var generator = new TerrainGenerator();
            for (int z = -1; z <= 0; z++)
            for (int x = -1; x <= 0; x++)
            {
                var data = generator.Generate(definition, new ChunkCoordinate(x, z));
                var geometry = TerrainGeometry.Build(data, horizontalScale, verticalScale);
                var mesh = TerrainMeshFactory.Create(geometry);
                mesh.hideFlags = HideFlags.DontSave;
                meshes.Add(mesh);
                var chunk = new GameObject($"Chunk {x},{z}");
                chunk.hideFlags = HideFlags.DontSave;
                chunks.Add(chunk);
                chunk.transform.SetParent(transform, false);
                chunk.transform.localPosition = new Vector3(x * cellsPerChunk * horizontalScale, 0, z * cellsPerChunk * horizontalScale);
                chunk.AddComponent<MeshFilter>().sharedMesh = mesh;
                chunk.AddComponent<MeshRenderer>().sharedMaterial = terrainMaterial;
            }
        }

        private void Clear()
        {
            foreach (var chunk in chunks)
            {
                if (chunk == null) continue;
                // Detach rendering immediately; Destroy is deferred in Play Mode.
                chunk.SetActive(false);
                Release(chunk);
            }
            chunks.Clear();
            foreach (var mesh in meshes) if (mesh != null) Release(mesh);
            meshes.Clear();
        }

        private static void Release(Object value)
        {
            if (Application.isPlaying) Destroy(value);
            else DestroyImmediate(value);
        }
    }
}
