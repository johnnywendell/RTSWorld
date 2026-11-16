using System;
using NUnit.Framework;
using RTSWorld.World.Chunks;
using RTSWorld.World.Environment;
using RTSWorld.World.Generation;

namespace RTSWorld.World.Tests
{
    public sealed class ContainedHydrologyTests
    {
        // Eight float32 relative rounding units at elevation100. Covers float cast and
        // interpolation, not geometric errors. All coordinates/heights here are logical.
        const double HeightTolerance = .0001;
        static double Station(RiverReach r, double x,double z) =>
            ((x-r.AX)*(r.BX-r.AX)+(z-r.AZ)*(r.BZ-r.AZ))/(r.Length*r.Length);

        [TestCase(7,0)] [TestCase(7,38)] [TestCase(7,55)] [TestCase(7,100)]
        [TestCase(12345,0)] [TestCase(12345,38)] [TestCase(12345,55)]
        [TestCase(67890,0)] [TestCase(67890,38)] [TestCase(-1,0)] [TestCase(-1,38)]
        public void RiverProfilesAreDownhillGradeLimitedAndHaveNoTransverseTaper(int seed,float sea)
        {
            var generator=new EnvironmentGenerator(new WorldDefinition(seed),new EnvironmentDefinition(sea,4));
            int rivers=0,samples=0;
            for(int rz=-1;rz<=1;rz++)for(int rx=-1;rx<=1;rx++)
            {
                if(!generator.TryGetRiver(rx*256,rz*256,out var r))continue;
                rivers++;
                double previous=r.LevelAt(0);
                for(int i=1;i<=400;i++)
                {
                    double level=r.LevelAt(i/400.0),interval=r.Length/400;
                    Assert.That(level,Is.LessThanOrEqualTo(previous+1e-10));
                    Assert.That(previous-level,Is.LessThanOrEqualTo(.25*interval+1e-10));
                    previous=level;
                }
                for(int i=8;i<=32;i++)
                {
                    double t=i/40.0;r.PointAt(t,out double x,out double z);
                    double nx=-(r.BZ-r.AZ)/r.Length,nz=(r.BX-r.AX)/r.Length;
                    foreach(int offset in new[]{-3,0,3})
                    {
                        long sx=(long)Math.Round(x+nx*offset),sz=(long)Math.Round(z+nz*offset);
                        var sample=generator.Sample(sx,sz);
                        Assert.That(sample.Elevation,Is.LessThan(sample.WaterLevel),$"Dry center/cross section {seed}/{sea}/{sx}/{sz}");
                        Assert.That(sample.WaterLevel,Is.EqualTo(r.LevelAt(Station(r,sx,sz))).Within(HeightTolerance),
                            $"Transverse tilt seed={seed} sea={sea} p={sx},{sz} offset={offset}");
                        samples++;
                    }
                }
            }
            if(sea<55){Assert.That(rivers,Is.GreaterThan(0));Assert.That(samples,Is.GreaterThan(0));}
        }

        [TestCase(7,0)] [TestCase(7,38)] [TestCase(12345,38)] [TestCase(67890,55)] [TestCase(-1,0)]
        public void LakesAreLevelAndWaterOwnershipChangesOnlyAcrossDryCells(int seed,float sea)
        {
            var world=new WorldDefinition(seed);
            var generator=new EnvironmentGenerator(world,new EnvironmentDefinition(sea,4));
            var terrain=new TerrainGenerator();
            // Includes original endpoint wall, negative coordinates and region boundaries.
            foreach(var chunk in new[]{new ChunkCoordinate(2,1),new ChunkCoordinate(-5,11),new ChunkCoordinate(-6,9),new ChunkCoordinate(5,5),new ChunkCoordinate(-1,-1)})
            {
                var data=generator.Generate(terrain.Generate(world,chunk));
                var fresh=new EnvironmentGenerator(world,new EnvironmentDefinition(sea,4)).Generate(terrain.Generate(world,chunk));
                for(int z=0;z<=32;z++)for(int x=0;x<=32;x++)
                {
                    var a=data[x,z];Assert.That(a,Is.EqualTo(fresh[x,z]));
                    Assert.That(a.Elevation,Is.LessThanOrEqualTo(data.BaseTerrain[x,z].Elevation));
                    if(a.Water==WaterKind.Ocean)Assert.That(a.WaterLevel,Is.EqualTo(sea));
                    if(x==32 || z==32)continue;
                    // The same two fixed triangles as presentation. A field-domain jump
                    // is legal only if the WHOLE source triangle is dry, so clipping cannot
                    // interpolate it into an artificial waterfall/wall.
                    void Triangle(EnvironmentSample p,EnvironmentSample q,EnvironmentSample r)
                    {
                        if(p.Elevation>=p.WaterLevel && q.Elevation>=q.WaterLevel && r.Elevation>=r.WaterLevel)return;
                        double range=Math.Max(p.WaterLevel,Math.Max(q.WaterLevel,r.WaterLevel))-
                            Math.Min(p.WaterLevel,Math.Min(q.WaterLevel,r.WaterLevel));
                        Assert.That(range,Is.LessThanOrEqualTo(.25*Math.Sqrt(2)+HeightTolerance),$"Wet field jump {seed}/{sea}/{chunk}/{x}/{z}");
                        if(p.Water==WaterKind.Lake && q.Water==WaterKind.Lake && r.Water==WaterKind.Lake)
                            Assert.That(range,Is.LessThanOrEqualTo(HeightTolerance));
                    }
                    Triangle(a,data[x,z+1],data[x+1,z]);Triangle(data[x+1,z],data[x,z+1],data[x+1,z+1]);
                }
                CollectionAssert.AreEqual(data.Resources,fresh.Resources);
            }
        }

        [Test]
        public void ReportedCameraRegionAndSeedSevenEndpointRemoveLegacyDeformationsWithoutRaisingTerrain()
        {
            var world=new WorldDefinition(7);var terrain=new TerrainGenerator();
            var legacy=new EnvironmentGenerator(world,new EnvironmentDefinition(38,3));
            var current=new EnvironmentGenerator(world,new EnvironmentDefinition(38,4));
            Assert.That(legacy.Sample(-153,357).WaterLevel,Is.EqualTo(45.682783f).Within(.00001));
            Assert.That(legacy.Sample(-150,360).WaterLevel,Is.EqualTo(44.81827f).Within(.00001));
            foreach(var point in new[]{(-153,357),(-150,360)})
            {
                var s=current.Sample(point.Item1,point.Item2);
                Assert.That(s.Elevation,Is.LessThanOrEqualTo(terrain.SampleElevation(world,point.Item1,point.Item2)),
                    "Contained banks may be cut, but cannot construct raised ridges");
                Assert.That(s.Water,Is.EqualTo(WaterKind.None),"Former uncontained tilted sheets must be dry, not a sideways waterfall");
            }
            Assert.That(Math.Abs(current.Sample(64,59).Elevation-current.Sample(64,60).Elevation),Is.LessThan(2.25));
        }
    }
}
