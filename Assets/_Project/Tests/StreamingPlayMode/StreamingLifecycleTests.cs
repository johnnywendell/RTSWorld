using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using RTSWorld.Presentation.Streaming;
using RTSWorld.World.Chunks;
using UnityEngine;
using UnityEngine.TestTools;

namespace RTSWorld.Streaming.PlayMode.Tests
{
    public sealed class StreamingLifecycleTests
    {
        [UnityTest]
        public IEnumerator RouteKeepsNativeMeshCountBoundedAndReportsTimings()
        {
            int baseline = Resources.FindObjectsOfTypeAll<Mesh>().Count(m => m.name.StartsWith("Stream chunk "));
            var root = new GameObject("Measured route");
            var observer = new GameObject("Measured observer");
            var material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            var times = new List<double>();
            double maxFrameMs = 0;
            try
            {
                var manager = root.AddComponent<ChunkManager>();
                manager.Configure(observer.transform, material, cells: 32, radius: 1, maxPerFrame: 1, milliseconds: 4);
                for (int waypoint = 0; waypoint < 12; waypoint++)
                {
                    observer.transform.position = new Vector3((waypoint - 6) * 64, 0, (waypoint % 3 - 1) * 64);
                    int frames = 0;
                    do
                    {
                        yield return null;
                        times.Add(manager.LastStepMilliseconds);
                        maxFrameMs = System.Math.Max(maxFrameMs, Time.unscaledDeltaTime * 1000.0);
                        Assert.That(manager.LastGeneratedCount, Is.LessThanOrEqualTo(1));
                        Assert.That(++frames, Is.LessThan(300), "Streaming did not converge.");
                    } while (manager.PendingCount > 0 || manager.ActiveCount != 9);
                    yield return null; // Allow deferred native Destroy to finish.
                    Assert.That(root.GetComponentsInChildren<MeshFilter>().Length, Is.EqualTo(9));
                    Assert.That(Resources.FindObjectsOfTypeAll<Mesh>().Count(m => m.name.StartsWith("Stream chunk ")),
                        Is.EqualTo(baseline + 9), "Resident native meshes must not grow with distance travelled.");
                }
                long generated = manager.TotalGenerated;
                for (int i = 0; i < 10; i++) yield return null;
                Assert.That(manager.TotalGenerated, Is.EqualTo(generated));
                times.Sort();
                TestContext.WriteLine($"WORLD003_ROUTE samples={times.Count} stepMedianMs={times[times.Count / 2]:F4} " +
                    $"stepP95Ms={times[(int)((times.Count - 1) * 0.95)]:F4} stepMaxMs={times.Last():F4} " +
                    $"maxObservedFrameMs={maxFrameMs:F4} generated={generated} unloaded={manager.TotalUnloaded} " +
                    "waypoints=12 cells=32 radius=1 maxPerFrame=1 budgetMs=4 nativeMeshes=9");
                manager.enabled = false;
                yield return null;
                Assert.That(Resources.FindObjectsOfTypeAll<Mesh>().Count(m => m.name.StartsWith("Stream chunk ")), Is.EqualTo(baseline));
            }
            finally { Object.Destroy(root); Object.Destroy(observer); Object.Destroy(material); }
        }

        [UnityTest]
        public IEnumerator NativeResourcesAreDestroyedAfterTransitionAndDisable()
        {
            var root = new GameObject("Play streaming test");
            var observer = new GameObject("Observer");
            var material = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            try
            {
                var manager = root.AddComponent<ChunkManager>();
                manager.Configure(observer.transform, material, cells: 8, radius: 0, maxPerFrame: 1);
                manager.Step();
                manager.TryGetMesh(new ChunkCoordinate(0, 0), out var original);
                observer.transform.position = new Vector3(-32, 0, 0);
                manager.Step();
                Assert.That(manager.ActiveCount, Is.EqualTo(1));
                yield return null;
                Assert.That(original == null, Is.True, "Unload must destroy the native mesh after the frame.");
                Assert.That(root.GetComponentsInChildren<MeshRenderer>().Length, Is.EqualTo(1));
                var remaining = root.GetComponentsInChildren<MeshFilter>().Select(f => f.sharedMesh).ToArray();
                manager.enabled = false;
                Assert.That(manager.ActiveCount, Is.EqualTo(0));
                yield return null;
                foreach (var mesh in remaining) Assert.That(mesh == null, Is.True);
                Assert.That(root.transform.childCount, Is.EqualTo(0));
            }
            finally
            {
                Object.Destroy(root); Object.Destroy(observer); Object.Destroy(material);
            }
        }
    }
}
