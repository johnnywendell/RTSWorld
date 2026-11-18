using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using RTSWorld.Simulation;
using RTSWorld.Units.Presentation;
using RTSWorld.Presentation.Streaming;
using RTSWorld.World.Chunks;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace RTSWorld.Tests
{
    public sealed class UnitDemoTests
    {
        [Serializable] public sealed class LoadRow
        {
            public int population, samples, batches;
            public double simulationMeanMs, simulationMedianMs, simulationP95Ms, simulationMaxMs;
            public double frameMeanMs, frameP95Ms, fps;
            public long ticks;
        }
        [Serializable] public sealed class LoadReport
        {
            public string unity, platform, processor, graphics, utc;
            public int warmupFrames=15, measuredFrames=60;
            public List<LoadRow> rows=new List<LoadRow>();
        }
        [UnityTest]
        public IEnumerator LoadScenariosRenderPauseResumeAndReleaseResources()
        {
            var go=new GameObject("Unit load test");
            var report=new LoadReport { unity=Application.unityVersion,platform=Application.platform.ToString(),
                processor=SystemInfo.processorType,graphics=SystemInfo.graphicsDeviceName,utc=DateTime.UtcNow.ToString("O") };
            Unity.Entities.World owned=null;
            try
            {
                var demo=go.AddComponent<UnitDemo>();
                foreach(int count in new[]{100,500,1000,5000})
                {
                    demo.Rebuild(count); owned=demo.Session.World;
                    Assert.AreEqual(count,demo.Session.Count);
                    Assert.AreEqual(count/2,demo.Villagers); Assert.AreEqual(count/2,demo.Soldiers);
                    for(int i=0;i<report.warmupFrames;i++) yield return null;
                    var sim=new List<double>(); var frame=new List<double>();
                    for(int i=0;i<report.measuredFrames;i++)
                    {
                        yield return null;
                        sim.Add(demo.SimulationMilliseconds); frame.Add(Time.unscaledDeltaTime*1000.0);
                        Assert.AreEqual(count,(int)demo.UpdatedEntities);
                        Assert.AreEqual(2*(int)Math.Ceiling(count/2.0/1023),demo.DrawBatches);
                    }
                    sim.Sort(); frame.Sort();
                    report.rows.Add(new LoadRow { population=count,samples=sim.Count,batches=demo.DrawBatches,
                        simulationMeanMs=sim.Average(),simulationMedianMs=sim[sim.Count/2],simulationP95Ms=sim[(int)((sim.Count-1)*.95)],
                        simulationMaxMs=sim.Last(),frameMeanMs=frame.Average(),frameP95Ms=frame[(int)((frame.Count-1)*.95)],
                        fps=1000/frame.Average(),ticks=(long)demo.Session.Tick });
                    ulong tick=demo.Session.Tick;
                    demo.Session.Paused=true;
                    yield return null; yield return null;
                    Assert.AreEqual(tick,demo.Session.Tick);
                    demo.Session.Paused=false;
                    // Presentation residency has no authority over unit lifetime.
                    demo.SetVisible(false);
                    yield return new WaitForSeconds(.15f);
                    Assert.Greater(demo.Session.Tick,tick);
                    Assert.AreEqual(count,demo.Session.Count);
                    Assert.AreEqual(0,demo.DrawBatches);
                    demo.SetVisible(true);
                }
                go.SetActive(false); Assert.IsFalse(owned.IsCreated);
                yield return null;
                Assert.IsFalse(Resources.FindObjectsOfTypeAll<Mesh>().Any(m=>m.name=="Unit shared box"));
                string directory=Path.Combine(Application.dataPath,"../docs/validation/ECS-002");
                Directory.CreateDirectory(directory);
                File.WriteAllText(Path.Combine(directory,"load-editor.json"),JsonUtility.ToJson(report,true));
                TestContext.WriteLine(JsonUtility.ToJson(report));
            }
            finally { Object.DestroyImmediate(go); }
        }
        [UnityTest]
        public IEnumerator RealVisualChunkUnloadPreservesLogicalUnits()
        {
            var root=new GameObject("Unit streaming lifetime test");
            var observer=new GameObject("Unit observer");
            var material=new Material(Shader.Find("Universal Render Pipeline/Lit"));
            var demoRoot=new GameObject("Independent units");
            try
            {
                var demo=demoRoot.AddComponent<UnitDemo>();
                var manager=root.AddComponent<ChunkManager>();
                manager.Configure(observer.transform,material,cells:8,radius:0,maxPerFrame:1);
                manager.Step();
                Assert.IsTrue(manager.TryGetMesh(new ChunkCoordinate(0,0),out var original));
                ulong tick=demo.Session.Tick;
                observer.transform.position=new Vector3(-32,0,0); manager.Step();
                yield return new WaitForSeconds(.15f);
                Assert.IsTrue(original==null);
                Assert.Greater(manager.TotalUnloaded,0);
                Assert.AreEqual(1000,demo.Session.Count);
                Assert.Greater(demo.Session.Tick,tick);
            }
            finally { Object.DestroyImmediate(root);Object.DestroyImmediate(observer);Object.DestroyImmediate(material);Object.DestroyImmediate(demoRoot); }
        }
    }
}
