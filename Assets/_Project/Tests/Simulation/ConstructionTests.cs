using NUnit.Framework;
using RTSWorld.Simulation;
using RTSWorld.World.Environment;
using RTSWorld.World.Generation;
using Unity.Entities;
using Unity.Mathematics;

namespace RTSWorld.Tests
{
    public sealed class ConstructionTests
    {
        [TestCase(true, PlacementIssue.Water)]
        [TestCase(false, PlacementIssue.Slope)]
        public void RejectsInvalidTerrain(bool wet, PlacementIssue expected)
        {
            var world = new WorldDefinition(7);
            var dry = new ProceduralTerrainContract(world);
            var terrain = wet ? new ProceduralTerrainContract(world, new EnvironmentDefinition(seaLevel: 100)) : dry;
            var min = new int2(-8);
            var max = new int2(8);
            var obstacles = new EnvironmentalObstacles(min, max, 32, .22, System.Array.Empty<ResourcePlacement>());
            using var session = new UnitSession(dry, min, max);
            session.SetNavigationGrid(new NavigationGrid(dry, min, max, 100, obstacles: obstacles));
            session.ConfigureEconomy(new int2(-5));
            session.SetNavigationGrid(new NavigationGrid(terrain, min, max, wet ? 100 : 0, obstacles: obstacles));
            var issue = session.ValidateBuilding(BuildingKind.Workshop, int2.zero, 0);
            Assert.That(issue.HasFlag(expected), Is.True);
        }

        [TestCase(1)]
        [TestCase(3)]
        public void ConstructionChargesOnceAndCompletesInFixedSteps(int count)
        {
            var terrain = new ProceduralTerrainContract(new WorldDefinition(7));
            var min = new int2(-16);
            var max = new int2(16);
            var tree = new ResourceKey(7, 4, 0, 0);
            var stone = new ResourceKey(7, 4, 0, 3);
            var extra = new ResourceKey(7, 4, 2, 3);
            var obstacles = new EnvironmentalObstacles(min, max, 32, .22, new[]
            {
                new ResourcePlacement(tree, NaturalResource.Tree, .5f, .5f, 0),
                new ResourcePlacement(stone, NaturalResource.Stone, .5f, .5f, 0),
                new ResourcePlacement(extra, NaturalResource.Tree, .5f, .5f, 0)
            });
            var grid = new NavigationGrid(terrain, min, max, 100, obstacles: obstacles);
            using var session = new UnitSession(terrain, min, max);
            session.SetNavigationGrid(grid);
            session.ConfigureEconomy(new int2(-7, 0), productivity: 40);
            Assert.That(session.EnablePhysicalDepots(new int2(-7, 0), 0), Is.True);
            Entity initialDepot = session.Buildings[0];
            var manager = session.World.EntityManager;
            var origin = new int2(-2, -4);
            var depot = BuildingDefinition.Get(BuildingKind.Depot);
            Assert.That(depot.ReceivesResources);
            Assert.That(BuildingDefinition.Get(BuildingKind.MainCenter).Health, Is.GreaterThan(depot.Health));
            Assert.That(BuildingDefinition.Get(BuildingKind.Workshop).ReceivesResources, Is.False);
            Assert.That(session.ValidateBuilding(BuildingKind.Depot, origin, 0), Is.EqualTo(PlacementIssue.Cost));
            Assert.That(session.ValidateBuilding(BuildingKind.Depot, int2.zero, 0).HasFlag(PlacementIssue.Obstacle));
            Assert.That(session.ValidateBuilding(BuildingKind.Depot, max, 0), Is.EqualTo(PlacementIssue.Bounds));
            Assert.That(session.PlaceBuilding(BuildingKind.Depot, origin, 0), Is.EqualTo(Entity.Null));
            using var workers = session.SpawnBatch(UnitDefinition.Villager, 0, count, new double3(-5.5, 0, -2.5), default);
            GatherUntil(tree, NaturalResource.Tree, 60);
            GatherUntil(stone, NaturalResource.Stone, 80);

            var timer = System.Diagnostics.Stopwatch.StartNew();
            for (int i = 0; i < 100; i++)
                Assert.That(session.ValidateBuilding(BuildingKind.Depot, origin, 0), Is.EqualTo(PlacementIssue.None));
            TestContext.WriteLine($"Placement (100 calls) {timer.Elapsed.TotalMilliseconds:F3} ms; last {session.PlacementMilliseconds:F4} ms");

            var field = NavigationSearch.BuildFlowField(grid, origin);
            Entity canceled = session.PlaceBuilding(BuildingKind.Depot, origin, 0);
            Assert.That(field.IsCurrent, Is.False);
            Assert.That(manager.GetComponentData<Building>(canceled).State, Is.EqualTo(ConstructionState.Planned));
            Assert.That(grid.TryGetCost(origin, out int cost) && cost == 0);
            Assert.That(session.ValidateBuilding(BuildingKind.Depot, origin, 0).HasFlag(PlacementIssue.Building));
            Assert.That(session.GetStock(0, NaturalResource.Tree), Is.EqualTo(60 - depot.Wood));
            Assert.That(session.GetStock(0, NaturalResource.Stone), Is.EqualTo(80 - depot.Stone));
            Assert.That(session.CancelBuilding(canceled, 1), Is.False);
            Assert.That(session.CancelBuilding(canceled, 0), Is.True);
            Assert.That(session.CancelBuilding(canceled, 0), Is.False);
            Assert.That(session.GetStock(0, NaturalResource.Tree), Is.EqualTo(60));
            Assert.That(grid.TryGetCost(origin, out cost) && cost > 0);

            Entity site = session.PlaceBuilding(BuildingKind.Depot, origin, 0);
            using var carrier = session.SpawnBatch(UnitDefinition.Villager, 0, 1, new double3(4.5, 0, 3.5), default);
            Assert.That(session.Gather(extra, 0, carrier), Is.EqualTo(UnitOrderResult.Accepted));
            for (int i = 0; i < 600 && ReadCarrier().Phase != GatherPhase.Delivering; i++) Tick();
            Assert.That(ReadCarrier().Phase, Is.EqualTo(GatherPhase.Delivering));
            var cargo = ReadCarrier().Cargo;
            Assert.That(cargo, Is.GreaterThan(0));
            Assert.That(session.DamageBuilding(initialDepot, 1000), Is.True);
            Tick();
            Assert.That(ReadCarrier().Phase, Is.EqualTo(GatherPhase.Blocked));
            Assert.That(ReadCarrier().Cargo, Is.EqualTo(cargo));
            Assert.That(session.DamageBuilding(initialDepot, 1000), Is.False);
            Assert.That(session.Gather(extra, 0, workers), Is.EqualTo(UnitOrderResult.Unreachable));
            Assert.That(session.ApplyOrder(UnitOrder.Build(site), 0, workers), Is.EqualTo(UnitOrderResult.Accepted));
            for (int i = 0; i < 600 && ReadSite().Work == 0; i++) Tick();
            Assert.That(ReadSite().State, Is.EqualTo(ConstructionState.UnderConstruction));
            double previous = ReadSite().Work;
            session.Advance(.01f);
            Assert.That(ReadSite().Work, Is.EqualTo(previous));
            double peak = 0;
            bool parallel = false;
            for (int i = 0; i < 600 && ReadSite().State != ConstructionState.Completed; i++)
            {
                previous = ReadSite().Work;
                Tick();
                if (ReadSite().Work - previous > 1.5 * session.FixedDeltaTime) parallel = true;
                peak = math.max(peak, session.ConstructionMilliseconds);
            }
            Assert.That(parallel, Is.EqualTo(count > 1));
            Assert.That(ReadSite().State, Is.EqualTo(ConstructionState.Completed));
            Assert.That(ReadSite().Work, Is.EqualTo(depot.Work));
            Assert.That(session.CancelBuilding(site, 0), Is.False);
            Assert.That(session.GetStock(0, NaturalResource.Tree), Is.EqualTo(60 - depot.Wood));
            for (int i = 0; i < 1400 && session.GetStock(0, NaturalResource.Tree) < 120 - depot.Wood - 1e-7; i++) Tick();
            Assert.That(session.GetStock(0, NaturalResource.Tree), Is.EqualTo(120 - depot.Wood).Within(1e-7));
            Assert.That(ReadCarrier().Cargo, Is.Zero);
            Assert.That(session.DamageBuilding(site, depot.Health), Is.True);
            Assert.That(grid.TryGetCost(origin, out cost) && cost > 0);
            TestContext.WriteLine($"Workers {count}; construction max {peak:F4} ms; footprint + field invalidation {session.FootprintMilliseconds:F4} ms; depot search {session.LastDepotSearchMilliseconds:F4} ms");

            GatherWorker ReadCarrier() => manager.GetComponentData<GatherWorker>(carrier[0]);
            Building ReadSite() => manager.GetComponentData<Building>(site);
            void Tick()
            {
                session.Advance(session.FixedDeltaTime);
                session.CompleteJobs();
            }
            void GatherUntil(ResourceKey key, NaturalResource kind, double amount)
            {
                Assert.That(session.Gather(key, 0, workers), Is.EqualTo(UnitOrderResult.Accepted));
                for (int i = 0; i < 1400 && session.GetStock(0, kind) < amount - 1e-7; i++) Tick();
                Assert.That(session.GetStock(0, kind), Is.EqualTo(amount).Within(1e-7));
            }
        }
    }
}
