using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using RTSWorld.World.Chunks;
using RTSWorld.World.Environment;
using RTSWorld.World.Generation;

namespace RTSWorld.World.Tests
{
    public sealed class EnvironmentGeneratorTests
    {
        private static EnvironmentChunkData Chunk(int x, int z, int seed = 12345, int cells = 16, float sea = 38)
        {
            var world = new WorldDefinition(seed, cellsPerChunk: cells);
            return new EnvironmentGenerator(world, new EnvironmentDefinition(sea)).Generate(new TerrainGenerator().Generate(world, new ChunkCoordinate(x, z)));
        }
        private static void Same(EnvironmentSample a, EnvironmentSample b)
        {
            Assert.That(a.Elevation, Is.EqualTo(b.Elevation)); Assert.That(a.WaterLevel, Is.EqualTo(b.WaterLevel));
            Assert.That(a.Temperature, Is.EqualTo(b.Temperature)); Assert.That(a.Humidity, Is.EqualTo(b.Humidity));
            Assert.That(a.Biome, Is.EqualTo(b.Biome)); Assert.That(a.Water, Is.EqualTo(b.Water));
        }

        [TestCase(0, 0)]
        [TestCase(-5, -5)]
        [TestCase(10, 11)]
        public void RepeatsAllFieldsResourcesAndLeavesBaseUnchanged(int x, int z)
        {
            var a = Chunk(x, z); var b = Chunk(x, z);
            for (int iz = 0; iz < a.Width; iz++)
            for (int ix = 0; ix < a.Width; ix++) Same(a[ix, iz], b[ix, iz]);
            CollectionAssert.AreEqual(a.Resources, b.Resources);
            var source = new TerrainGenerator().Generate(a.BaseTerrain.Definition, a.BaseTerrain.Coordinate);
            for (int iz = 0; iz < a.Width; iz++)
            for (int ix = 0; ix < a.Width; ix++) Assert.That(a.BaseTerrain[ix, iz].Elevation, Is.EqualTo(source[ix, iz].Elevation));
        }

        [Test]
        public void ClimateAndResourceOutputsChangeWithSeed()
        {
            var a = Chunk(3, 3); var b = Chunk(3, 3, seed: 67890);
            Assert.That(a[0, 0].Temperature, Is.Not.EqualTo(b[0, 0].Temperature));
            Assert.That(a[0, 0].Humidity, Is.Not.EqualTo(b[0, 0].Humidity));
            Assert.That(a.Resources.SequenceEqual(b.Resources), Is.False);
        }

        [TestCase(0, 0)]
        [TestCase(-1, -1)]
        [TestCase(3, 3)]
        [TestCase(10, 11)]
        [TestCase(15, 15)]
        public void BordersMatchForSurfaceClimateAndWater(int x, int z)
        {
            var a = Chunk(x, z); var east = Chunk(x + 1, z); var north = Chunk(x, z + 1);
            for (int i = 0; i < 17; i++) { Same(a[16, i], east[0, i]); Same(a[i, 16], north[i, 0]); }
        }

        [Test]
        public void ChunkPartitionDoesNotChangeEnvironmentOrResourceOwnership()
        {
            var large = Chunk(-1, -1, cells: 32);
            var keys = new HashSet<ResourceKey>();
            for (int z = -2; z <= -1; z++)
            for (int x = -2; x <= -1; x++)
            {
                var small = Chunk(x, z);
                for (int iz = 0; iz < 17; iz++)
                for (int ix = 0; ix < 17; ix++) Same(small[ix, iz], large[(x + 2) * 16 + ix, (z + 2) * 16 + iz]);
                foreach (var r in small.Resources) Assert.That(keys.Add(r.Key), Is.True);
            }
            CollectionAssert.AreEquivalent(large.Resources.Select(r => r.Key), keys);
        }

        [TestCase(50, 0.8f, 0.1f, EnvironmentBiome.Desert)]
        [TestCase(50, 0.5f, 0.2f, EnvironmentBiome.Arid)]
        [TestCase(50, 0.5f, 0.4f, EnvironmentBiome.Plains)]
        [TestCase(50, 0.5f, 0.6f, EnvironmentBiome.TemperateForest)]
        [TestCase(50, 0.5f, 0.9f, EnvironmentBiome.DenseForest)]
        [TestCase(65, 0.1f, 0.4f, EnvironmentBiome.Snow)]
        [TestCase(50, 0.1f, 0.4f, EnvironmentBiome.Tundra)]
        [TestCase(80, 0.4f, 0.4f, EnvironmentBiome.Mountain)]
        [TestCase(39, 0.4f, 0.4f, EnvironmentBiome.Coast)]
        public void ClassificationRules(float elevation, float temperature, float humidity, EnvironmentBiome expected) =>
            Assert.That(EnvironmentGenerator.Classify(elevation, temperature, humidity, 38, WaterKind.None), Is.EqualTo(expected));

        [Test]
        public void RepresentativeWorldContainsAllBiomesAndWaterKinds()
        {
            var generator = new EnvironmentGenerator(new WorldDefinition(12345), new EnvironmentDefinition());
            var biomes = new HashSet<EnvironmentBiome>(); var waters = new HashSet<WaterKind>();
            for (int z = -768; z <= 768; z += 4)
            for (int x = -768; x <= 768; x += 4)
            {
                var s = generator.Sample(x, z); biomes.Add(s.Biome); waters.Add(s.Water);
                Assert.That(s.Temperature, Is.InRange(0f, 1f)); Assert.That(s.Humidity, Is.InRange(0f, 1f));
                if (s.Water != WaterKind.None) Assert.That(s.Elevation, Is.LessThan(s.WaterLevel));
            }
            CollectionAssert.AreEquivalent(Enum.GetValues(typeof(EnvironmentBiome)), biomes);
            CollectionAssert.AreEquivalent(Enum.GetValues(typeof(WaterKind)), waters);
        }

        [Test]
        public void ResourcesStayDryAndUniqueAndUseBiomeDensities()
        {
            var keys = new HashSet<ResourceKey>(); var kinds = new HashSet<NaturalResource>();
            for (int cz = -4; cz < 8; cz++)
            for (int cx = -4; cx < 8; cx++)
            {
                var data = Chunk(cx, cz); var origin = data.BaseTerrain.Coordinate.GetSampleCoordinate(16, 0, 0);
                foreach (var r in data.Resources)
                {
                    Assert.That(keys.Add(r.Key), Is.True); kinds.Add(r.Kind);
                    int x = (int)(r.Key.X - origin.X), z = (int)(r.Key.Z - origin.Z);
                    Assert.That(x, Is.InRange(0, 15)); Assert.That(z, Is.InRange(0, 15));
                    Assert.That(r.OffsetX, Is.InRange(0.25f, 0.75f)); Assert.That(r.OffsetZ, Is.InRange(0.25f, 0.75f));
                    Assert.That(data[x, z].Water, Is.EqualTo(WaterKind.None));
                    Assert.That(data[x + 1, z + 1].Water, Is.EqualTo(WaterKind.None));
                    if (r.Kind == NaturalResource.Tree) Assert.That(EnvironmentGenerator.TreeDensity(data[x, z].Biome), Is.GreaterThan(0));
                }
            }
            CollectionAssert.AreEquivalent(Enum.GetValues(typeof(NaturalResource)), kinds);
            Assert.That(EnvironmentGenerator.TreeDensity(EnvironmentBiome.DenseForest), Is.GreaterThan(EnvironmentGenerator.TreeDensity(EnvironmentBiome.Plains)));
        }

        [Test]
        public void LakeInteriorHasNoRiverBankRidges()
        {
            var gen = new EnvironmentGenerator(new WorldDefinition(12345), new EnvironmentDefinition());
            var center = gen.Sample(180, -72);
            Assert.That(center.Water, Is.EqualTo(WaterKind.Lake));
            for (int dz = -12; dz <= 12; dz++)
            for (int dx = -12; dx <= 12; dx++)
            {
                if (dx * dx + dz * dz > 144) continue;
                var sample = gen.Sample(180 + dx, -72 + dz);
                Assert.That(sample.Water, Is.EqualTo(WaterKind.Lake));
                Assert.That(sample.WaterLevel, Is.EqualTo(center.WaterLevel));
                Assert.That(sample.WaterLevel - sample.Elevation, Is.GreaterThan(1.4f));
            }
        }

        [Test]
        public void RiverCenterlinesAreWetAndDescendBetweenTheirBasins()
        {
            var generator = new EnvironmentGenerator(new WorldDefinition(12345), new EnvironmentDefinition());
            int found = 0;
            for (int z = -2; z <= 2; z++)
            for (int x = -2; x <= 2; x++)
            {
                if (!generator.TryGetRiver(x * 256, z * 256, out var river)) continue;
                found++;
                double previous = double.PositiveInfinity;
                for (int i = 1; i < 40; i++)
                {
                    double t = i / 40.0;
                    river.PointAt(t, out double gx, out double gz);
                    var sample = generator.Sample((long)Math.Round(gx), (long)Math.Round(gz));
                    Assert.That(sample.Water, Is.Not.EqualTo(WaterKind.None), $"Dry river at {gx},{gz}");
                    Assert.That(sample.Elevation, Is.LessThan(sample.WaterLevel));
                    Assert.That(sample.WaterLevel, Is.EqualTo(river.LevelAt(t)).Within(0.3));
                    Assert.That(sample.WaterLevel, Is.LessThanOrEqualTo(previous + 0.001));
                    previous = sample.WaterLevel;
                }
            }
            Assert.That(found, Is.GreaterThan(0));
        }

        [Test]
        public void SeaLevelIsConfigurableAndUnknownVersionsAreRejected()
        {
            var low = Chunk(0, 0, sea: 0); var high = Chunk(0, 0, sea: 100);
            Assert.That(high[0, 0].Water, Is.EqualTo(WaterKind.Ocean));
            Assert.That(low.BaseTerrain[0, 0].Elevation, Is.EqualTo(high.BaseTerrain[0, 0].Elevation));
            Assert.Throws<ArgumentOutOfRangeException>(() => new EnvironmentDefinition(version: 2));
            Assert.Throws<ArgumentOutOfRangeException>(() => new EnvironmentDefinition(float.NaN));
        }

        [Test]
        public void AdditiveBaseSamplerMatchesV1ChunksAtNegativeAndExtremeCoordinates()
        {
            var generator = new TerrainGenerator(); var world = new WorldDefinition(-1);
            foreach (var coordinate in new[] { new ChunkCoordinate(-2, 3), new ChunkCoordinate(int.MaxValue, int.MinValue) })
            {
                var chunk = generator.Generate(world, coordinate);
                for (int z = 0; z <= 32; z++)
                for (int x = 0; x <= 32; x++)
                {
                    var global = coordinate.GetSampleCoordinate(32, x, z);
                    Assert.That(generator.SampleElevation(world, global.X, global.Z), Is.EqualTo(chunk[x, z].Elevation));
                }
            }
        }
    }
}
