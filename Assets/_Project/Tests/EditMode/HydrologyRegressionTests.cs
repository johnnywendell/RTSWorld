using System;
using System.IO;
using System.Security.Cryptography;
using NUnit.Framework;
using RTSWorld.World.Chunks;
using RTSWorld.World.Environment;
using RTSWorld.World.Generation;

namespace RTSWorld.World.Tests
{
    public sealed class HydrologyRegressionTests
    {
        [Test]
        public void SeedSevenRetainsMountainsButRemovesTheRiverEndpointWallAndRaisedStrips()
        {
            var world = new WorldDefinition(7);
            var terrain = new TerrainGenerator();
            var legacy = new EnvironmentGenerator(world, new EnvironmentDefinition(version: 2));
            var current = new EnvironmentGenerator(world, new EnvironmentDefinition());
            Assert.That(Math.Abs(legacy.Sample(64, 59).Elevation - legacy.Sample(64, 60).Elevation), Is.GreaterThan(14));
            Assert.That(Math.Abs(current.Sample(64, 59).Elevation - current.Sample(64, 60).Elevation), Is.LessThan(2.25));
            int changed = 0, preservedHighland = 0, raised = 0;
            for (int z = -64; z <= 96; z++)
            for (int x = -64; x <= 96; x++)
            {
                float source = terrain.SampleElevation(world, x, z);
                var sample = current.Sample(x, z);
                if (sample.Elevation != source) changed++;
                else if (source > 60) preservedHighland++;
                if (sample.Elevation > source) raised++;
            }
            Assert.That(changed, Is.GreaterThan(0));
            Assert.That(changed, Is.LessThan(161 * 161 / 4), "Avoid flattening unrelated relief.");
            Assert.That(preservedHighland, Is.GreaterThan(100));
            Assert.That(raised, Is.Zero, "Hydrology must not build narrow dams across existing water.");
        }

        [TestCase(7, 0)] [TestCase(7, 38)] [TestCase(7, 55)] [TestCase(7, 100)]
        [TestCase(12345, 0)] [TestCase(12345, 38)] [TestCase(12345, 55)] [TestCase(12345, 100)]
        [TestCase(67890, 0)] [TestCase(67890, 38)] [TestCase(67890, 55)] [TestCase(67890, 100)]
        [TestCase(-1, 0)] [TestCase(-1, 38)] [TestCase(-1, 55)] [TestCase(-1, 100)]
        public void HydraulicFieldsBoundNewSlopesWithoutClampingTheBaseRelief(int seed, float sea)
        {
            var world = new WorldDefinition(seed);
            var terrain = new TerrainGenerator();
            // This contract covers the legacy continuous potential, including dry vertices.
            // V4 switches ownership only in dry buffers; its rendered-water bounds have separate regressions.
            var generator = new EnvironmentGenerator(world, new EnvironmentDefinition(sea, version: 3));
            const int min = -32, max = 288, width = max - min + 1;
            var baseHeights = new float[width * width];
            var samples = new EnvironmentSample[width * width];
            for (int z = min; z <= max; z++)
            for (int x = min; x <= max; x++)
            {
                int index = (z - min) * width + x - min;
                baseHeights[index] = terrain.SampleElevation(world, x, z);
                samples[index] = generator.Sample(x, z);
                Assert.That(samples[index].Elevation, Is.LessThanOrEqualTo(baseHeights[index]));
            }
            void Edge(int a, int b, double distance)
            {
                // For the v3 field: cone grade1.25 + reach grade<0.81 and meander
                // derivative<0.16 imply grade<2.25. Max/min preserve this bound;
                // an existing base slope may legitimately exceed it.
                double bound = Math.Max(Math.Abs(baseHeights[a] - baseHeights[b]), 2.25 * distance) + 0.00005;
                Assert.That(Math.Abs(samples[a].Elevation - samples[b].Elevation), Is.LessThanOrEqualTo(bound),
                    $"New terrain cliff seed={seed} sea={sea} index={a}->{b}");
                Assert.That(Math.Abs(samples[a].WaterLevel - samples[b].WaterLevel), Is.LessThanOrEqualTo(2 * distance + 0.00005),
                    $"Water cliff seed={seed} sea={sea} index={a}->{b}");
            }
            for (int z = 0; z < width - 1; z++)
            for (int x = 0; x < width - 1; x++)
            {
                int a = z * width + x;
                Edge(a, a + 1, 1); Edge(a, a + width, 1);
                Edge(a + 1, a + width, Math.Sqrt(2));
            }
        }

        [TestCase(1, 1)] [TestCase(-1, -1)] [TestCase(7, 7)] [TestCase(-9, -9)] [TestCase(86, 11)]
        public void BasinInfluencesMatchAcrossChunksAndRegionalBoundaries(int cx, int cz)
        {
            var world = new WorldDefinition(7);
            var generator = new EnvironmentGenerator(world, new EnvironmentDefinition());
            var terrain = new TerrainGenerator();
            EnvironmentChunkData Chunk(int x, int z) => generator.Generate(terrain.Generate(world, new ChunkCoordinate(x, z)));
            var north = Chunk(cx, cz + 1); var east = Chunk(cx + 1, cz); var center = Chunk(cx, cz);
            void Same(EnvironmentSample a, EnvironmentSample b)
            {
                Assert.That(a.Elevation, Is.EqualTo(b.Elevation)); Assert.That(a.WaterLevel, Is.EqualTo(b.WaterLevel));
                Assert.That(a.Temperature, Is.EqualTo(b.Temperature)); Assert.That(a.Humidity, Is.EqualTo(b.Humidity));
                Assert.That(a.Water, Is.EqualTo(b.Water)); Assert.That(a.Biome, Is.EqualTo(b.Biome));
            }
            for (int i = 0; i <= 32; i++)
            { Same(center[32, i], east[0, i]); Same(center[i, 32], north[i, 0]); }
        }

        [TestCase(1, "625E68D39AB86225980AD6C0E66E43DC8931DD65A79C81B816F49C6F14BF2412")]
        [TestCase(2, "EEC39C72DE01E9EFF470061869039E5229C733B62BB91895A3D8B1EF462A094D")]
        public void LegacySamplesAndResourcesKeepTheirPreFixFingerprints(int version, string expected)
        {
            using var stream = new MemoryStream(); using var writer = new BinaryWriter(stream);
            foreach (int seed in new[] { 7, 12345, -1 })
            foreach (var coord in new[] { new ChunkCoordinate(2, 1), new ChunkCoordinate(5, 5), new ChunkCoordinate(3, -5), new ChunkCoordinate(-1, -1) })
            {
                var world = new WorldDefinition(seed);
                var data = new EnvironmentGenerator(world, new EnvironmentDefinition(38, version)).Generate(new TerrainGenerator().Generate(world, coord));
                for (int z = 0; z < data.Width; z++)
                for (int x = 0; x < data.Width; x++)
                {
                    var a = data[x, z]; writer.Write(a.Elevation); writer.Write(a.WaterLevel);
                    writer.Write(a.Temperature); writer.Write(a.Humidity); writer.Write((int)a.Biome); writer.Write((int)a.Water);
                }
                writer.Write(data.Resources.Count);
                foreach (var r in data.Resources)
                {
                    writer.Write(r.Key.Seed); writer.Write(r.Key.Version); writer.Write(r.Key.X); writer.Write(r.Key.Z);
                    writer.Write((int)r.Kind); writer.Write(r.OffsetX); writer.Write(r.OffsetZ); writer.Write(r.Elevation);
                }
            }
            writer.Flush(); using var sha = SHA256.Create();
            Assert.That(BitConverter.ToString(sha.ComputeHash(stream.ToArray())).Replace("-", ""), Is.EqualTo(expected));
        }
    }
}
