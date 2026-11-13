using System;
using System.Collections.Generic;
using RTSWorld.World.Chunks;
using RTSWorld.World.Generation;

namespace RTSWorld.World.Environment
{
    /// <summary>Versioned, global analytical climate/basin/river overlay. No hydrodynamics or scene state.</summary>
    public sealed class EnvironmentGenerator
    {
        private readonly WorldDefinition world;
        private readonly EnvironmentDefinition settings;
        private readonly TerrainGenerator terrain = new TerrainGenerator();
        private readonly Dictionary<(long, long), Basin> basins = new Dictionary<(long, long), Basin>();

        private readonly struct Basin
        {
            public readonly double AX, AZ, BX, BZ, ALevel, BLevel, ARadius, BRadius;
            public Basin(double ax, double az, double bx, double bz, double a, double b, double ar, double br)
            { AX = ax; AZ = az; BX = bx; BZ = bz; ALevel = a; BLevel = b; ARadius = ar; BRadius = br; }
        }

        public EnvironmentGenerator(WorldDefinition world, EnvironmentDefinition settings)
        { this.world = world ?? throw new ArgumentNullException(nameof(world)); this.settings = settings ?? throw new ArgumentNullException(nameof(settings)); }

        public EnvironmentChunkData Generate(TerrainChunkData source)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (source.Definition.Seed != world.Seed || source.Definition.CellsPerChunk != world.CellsPerChunk ||
                source.Definition.GeneratorVersion != world.GeneratorVersion) throw new ArgumentException("Source world mismatch.");
            int n = world.CellsPerChunk, width = n + 1;
            var samples = new EnvironmentSample[width * width];
            var origin = source.Coordinate.GetSampleCoordinate(n, 0, 0);
            for (int z = 0; z <= n; z++)
            for (int x = 0; x <= n; x++) samples[z * width + x] = Evaluate(origin.X + x, origin.Z + z, source[x, z].Elevation);
            var resources = new List<ResourcePlacement>();
            for (int z = 0; z < n; z++)
            for (int x = 0; x < n; x++)
            {
                var a = samples[z * width + x]; var b = samples[z * width + x + 1];
                var c = samples[(z + 1) * width + x]; var d = samples[(z + 1) * width + x + 1];
                float slope = Math.Max(Math.Max(Math.Abs(a.Elevation - b.Elevation), Math.Abs(a.Elevation - c.Elevation)), Math.Abs(a.Elevation - d.Elevation));
                if (a.Water != WaterKind.None || b.Water != WaterKind.None || c.Water != WaterKind.None || d.Water != WaterKind.None || slope > 1.8f) continue;
                long gx = origin.X + x, gz = origin.Z + z;
                double roll = EnvironmentNoise.Hash(world.Seed, gx, gz, 20);
                double trees = TreeDensity(a.Biome), stone = a.Biome == EnvironmentBiome.Mountain ? 0.055 : 0.014;
                NaturalResource kind;
                if (roll < trees) kind = NaturalResource.Tree;
                else if (roll < trees + stone) kind = NaturalResource.Stone;
                else if (roll < trees + stone + 0.006) kind = NaturalResource.Gold;
                else continue;
                float ox = (float)(0.25 + EnvironmentNoise.Hash(world.Seed, gx, gz, 21) * 0.5);
                float oz = (float)(0.25 + EnvironmentNoise.Hash(world.Seed, gx, gz, 22) * 0.5);
                // Matches the same diagonal as TerrainGeometry, rather than bilinear terrain that is not rendered.
                float height = ox + oz <= 1 ? a.Elevation + (b.Elevation - a.Elevation) * ox + (c.Elevation - a.Elevation) * oz :
                    d.Elevation + (c.Elevation - d.Elevation) * (1 - ox) + (b.Elevation - d.Elevation) * (1 - oz);
                resources.Add(new ResourcePlacement(new ResourceKey(world.Seed, settings.Version, gx, gz), kind, ox, oz, height));
            }
            return new EnvironmentChunkData(settings, source, samples, resources.ToArray());
        }

        public EnvironmentSample Sample(long x, long z) => Evaluate(x, z, terrain.SampleElevation(world, x, z));

        public bool TryGetRiver(long logicalX, long logicalZ, out RiverReach reach)
        {
            var b = GetBasin(logicalX, logicalZ);
            reach = new RiverReach(b.AX, b.AZ, b.BX, b.BZ, b.ALevel, b.BLevel, b.ARadius, b.BRadius);
            return b.ALevel > settings.SeaLevel + 3 && b.ALevel - b.BLevel > 1;
        }

        private EnvironmentSample Evaluate(long x, long z, float baseHeight)
        {
            double elevation = baseHeight, water = settings.SeaLevel;
            WaterKind kind = WaterKind.Ocean;
            var basin = GetBasin(x, z);
            ApplyLake(x, z, basin.AX, basin.AZ, basin.ARadius, basin.ALevel, ref elevation, ref water, ref kind);
            ApplyLake(x, z, basin.BX, basin.BZ, basin.BRadius, basin.BLevel, ref elevation, ref water, ref kind);
            double dx = basin.BX - basin.AX, dz = basin.BZ - basin.AZ;
            double length = Math.Sqrt(dx * dx + dz * dz);
            double t = ((x - basin.AX) * dx + (z - basin.AZ) * dz) / (length * length);
            if (t >= 0 && t <= 1 && basin.ALevel > settings.SeaLevel + 3 && basin.ALevel - basin.BLevel > 1)
            {
                double curve = Math.Sin(t * Math.PI) * 8;
                double distance = Math.Abs(((x - basin.AX) * -dz + (z - basin.AZ) * dx) / length - curve);
                var reach = new RiverReach(basin.AX, basin.AZ, basin.BX, basin.BZ, basin.ALevel, basin.BLevel, basin.ARadius, basin.BRadius);
                double level = reach.LevelAt(t);
                // A river entering an already wet lake must not raise its own parallel banks through the lake bed.
                bool insideLake = kind == WaterKind.Lake && elevation < water;
                if (distance < 7 && !insideLake) ApplyBasin(distance / 4, level, WaterKind.River, ref elevation, ref water, ref kind);
            }
            float temperature = (float)Math.Max(0, Math.Min(1,
                (EnvironmentNoise.Field(world.Seed, x, z, 192, 1) - 0.5) * 1.8 + 0.65 - Math.Max(0, elevation - 45) * 0.008));
            float humidity = (float)Math.Max(0, Math.Min(1, (EnvironmentNoise.Field(world.Seed, x, z, 128, 2) - 0.5) * 1.7 + 0.5));
            WaterKind actual = elevation < water - 0.001 ? kind : WaterKind.None;
            var biome = Classify((float)elevation, temperature, humidity, settings.SeaLevel, actual);
            return new EnvironmentSample((float)elevation, temperature, humidity, (float)water, biome, actual);
        }

        public static EnvironmentBiome Classify(float elevation, float temperature, float humidity, float seaLevel, WaterKind water)
        {
            if (water == WaterKind.Ocean) return EnvironmentBiome.Ocean;
            if (elevation < seaLevel + 2.5f && water == WaterKind.None) return EnvironmentBiome.Coast;
            if (temperature < 0.2f) return elevation > 60 ? EnvironmentBiome.Snow : EnvironmentBiome.Tundra;
            if (elevation > 73) return EnvironmentBiome.Mountain;
            if (temperature > 0.65f && humidity < 0.28f) return EnvironmentBiome.Desert;
            if (humidity < 0.35f) return EnvironmentBiome.Arid;
            if (humidity > 0.73f) return EnvironmentBiome.DenseForest;
            if (humidity > 0.51f) return EnvironmentBiome.TemperateForest;
            return EnvironmentBiome.Plains;
        }

        public static double TreeDensity(EnvironmentBiome biome)
        {
            switch (biome)
            {
                case EnvironmentBiome.DenseForest: return 0.22;
                case EnvironmentBiome.TemperateForest: return 0.12;
                case EnvironmentBiome.Plains: return 0.025;
                case EnvironmentBiome.Tundra: return 0.008;
                default: return 0;
            }
        }

        private Basin GetBasin(long x, long z)
        {
            long tx = x / 256, tz = z / 256;
            if (x % 256 < 0) tx--; if (z % 256 < 0) tz--;
            if (basins.TryGetValue((tx, tz), out var basin)) return basin;
            double ax = tx * 256 + 60 + EnvironmentNoise.Hash(world.Seed, tx, tz, 8) * 12;
            double az = tz * 256 + 60, bx = tx * 256 + 180, bz = tz * 256 + 184;
            double a = Math.Max(settings.SeaLevel, RimLevel(ax, az, 16) - 2);
            double b = Math.Max(settings.SeaLevel, RimLevel(bx, bz, 24) - 2);
            basin = a >= b ? new Basin(ax, az, bx, bz, a, b, 16, 24) : new Basin(bx, bz, ax, az, b, a, 24, 16);
            if (basins.Count >= 9) basins.Clear(); // Bounded metadata only, never a world-sized cache.
            basins.Add((tx, tz), basin);
            return basin;
        }

        private double RimLevel(double x, double z, double radius)
        {
            double lowest = 100;
            for (int i = 0; i < 8; i++)
            {
                double angle = i * Math.PI / 4;
                lowest = Math.Min(lowest, terrain.SampleElevation(world, (long)Math.Round(x + Math.Cos(angle) * radius * 1.4),
                    (long)Math.Round(z + Math.Sin(angle) * radius * 1.4)));
            }
            return lowest;
        }

        private void ApplyLake(long x, long z, double cx, double cz, double radius, double level,
            ref double elevation, ref double water, ref WaterKind kind)
        {
            if (level <= settings.SeaLevel + 0.001) return;
            double r = Math.Sqrt((x - cx) * (x - cx) + (z - cz) * (z - cz)) / radius;
            if (r < 1.5) ApplyBasin(r, level, WaterKind.Lake, ref elevation, ref water, ref kind);
        }

        private static void ApplyBasin(double radius, double level, WaterKind type,
            ref double elevation, ref double water, ref WaterKind kind)
        {
            if (radius >= 1.5) return;
            double carved = Math.Min(elevation, level - 2.5 * Math.Max(0, 1 - radius * radius));
            double rim = Math.Max(elevation, level + 0.8);
            double shaped = radius < 1.2 ? EnvironmentNoise.Lerp(carved, rim, EnvironmentNoise.Smooth((radius - 0.8) / 0.4)) :
                EnvironmentNoise.Lerp(rim, elevation, EnvironmentNoise.Smooth((radius - 1.2) / 0.3));
            elevation = shaped;
            if (radius < 1.2 && level >= water) { water = level; kind = type; }
        }
    }
}
