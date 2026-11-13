using System;
using System.Collections.Generic;
using RTSWorld.World.Chunks;

namespace RTSWorld.World.Environment
{
    public enum EnvironmentBiome { Plains, TemperateForest, DenseForest, Desert, Arid, Snow, Tundra, Mountain, Coast, Ocean }
    public enum WaterKind { None, Ocean, Lake, River }
    public enum NaturalResource { Tree, Stone, Gold }

    public readonly struct RiverReach
    {
        public readonly double AX, AZ, BX, BZ, ALevel, BLevel, ARadius, BRadius;
        public RiverReach(double ax, double az, double bx, double bz, double a, double b, double ar, double br)
        { AX = ax; AZ = az; BX = bx; BZ = bz; ALevel = a; BLevel = b; ARadius = ar; BRadius = br; }
        public double Length => Math.Sqrt((BX - AX) * (BX - AX) + (BZ - AZ) * (BZ - AZ));
        public double LevelAt(double t) => EnvironmentNoise.Lerp(ALevel, BLevel,
            Math.Max(0, Math.Min(1, (t * Length - ARadius) / (Length - ARadius - BRadius))));
        public void PointAt(double t, out double x, out double z)
        {
            double curve = Math.Sin(t * Math.PI) * 8;
            x = AX + (BX - AX) * t - (BZ - AZ) / Length * curve;
            z = AZ + (BZ - AZ) * t + (BX - AX) / Length * curve;
        }
    }

    public sealed class EnvironmentDefinition
    {
        public const int CurrentVersion = 1;
        public int Version { get; }
        public float SeaLevel { get; }
        public EnvironmentDefinition(float seaLevel = 38, int version = CurrentVersion)
        {
            if (version != CurrentVersion) throw new ArgumentOutOfRangeException(nameof(version));
            if (float.IsNaN(seaLevel) || float.IsInfinity(seaLevel) || seaLevel < 0 || seaLevel > 100)
                throw new ArgumentOutOfRangeException(nameof(seaLevel));
            Version = version; SeaLevel = seaLevel;
        }
    }

    public readonly struct EnvironmentSample
    {
        public float Elevation { get; }
        public float Temperature { get; }
        public float Humidity { get; }
        public float WaterLevel { get; }
        public EnvironmentBiome Biome { get; }
        public WaterKind Water { get; }
        public EnvironmentSample(float elevation, float temperature, float humidity, float waterLevel,
            EnvironmentBiome biome, WaterKind water)
        { Elevation = elevation; Temperature = temperature; Humidity = humidity; WaterLevel = waterLevel; Biome = biome; Water = water; }
    }

    /// <summary>Collision-free logical identity within a seed/version: one candidate per global cell.</summary>
    public readonly struct ResourceKey : IEquatable<ResourceKey>
    {
        public int Seed { get; }
        public int Version { get; }
        public long X { get; }
        public long Z { get; }
        public ResourceKey(int seed, int version, long x, long z) { Seed = seed; Version = version; X = x; Z = z; }
        public bool Equals(ResourceKey other) => Seed == other.Seed && Version == other.Version && X == other.X && Z == other.Z;
        public override bool Equals(object obj) => obj is ResourceKey other && Equals(other);
        public override int GetHashCode() => unchecked(((Seed * 397 ^ Version) * 397 ^ X.GetHashCode()) * 397 ^ Z.GetHashCode());
        public override string ToString() => $"{Seed}:{Version}:{X}:{Z}";
    }

    public readonly struct ResourcePlacement
    {
        public ResourceKey Key { get; }
        public NaturalResource Kind { get; }
        public float OffsetX { get; }
        public float OffsetZ { get; }
        public float Elevation { get; }
        public ResourcePlacement(ResourceKey key, NaturalResource kind, float offsetX, float offsetZ, float elevation)
        { Key = key; Kind = kind; OffsetX = offsetX; OffsetZ = offsetZ; Elevation = elevation; }
    }

    public sealed class EnvironmentChunkData
    {
        private readonly EnvironmentSample[] samples;
        public EnvironmentDefinition Definition { get; }
        public TerrainChunkData BaseTerrain { get; }
        public TerrainChunkData Surface { get; }
        public IReadOnlyList<ResourcePlacement> Resources { get; }
        public int Width => BaseTerrain.SamplesPerAxis;
        public EnvironmentSample this[int x, int z]
        {
            get
            {
                if (x < 0 || x >= Width || z < 0 || z >= Width) throw new ArgumentOutOfRangeException();
                return samples[z * Width + x];
            }
        }
        internal EnvironmentChunkData(EnvironmentDefinition definition, TerrainChunkData source,
            EnvironmentSample[] samples, ResourcePlacement[] resources)
        {
            Definition = definition; BaseTerrain = source;
            this.samples = (EnvironmentSample[])samples.Clone();
            Resources = Array.AsReadOnly((ResourcePlacement[])resources.Clone());
            var surface = new TerrainSample[samples.Length];
            for (int i = 0; i < surface.Length; i++) surface[i] = new TerrainSample(samples[i].Elevation);
            Surface = new TerrainChunkData(source.Definition, source.Coordinate, surface);
        }
    }
}
