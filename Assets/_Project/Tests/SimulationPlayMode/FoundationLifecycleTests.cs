using System.Collections;
using NUnit.Framework;
using RTSWorld.Simulation;
using UnityEngine;
using UnityEngine.TestTools;

namespace RTSWorld.Tests
{
    public class FoundationLifecycleTests
    {
        [UnityTest]
        public IEnumerator DemoTicksAndReleasesItsWorldOnDisable()
        {
            var go = new GameObject("ECS lifecycle test");
            try
            {
                var demo = go.AddComponent<FoundationDemo>();
                var world = demo.Session.World;
                Assert.AreEqual(256, demo.Session.Count);
                yield return new WaitForSeconds(0.2f);
                demo.Session.CompleteJobs();
                Assert.Greater(demo.Session.Tick, 1UL);
                go.SetActive(false);
                Assert.IsFalse(world.IsCreated);
                go.SetActive(true);
                Assert.AreEqual(256, demo.Session.Count);
                Assert.AreEqual(0UL, demo.Session.Tick);
            }
            finally { Object.DestroyImmediate(go); }
        }
    }
}
