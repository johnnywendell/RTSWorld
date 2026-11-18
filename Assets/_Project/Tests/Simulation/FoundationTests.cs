using System;
using NUnit.Framework;
using RTSWorld.Simulation;
using RTSWorld.World.Generation;
using RTSWorld.World.Environment;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;

namespace RTSWorld.Tests
{
    public class FoundationTests
    {
        [Test]
        public void LifecycleInitializesUnmanagedDataAndDisposesWorld()
        {
            var s = new FoundationSession();
            var world = s.World;
            var e = s.Spawn(42, new double3(-32.5, 10, 32), new double3(1, 0, 0));
            Assert.AreEqual(1, s.Count);
            Assert.AreEqual(42, world.EntityManager.GetComponentData<LogicalEntityId>(e).Value);
            Assert.AreEqual(quaternion.identity, world.EntityManager.GetComponentData<LogicalPose>(e).Rotation);
            Assert.AreEqual(new int2(-2, 1), world.EntityManager.GetComponentData<LogicalRegion>(e).Chunk);
            Assert.AreEqual(0, world.EntityManager.GetComponentData<EntitySimulationState>(e).Updates);
            world.EntityManager.DestroyEntity(e);
            Assert.AreEqual(0, s.Count);
            s.Dispose();
            Assert.IsFalse(world.IsCreated);
        }

        [Test]
        public void FixedGroupAccumulatesAndJobsMoveAcrossNegativeChunkBoundary()
        {
            using (var s = new FoundationSession(0.125f))
            {
                var e = s.Spawn(1, new double3(-0.2, 10, 0), new double3(1, 0, 0));
                s.Advance(0); // Entities catch-up manager initializes tick zero.
                Assert.AreEqual(1, s.Tick);
                s.Advance(0.0625f);
                Assert.AreEqual(1, s.Tick);
                s.Advance(0.0625f);
                s.CompleteJobs();
                Assert.AreEqual(2, s.Tick);
                var manager = s.World.EntityManager;
                Assert.AreEqual(0.05, manager.GetComponentData<LogicalPose>(e).Position.x, 1e-12);
                Assert.AreEqual(10, manager.GetComponentData<LogicalPose>(e).Position.y);
                Assert.AreEqual(2, manager.GetComponentData<EntitySimulationState>(e).Updates);
                Assert.AreEqual(new int2(0, 0), manager.GetComponentData<LogicalRegion>(e).Chunk);
            }
        }

        [Test]
        public void ReplayIsIdenticalAcrossFramePartitionsAndEntityInsertionOrder()
        {
            using (var a = new FoundationSession(0.125f))
            using (var b = new FoundationSession(0.125f))
            {
                var ea = a.Spawn(1, new double3(-100, 0, 100), new double3(2, 0, -3));
                a.Spawn(2, double3.zero, double3.zero);
                b.Spawn(2, double3.zero, double3.zero);
                var eb = b.Spawn(1, new double3(-100, 0, 100), new double3(2, 0, -3));
                a.Advance(0); b.Advance(0);
                for (int i = 0; i < 8; i++) a.Advance(0.125f);
                for (int i = 0; i < 32; i++) b.Advance(0.03125f);
                a.CompleteJobs(); b.CompleteJobs();
                Assert.AreEqual(9, a.Tick);
                Assert.AreEqual(a.Tick, b.Tick);
                Assert.AreEqual(a.World.EntityManager.GetComponentData<LogicalPose>(ea).Position,
                    b.World.EntityManager.GetComponentData<LogicalPose>(eb).Position);
            }
        }

        [Test]
        public void WorldsAreIsolatedAndRejectDuplicateIdsAndInvalidInput()
        {
            using (var a = new FoundationSession())
            using (var b = new FoundationSession())
            {
                a.Spawn(1, double3.zero, double3.zero);
                Assert.AreEqual(0, b.Count);
                Assert.Throws<ArgumentException>(() => a.Spawn(1, double3.zero, double3.zero));
                Assert.Throws<ArgumentOutOfRangeException>(() => a.Spawn(2, new double3(double.NaN), double3.zero));
                Assert.Throws<ArgumentOutOfRangeException>(() => a.Advance(-1));
                Assert.Throws<ArgumentOutOfRangeException>(() => a.Advance(float.PositiveInfinity));
            }
            Assert.Throws<ArgumentOutOfRangeException>(() => new FoundationSession(0));
            Assert.Throws<ArgumentOutOfRangeException>(() => new FoundationSession(float.NaN));
            Assert.Throws<ArgumentOutOfRangeException>(() => new FoundationSession(cellsPerChunk: 0));
        }

        [TestCase(-33, -1)]
        [TestCase(0, 0)]
        [TestCase(32, 33)]
        public void TerrainSamplesMatchVersionedGeneratorsWithoutVisualChunks(int x, int z)
        {
            var definition = new WorldDefinition(7);
            var settings = new EnvironmentDefinition(version: 4);
            var baseQuery = new ProceduralTerrainContract(definition);
            var envQuery = new ProceduralTerrainContract(definition, settings);
            Assert.AreEqual(new TerrainGenerator().SampleElevation(definition, x, z), baseQuery.Elevation(x, z));
            Assert.AreEqual(new EnvironmentGenerator(definition, settings).Sample(x, z).Elevation, envQuery.Elevation(x, z));
            Assert.AreEqual((int)math.floor(x / 32.0), baseQuery.Region(x, z).X);
            double h = envQuery.Elevation(x + 0.25, z + 0.25);
            var generator = new EnvironmentGenerator(definition, settings);
            double expected = generator.Sample(x, z).Elevation * 0.5
                + generator.Sample(x + 1, z).Elevation * 0.25 + generator.Sample(x, z + 1).Elevation * 0.25;
            Assert.AreEqual(expected, h, 1e-12);
            Assert.Throws<ArgumentOutOfRangeException>(() => baseQuery.Elevation(double.NaN, 0));
        }

        [BurstCompile(CompileSynchronously = true)]
        private struct BurstProbe : IJob
        {
            public NativeArray<int> Result;
            public void Execute() { bool managed = false; MarkManaged(ref managed); Result[0] = managed ? 0 : 1; }
            [BurstDiscard] private static void MarkManaged(ref bool value) { value = true; }
        }

        [Test]
        public void ScheduledJobActuallyExecutesBurstNativeCode()
        {
            using (var result = new NativeArray<int>(1, Allocator.TempJob))
            {
                new BurstProbe { Result = result }.Schedule().Complete();
                Assert.AreEqual(1, result[0], "Managed fallback detected: Burst must be enabled.");
            }
        }

        [Test]
        public void AssemblyHasNoPresentationCameraOrNetworkingReferences()
        {
            var references = typeof(FoundationSession).Assembly.GetReferencedAssemblies();
            foreach (var reference in references)
            {
                Assert.IsFalse(reference.Name.Contains("Presentation"));
                Assert.IsFalse(reference.Name.Contains("Camera"));
                Assert.IsFalse(reference.Name.Contains("Netcode"));
            }
        }
    }
}
