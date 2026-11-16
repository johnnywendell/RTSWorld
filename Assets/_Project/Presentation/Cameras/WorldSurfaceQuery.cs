using System;
using RTSWorld.Presentation.Streaming;
using RTSWorld.Presentation.Environment;
using RTSWorld.World.Generation;
using RTSWorld.World.Environment;
using UnityEngine;

namespace RTSWorld.Presentation.Cameras
{
    public interface ICameraSurface
    {
        Transform ReferenceFrame { get; }
        float HorizontalScale { get; }
        float MinimumHeight { get; }
        float MaximumHeight { get; }
        float HeightAt(double logicalX, double logicalZ);
    }

    /// <summary>Read-only presentation adapter; queries the same triangulated fields even when unloaded.</summary>
    public sealed class WorldSurfaceQuery : MonoBehaviour, ICameraSurface
    {
        [SerializeField] private ChunkManager manager;
        private readonly TerrainGenerator terrain = new TerrainGenerator();
        private WorldDefinition definition;
        private EnvironmentGenerator environment;
        private EnvironmentPresentation appliedLayer;
        private int appliedRevision;
        public Transform ReferenceFrame => manager.transform;
        public float HorizontalScale => manager.HorizontalScale;
        public float MinimumHeight => -3 * manager.VerticalScale;
        // v1 terrain<=100, legacy berm<=100.8, water<=100. Independent of visible chunks.
        public float MaximumHeight => 101 * manager.VerticalScale;
        public void Configure(ChunkManager stream) { manager = stream != null ? stream : throw new ArgumentNullException(nameof(stream)); definition = null; }
        private void EnsureSource()
        {
            if (manager == null) throw new InvalidOperationException("Assign a ChunkManager to WorldSurfaceQuery.");
            var layer = manager.EnvironmentLayer;
            if (layer != null && !layer.isActiveAndEnabled) layer = null;
            if (definition != null && definition.Seed == manager.Seed && definition.CellsPerChunk == manager.CellsPerChunk &&
                appliedLayer == layer && (layer == null || appliedRevision == layer.Revision)) return;
            definition = new WorldDefinition(manager.Seed, cellsPerChunk: manager.CellsPerChunk);
            appliedLayer = layer; appliedRevision = layer != null ? layer.Revision : 0;
            environment = layer != null ? new EnvironmentGenerator(definition, new EnvironmentDefinition(layer.SeaLevel, layer.EnvironmentVersion)) : null;
        }
        public float HeightAt(double logicalX, double logicalZ)
        {
            EnsureSource();
            long x = (long)Math.Floor(logicalX), z = (long)Math.Floor(logicalZ);
            double fx = logicalX - x, fz = logicalZ - z;
            void Point(long px, long pz, out double ground, out double water)
            {
                if (environment != null) { var sample = environment.Sample(px, pz); ground = sample.Elevation; water = sample.WaterLevel; }
                else { ground = terrain.SampleElevation(definition, px, pz); water = ground; }
            }
            Point(x + 1, z, out double b, out double wb); Point(x, z + 1, out double c, out double wc);
            double g, w;
            if (fx + fz <= 1)
            {
                Point(x, z, out double a, out double wa);
                g = a + (b - a) * fx + (c - a) * fz; w = wa + (wb - wa) * fx + (wc - wa) * fz;
            }
            else
            {
                Point(x + 1, z + 1, out double d, out double wd);
                g = d + (c - d) * (1 - fx) + (b - d) * (1 - fz);
                w = wd + (wc - wd) * (1 - fx) + (wb - wd) * (1 - fz);
            }
            return (float)(Math.Max(g, w) * manager.VerticalScale);
        }
    }
}
