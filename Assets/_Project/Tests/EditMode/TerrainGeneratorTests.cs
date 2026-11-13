using System;
using NUnit.Framework;
using RTSWorld.World.Chunks;
using RTSWorld.World.Generation;

namespace RTSWorld.World.Tests
{
    public sealed class TerrainGeneratorTests
    {
        private static void AssertSame(TerrainSample a, TerrainSample b)
        {
            Assert.That(Bits(a.Elevation), Is.EqualTo(Bits(b.Elevation)));
            Assert.That(a.Biome, Is.EqualTo(b.Biome));
            Assert.That(a.Resource, Is.EqualTo(b.Resource));
        }

        private static int Bits(float value) => BitConverter.ToInt32(BitConverter.GetBytes(value), 0);

        [TestCase(0, 0, 0)]
        [TestCase(-123, -2, 3)]
        [TestCase(int.MaxValue, int.MinValue, int.MaxValue)]
        public void RepeatedGenerationMatchesEverySample(int seed, int x, int z)
        {
            var definition = new WorldDefinition(seed);
            var coordinate = new ChunkCoordinate(x, z);
            var generator = new TerrainGenerator();
            var a = generator.Generate(definition, coordinate);
            generator.Generate(new WorldDefinition(999), new ChunkCoordinate(7, -9));
            var b = new TerrainGenerator().Generate(new WorldDefinition(seed), coordinate);
            Assert.That(a.SampleCount, Is.EqualTo(33 * 33));
            Assert.That(a.Definition.Seed, Is.EqualTo(seed));
            Assert.That(a.Definition.GeneratorVersion, Is.EqualTo(1));
            Assert.That(a.Coordinate, Is.EqualTo(coordinate));
            for (int iz = 0; iz < 33; iz++)
            for (int ix = 0; ix < 33; ix++) AssertSame(a[ix, iz], b[ix, iz]);
        }

        [TestCase(0, 1)]
        [TestCase(-1, int.MaxValue)]
        [TestCase(int.MinValue, 12345)]
        public void DifferentSeedsProduceDifferentTerrain(int seedA, int seedB)
        {
            var generator = new TerrainGenerator();
            var a = generator.Generate(new WorldDefinition(seedA), new ChunkCoordinate(-1, 2));
            var b = generator.Generate(new WorldDefinition(seedB), new ChunkCoordinate(-1, 2));
            int differences = 0;
            for (int z = 0; z < 33; z++)
            for (int x = 0; x < 33; x++)
                if (Bits(a[x, z].Elevation) != Bits(b[x, z].Elevation)) differences++;
            Assert.That(differences, Is.GreaterThan(a.SampleCount / 2));
        }

        [TestCase(0, 0, 32)]
        [TestCase(-1, -1, 32)]
        [TestCase(-3, 2, 7)]
        [TestCase(int.MaxValue - 1, int.MinValue, 32)]
        [TestCase(2, -4, 1)]
        [TestCase(-2, 3, 256)]
        public void AdjacentChunksShareExactEdgesAndCorner(int cx, int cz, int cells)
        {
            var generator = new TerrainGenerator();
            var definition = new WorldDefinition(-7781, cellsPerChunk: cells);
            // Generate out of spatial order to exercise independence from neighbour state.
            var diagonal = generator.Generate(definition, new ChunkCoordinate(cx + 1, cz + 1));
            var north = generator.Generate(definition, new ChunkCoordinate(cx, cz + 1));
            var east = generator.Generate(definition, new ChunkCoordinate(cx + 1, cz));
            var origin = generator.Generate(definition, new ChunkCoordinate(cx, cz));
            for (int i = 0; i <= cells; i++)
            {
                AssertSame(origin[cells, i], east[0, i]);
                AssertSame(origin[i, cells], north[i, 0]);
                Assert.That(origin.Coordinate.GetSampleCoordinate(cells, cells, i),
                    Is.EqualTo(east.Coordinate.GetSampleCoordinate(cells, 0, i)));
            }
            AssertSame(origin[cells, cells], diagonal[0, 0]);
        }

        [Test]
        public void ChunkPartitionDoesNotChangeGlobalHeight()
        {
            var generator = new TerrainGenerator();
            var large = generator.Generate(new WorldDefinition(42, cellsPerChunk: 32), new ChunkCoordinate(-1, 0));
            var small = generator.Generate(new WorldDefinition(42, cellsPerChunk: 16), new ChunkCoordinate(-1, 1));
            for (int z = 0; z <= 16; z++)
            for (int x = 0; x <= 16; x++) AssertSame(large[x + 16, z + 16], small[x, z]);
        }

        [TestCase(0, 0, 0L, 0L)]
        [TestCase(-1, -2, -32L, -64L)]
        [TestCase(int.MaxValue, int.MinValue, 68719476704L, -68719476736L)]
        public void GlobalCoordinatesUseWideIntegerArithmetic(int cx, int cz, long expectedX, long expectedZ)
        {
            var result = new ChunkCoordinate(cx, cz).GetSampleCoordinate(32, 0, 0);
            Assert.That(result.X, Is.EqualTo(expectedX));
            Assert.That(result.Z, Is.EqualTo(expectedZ));
        }

        [TestCase(0, 0, 0, 0, 1114399562)]
        [TestCase(-1, -1, 25, 21, 1114640964)]
        [TestCase(0, 0, 17, 23, 1113887457)]
        [TestCase(int.MaxValue, int.MinValue, 0, 0, 1100343752)]
        public void VersionOneMatchesFixedReferenceBits(int cx, int cz, int x, int z, int expectedBits)
        {
            // Fixture independently computed with the documented v1 integer hash/interpolation.
            // Any intentional output change requires a new version, not silently new fixture values.
            var chunk = new TerrainGenerator().Generate(new WorldDefinition(12345), new ChunkCoordinate(cx, cz));
            Assert.That(Bits(chunk[x, z].Elevation), Is.EqualTo(expectedBits));
        }

        [TestCase(-71, -2, -3)]
        [TestCase(8, 0, 0)]
        public void ElevationsAreBoundedNonFlatAndFutureSlotsAreUnassigned(int seed, int cx, int cz)
        {
            var chunk = new TerrainGenerator().Generate(new WorldDefinition(seed), new ChunkCoordinate(cx, cz));
            float min = 100, max = 0;
            for (int z = 0; z < 33; z++)
            for (int x = 0; x < 33; x++)
            {
                var sample = chunk[x, z];
                Assert.That(sample.Elevation, Is.InRange(0f, 100f));
                Assert.That(sample.Biome, Is.EqualTo(BiomeId.Unassigned));
                Assert.That(sample.Resource, Is.EqualTo(ResourceId.None));
                min = Math.Min(min, sample.Elevation);
                max = Math.Max(max, sample.Elevation);
            }
            Assert.That(max - min, Is.GreaterThan(0.1f));
        }

        [TestCase(0)]
        [TestCase(2)]
        [TestCase(-1)]
        public void UnsupportedVersionIsRejected(int version) =>
            Assert.Throws<ArgumentOutOfRangeException>(() => new WorldDefinition(1, version));

        [TestCase(0)]
        [TestCase(-1)]
        [TestCase(257)]
        public void InvalidChunkSizeIsRejected(int size) =>
            Assert.Throws<ArgumentOutOfRangeException>(() => new WorldDefinition(1, cellsPerChunk: size));

        [Test]
        public void InvalidCoordinatesAndNullDefinitionAreRejected()
        {
            var generator = new TerrainGenerator();
            Assert.Throws<ArgumentNullException>(() => generator.Generate(null, new ChunkCoordinate()));
            var chunk = generator.Generate(new WorldDefinition(1), new ChunkCoordinate());
            Assert.Throws<ArgumentOutOfRangeException>(() => { var unused = chunk[-1, 0]; });
            Assert.Throws<ArgumentOutOfRangeException>(() => { var unused = chunk[0, 33]; });
            Assert.Throws<ArgumentOutOfRangeException>(() => new ChunkCoordinate().GetSampleCoordinate(32, 33, 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => new ChunkCoordinate().GetSampleCoordinate(0, 0, 0));
        }
    }
}
