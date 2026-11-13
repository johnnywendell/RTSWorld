using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using RTSWorld.Presentation.Environment;
using RTSWorld.Presentation.Streaming;
using UnityEngine;
using UnityEngine.TestTools;

namespace RTSWorld.Streaming.PlayMode.Tests
{
    public sealed class EnvironmentLifecycleTests
    {
        [UnityTest]
        public IEnumerator EnvironmentStreamingReleasesWaterAndResourcesAndReportsCost()
        {
            int baseline = Resources.FindObjectsOfTypeAll<Mesh>().Count(m => m.name.StartsWith("Stream chunk "));
            var root = new GameObject("Environment lifecycle"); var observer = new GameObject("Observer");
            var material = new Material(Shader.Find("RTSWorld/Environment Colors"));
            var times = new List<double>();
            try
            {
                var layer = root.AddComponent<EnvironmentPresentation>(); layer.Configure(material);
                var manager = root.AddComponent<ChunkManager>();
                manager.Configure(observer.transform, material, cells: 32, radius: 1, maxPerFrame: 1, milliseconds: 4);
                manager.SetEnvironment(layer);
                for (int waypoint = 0; waypoint < 8; waypoint++)
                {
                    observer.transform.position = new Vector3((waypoint - 4) * 64, 0, (waypoint % 3 - 1) * 64);
                    int guard = 0;
                    do
                    {
                        yield return null;
                        times.Add(manager.LastStepMilliseconds);
                        Assert.That(manager.LastGeneratedCount, Is.LessThanOrEqualTo(1));
                        Assert.That(++guard, Is.LessThan(300));
                    } while (manager.ActiveCount != 9 || manager.PendingCount != 0);
                    yield return null;
                    Assert.That(root.transform.childCount, Is.EqualTo(9));
                    Assert.That(Resources.FindObjectsOfTypeAll<Mesh>().Count(m => m.name.StartsWith("Stream chunk ")), Is.EqualTo(baseline + 9));
                    Assert.That(root.GetComponentsInChildren<MeshFilter>().All(f => f.sharedMesh.colors.Length == f.sharedMesh.vertexCount), Is.True);
                }
                var old = root.GetComponentsInChildren<MeshFilter>().Select(f => f.sharedMesh).ToArray();
                long generated = manager.TotalGenerated;
                for (int i = 0; i < 5; i++) yield return null;
                Assert.That(manager.TotalGenerated, Is.EqualTo(generated));
                layer.Configure(material, 55); manager.Seed = 67890;
                yield return null;
                for (int i = 0; i < 30 && manager.PendingCount > 0; i++) yield return null;
                yield return null;
                foreach (var mesh in old) Assert.That(mesh == null, Is.True);
                Assert.That(manager.ActiveCount, Is.EqualTo(9));
                times.Sort();
                TestContext.WriteLine($"WORLD004_ROUTE samples={times.Count} medianMs={times[times.Count / 2]:F4} " +
                    $"p95Ms={times[(int)((times.Count - 1) * 0.95)]:F4} maxMs={times.Last():F4} " +
                    "waypoints=8 cells=32 radius=1 maxPerFrame=1 budgetMs=4 sea=38 environmentVersion=1 meshes=9");
                manager.enabled = false;
                yield return null;
                Assert.That(root.transform.childCount, Is.EqualTo(0));
                Assert.That(Resources.FindObjectsOfTypeAll<Mesh>().Count(m => m.name.StartsWith("Stream chunk ")), Is.EqualTo(baseline));
            }
            finally { Object.Destroy(root); Object.Destroy(observer); Object.Destroy(material); }
        }
    }
}
