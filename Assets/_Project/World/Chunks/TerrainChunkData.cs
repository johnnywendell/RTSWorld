using System;
using RTSWorld.World.Generation;

namespace RTSWorld.World.Chunks
{
    // Reserved data slots only. Classification and resource placement are future tasks.
    public enum BiomeId : ushort { Unassigned = 0 }
    public enum ResourceId : ushort { None = 0 }

    public readonly struct TerrainSample
    {
        public float Elevation { get; }
        public BiomeId Biome { get; }
        public ResourceId Resource { get; }

        public TerrainSample(float elevation, BiomeId biome = BiomeId.Unassigned,
            ResourceId resource = ResourceId.None)
        {
            Elevation = elevation;
            Biome = biome;
            Resource = resource;
        }
    }

    /// <summary>Immutable sample grid. X varies fastest; shared edge samples are included.</summary>
    public sealed class TerrainChunkData
    {
        private readonly TerrainSample[] samples;
        public WorldDefinition Definition { get; }
        public ChunkCoordinate Coordinate { get; }
        public int SamplesPerAxis => Definition.CellsPerChunk + 1;
        public int SampleCount => samples.Length;

        internal TerrainChunkData(WorldDefinition definition, ChunkCoordinate coordinate,
            TerrainSample[] samples)
        {
            Definition = definition;
            Coordinate = coordinate;
            // No mutable array supplied by a caller can change the stored chunk.
            this.samples = (TerrainSample[])samples.Clone();
        }

        public TerrainSample this[int localX, int localZ]
        {
            get
            {
                if (localX < 0 || localX >= SamplesPerAxis)
                    throw new ArgumentOutOfRangeException(nameof(localX));
                if (localZ < 0 || localZ >= SamplesPerAxis)
                    throw new ArgumentOutOfRangeException(nameof(localZ));
                return samples[localZ * SamplesPerAxis + localX];
            }
        }
    }
}
