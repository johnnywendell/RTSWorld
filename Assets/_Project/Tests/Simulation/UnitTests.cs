using System;
using System.Collections.Generic;
using NUnit.Framework;
using RTSWorld.Simulation;
using RTSWorld.World.Generation;
using RTSWorld.World.Environment;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace RTSWorld.Tests
{
    public sealed class UnitTests
    {
        private static ProceduralTerrainContract Terrain() => new ProceduralTerrainContract(new WorldDefinition(7));
        private static UnitSession Session(float step=0.125f) => new UnitSession(Terrain(), new int2(-64),new int2(64),step);

        [TestCase(100)] [TestCase(500)] [TestCase(1000)] [TestCase(5000)]
        public void BatchSpawnAttributesUniqueIdentityTicksAndDestroy(int count)
        {
            using(var session=Session())
            {
                var em=session.World.EntityManager;
                using(var a=session.SpawnBatch(UnitDefinition.Villager,3,count/2,new double3(-60,0,-60),new double2(1,0)))
                using(var b=session.SpawnBatch(UnitDefinition.Soldier,4,count-count/2,new double3(0,0,-60),new double2(1,0),1))
                {
                    var ids=new HashSet<ulong>();
                    foreach(var e in a) Validate(e,UnitDefinition.Villager,3);
                    foreach(var e in b) Validate(e,UnitDefinition.Soldier,4);
                    Assert.AreEqual(count,session.Count);
                    for(int i=0;i<8;i++) session.Advance(0.125f);
                    session.CompleteJobs();
                    foreach(var e in a) Assert.AreEqual(session.Tick,em.GetComponentData<EntitySimulationState>(e).Updates);
                    foreach(var e in b) Assert.AreEqual(session.Tick,em.GetComponentData<EntitySimulationState>(e).Updates);
                    ulong greatest=0; foreach(ulong id in ids) greatest=math.max(greatest,id);
                    session.DestroyBatch(a); session.DestroyBatch(b);
                    Assert.AreEqual(0,session.Count);
                    using(var replacement=session.SpawnBatch(UnitDefinition.Villager,0,1,double3.zero,double2.zero))
                        Assert.Greater(em.GetComponentData<LogicalEntityId>(replacement[0]).Value,greatest);
                    void Validate(Entity e,UnitDefinition definition,int owner)
                    {
                        Assert.IsTrue(ids.Add(em.GetComponentData<LogicalEntityId>(e).Value));
                        Assert.AreEqual(definition.Kind,em.GetComponentData<UnitIdentity>(e).Kind);
                        Assert.AreEqual(owner,em.GetComponentData<UnitIdentity>(e).Owner);
                        var hp=em.GetComponentData<UnitHealth>(e);
                        Assert.AreEqual(definition.Health,hp.Current); Assert.AreEqual(hp.Current,hp.Maximum);
                        var attributes=em.GetComponentData<UnitAttributes>(e);
                        Assert.AreEqual(definition.Speed,attributes.MaxSpeed);
                        Assert.AreEqual(definition.Attack,attributes.Attack);
                        Assert.AreEqual(definition.Defense,attributes.Defense);
                        Assert.AreEqual(definition.Range,attributes.Range);
                        Assert.AreEqual(definition.CarryCapacity,attributes.CarryCapacity);
                        Assert.AreEqual(quaternion.identity,em.GetComponentData<LogicalPose>(e).Rotation);
                        Assert.IsTrue(em.HasComponent<MovementVelocity>(e)); Assert.IsTrue(em.HasComponent<LogicalRegion>(e));
                    }
                }
            }
        }
        [Test]
        public void FixedStepPauseResumeSpeedClampBoundsAndOrientation()
        {
            using(var s=Session())
            using(var entities=s.SpawnBatch(UnitDefinition.Villager,0,1,new double3(63.8,0,0),new double2(100,0)))
            {
                Entity e=entities[0]; var em=s.World.EntityManager;
                s.Paused=true; s.Advance(100); Assert.AreEqual(0,s.Tick);
                s.Paused=false; s.Advance(0); s.CompleteJobs();
                Assert.AreEqual(64,em.GetComponentData<LogicalPose>(e).Position.x,1e-10);
                Assert.LessOrEqual(math.length(em.GetComponentData<MovementVelocity>(e).Value),2);
                Assert.AreEqual(new int2(2,0),em.GetComponentData<LogicalRegion>(e).Chunk);
                var rotation=em.GetComponentData<LogicalPose>(e).Rotation;
                Assert.Greater(math.dot(math.forward(rotation),new float3(1,0,0)),0.999);
                s.Advance(0.0625f); Assert.AreEqual(1,s.Tick);
                s.Paused=true; s.Advance(20); Assert.AreEqual(1,s.Tick);
                s.Paused=false; s.Advance(0.0625f); s.CompleteJobs(); Assert.AreEqual(2,s.Tick);
                Assert.AreEqual(UnitActivity.Idle,em.GetComponentData<UnitMotion>(e).Activity);
                s.SetIntention(e,new double2(-1,0)); s.Advance(0.125f); s.CompleteJobs();
                Assert.AreEqual(63.875,em.GetComponentData<LogicalPose>(e).Position.x,1e-10);
                Assert.AreEqual(UnitActivity.Moving,em.GetComponentData<UnitMotion>(e).Activity);
            }
        }
        [Test]
        public void SameTicksAndInputsProduceIdenticalResultsAcrossFramePartitions()
        {
            using(var a=Session()) using(var b=Session())
            using(var ea=a.SpawnBatch(UnitDefinition.Soldier,0,1,new double3(-1,0,-1),new double2(2,1)))
            using(var eb=b.SpawnBatch(UnitDefinition.Soldier,0,1,new double3(-1,0,-1),new double2(2,1)))
            {
                a.Advance(0); b.Advance(0);
                for(int i=0;i<8;i++) a.Advance(0.125f);
                for(int i=0;i<32;i++) b.Advance(0.03125f);
                a.CompleteJobs(); b.CompleteJobs();
                Assert.AreEqual(a.Tick,b.Tick);
                Assert.AreEqual(a.World.EntityManager.GetComponentData<LogicalPose>(ea[0]).Position,
                    b.World.EntityManager.GetComponentData<LogicalPose>(eb[0]).Position);
                Assert.AreEqual(a.World.EntityManager.GetComponentData<LogicalPose>(ea[0]).Rotation,
                    b.World.EntityManager.GetComponentData<LogicalPose>(eb[0]).Rotation);
            }
        }
        [TestCase(false)] [TestCase(true)]
        public void TerrainSnapshotMatchesContractAtSpawnAndAfterMotionIncludingEdges(bool environment)
        {
            var terrain=new ProceduralTerrainContract(new WorldDefinition(7),environment ? new EnvironmentDefinition(version:4) : null);
            using(var s=new UnitSession(terrain,new int2(-2),new int2(2),0.125f))
            using(var entities=s.SpawnBatch(UnitDefinition.Villager,0,1,new double3(-1.25,30,-0.75),new double2(2,1)))
            {
                var em=s.World.EntityManager; var e=entities[0];
                for(int i=0;i<20;i++)
                {
                    var p=em.GetComponentData<LogicalPose>(e).Position;
                    Assert.AreEqual(terrain.Elevation(p.x,p.z),p.y,1e-12);
                    s.Advance(0.125f); s.CompleteJobs();
                }
            }
        }
        [Test]
        public void MissingTerrainHoldsPoseAndUnavailableSnapshotCanResume()
        {
            using(var absent=new UnitSession(null,new int2(-2),new int2(2)))
            using(var ea=absent.SpawnBatch(UnitDefinition.Villager,0,1,new double3(0,42,0),new double2(1,0)))
            {
                absent.Advance(1); absent.CompleteJobs();
                var em=absent.World.EntityManager;
                Assert.AreEqual(new double3(0,42,0),em.GetComponentData<LogicalPose>(ea[0]).Position);
                Assert.AreEqual(UnitActivity.WaitingForTerrain,em.GetComponentData<UnitMotion>(ea[0]).Activity);
                Assert.Throws<InvalidOperationException>(()=>absent.SetTerrainAvailable(true));
            }
            using(var s=Session())
            using(var e=s.SpawnBatch(UnitDefinition.Villager,0,1,double3.zero,new double2(1,0)))
            {
                var em=s.World.EntityManager; var initial=em.GetComponentData<LogicalPose>(e[0]).Position;
                s.SetTerrainAvailable(false); s.Advance(0); s.CompleteJobs();
                Assert.AreEqual(initial,em.GetComponentData<LogicalPose>(e[0]).Position);
                s.SetTerrainAvailable(true); s.Advance(0.125f); s.CompleteJobs();
                Assert.Greater(em.GetComponentData<LogicalPose>(e[0]).Position.x,initial.x);
            }
        }
        [Test]
        public void InvalidInputsDoNotCreatePartialBatchesAndDisposeIsIdempotent()
        {
            using(var s=Session())
            {
                var invalid=UnitDefinition.Soldier; invalid.Health=float.NaN;
                Assert.Throws<ArgumentOutOfRangeException>(()=>s.SpawnBatch(invalid,0,1,double3.zero,double2.zero));
                Assert.Throws<ArgumentOutOfRangeException>(()=>s.SpawnBatch(UnitDefinition.Villager,0,1000,new double3(63,0,63),double2.zero));
                Assert.Throws<ArgumentOutOfRangeException>(()=>s.Advance(float.NaN));
                Assert.AreEqual(0,s.Count);
                s.Dispose(); Assert.IsFalse(s.World.IsCreated);
            }
            Assert.Throws<ArgumentOutOfRangeException>(()=>new UnitSession(null,new int2(0),new int2(257)));
        }
    }
}
