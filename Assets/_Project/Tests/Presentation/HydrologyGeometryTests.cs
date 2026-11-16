using NUnit.Framework;
using RTSWorld.Presentation.Environment;
using RTSWorld.World.Chunks;
using RTSWorld.World.Environment;
using RTSWorld.World.Generation;
using UnityEngine;

namespace RTSWorld.Presentation.Tests
{
    public sealed class HydrologyGeometryTests
    {
        [TestCase(7,86,11,38,2,1.2f)] [TestCase(7,-5,11,38,2,1.2f)] [TestCase(7,2,1,38,2,1.2f)]
        [TestCase(7,5,5,0,.5f,3)] [TestCase(12345,3,-5,38,4,.75f)]
        [TestCase(67890,5,5,55,2,1.2f)] [TestCase(-1,-1,-1,0,2,1.2f)]
        public void RenderedWaterHasValidUpwardTrianglesAndOnlyLongitudinalGrades(int seed,int cx,int cz,float sea,float horizontal,float vertical)
        {
            var world=new WorldDefinition(seed);
            var data=new EnvironmentGenerator(world,new EnvironmentDefinition(sea,4))
                .Generate(new TerrainGenerator().Generate(world,new ChunkCoordinate(cx,cz)));
            var mesh=EnvironmentMeshBuilder.Build(data,horizontal,vertical,EnvironmentGeometryView.WaterOnly);
            try
            {
                var vertices=mesh.vertices;var indices=mesh.triangles;
                // Height error allowance: 8 float32 relative rounding units at max scaled
                // elevation100. Grid reconstruction can combine two longitudinal pieces,
                // so its 2D gradient bound is sqrt(2)*.25, not a tuned visual threshold.
                double tolerance=8*1.1920928955078125e-7*100*vertical;
                double grade=.25*System.Math.Sqrt(2)*vertical/horizontal;
                for(int i=0;i<indices.Length;i+=3)
                {
                    foreach(int index in new[]{indices[i],indices[i+1],indices[i+2]})Assert.That(index,Is.InRange(0,vertices.Length-1));
                    var a=vertices[indices[i]];var b=vertices[indices[i+1]];var c=vertices[indices[i+2]];
                    double ny=((double)b.z-a.z)*(c.x-a.x)-((double)b.x-a.x)*(c.z-a.z);
                    Assert.That(ny,Is.GreaterThan(0),$"Vertical, inverted or degenerate water triangle {i/3}");
                    void Edge(Vector3 p,Vector3 q)
                    {
                        double dx=(double)p.x-q.x,dz=(double)p.z-q.z;
                        Assert.That(System.Math.Abs((double)p.y-q.y),Is.LessThanOrEqualTo(grade*System.Math.Sqrt(dx*dx+dz*dz)+tolerance),
                            $"Artificial water wall {seed}/{cx}/{cz}/triangle{i/3}");
                    }
                    Edge(a,b);Edge(b,c);Edge(c,a);
                }
            }
            finally { Object.DestroyImmediate(mesh); }
        }
    }
}
