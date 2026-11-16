using System.Collections;
using NUnit.Framework;
using RTSWorld.Presentation.Environment;
using RTSWorld.Presentation.Streaming;
using RTSWorld.World.Chunks;
using UnityEngine;
using UnityEngine.TestTools;

namespace RTSWorld.Streaming.PlayMode.Tests
{
    public sealed class HydrologyStreamingTests
    {
        [UnityTest]
        public IEnumerator VersionFourReconstructsTheNegativeCameraRegionAfterUnload()
        {
            var root=new GameObject("FIX003 negative streaming");var observer=new GameObject("FIX003 observer");
            var material=new Material(Shader.Find("RTSWorld/Environment Colors"));
            try
            {
                observer.transform.position=new Vector3(-288,0,736);
                var layer=root.AddComponent<EnvironmentPresentation>();layer.Configure(material,38,4);
                var manager=root.AddComponent<ChunkManager>();
                manager.Configure(observer.transform,material,7,32,2,1.2f,2,8,50);manager.SetEnvironment(layer);
                IEnumerator Drain()
                {
                    int guard=0;
                    do { yield return null;Assert.That(++guard,Is.LessThan(200)); }
                    while(manager.PendingCount!=0 || manager.ActiveCount!=25);
                }
                yield return Drain();
                var coordinate=new ChunkCoordinate(-5,11);
                Assert.That(manager.TryGetMesh(coordinate,out var original),Is.True);
                var vertices=original.vertices;var normals=original.normals;var indices=original.triangles;var colors=original.colors;
                observer.transform.position=new Vector3(640,0,-640);yield return Drain();
                Assert.That(original==null,Is.True);
                observer.transform.position=new Vector3(-288,0,736);yield return Drain();
                Assert.That(manager.TryGetMesh(coordinate,out var rebuilt),Is.True);
                CollectionAssert.AreEqual(vertices,rebuilt.vertices);CollectionAssert.AreEqual(normals,rebuilt.normals);
                CollectionAssert.AreEqual(indices,rebuilt.triangles);CollectionAssert.AreEqual(colors,rebuilt.colors);
                long generated=manager.TotalGenerated;yield return null;yield return null;
                Assert.That(manager.TotalGenerated,Is.EqualTo(generated));
                manager.enabled=false;yield return null;Assert.That(root.transform.childCount,Is.Zero);
            }
            finally { Object.Destroy(root);Object.Destroy(observer);Object.Destroy(material); }
        }
    }
}
