using System.Linq;
using NUnit.Framework;
using RTSWorld.Simulation;
using RTSWorld.World.Chunks;
using RTSWorld.World.Environment;
using RTSWorld.World.Generation;
using Unity.Mathematics;

namespace RTSWorld.Tests
{
    public sealed class IntegratedWorldTests
    {
        [TestCase(-5, 11)]
        [TestCase(2, 2)]
        public void LogicalSnapshotAgreesWithEnvironmentalTrianglesAndResourceCatalogue(int chunkX, int chunkZ)
        {
            var terrain = new ProceduralTerrainContract(new WorldDefinition(7), new EnvironmentDefinition(38, 4));
            var coordinate = new ChunkCoordinate(chunkX, chunkZ);
            var data = terrain.GetEnvironmentChunk(coordinate);
            var repeated = terrain.GetEnvironmentChunk(coordinate);
            Assert.That(data.Resources.Select(x => x.Key), Is.EqualTo(repeated.Resources.Select(x => x.Key)));
            Assert.That(data.Resources.Select(x => x.Key).Distinct().Count(), Is.EqualTo(data.Resources.Count));
            foreach (var resource in data.Resources)
            {
                Assert.That(terrain.Region(resource.Key.X + resource.OffsetX, resource.Key.Z + resource.OffsetZ), Is.EqualTo(coordinate));
                Assert.That(terrain.Elevation(resource.Key.X + resource.OffsetX, resource.Key.Z + resource.OffsetZ),
                    Is.EqualTo(resource.Elevation).Within(.00002));
            }

            var min = new int2(chunkX, chunkZ) * 32;
            var grid = new NavigationGrid(terrain, min, min + 32, 2.25, captureSurface: true);
            int wet = 0, dryPassable = 0;
            for (int z = 0; z < 32; z++)
                for (int x = 0; x < 32; x++)
                {
                    var cell = min + new int2(x, z);
                    var a = data[x, z];
                    var b = data[x + 1, z];
                    var c = data[x, z + 1];
                    var d = data[x + 1, z + 1];
                    Assert.That(grid.TryGetSurface(cell.x + .5, cell.y + .5, out double height), Is.True);
                    Assert.That(height, Is.EqualTo(math.max(((double)b.Elevation + c.Elevation) * .5,
                        ((double)b.WaterLevel + c.WaterLevel) * .5)).Within(.000001));
                    grid.TryGetCost(cell, out int cost);
                    if (a.WaterLevel > a.Elevation || b.WaterLevel > b.Elevation ||
                        c.WaterLevel > c.Elevation || d.WaterLevel > d.Elevation)
                    {
                        wet++;
                        Assert.That(cost, Is.Zero);
                    }
                    else
                    {
                        double steepest = math.max(
                            math.length(new double2(b.Elevation - a.Elevation, c.Elevation - a.Elevation)),
                            math.length(new double2(d.Elevation - c.Elevation, d.Elevation - b.Elevation)));
                        Assert.That(cost > 0, Is.EqualTo(steepest <= 2.25));
                        if (cost > 0) dryPassable++;
                    }
                }

            Assert.That(dryPassable + wet, Is.GreaterThan(0));
            if (chunkX < 0) Assert.That(wet, Is.GreaterThan(0));
        }
    }
}
