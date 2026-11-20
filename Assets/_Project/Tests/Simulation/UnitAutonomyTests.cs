using System;
using System.Diagnostics;
using NUnit.Framework;
using RTSWorld.Simulation;
using RTSWorld.World.Environment;
using RTSWorld.World.Generation;
using Unity.Entities;
using Unity.Mathematics;

namespace RTSWorld.Tests
{
    public sealed class UnitAutonomyTests
    {
        private static readonly ResourceKey Tree = new(7, 4, 1, 0);
        private static readonly ResourceKey Stone = new(7, 4, 4, 0);

        [Test]
        public void ManualMoveWinsAndStopRequiresExplicitReactivation()
        {
            using var session = Create();
            using var workers = session.SpawnBatch(UnitDefinition.Villager, 0, 1, new double3(-3.5, 0, .5), default);
            var unit = workers[0];
            var manager = session.World.EntityManager;
            Assert.That(session.EnableAutonomy(unit, AutonomyMode.Work));
            Advance(session, 12);
            Assert.That(manager.GetComponentData<GatherWorker>(unit).Resource, Is.EqualTo(Tree));
            Assert.That(session.ApplyOrder(UnitOrder.Move(new int2(-9, 0)), 0, workers), Is.EqualTo(UnitOrderResult.Accepted));
            Advance(session, 15);
            Assert.That(manager.GetComponentData<UnitAutonomy>(unit).State, Is.EqualTo(AutonomyState.Manual));
            Assert.That(manager.GetComponentData<GatherWorker>(unit).Phase, Is.EqualTo(GatherPhase.Idle));
            Assert.That(session.ApplyOrder(UnitOrder.Stop(), 0, workers), Is.EqualTo(UnitOrderResult.Accepted));
            Advance(session, 48);
            Assert.That(manager.GetComponentData<UnitAutonomy>(unit).State, Is.EqualTo(AutonomyState.Stopped));
            Assert.That(manager.GetComponentData<GatherWorker>(unit).Phase, Is.EqualTo(GatherPhase.Idle));
            Assert.That(session.EnableAutonomy(unit, AutonomyMode.Work));
            Advance(session, 12);
            Assert.That(manager.GetComponentData<UnitAutonomy>(unit).State, Is.EqualTo(AutonomyState.Working));
        }

        [Test]
        public void WorkersSplitResourcesAndSwitchAfterDepletion()
        {
            using var session = Create(wood: 2, stone: 80, secondX: 2);
            using var workers = session.SpawnBatch(UnitDefinition.Villager, 0, 2, new double3(-1.5, 0, .5), default, 1);
            var manager = session.World.EntityManager;
            foreach (var worker in workers) Assert.That(session.EnableAutonomy(worker, AutonomyMode.Work));
            Advance(session, 12);
            var first = manager.GetComponentData<GatherWorker>(workers[0]).Resource;
            var second = manager.GetComponentData<GatherWorker>(workers[1]).Resource;
            Assert.That(first, Is.Not.EqualTo(second));
            Advance(session, 500);
            Assert.That(session.GetStock(0, NaturalResource.Tree), Is.EqualTo(2).Within(1e-6));
            Assert.That(session.GetStock(0, NaturalResource.Stone), Is.GreaterThan(0));
        }

        [Test]
        public void InaccessibleClosestResourceIsSkipped()
        {
            using var session = Create(blockTree: true, secondX: 6);
            using var workers = session.SpawnBatch(UnitDefinition.Villager, 0, 1, new double3(-3.5, 0, .5), default);
            var manager = session.World.EntityManager;
            Assert.That(session.EnableAutonomy(workers[0], AutonomyMode.Work));
            Advance(session, 24);
            Assert.That(manager.GetComponentData<GatherWorker>(workers[0]).Resource, Is.EqualTo(new ResourceKey(7, 4, 6, 0)));
        }

        [Test]
        public void ConstructionCompletesAndWorkerResumesEconomy()
        {
            using var session = Create(supplies: 30);
            var site = session.PlaceBuilding(BuildingKind.Workshop, new int2(-3, 2), 0);
            Assert.That(site, Is.Not.EqualTo(Entity.Null));
            using var workers = session.SpawnBatch(UnitDefinition.Villager, 0, 1, new double3(-3.5, 0, .5), default);
            var manager = session.World.EntityManager;
            Assert.That(session.EnableAutonomy(workers[0], AutonomyMode.Work, autoBuild: true));
            Advance(session, 12);
            Assert.That(manager.GetComponentData<UnitAutonomy>(workers[0]).State, Is.EqualTo(AutonomyState.Building));
            Advance(session, 500);
            Assert.That(manager.GetComponentData<Building>(site).State, Is.EqualTo(ConstructionState.Completed));
            Assert.That(session.DeliveredTotal, Is.GreaterThan(0));
        }

        [Test]
        public void MissingDepotKeepsCargoAndAReplacementLetsWorkerDeliver()
        {
            using var session = Create(supplies: 30, physical: true);
            using var workers = session.SpawnBatch(UnitDefinition.Villager, 0, 1, new double3(-2.5, 0, .5), default);
            var manager = session.World.EntityManager;
            Assert.That(session.EnableAutonomy(workers[0], AutonomyMode.Work));
            for (int i = 0; i < 140 && Read().Cargo <= 0; i++) Advance(session, 1);
            double cargo = Read().Cargo;
            Assert.That(cargo, Is.GreaterThan(0));
            Assert.That(session.DamageBuilding(session.Buildings[0], 1000));
            Advance(session, 100);
            Assert.That(Read().Cargo, Is.GreaterThanOrEqualTo(cargo));
            Assert.That(Read().Phase, Is.EqualTo(GatherPhase.Blocked));
            Assert.That(session.DeliveredTotal, Is.Zero);

            var replacement = session.PlaceBuilding(BuildingKind.Depot, new int2(-8, 3), 0);
            Assert.That(replacement, Is.Not.EqualTo(Entity.Null));
            using var builders = session.SpawnBatch(UnitDefinition.Villager, 0, 4, new double3(-7.5, 0, 2.5), default, .25);
            Assert.That(session.ApplyOrder(UnitOrder.Build(replacement), 0, builders), Is.EqualTo(UnitOrderResult.Accepted));
            Advance(session, 300);
            Assert.That(session.DeliveredTotal, Is.GreaterThan(0));

            GatherWorker Read() => manager.GetComponentData<GatherWorker>(workers[0]);
        }

        [Test]
        public void GuardRespectsFactionAndReturnsWhenTargetLeavesOrDies()
        {
            using var session = Create();
            session.ConfigureAutonomy(4, 5);
            using var guard = session.SpawnBatch(UnitDefinition.Soldier, 0, 1, new double3(-5.5, 0, -3.5), default);
            using var ally = session.SpawnBatch(UnitDefinition.Soldier, 0, 1, new double3(-4.5, 0, -3.5), default);
            using var enemy = session.SpawnBatch(UnitDefinition.Soldier, 1, 1, new double3(-1.5, 0, -3.5), default);
            var manager = session.World.EntityManager;
            Assert.That(session.EnableAutonomy(guard[0], AutonomyMode.Guard));
            Advance(session, 14);
            Assert.That(manager.GetComponentData<UnitCombat>(guard[0]).Target, Is.EqualTo(enemy[0]));
            Assert.That(manager.GetComponentData<LogicalPose>(guard[0]).Position.x, Is.GreaterThan(-5.5));
            session.SetIntention(enemy[0], new double2(3, 0));
            Advance(session, 100);
            Assert.That(manager.GetComponentData<UnitCombat>(guard[0]).Target, Is.EqualTo(Entity.Null));
            Assert.That(manager.GetComponentData<UnitAutonomy>(guard[0]).State, Is.EqualTo(AutonomyState.Guard));
            Assert.That(manager.GetComponentData<LogicalPose>(guard[0]).Position.x, Is.EqualTo(-5.5).Within(.6));

            using var nearby = session.SpawnBatch(UnitDefinition.Soldier, 1, 1, new double3(-4.5, 0, -3.5), default);
            Advance(session, 8);
            Assert.That(manager.GetComponentData<UnitCombat>(guard[0]).Target, Is.EqualTo(nearby[0]));
            session.DestroyBatch(nearby);
            Advance(session, 12);
            Assert.That(manager.GetComponentData<UnitCombat>(guard[0]).Target, Is.EqualTo(Entity.Null));
        }

        [Test]
        public void HoldPositionAttacksOnlyInReach()
        {
            using var session = Create();
            using var guard = session.SpawnBatch(UnitDefinition.Soldier, 0, 1, new double3(-5.5, 0, -3.5), default);
            using var enemy = session.SpawnBatch(UnitDefinition.Soldier, 1, 1, new double3(-1.5, 0, -3.5), default);
            var manager = session.World.EntityManager;
            Assert.That(session.EnableAutonomy(guard[0], AutonomyMode.HoldPosition));
            Advance(session, 24);
            Assert.That(manager.GetComponentData<UnitCombat>(guard[0]).Target, Is.EqualTo(Entity.Null));
            session.SetIntention(enemy[0], new double2(-3, 0));
            Advance(session, 24);
            Assert.That(manager.GetComponentData<UnitHealth>(enemy[0]).Current, Is.LessThan(100));
            Assert.That(manager.GetComponentData<LogicalPose>(guard[0]).Position.x, Is.EqualTo(-5.5));
        }

        [TestCase(100)]
        [TestCase(500)]
        [TestCase(1000)]
        [TestCase(5000)]
        public void DecisionsAreStaggeredAndQueriesStayBounded(int population)
        {
            Measure(0);
            Measure(population / 2);

            void Measure(int active)
            {
                using var session = Create();
                int workers = population / 2;
                using var own = session.SpawnBatch(UnitDefinition.Villager, 0, workers,
                    new double3(-3.5, 0, .5), default, .03);
                using var foes = session.SpawnBatch(UnitDefinition.Soldier, 1, population - workers,
                    new double3(7.5, 0, -6.5), default, .03);
                for (int i = 0; i < active; i++) session.EnableAutonomy(own[i], AutonomyMode.Work);
                Advance(session, 24);
                const int samples = 240;
                int decisions = 0, queries = 0;
                int deliveries = session.AutonomousDeliveries;
                double perception = 0, evaluation = 0, execution = 0;
                long allocated = GC.GetAllocatedBytesForCurrentThread();
                long started = Stopwatch.GetTimestamp();
                for (int i = 0; i < samples; i++)
                {
                    Advance(session, 1);
                    decisions += session.AutonomyDecisions;
                    queries += session.AutonomyQueries;
                    perception += session.AutonomyPerceptionMs;
                    evaluation += session.AutonomyDecisionMs;
                    execution += session.AutonomyExecutionMs;
                }
                double ms = (Stopwatch.GetTimestamp() - started) * 1000.0 / Stopwatch.Frequency;
                long bytes = GC.GetAllocatedBytesForCurrentThread() - allocated;
                Assert.That(decisions, Is.EqualTo(active * (samples / 12)));
                Assert.That(queries, Is.LessThanOrEqualTo(decisions * 49));
                TestContext.WriteLine(FormattableString.Invariant(
                    $"ECS009 {population},{active},{samples},{ms:F4},{perception:F4},{evaluation:F4},{execution:F4},{decisions},{queries},{bytes},{session.AutonomousDeliveries - deliveries}"));
            }
        }

        private static UnitSession Create(double wood = 60, double stone = 80, double supplies = 0,
            bool physical = false, int secondX = 4, bool blockTree = false)
        {
            var terrain = new ProceduralTerrainContract(new WorldDefinition(7));
            var min = new int2(-16);
            var max = new int2(16);
            var other = new ResourceKey(7, 4, secondX, Stone.Z);
            var obstacles = new EnvironmentalObstacles(min, max, 32, .22, new[]
            {
                new ResourcePlacement(Tree, NaturalResource.Tree, .5f, .5f, 0),
                new ResourcePlacement(other, NaturalResource.Stone, .5f, .5f, 0)
            });
            var session = new UnitSession(terrain, min, max);
            session.SetNavigationGrid(new NavigationGrid(terrain, min, max, 100,
                isObstacle: blockTree ? cell => math.abs(cell.x - 1) <= 3 && math.abs(cell.y) <= 3 : null,
                obstacles: obstacles));
            session.ConfigureEconomy(new int2(-8, 0), productivity: 25, wood: wood, stone: stone,
                startingWood: supplies, startingStone: supplies);
            if (physical) Assert.That(session.EnablePhysicalDepots(new int2(-8, 0), 0));
            return session;
        }

        private static void Advance(UnitSession session, int steps)
        {
            for (int i = 0; i < steps; i++)
            {
                session.Advance(session.FixedDeltaTime);
                session.CompleteJobs();
            }
        }
    }
}
