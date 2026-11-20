using System.Linq;
using NUnit.Framework;
using RTSWorld.Presentation.Environment;
using RTSWorld.World.Chunks;
using RTSWorld.World.Environment;
using RTSWorld.World.Generation;
using UnityEngine;

namespace RTSWorld.Presentation.Tests
{
    public sealed class EnvironmentArtTests
    {
        [Test]
        public void ArtIsRepeatableAndResourcesKeepTheirFootprintAcrossNegativeChunks()
        {
            var world = new WorldDefinition(7, cellsPerChunk: 32);
            var generator = new EnvironmentGenerator(world,new EnvironmentDefinition());
            var terrain = new TerrainGenerator();
            var first = new ChunkCoordinate(-5, 11);
            var second = new ChunkCoordinate(-4, 11);
            var a = Get(first);
            var b = Get(second);
            var secondFirst = Get(second);
            var firstSecond = Get(first);
            CollectionAssert.AreEqual(a, firstSecond);
            CollectionAssert.AreEqual(b, secondFirst);
            Assert.That(a.Length, Is.Positive);
            Assert.That(a.Any(p => p.Decorative), Is.True);
            Assert.That(a.Concat(b).Select(p => p.Cell).Distinct().Count(), Is.EqualTo(a.Length + b.Length));

            EnvironmentArtPlacement[] Get(ChunkCoordinate coordinate)
            {
                var chunk = generator.Generate(terrain.Generate(world, coordinate));
                var resources = chunk.Resources.ToArray();
                var art = EnvironmentArtDistribution.Build(chunk, resources);
                Assert.That(art.Count(p => !p.Decorative), Is.EqualTo(resources.Length));
                for (int i = 0; i < resources.Length; i++)
                {
                    Assert.That(art[i].Cell, Is.EqualTo(resources[i].Key));
                    Assert.That(art[i].Elevation, Is.EqualTo(resources[i].Elevation));
                    Assert.That(art[i].OffsetX, Is.EqualTo(resources[i].OffsetX));
                    Assert.That(art[i].OffsetZ, Is.EqualTo(resources[i].OffsetZ));
                }
                return art;
            }
        }

        [Test]
        public void TerrainAndShorelineKeepTheExactPreviousTopology()
        {
            var world = new WorldDefinition(7, cellsPerChunk: 32);
            var generator = new EnvironmentGenerator(world,new EnvironmentDefinition());
            var data = generator.Generate(new TerrainGenerator().Generate(world, new ChunkCoordinate(-5, 11)));
            var previous = EnvironmentMeshBuilder.Build(data, 2, 1.2f, includeResource: _ => false, instanceTreesAndRocks: true);
            var current = EnvironmentMeshBuilder.Build(data, 2, 1.2f, includeResource: _ => false, instanceTreesAndRocks: true, artProfile:true);
            try
            {
                CollectionAssert.AreEqual(previous.vertices, current.vertices);
                CollectionAssert.AreEqual(previous.triangles, current.triangles);
                Assert.That(current.colors.Any(c => c.a < .5f), Is.True);
                Assert.That(current.colors.Any(c => c.a > .89f && c.a < .91f), Is.True);
                Assert.That(current.colors.Distinct().Count(), Is.GreaterThan(previous.colors.Distinct().Count()));
            }
            finally
            {
                Object.DestroyImmediate(previous);
                Object.DestroyImmediate(current);
            }
        }
    }
}
