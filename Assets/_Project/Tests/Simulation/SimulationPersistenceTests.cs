using System;
using System.IO;
using NUnit.Framework;
using RTSWorld.Simulation;
using RTSWorld.World.Generation;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace RTSWorld.Tests
{
    public sealed class SimulationPersistenceTests
    {
        private string path;

        [SetUp]
        public void Setup() => path = Path.Combine(Application.dataPath, "..", "Library", "RTSWorld", Guid.NewGuid() + ".rts");

        [TearDown]
        public void TearDown() => File.Delete(path);

        private static UnitSession CreateSession()
        {
            var terrain = new ProceduralTerrainContract(new WorldDefinition(7));
            var session = new UnitSession(terrain, new int2(-8), new int2(8), crowdEnabled: true);
            session.SetNavigationGrid(new NavigationGrid(terrain, new int2(-8), new int2(8), 100));
            return session;
        }

        [Test]
        public void RepeatedSaveAndLoadReconstructAnActiveRouteWithoutAddingTicks()
        {
            using var original = CreateSession();
            using var units = original.SpawnBatch(UnitDefinition.Villager, 0, 1, new double3(-3.5, 0, .5), default);
            Assert.That(original.ApplyOrder(UnitOrder.Move(new int2(4, 0)), 0, units), Is.EqualTo(UnitOrderResult.Accepted));
            original.Advance(.02f);
            var saved = original.CaptureSnapshot();
            SimulationSaveFile.SaveAsync(path, saved).GetAwaiter().GetResult();
            SimulationSaveFile.SaveAsync(path, saved).GetAwaiter().GetResult();

            for (int i = 0; i < 2; i++)
            {
                using var restored = UnitSession.RestoreSnapshot(SimulationSaveFile.LoadAsync(path).GetAwaiter().GetResult());
                Assert.That(restored.Terrain.Definition.Seed, Is.EqualTo(7));
                Assert.That(restored.Count, Is.EqualTo(1));
                Assert.That(restored.Tick, Is.EqualTo(saved.Tick));
                var before = restored.CaptureSnapshot().Units[0];
                Assert.That(before.Id, Is.EqualTo(saved.Units[0].Id));
                Assert.That(before.Position, Is.EqualTo(saved.Units[0].Position));
                Assert.That(before.HasRoute, Is.True);
                restored.Advance(.01f);
                Assert.That(restored.Tick, Is.EqualTo(saved.Tick));
                restored.Advance(.025f);
                Assert.That(restored.Tick, Is.EqualTo(saved.Tick + 1));
                Assert.That(restored.CaptureSnapshot().Units[0].Position.x, Is.GreaterThan(before.Position.x));
            }
        }

        [TestCase("version")]
        [TestCase("truncated")]
        [TestCase("integrity")]
        [TestCase("header")]
        public void RejectsInvalidFiles(string defect)
        {
            using var session = CreateSession();
            SimulationSaveFile.SaveAsync(path, session.CaptureSnapshot()).GetAwaiter().GetResult();
            var bytes = File.ReadAllBytes(path);
            switch (defect)
            {
                case "version": bytes[4]++; break;
                case "truncated": Array.Resize(ref bytes, bytes.Length - 1); break;
                case "integrity": bytes[bytes.Length - 1] ^= 1; break;
                case "header": bytes[0] = 0; break;
            }
            File.WriteAllBytes(path, bytes);
            Assert.Throws<InvalidDataException>(() => SimulationSaveFile.LoadAsync(path).GetAwaiter().GetResult());
        }

        [Test]
        public void CombatTargetAndCooldownSurviveRoundTrip()
        {
            using var original = CreateSession();
            using var allies = original.SpawnBatch(UnitDefinition.Soldier, 0, 1, new double3(.5, 0, .5), default);
            using var foes = original.SpawnBatch(UnitDefinition.Soldier, 1, 1, new double3(1.5, 0, .5), default);
            Assert.That(original.ApplyOrder(UnitOrder.Attack(foes[0]), 0, allies), Is.EqualTo(UnitOrderResult.Accepted));
            original.Advance(0);
            var saved = original.CaptureSnapshot();
            SimulationSaveFile.SaveAsync(path, saved).GetAwaiter().GetResult();
            using var restored = UnitSession.RestoreSnapshot(SimulationSaveFile.LoadAsync(path).GetAwaiter().GetResult());
            var attacker = Array.Find(saved.Units, unit => unit.Owner == 0);
            var target = Array.Find(saved.Units, unit => unit.Owner == 1);
            Assert.That(target.Health, Is.LessThan(UnitDefinition.Soldier.Health));
            Assert.That(Array.Find(restored.CaptureSnapshot().Units, unit => unit.Owner == 0).AttackTarget,
                Is.EqualTo(target.Id));
            restored.Advance(.1f);
            Assert.That(Array.Find(restored.CaptureSnapshot().Units, unit => unit.Owner == 1).Health, Is.EqualTo(target.Health));
            for (int i = 0; i < 12; i++) restored.Advance(.05f);
            Assert.That(Array.Find(restored.CaptureSnapshot().Units, unit => unit.Owner == 1).Health, Is.LessThan(target.Health));
            Assert.That(attacker.NextStrike, Is.GreaterThan(saved.CombatTime));
        }

        [Test]
        public void MissingAttackTargetCancelsAttackAndPreservesHealthAndStoppedAutonomy()
        {
            using var original = CreateSession();
            using var members = original.SpawnBatch(UnitDefinition.Soldier, 0, 1, new double3(-1.5, 0, .5), default);
            using var enemies = original.SpawnBatch(UnitDefinition.Soldier, 1, 1, new double3(.5, 0, .5), default);
            original.EnableAutonomy(members[0], AutonomyMode.Guard);
            original.ApplyOrder(UnitOrder.Stop(), 0, members);
            Assert.That(original.ApplyOrder(UnitOrder.Attack(enemies[0]), 0, members), Is.EqualTo(UnitOrderResult.Accepted));
            var saved = original.CaptureSnapshot();
            var soldier = Array.Find(saved.Units, unit => unit.Owner == 0);
            Assert.That(soldier.AttackTarget, Is.Not.Zero);
            soldier.Health = 63;
            soldier.AutonomyState = AutonomyState.Stopped;
            saved.Units = new[] { soldier };

            using var restored = UnitSession.RestoreSnapshot(saved);
            var loaded = restored.CaptureSnapshot().Units[0];
            Assert.That(loaded.AttackTarget, Is.Zero);
            Assert.That(loaded.Combat, Is.EqualTo(CombatPhase.Idle));
            Assert.That(loaded.Health, Is.EqualTo(63));
            Assert.That(loaded.AutonomyState, Is.EqualTo(AutonomyState.Stopped));
        }
    }
}
