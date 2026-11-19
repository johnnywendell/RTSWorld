using System;
using System.Collections.Generic;
using System.Diagnostics;
using NUnit.Framework;
using RTSWorld.Simulation;
using RTSWorld.World.Environment;
using RTSWorld.World.Generation;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace RTSWorld.Tests
{
    public sealed class UnitCombatTests
    {
        [Test]
        public void DirectedOrdersRespectFactionRoleRangeAndCooldown()
        {
            using var session = CreateSession();
            using var attacker = Spawn(session, 0, 0);
            using var ally = Spawn(session, 0, 1);
            using var worker = Spawn(session, 0, 2, kind: UnitKind.Villager);
            using var enemy = Spawn(session, 2, 1);
            var manager = session.World.EntityManager;
            Assert.That(UnitSession.AreHostile(2, 2), Is.False);
            Assert.That(UnitSession.AreHostile(2, 3), Is.True);
            Assert.That(manager.GetComponentData<UnitIdentity>(enemy[0]).Owner, Is.EqualTo(2));
            var health = manager.GetComponentData<UnitHealth>(enemy[0]);
            Assert.That(health.Current, Is.EqualTo(health.Maximum).And.EqualTo(100));
            Assert.That(session.ApplyOrder(UnitOrder.Attack(ally[0]), 0, attacker), Is.EqualTo(UnitOrderResult.InvalidTarget));
            Assert.That(session.ApplyOrder(UnitOrder.Attack(enemy[0]), 0, worker), Is.EqualTo(UnitOrderResult.Empty));

            Tick(session); // Initialize the fixed-rate manager before timing the attack.
            Assert.That(session.ApplyOrder(UnitOrder.Attack(enemy[0]), 0, attacker), Is.EqualTo(UnitOrderResult.Accepted));
            Tick(session);
            Assert.That(Health(session, enemy[0]), Is.EqualTo(90));
            Assert.That(manager.GetComponentData<MovementVelocity>(attacker[0]).Value, Is.EqualTo(double3.zero));
            Tick(session, 11);
            Assert.That(Health(session, enemy[0]), Is.EqualTo(90));
            Tick(session);
            Assert.That(Health(session, enemy[0]), Is.EqualTo(80));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void DisconnectedTargetBlocksPursuitAndReachableTargetUsesPassage(bool opening)
        {
            var terrain = new ProceduralTerrainContract(new WorldDefinition(7, cellsPerChunk: 4));
            var min = new int2(-8);
            var max = new int2(8);
            var grid = new NavigationGrid(terrain, min, max, 100, cell => cell.x == 0 && (!opening || cell.y != 2));
            using var session = new UnitSession(terrain, min, max);
            session.SetNavigationGrid(grid);
            using var attackers = Spawn(session, 0, -1, count: 8);
            using var enemy = Spawn(session, 1, 1);
            Assert.That(session.ApplyOrder(UnitOrder.Attack(enemy[0]), 0, attackers), Is.EqualTo(UnitOrderResult.Accepted));
            Tick(session);
            Assert.That(Health(session, enemy[0]), Is.EqualTo(100));
            var manager = session.World.EntityManager;
            if (!opening)
                Assert.That(manager.GetComponentData<UnitCombat>(attackers[0]).Phase, Is.EqualTo(CombatPhase.Blocked));

            for (int i = 0; i < 160 && manager.Exists(enemy[0]); i++)
            {
                Tick(session);
                foreach (var unit in attackers)
                {
                    var cell = (int2)math.floor(manager.GetComponentData<LogicalPose>(unit).Position.xz);
                    Assert.That(grid.TryGetCost(cell, out int cost) && cost > 0, Is.True);
                }
            }

            Assert.That(manager.Exists(enemy[0]), Is.EqualTo(!opening));
            var positions = new HashSet<double2>();
            foreach (var unit in attackers) positions.Add(manager.GetComponentData<LogicalPose>(unit).Position.xz);
            if (opening) Assert.That(positions.Count, Is.GreaterThan(1));
        }

        [Test]
        public void AttackCannotCrossANonNavigableCellEvenWithinRange()
        {
            var terrain = new ProceduralTerrainContract(new WorldDefinition(7));
            using var session = new UnitSession(terrain, new int2(-8), new int2(8));
            session.SetNavigationGrid(new NavigationGrid(terrain, new int2(-8), new int2(8), 100, c => c.x == 0));
            using var attacker = session.SpawnBatch(UnitDefinition.Soldier, 0, 1, new double3(-.1, 0, .5), default);
            using var enemy = session.SpawnBatch(UnitDefinition.Soldier, 1, 1, new double3(1.1, 0, .5), default);
            session.ApplyOrder(UnitOrder.Attack(enemy[0]), 0, attacker);
            Tick(session, 20);
            Assert.That(Health(session, enemy[0]), Is.EqualTo(100));
        }

        [Test]
        public void SimultaneousStrikesRemoveBothUnitsAndRejectStaleTargets()
        {
            using var session = CreateSession();
            var definition = UnitDefinition.Soldier;
            definition.Health = 5;
            using var left = session.SpawnBatch(definition, 0, 1, new double3(.5, 0, .5), default);
            using var right = session.SpawnBatch(definition, 1, 1, new double3(1.5, 0, .5), default);
            using var observer = Spawn(session, 0, -4);
            session.ApplyOrder(UnitOrder.Attack(right[0]), 0, left);
            session.ApplyOrder(UnitOrder.Attack(left[0]), 1, right);
            session.ApplyOrder(UnitOrder.Attack(right[0]), 0, observer);
            Tick(session);
            Assert.That(session.Count, Is.EqualTo(1));
            Assert.That(session.DefeatedUnits, Is.EqualTo(2));
            Assert.That(session.EngagedUnits, Is.Zero);
            Assert.That(session.World.EntityManager.GetComponentData<UnitCombat>(observer[0]).Target, Is.EqualTo(Entity.Null));
            Assert.That(session.ApplyOrder(UnitOrder.Attack(right[0]), 0, observer), Is.EqualTo(UnitOrderResult.InvalidTarget));
        }

        [Test]
        public void ReplacingAttackPreservesCooldownAndStopAndMoveCancelIt()
        {
            using var session = CreateSession();
            using var attacker = Spawn(session, 0, 0);
            using var first = Spawn(session, 1, 1);
            using var next = Spawn(session, 2, -1);
            var manager = session.World.EntityManager;
            session.ApplyOrder(UnitOrder.Attack(first[0]), 0, attacker);
            Tick(session);
            session.ApplyOrder(UnitOrder.Attack(next[0]), 0, attacker);
            Assert.That(manager.GetComponentData<UnitCombat>(attacker[0]).Target, Is.EqualTo(next[0]));
            Tick(session);
            Assert.That(Health(session, next[0]), Is.EqualTo(100));
            Assert.That(session.ApplyOrder(UnitOrder.Stop(), 0, attacker), Is.EqualTo(UnitOrderResult.Accepted));
            Tick(session, 16);
            Assert.That(Health(session, next[0]), Is.EqualTo(100));
            session.ApplyOrder(UnitOrder.Attack(next[0]), 0, attacker);
            Assert.That(session.ApplyOrder(UnitOrder.Move(new int2(2, 2)), 0, attacker), Is.EqualTo(UnitOrderResult.Accepted));
            Tick(session);
            Assert.That(manager.GetComponentData<UnitCombat>(attacker[0]).Target, Is.EqualTo(Entity.Null));
            Assert.That(Health(session, first[0]), Is.EqualTo(90));
            Assert.That(Health(session, next[0]), Is.EqualTo(100));
            session.ApplyOrder(UnitOrder.Attack(next[0]), 0, attacker);
            session.DestroyBatch(next);
            Assert.That(manager.GetComponentData<UnitCombat>(attacker[0]).Target, Is.EqualTo(Entity.Null));
        }

        [Test]
        public void PursuitResumesWhenTargetMovesOutOfRange()
        {
            using var session = CreateSession();
            using var attacker = Spawn(session, 0, 0);
            using var enemy = Spawn(session, 1, 1);
            var manager = session.World.EntityManager;
            session.ApplyOrder(UnitOrder.Attack(enemy[0]), 0, attacker);
            Tick(session);
            Assert.That(Health(session, enemy[0]), Is.EqualTo(90));
            Assert.That(session.ApplyOrder(UnitOrder.Move(new int2(7, 0)), 1, enemy), Is.EqualTo(UnitOrderResult.Accepted));
            Tick(session, 75);
            Assert.That(manager.GetComponentData<LogicalPose>(attacker[0]).Position.x, Is.GreaterThan(5));
            Assert.That(Health(session, enemy[0]), Is.LessThan(90));
        }

        [Test]
        public void DamageInterruptsWorkAndDeathAccountsForHeldCargoExactlyOnce()
        {
            var terrain = new ProceduralTerrainContract(new WorldDefinition(7));
            var min = new int2(-8);
            var max = new int2(8);
            var key = new ResourceKey(7, 4, 0, 0);
            var obstacles = new EnvironmentalObstacles(min, max, 32, .22,
                new[] { new ResourcePlacement(key, NaturalResource.Tree, .5f, .5f, 0) });
            using var session = new UnitSession(terrain, min, max);
            session.SetNavigationGrid(new NavigationGrid(terrain, min, max, 100, obstacles: obstacles));
            session.ConfigureEconomy(new int2(-5, 0));
            using var worker = Spawn(session, 0, -4, kind: UnitKind.Villager);
            var manager = session.World.EntityManager;
            Assert.That(session.Gather(key, 0, worker), Is.EqualTo(UnitOrderResult.Accepted));
            for (int i = 0; i < 200 && manager.GetComponentData<GatherWorker>(worker[0]).Cargo == 0; i++) Tick(session);
            var cargo = manager.GetComponentData<GatherWorker>(worker[0]).Cargo;
            Assert.That(cargo, Is.GreaterThan(0));
            var position = manager.GetComponentData<LogicalPose>(worker[0]).Position;
            using var enemy = session.SpawnBatch(UnitDefinition.Soldier, 1, 1, position, default);
            session.ApplyOrder(UnitOrder.Attack(worker[0]), 1, enemy);
            Tick(session);
            Assert.That(manager.GetComponentData<GatherWorker>(worker[0]).Phase, Is.EqualTo(GatherPhase.Idle));
            Assert.That(manager.GetComponentData<GatherWorker>(worker[0]).Cargo, Is.EqualTo(cargo));
            Assert.That(session.ActiveWorkers, Is.Zero);
            Tick(session, 70);
            Assert.That(manager.Exists(worker[0]), Is.False);
            Assert.That(session.LostCargo, Is.EqualTo(cargo));
            Assert.That(session.TryGetResource(key, out var amount), Is.True);
            Assert.That(amount.Remaining + session.LostCargo + session.DeliveredTotal, Is.EqualTo(amount.Initial).Within(1e-8));
        }

        [Test]
        public void RepeatOfTheSameFixedOrdersProducesTheSameOutcome()
        {
            Assert.That(Run(), Is.EqualTo(Run()));
            static (ulong Tick, int Count, double3 Pose, float Health) Run()
            {
                using var session = CreateSession();
                using var attackers = Spawn(session, 0, -6, count: 3);
                using var enemy = Spawn(session, 1, 4);
                session.ApplyOrder(UnitOrder.Attack(enemy[0]), 0, attackers);
                Tick(session, 75);
                return (session.Tick, session.Count,
                    session.World.EntityManager.GetComponentData<LogicalPose>(attackers[0]).Position,
                    Health(session, enemy[0]));
            }
        }

        [TestCase(100)]
        [TestCase(500)]
        [TestCase(1000)]
        [TestCase(5000)]
        public void ProfilesSimultaneousLethalStrikesAgainstDistinctTargets(int count)
        {
            var terrain = new ProceduralTerrainContract(new WorldDefinition(7));
            var min = new int2(-32);
            var max = new int2(32);
            using var session = new UnitSession(terrain, min, max);
            session.SetNavigationGrid(new NavigationGrid(terrain, min, max, 100));
            var lethal = UnitDefinition.Soldier;
            lethal.Health = lethal.Attack;
            using var own = session.SpawnBatch(lethal, 0, count / 2, new double3(-25.5, 0, -25.5), default, 1);
            using var foe = session.SpawnBatch(lethal, 1, count / 2, new double3(-25, 0, -25.5), default, 1);
            Tick(session);
            var timer = Stopwatch.StartNew();
            for (int i = 0; i < own.Length; i++)
            {
                if (session.ApplyOrder(UnitOrder.Attack(foe[i]), 0, own.GetSubArray(i, 1)) != UnitOrderResult.Accepted ||
                    session.ApplyOrder(UnitOrder.Attack(own[i]), 1, foe.GetSubArray(i, 1)) != UnitOrderResult.Accepted)
                    Assert.Fail($"Par {i} não recebeu ordens recíprocas.");
            }
            double orderMs = timer.Elapsed.TotalMilliseconds;
            timer.Restart();
            Tick(session);
            double tickMs = timer.Elapsed.TotalMilliseconds;
            Assert.That(session.InRangeUnits, Is.EqualTo(count));
            Assert.That(session.DefeatedUnits, Is.EqualTo(count));
            Assert.That(session.Count, Is.Zero);
            string record = FormattableString.Invariant(
                $"COMBAT_WAVE total={count} in_range={session.InRangeUnits} deaths={session.DefeatedUnits} commands_ms={orderMs:F4} target_ms={session.TargetMilliseconds:F4} pursuit_ms={session.PursuitMilliseconds:F4} attack_ms={session.StrikeMilliseconds:F4} damage_ms={session.DamageMilliseconds:F4} death_ms={session.DeathMilliseconds:F4} tick_ms={tickMs:F4}");
            TestContext.WriteLine(record);
            UnityEngine.Debug.Log(record);
        }

        private static UnitSession CreateSession()
        {
            var terrain = new ProceduralTerrainContract(new WorldDefinition(7));
            var min = new int2(-12);
            var max = new int2(12);
            var session = new UnitSession(terrain, min, max);
            session.SetNavigationGrid(new NavigationGrid(terrain, min, max, 100));
            return session;
        }

        private static NativeArray<Entity> Spawn(UnitSession session, int owner, double x, int count = 1,
            UnitKind kind = UnitKind.Soldier) => session.SpawnBatch(
            kind == UnitKind.Soldier ? UnitDefinition.Soldier : UnitDefinition.Villager,
            owner, count, new double3(x + .5, 0, .5), default, .04);

        private static float Health(UnitSession session, Entity entity) =>
            session.World.EntityManager.GetComponentData<UnitHealth>(entity).Current;

        private static void Tick(UnitSession session, int count = 1)
        {
            for (int i = 0; i < count; i++)
            {
                session.Advance(session.FixedDeltaTime);
                session.CompleteJobs();
            }
        }
    }
}
