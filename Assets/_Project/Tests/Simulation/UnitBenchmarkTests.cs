using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using NUnit.Framework;
using RTSWorld.Simulation;
using RTSWorld.World.Generation;
using Unity.Mathematics;
using UnityEngine;

namespace RTSWorld.Tests
{
    public sealed class UnitBenchmarkTests
    {
        [TestCase(100)] [TestCase(500)] [TestCase(1000)] [TestCase(5000)]
        public void ProfileCompletedFixedTicksWithoutRendering(int count)
        {
            const int warmup=16, samples=120;
            var stopwatch=Stopwatch.StartNew();
            using(var session=new UnitSession(new ProceduralTerrainContract(new WorldDefinition(7)),new int2(-64),new int2(64)))
            {
                double setupMs=stopwatch.Elapsed.TotalMilliseconds;
                stopwatch.Restart();
                using(session.SpawnBatch(UnitDefinition.Villager,0,count/2,new double3(-60,0,-60),new double2(0,2))) { }
                using(session.SpawnBatch(UnitDefinition.Soldier,1,count-count/2,new double3(0,0,-60),new double2(0,3),1.1)) { }
                double spawnMs=stopwatch.Elapsed.TotalMilliseconds;
                session.Advance(0);session.CompleteJobs();
                for(int i=0;i<warmup;i++){ session.Advance(session.FixedDeltaTime);session.CompleteJobs(); }
                var csv=new System.Text.StringBuilder("sample,tick,simulation_sync_ms\n");
                double sum=0;
                for(int i=0;i<samples;i++)
                {
                    ulong before=session.Tick;
                    stopwatch.Restart();session.Advance(session.FixedDeltaTime);session.CompleteJobs();stopwatch.Stop();
                    double ms=stopwatch.Elapsed.TotalMilliseconds;sum+=ms;
                    Assert.AreEqual(before+1,session.Tick,"Every measurement must contain exactly one completed fixed tick.");
                    csv.AppendLine(string.Format(CultureInfo.InvariantCulture,"{0},{1},{2:R}",i,session.Tick,ms));
                }
                Assert.AreEqual(count,session.Count);
                string directory=Path.Combine(Application.dataPath,"../docs/validation/ECS-002");Directory.CreateDirectory(directory);
                File.WriteAllText(Path.Combine(directory,$"ticks-{count}.csv"),csv.ToString());
                File.WriteAllText(Path.Combine(directory,$"setup-{count}.txt"),
                    string.Format(CultureInfo.InvariantCulture,"seed=7 terrainVersion=1 step={0:R} bounds=[-64,64] warmup={1} samples={2} setupMs={3:R} spawnMs={4:R}\n",
                        session.FixedDeltaTime,warmup,samples,setupMs,spawnMs));
                TestContext.WriteLine($"ECS002_FIXED population={count} samples={samples} completedTickMeanMs={sum/samples:F5}");
            }
        }
    }
}
