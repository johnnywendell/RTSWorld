using NUnit.Framework;
using RTSWorld.Simulation;
using RTSWorld.World.Environment;
using RTSWorld.World.Generation;
using Unity.Mathematics;

namespace RTSWorld.Tests
{
    public sealed class ResourceEconomyTests
    {
        [TestCase(NaturalResource.Tree, 1)]
        [TestCase(NaturalResource.Stone, 10)]
        [TestCase(NaturalResource.Tree, 50)]
        [TestCase(NaturalResource.Stone, 100)]
        public void WorkersConserveResourceAndDeliverToDepot(NaturalResource kind, int population)
        {
            var terrain = new ProceduralTerrainContract(new WorldDefinition(7));
            var min = new int2(-24); var max = new int2(24);
            var key = new ResourceKey(7, 4, 0, 0);
            var resource = new ResourcePlacement(key, kind, .5f, .5f, 0);
            var obstacles = new EnvironmentalObstacles(min, max, 32, .22, new[] { resource });
            var grid = new NavigationGrid(terrain, min, max, 100, obstacles: obstacles);
            using var session = new UnitSession(terrain, min, max);
            session.SetNavigationGrid(grid);
            session.ConfigureEconomy(new int2(-5, 0), productivity: 10, wood: 21, stone: 23);
            using var workers = session.SpawnBatch(UnitDefinition.Villager, 0, population, new double3(-16.5, 0, -5.5), default, .8);
            Assert.That(session.TryGetResource(key, out var before), Is.True);
            Assert.That(before.Remaining, Is.EqualTo(before.Initial));
            Assert.That(session.Gather(key, 0, workers), Is.EqualTo(UnitOrderResult.Accepted));
            var manager = session.World.EntityManager;
            var timer = System.Diagnostics.Stopwatch.StartNew();
            double maxEconomy = 0;
            int countGathered = 0;
            int deliveries = 0;
            double previousStock = 0;
            for (int i = 0; i < 1800; i++)
            {
                session.Advance(.05f);
                session.CompleteJobs();
                maxEconomy = math.max(maxEconomy, session.EconomyMilliseconds);
                countGathered += session.GatheringWorkers;
                double stock = session.GetStock(0, kind);
                if (stock > previousStock) deliveries++;
                previousStock = stock;
                Assert.That(session.TryGetResource(key, out var now), Is.True);
                double cargo = 0;
                foreach (var worker in workers)
                {
                    var held = manager.GetComponentData<GatherWorker>(worker).Cargo;
                    Assert.That(held, Is.InRange(0.0, 10.0));
                    cargo += held;
                }
                Assert.That(stock + cargo + now.Remaining, Is.EqualTo(before.Initial).Within(1e-7));
                if (stock >= before.Initial - 1e-8) break;
            }

            Assert.That(countGathered, Is.GreaterThan(0));
            Assert.That(deliveries, Is.GreaterThan(0));
            if (population == 1) Assert.That(deliveries, Is.GreaterThanOrEqualTo(3));
            Assert.That(session.GetStock(0, kind), Is.EqualTo(before.Initial).Within(1e-8));
            Assert.That(session.TryGetResource(key, out var after) && after.Remaining == 0, Is.True);
            Assert.That(grid.TryGetCost(int2.zero, out int cost) && cost > 0, Is.True);
            TestContext.WriteLine(Formattable(population, session.Tick, timer.Elapsed.TotalMilliseconds, maxEconomy));
        }

        [Test]
        public void StopAndMovePreserveCargoAndWorkerRemovalAccountsForLoss()
        {
            var terrain = new ProceduralTerrainContract(new WorldDefinition(7));
            var min = new int2(-8); var max = new int2(8);
            var key = new ResourceKey(7, 4, 0, 0);
            var index = new EnvironmentalObstacles(min, max, 32, .22, new[] { new ResourcePlacement(key, NaturalResource.Tree, .5f, .5f, 0) });
            using var session = new UnitSession(terrain, min, max);
            session.SetNavigationGrid(new NavigationGrid(terrain, min, max, 100, obstacles: index));
            session.ConfigureEconomy(new int2(-5, 0));
            using var worker = session.SpawnBatch(UnitDefinition.Villager, 0, 1, new double3(-3.5, 0, .5), default);
            using var soldier = session.SpawnBatch(UnitDefinition.Soldier, 0, 1, new double3(-3.5, 0, 1.5), default);
            Assert.That(session.Gather(key, 0, soldier), Is.EqualTo(UnitOrderResult.Empty));
            Assert.That(session.Gather(key, 0, worker), Is.EqualTo(UnitOrderResult.Accepted));
            var manager = session.World.EntityManager;
            for (int i = 0; i < 200 && manager.GetComponentData<GatherWorker>(worker[0]).Cargo == 0; i++)
            {
                session.Advance(.05f);
                session.CompleteJobs();
            }
            var before = manager.GetComponentData<GatherWorker>(worker[0]).Cargo;
            Assert.That(before, Is.GreaterThan(0));
            Assert.That(session.ApplyOrder(UnitOrder.Stop(), 0, worker), Is.EqualTo(UnitOrderResult.Accepted));
            session.Advance(.05f);
            session.CompleteJobs();
            var stopped = manager.GetComponentData<GatherWorker>(worker[0]);
            Assert.That(stopped.Phase, Is.EqualTo(GatherPhase.Idle));
            Assert.That(stopped.Cargo, Is.EqualTo(before));
            Assert.That(session.Gather(key, 0, worker), Is.EqualTo(UnitOrderResult.Accepted));
            Assert.That(session.ApplyOrder(UnitOrder.Move(new int2(-6, -1)), 0, worker), Is.EqualTo(UnitOrderResult.Accepted));
            var replaced = manager.GetComponentData<GatherWorker>(worker[0]);
            Assert.That(replaced.Phase, Is.EqualTo(GatherPhase.Idle));
            Assert.That(replaced.Cargo, Is.EqualTo(before));
            session.DestroyBatch(worker);
            Assert.That(session.LostCargo, Is.EqualTo(before));
            Assert.That(session.DeliveredTotal, Is.Zero);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void GatherRejectsDisconnectedResourceOrBlockedDepot(bool blockDepot)
        {
            var terrain = new ProceduralTerrainContract(new WorldDefinition(7));
            var min = new int2(-12); var max = new int2(12);
            var key = new ResourceKey(7, 4, blockDepot ? -5 : 5, 0);
            var spare = new ResourceKey(7, 4, -5, 4);
            var index = new EnvironmentalObstacles(min, max, 32, .22, new[]
            {
                new ResourcePlacement(key, NaturalResource.Tree, .5f, .5f, 0),
                new ResourcePlacement(spare, NaturalResource.Gold, .5f, .5f, 0)
            });
            using var session = new UnitSession(terrain, min, max);
            session.SetNavigationGrid(new NavigationGrid(terrain, min, max, 100, c => c.x == 0, obstacles: index));
            session.ConfigureEconomy(new int2(-5, 4));
            using var worker = session.SpawnBatch(UnitDefinition.Villager, 0, 1, new double3(-3.5, 0, .5), default);
            if (blockDepot) Assert.That(session.SetEnvironmentalResourceRadius(spare, .5), Is.True);
            Assert.That(session.Gather(key, 0, worker), Is.EqualTo(UnitOrderResult.Unreachable));
            Assert.That(session.World.EntityManager.GetComponentData<GatherWorker>(worker[0]).Phase, Is.EqualTo(GatherPhase.Idle));
        }

        private static string Formattable(int workers, ulong ticks, double total, double max) =>
            System.FormattableString.Invariant($"ECONOMY workers={workers} ticks={ticks} harness_ms={total:F4} max_economy_tick_ms={max:F5}");
    }
}
