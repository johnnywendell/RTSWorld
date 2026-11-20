using System;
using System.Collections.Generic;
using System.IO;
using RTSWorld.World.Environment;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace RTSWorld.Simulation
{
    public sealed partial class UnitSession
    {
        public NavigationGrid Navigation => navigationGrid;
        public ProceduralTerrainContract Terrain => proceduralTerrain;

        public bool IsResourceAvailable(ResourceKey key) =>
            navigationGrid?.Obstacles == null || !navigationGrid.Obstacles.TryGet(key, out var resource) || resource.Available;

        /// <summary>Call on the owner thread between Advance calls.</summary>
        public SimulationSnapshot CaptureSnapshot()
        {
            if (proceduralTerrain == null || navigationGrid?.HasCustomConstraints == true)
                throw new InvalidOperationException("A sessão exige terreno procedural e navegação reproduzível.");
            CompleteJobs();
            var world = proceduralTerrain.Definition;
            var environment = proceduralTerrain.Environment;
            var obstacles = navigationGrid?.Obstacles;
            var saved = new SimulationSnapshot
            {
                Seed = world.Seed, TerrainVersion = world.GeneratorVersion, CellsPerChunk = world.CellsPerChunk,
                EnvironmentVersion = environment?.Version ?? 0, SeaLevel = environment?.SeaLevel ?? 0,
                Min = (int2)Bounds.Min, Max = (int2)Bounds.Max, FixedStep = FixedDeltaTime,
                Paused = Paused, Crowd = crowdEnabled, Tick = Tick, NextUnitId = nextId,
                PendingTime = math.max(0, elapsed - movement.LastFixedElapsed),
                TerrainAvailable = movement.TerrainAvailable, HasNavigation = navigationGrid != null,
                HasResources = obstacles != null, CaptureSurface = navigationGrid?.CapturesSurface ?? false,
                MaximumSlope = navigationGrid?.MaximumSlope ?? 0, Clearance = obstacles?.Clearance ?? 0,
                Economy = EconomyEnabled, PhysicalDepots = physicalDepots, Depot = DepotCell,
                WoodPerNode = initialWood, StonePerNode = initialStone, Productivity = gatherPerSecond,
                Delivered = DeliveredTotal, Lost = LostCargo, CombatTime = combatTime, Defeated = DefeatedUnits,
                DecisionInterval = decisionInterval, GuardLeash = guardLeash,
                Buildings = new SavedBuilding[buildings.Count], Stock = new SavedStock[stock.Count]
            };
            var resources = new List<SavedResource>();
            if (obstacles != null)
                foreach (var key in obstacles.ResourceKeys)
                {
                    obstacles.TryGet(key, out var resource);
                    extracted.TryGetValue(key, out double amount);
                    resources.Add(new SavedResource { X = key.X, Z = key.Z, Extracted = amount,
                        Available = resource.Available, Radius = resource.Radius });
                }
            saved.Resources = resources.ToArray();

            int i = 0;
            foreach (var entry in stock)
                saved.Stock[i++] = new SavedStock { Owner = entry.Key.owner, Kind = entry.Key.kind, Amount = entry.Value };

            var manager = World.EntityManager;
            for (i = 0; i < buildings.Count; i++)
            {
                var entity = buildings[i];
                var building = manager.GetComponentData<Building>(entity);
                saved.Buildings[i] = new SavedBuilding { Id = PersistentId(manager, entity), Kind = building.Kind,
                    Owner = building.Owner, Origin = building.Origin, Health = building.Health,
                    State = building.State, Work = building.Work };
            }

            using var handles = units.ToEntityArray(Allocator.Temp);
            saved.Units = new SavedUnit[handles.Length];
            for (i = 0; i < handles.Length; i++)
                saved.Units[i] = CaptureUnit(manager, handles[i]);
            return saved;
        }

        private static ulong PersistentId(EntityManager manager, Entity entity) =>
            manager.Exists(entity) && manager.HasComponent<LogicalEntityId>(entity)
                ? manager.GetComponentData<LogicalEntityId>(entity).Value : 0;

        private SavedUnit CaptureUnit(EntityManager manager, Entity entity)
        {
            var identity = manager.GetComponentData<UnitIdentity>(entity);
            var pose = manager.GetComponentData<LogicalPose>(entity);
            var health = manager.GetComponentData<UnitHealth>(entity);
            var attributes = manager.GetComponentData<UnitAttributes>(entity);
            var motion = manager.GetComponentData<UnitMotion>(entity);
            var combat = manager.GetComponentData<UnitCombat>(entity);
            var worker = manager.GetComponentData<GatherWorker>(entity);
            bool hasAutonomy = manager.HasComponent<UnitAutonomy>(entity);
            var ai = hasAutonomy ? manager.GetComponentData<UnitAutonomy>(entity) : default;
            buildTargets.TryGetValue(entity, out var building);
            var buffer = manager.GetBuffer<FormationWaypoint>(entity);
            var waypoints = motion.HasFormation && buffer.Length > 0 ? new int2[buffer.Length] : Array.Empty<int2>();
            for (int i = 0; i < waypoints.Length; i++) waypoints[i] = buffer[i].Cell;
            return new SavedUnit
            {
                Id = PersistentId(manager, entity), Owner = identity.Owner,
                Definition = new UnitDefinition { Kind = identity.Kind, Health = health.Maximum,
                    Speed = attributes.MaxSpeed, CarryCapacity = attributes.CarryCapacity, Attack = attributes.Attack,
                    Defense = attributes.Defense, Range = attributes.Range, OccupancyRadius = attributes.OccupancyRadius,
                    AttackInterval = (float)combat.Interval },
                Health = health.Current, Position = pose.Position, Rotation = pose.Rotation.value,
                Velocity = manager.GetComponentData<MovementVelocity>(entity).Value,
                Updates = manager.GetComponentData<EntitySimulationState>(entity).Updates,
                DesiredVelocity = motion.DesiredVelocity, Activity = motion.Activity,
                HasRoute = motion.Field > 0, HasFormation = motion.HasFormation, Destination = motion.RouteDestination,
                InteractionArrival = motion.InteractionArrival, InteractionCell = motion.InteractionCell,
                Waypoints = waypoints, FormationStep = motion.HasFormation ? motion.FormationStep : -1,
                Gather = worker.Phase, ResourceX = worker.Resource.X, ResourceZ = worker.Resource.Z,
                WorkCell = worker.WorkCell, DeliveryCell = worker.DeliveryCell,
                CargoKind = worker.CargoKind, Cargo = worker.Cargo,
                Combat = combat.Phase, AttackTarget = PersistentId(manager, combat.Target),
                NextStrike = combat.NextStrike, NextRoute = combat.NextRoute,
                HitUntil = combat.HitUntil, StrikeUntil = combat.StrikeUntil,
                BuildTarget = PersistentId(manager, building), Autonomous = hasAutonomy, AutoBuild = ai.AutoBuild,
                AutonomyMode = ai.Mode, AutonomyState = ai.State, Anchor = ai.Anchor, Returning = ai.Returning,
                WorkOffset = ai.WorkOffset, FailedTarget = PersistentId(manager, ai.FailedTarget), RetryTargetAt = ai.RetryTargetAt
            };
        }

        public static UnitSession RestoreSnapshot(SimulationSnapshot saved)
        {
            if (saved == null || saved.Units == null || saved.Stock == null || saved.Buildings == null ||
                saved.Resources == null || saved.NextUnitId == 0 || saved.DecisionInterval < 1 ||
                !Nonnegative(saved.PendingTime) || !Nonnegative(saved.CombatTime) || !Nonnegative(saved.Delivered) || !Nonnegative(saved.Lost) ||
                !Nonnegative(saved.Productivity) || !Nonnegative(saved.WoodPerNode) || !Nonnegative(saved.StonePerNode) ||
                !Nonnegative(saved.GuardLeash) || saved.Defeated < 0 ||
                (saved.HasResources && (!saved.HasNavigation || saved.EnvironmentVersion == 0)) ||
                (saved.Economy && (!saved.HasResources || saved.Productivity <= 0 ||
                    saved.WoodPerNode <= 0 || saved.StonePerNode <= 0)) || (saved.PhysicalDepots && !saved.Economy))
                throw new InvalidDataException("Estado da sessão inválido.");
            var terrain = saved.CreateTerrain();
            var result = new UnitSession(terrain, saved.Min, saved.Max, saved.FixedStep, saved.Crowd, saved.Tick != 0);
            try
            {
                result.ApplySnapshot(saved);
                return result;
            }
            catch
            {
                result.Dispose();
                throw;
            }
        }

        private void ApplySnapshot(SimulationSnapshot saved)
        {
            EnvironmentalObstacles obstacles = saved.HasResources
                ? EnvironmentalObstacles.Build(proceduralTerrain, saved.Min, saved.Max, saved.Clearance) : null;
            var resourceKeys = new HashSet<ResourceKey>();
            foreach (var resource in saved.Resources)
            {
                var key = new ResourceKey(saved.Seed, saved.EnvironmentVersion, resource.X, resource.Z);
                if (obstacles == null || !obstacles.TryGet(key, out var baseline) || !resourceKeys.Add(key) || !Nonnegative(resource.Extracted))
                    throw new InvalidDataException("Recurso inválido no salvamento.");
                double initial = baseline.Placement.Kind switch
                {
                    NaturalResource.Tree => saved.WoodPerNode,
                    NaturalResource.Stone => saved.StonePerNode,
                    _ => 0
                };
                if (!Nonnegative(resource.Radius) || resource.Radius > 1 || resource.Extracted > initial)
                    throw new InvalidDataException("Quantidade ou ocupação de recurso inválida.");
                if (resource.Radius != baseline.Radius) obstacles.SetRadius(key, resource.Radius, null);
                if (resource.Available != baseline.Available) obstacles.SetAvailable(key, resource.Available, null);
                if (resource.Extracted > 0) extracted.Add(key, resource.Extracted);
            }

            if (saved.Resources.Length != (obstacles?.ResourceCount ?? 0))
                throw new InvalidDataException("Catálogo procedural incompleto.");
            if (saved.HasNavigation)
                SetNavigationGrid(new NavigationGrid(proceduralTerrain, saved.Min, saved.Max, saved.MaximumSlope,
                    captureSurface: saved.CaptureSurface, obstacles: obstacles));
            if (saved.Economy) EnableEconomyUpdates();
            physicalDepots = saved.PhysicalDepots;
            DepotCell = saved.Depot;
            initialWood = saved.WoodPerNode;
            initialStone = saved.StonePerNode;
            gatherPerSecond = saved.Productivity;
            DeliveredTotal = saved.Delivered;
            LostCargo = saved.Lost;
            foreach (var entry in saved.Stock)
                if (!Enum.IsDefined(typeof(NaturalResource), entry.Kind) || !Nonnegative(entry.Amount) ||
                    !stock.TryAdd((entry.Owner, entry.Kind), entry.Amount))
                    throw new InvalidDataException("Estoque inválido no salvamento.");

            var manager = World.EntityManager;
            var ids = new Dictionary<ulong, Entity>();
            foreach (var building in saved.Buildings)
            {
                var definition = BuildingDefinition.Get(building.Kind);
                if (!Enum.IsDefined(typeof(ConstructionState), building.State) ||
                    !Nonnegative(building.Work) || building.Work > definition.Work ||
                    building.Health <= 0 || building.Health > definition.Health ||
                    navigationGrid == null || math.any(building.Origin < saved.Min) ||
                    math.any((double2)building.Origin + definition.Size > saved.Max))
                    throw new InvalidDataException("Construção inválida no salvamento.");
                var entity = manager.CreateEntity(typeof(Building), typeof(LogicalEntityId));
                RegisterId(manager, ids, entity, building.Id, saved.NextUnitId);
                manager.SetComponentData(entity, new Building { Kind = building.Kind, Origin = building.Origin,
                    Owner = building.Owner, Health = building.Health, State = building.State, Work = building.Work });
                buildings.Add(entity);
                UpdateBuildingFootprint(building.Origin, definition.Size, true);
            }

            using var handles = new NativeArray<Entity>(saved.Units.Length, Allocator.Temp);
            manager.CreateEntity(archetype, handles);
            for (int i = 0; i < handles.Length; i++)
                RegisterId(manager, ids, handles[i], saved.Units[i].Id, saved.NextUnitId);
            for (int i = 0; i < handles.Length; i++)
                RestoreUnit(manager, ids, handles[i], saved.Units[i], saved);

            nextId = saved.NextUnitId;
            elapsed = saved.PendingTime;
            Paused = saved.Paused;
            SetTerrainAvailable(saved.TerrainAvailable);
            combatTime = saved.CombatTime;
            DefeatedUnits = saved.Defeated;
            decisionInterval = saved.DecisionInterval;
            guardLeash = saved.GuardLeash;
            manager.SetComponentData(settings, new SimulationClock { Tick = saved.Tick });
        }

        private static bool Nonnegative(double value) => math.isfinite(value) && value >= 0;

        private static void RegisterId(EntityManager manager, Dictionary<ulong, Entity> ids, Entity entity, ulong id, ulong next)
        {
            if (id == 0 || id >= next || !ids.TryAdd(id, entity))
                throw new InvalidDataException("Identificador duplicado ou inválido no salvamento.");
            manager.SetComponentData(entity, new LogicalEntityId { Value = id });
        }

        private void RestoreUnit(EntityManager manager, Dictionary<ulong, Entity> ids, Entity entity,
            SavedUnit unit, SimulationSnapshot saved)
        {
            unit.Definition.Validate();
            if (!math.all(math.isfinite(unit.Position)) || !math.all(math.isfinite(unit.Rotation)) ||
                !math.all(math.isfinite(unit.Velocity)) || !math.all(math.isfinite(unit.DesiredVelocity)) ||
                math.any(unit.Position.xz < Bounds.Min) || math.any(unit.Position.xz > Bounds.Max) ||
                unit.Owner < 0 || unit.WorkOffset < 0 || !Nonnegative(unit.Health) || unit.Health > unit.Definition.Health ||
                !Nonnegative(unit.Cargo) || unit.Cargo > unit.Definition.CarryCapacity ||
                !Nonnegative(unit.NextStrike) || !Nonnegative(unit.NextRoute) ||
                !Nonnegative(unit.HitUntil) || !Nonnegative(unit.StrikeUntil) ||
                !Enum.IsDefined(typeof(UnitActivity), unit.Activity) ||
                !Enum.IsDefined(typeof(GatherPhase), unit.Gather) ||
                !Enum.IsDefined(typeof(CombatPhase), unit.Combat) ||
                !Enum.IsDefined(typeof(AutonomyMode), unit.AutonomyMode) ||
                !Enum.IsDefined(typeof(AutonomyState), unit.AutonomyState) ||
                unit.Waypoints == null || unit.FormationStep < -1 || unit.FormationStep >= math.max(1, unit.Waypoints.Length))
                throw new InvalidDataException("Unidade inválida no salvamento.");

            InitializeUnit(manager, entity, unit.Definition, unit.Owner, unit.Position, unit.DesiredVelocity, saved.CellsPerChunk);
            manager.SetComponentData(entity, new LogicalEntityId { Value = unit.Id });
            manager.SetComponentData(entity, new LogicalPose { Position = unit.Position, Rotation = new quaternion(unit.Rotation) });
            manager.SetComponentData(entity, new MovementVelocity { Value = unit.Velocity });
            manager.SetComponentData(entity, new UnitHealth { Current = unit.Health, Maximum = unit.Definition.Health });
            manager.SetComponentData(entity, new EntitySimulationState { Updates = unit.Updates });

            var motion = new UnitMotion { DesiredVelocity = unit.DesiredVelocity, Activity = unit.Activity,
                InteractionArrival = unit.InteractionArrival, InteractionCell = unit.InteractionCell };
            if (unit.HasRoute && navigationGrid != null && navigationGrid.TryGetCost(unit.Destination, out int cost) &&
                cost > 0 && AcquireField(unit.Destination, null, out int slot))
            {
                motion.Field = slot + 1;
                motion.RouteDestination = unit.Destination;
                motion.NavigationRevision = navigationRevision;
                motion.HasFormation = unit.HasFormation;
                motion.FormationStep = unit.FormationStep;
                var buffer = manager.GetBuffer<FormationWaypoint>(entity);
                foreach (var cell in unit.Waypoints) buffer.Add(new FormationWaypoint { Cell = cell });
            }
            else if (unit.HasRoute) motion.Activity = UnitActivity.WaitingForNavigation;
            manager.SetComponentData(entity, motion);

            var key = new ResourceKey(saved.Seed, saved.EnvironmentVersion, unit.ResourceX, unit.ResourceZ);
            var gather = unit.Gather;
            if (gather != GatherPhase.Idle && navigationGrid?.Obstacles != null &&
                navigationGrid.Obstacles.TryGet(key, out _))
            {
                workers.Add(entity);
                scheduledWorkers.Add(entity);
            }
            else gather = GatherPhase.Idle;
            manager.SetComponentData(entity, new GatherWorker { Phase = gather, Resource = key,
                BlockedDepotRevision = depotRevision,
                Cargo = unit.Cargo, CargoKind = unit.CargoKind, WorkCell = unit.WorkCell, DeliveryCell = unit.DeliveryCell });

            ids.TryGetValue(unit.AttackTarget, out var attack);
            if (attack != Entity.Null && manager.HasComponent<UnitIdentity>(attack))
            {
                attackers.Add(entity);
                scheduledAttackers.Add(entity);
            }
            else
            {
                attack = Entity.Null;
                if (unit.Combat != CombatPhase.Idle) HoldPosition(manager, entity);
            }

            manager.SetComponentData(entity, new UnitCombat { Target = attack,
                Phase = attack == Entity.Null ? CombatPhase.Idle : unit.Combat, Interval = unit.Definition.AttackInterval,
                NextStrike = unit.NextStrike, NextRoute = unit.NextRoute, HitUntil = unit.HitUntil, StrikeUntil = unit.StrikeUntil });
            if (ids.TryGetValue(unit.BuildTarget, out var site) && manager.HasComponent<Building>(site))
                buildTargets.Add(entity, site);
            else if (unit.BuildTarget != 0) HoldPosition(manager, entity);
            if (unit.Autonomous)
            {
                ids.TryGetValue(unit.FailedTarget, out var failed);
                manager.AddComponentData(entity, new UnitAutonomy { Mode = unit.AutonomyMode, State = unit.AutonomyState,
                    Anchor = unit.Anchor, AutoBuild = unit.AutoBuild, Returning = unit.Returning,
                    WorkOffset = unit.WorkOffset, FailedTarget = failed, RetryTargetAt = unit.RetryTargetAt });
            }
        }
    }
}
