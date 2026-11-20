using System;
using System.Collections.Generic;
using System.Diagnostics;
using RTSWorld.World.Environment;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace RTSWorld.Simulation
{
    public enum GatherPhase : byte { Idle, Approaching, Gathering, Delivering, Blocked }

    public struct GatherWorker : IComponentData
    {
        public GatherPhase Phase;
        public ResourceKey Resource;
        public int2 WorkCell, DeliveryCell;
        public NaturalResource CargoKind;
        public double Cargo;
        public uint BlockedDepotRevision;
    }

    public readonly struct ResourceAmount
    {
        public ResourceAmount(NaturalResource kind, double remaining, double initial)
        { Kind = kind; Remaining = remaining; Initial = initial; }
        public NaturalResource Kind { get; }
        public double Remaining { get; }
        public double Initial { get; }
    }

    public sealed partial class UnitSession
    {
        private readonly Dictionary<ResourceKey, double> extracted = new();
        private readonly Dictionary<(int owner, NaturalResource kind), double> stock = new();
        private readonly List<Entity> workers = new();
        private readonly HashSet<Entity> scheduledWorkers = new();
        private readonly List<int2> approaches = new();
        private readonly List<Entity> gatherBatch = new();
        private double gatherPerSecond, initialWood, initialStone;
        public int2 DepotCell { get; private set; }
        public ulong DepotId => 1;
        public bool EconomyEnabled { get; private set; }
        public int ActiveWorkers { get; private set; }
        public int GatheringWorkers { get; private set; }
        public int TransportingWorkers { get; private set; }
        public double DeliveredTotal { get; private set; }
        public double LostCargo { get; private set; }
        public double EconomyMilliseconds { get; private set; }
        public event Action<ResourceKey> ResourceDepleted;

        public void ConfigureEconomy(int2 depot, double productivity = 4, double wood = 60, double stone = 80,
            double startingWood = 0, double startingStone = 0)
        {
            if (EconomyEnabled) throw new InvalidOperationException("Economy already configured.");
            if (navigationGrid?.Obstacles == null || !navigationGrid.TryGetCost(depot, out int cost) || cost == 0)
                throw new ArgumentException("Depot needs a traversable cell.", nameof(depot));
            if (!math.isfinite(productivity) || productivity <= 0 || !math.isfinite(wood) || wood <= 0 ||
                !math.isfinite(stone) || stone <= 0 ||
                !math.isfinite(startingWood) || startingWood < 0 ||
                !math.isfinite(startingStone) || startingStone < 0)
                throw new ArgumentOutOfRangeException(nameof(productivity));
            DepotCell = depot;
            gatherPerSecond = productivity;
            initialWood = wood;
            initialStone = stone;
            if (startingWood > 0) stock[(0, NaturalResource.Tree)] = startingWood;
            if (startingStone > 0) stock[(0, NaturalResource.Stone)] = startingStone;
            EnableEconomyUpdates();
        }

        private void EnableEconomyUpdates()
        {
            EconomyEnabled = true;
            var economy = World.GetOrCreateSystemManaged<ResourceEconomySystem>();
            economy.Session = this;
            group.AddSystemToUpdateList(economy);
            group.SortSystems();
        }

        public bool TryGetResource(ResourceKey key, out ResourceAmount amount)
        {
            amount = default;
            if (!EconomyEnabled || !navigationGrid.Obstacles.TryGet(key, out var resource) ||
                resource.Placement.Kind == NaturalResource.Gold) return false;
            double initial = resource.Placement.Kind == NaturalResource.Tree ? initialWood : initialStone;
            extracted.TryGetValue(key, out double used);
            amount = new ResourceAmount(resource.Placement.Kind, resource.Available ? initial - used : 0, initial);
            return true;
        }

        public double GetStock(int owner, NaturalResource kind) =>
            stock.TryGetValue((owner, kind), out double amount) ? amount : 0;

        public UnitOrderResult Gather(ResourceKey key, int owner, NativeArray<Entity> entities)
        {
            if (!TryGetResource(key, out var amount) || amount.Remaining <= 0) return UnitOrderResult.InvalidDestination;
            CompleteJobs();
            var manager = World.EntityManager;
            gatherBatch.Clear();
            commandMembers.Clear();
            foreach (var entity in entities)
            {
                if (!manager.Exists(entity) || !manager.HasComponent<UnitIdentity>(entity) ||
                    !commandMembers.Add(entity)) continue;
                var identity = manager.GetComponentData<UnitIdentity>(entity);
                if (identity.Owner == owner && identity.Kind == UnitKind.Villager &&
                    manager.GetComponentData<UnitAttributes>(entity).CarryCapacity > 0)
                    gatherBatch.Add(entity);
            }
            if (gatherBatch.Count == 0) return UnitOrderResult.Empty;

            // One component lookup per order, reused for all candidate positions.
            var source = (int2)math.floor(manager.GetComponentData<LogicalPose>(gatherBatch[0]).Position.xz);
            if (!TryDeliveryCell(owner, source, out var depot))
                return UnitOrderResult.Unreachable;
            if (!AcquireField(depot, null, out int depotSlot)) return UnitOrderResult.CacheBusy;
            var depotField = sharedFields[depotSlot];
            navigationGrid.Obstacles.FindInteractionCells(navigationGrid, key, 3.5, approaches, depotField);
            if (approaches.Count == 0) return UnitOrderResult.Unreachable;
            foreach (var entity in gatherBatch)
            {
                var cell = (int2)math.floor(manager.GetComponentData<LogicalPose>(entity).Position.xz);
                if (!depotField.TryGetDirection(cell, out _)) return UnitOrderResult.Unreachable;
            }

            int assigned = 0;
            int previousAssignments = issuingAutomaticOrder && resourceAssignments.TryGetValue(key, out int count)
                ? count : 0;
            foreach (var entity in gatherBatch)
            {
                var worker = manager.GetComponentData<GatherWorker>(entity);
                int2 approach = approaches[(previousAssignments + assigned) % math.min(3, approaches.Count)];
                bool deliverFirst = worker.Cargo > 0 && worker.CargoKind != amount.Kind;
                if (!SetGatherLeg(manager, entity, deliverFirst ? depot : approach)) continue;
                CancelBuild(entity);
                CancelAttack(manager, entity);
                worker.DeliveryCell = depot;
                worker.Resource = key;
                worker.WorkCell = approach;
                worker.Phase = deliverFirst ? GatherPhase.Delivering : GatherPhase.Approaching;
                manager.SetComponentData(entity, worker);
                MarkManual(manager, entity);
                if (scheduledWorkers.Add(entity)) workers.Add(entity);
                assigned++;
            }
            return assigned > 0 ? UnitOrderResult.Accepted : UnitOrderResult.CacheBusy;
        }

        private bool SetGatherLeg(EntityManager manager, Entity entity, int2 destination)
        {
            if (!navigationGrid.TryGetCost(destination, out int cost) || cost == 0 ||
                !AcquireField(destination, null, out int slot)) return false;
            SetMovementField(manager, entity, slot, false, interactionCell: crowdEnabled ? destination : null);
            manager.GetBuffer<FormationWaypoint>(entity).Clear();
            return true;
        }

        private void CancelGather(EntityManager manager, Entity entity)
        {
            if (!EconomyEnabled) return;
            var worker = manager.GetComponentData<GatherWorker>(entity);
            worker.Phase = GatherPhase.Idle;
            manager.SetComponentData(entity, worker);
        }

        internal void TickEconomy(float seconds)
        {
            if (workers.Count == 0)
            {
                ActiveWorkers = GatheringWorkers = TransportingWorkers = 0;
                EconomyMilliseconds = 0;
                return;
            }

            long started = Stopwatch.GetTimestamp();
            CompleteJobs();
            var manager = World.EntityManager;
            ActiveWorkers = GatheringWorkers = TransportingWorkers = 0;
            int retained = 0;
            for (int i = 0; i < workers.Count; i++)
            {
                Entity entity = workers[i];
                if (!manager.Exists(entity)) { scheduledWorkers.Remove(entity); continue; }
                var worker = manager.GetComponentData<GatherWorker>(entity);
                UpdateWorker(manager, entity, ref worker, seconds);
                manager.SetComponentData(entity, worker);
                if (worker.Phase == GatherPhase.Idle ||
                    (worker.Phase == GatherPhase.Blocked && (!physicalDepots || worker.Cargo <= 0)))
                {
                    scheduledWorkers.Remove(entity);
                    continue;
                }

                workers[retained++] = entity;
                ActiveWorkers++;
                if (worker.Phase == GatherPhase.Gathering) GatheringWorkers++;
                if (worker.Phase == GatherPhase.Delivering && worker.Cargo > 0) TransportingWorkers++;
            }

            workers.RemoveRange(retained, workers.Count - retained);
            EconomyMilliseconds = MillisecondsSince(started);
        }

        private void UpdateWorker(EntityManager manager, Entity entity, ref GatherWorker worker, float seconds)
        {
            if (worker.Phase == GatherPhase.Idle) return;
            if (worker.Phase == GatherPhase.Blocked)
            {
                if (physicalDepots && worker.Cargo > 0 && worker.BlockedDepotRevision != depotRevision &&
                    movement.TerrainAvailable)
                    SendToDepot(manager, entity, ref worker);
                return;
            }
            var motion = manager.GetComponentData<UnitMotion>(entity);
            if (motion.Activity == UnitActivity.WaitingForTerrain) return;
            double2 point = manager.GetComponentData<LogicalPose>(entity).Position.xz;
            var position = (int2)math.floor(point);
            if (worker.Phase == GatherPhase.Gathering &&
                (!navigationGrid.TryGetCost(position, out int workCost) || workCost == 0))
            {
                FinishWorker(manager, entity, ref worker, GatherPhase.Blocked);
                return;
            }
            bool resourceAvailable = TryGetResource(worker.Resource, out var amount) && amount.Remaining > 0;

            if (worker.Phase != GatherPhase.Delivering && !resourceAvailable)
            {
                SendToDepot(manager, entity, ref worker);
                return;
            }

            if (worker.Phase == GatherPhase.Delivering || worker.Phase == GatherPhase.Approaching)
            {
                if (worker.Phase == GatherPhase.Delivering &&
                    !IsDeliveryCell(manager.GetComponentData<UnitIdentity>(entity).Owner, worker.DeliveryCell))
                {
                    SendToDepot(manager, entity, ref worker);
                    return;
                }
                int2 destination = worker.Phase == GatherPhase.Delivering ? worker.DeliveryCell : worker.WorkCell;
                if (motion.Activity == UnitActivity.WaitingForNavigation)
                {
                    if (physicalDepots && worker.Phase == GatherPhase.Delivering)
                        SendToDepot(manager, entity, ref worker);
                    else if (!SetGatherLeg(manager, entity, destination))
                        FinishWorker(manager, entity, ref worker, GatherPhase.Blocked);
                    return;
                }
                if (motion.Activity == UnitActivity.Unreachable)
                {
                    if (physicalDepots && worker.Phase == GatherPhase.Delivering)
                        SendToDepot(manager, entity, ref worker);
                    else FinishWorker(manager, entity, ref worker, GatherPhase.Blocked);
                    return;
                }
                bool inInteractionArea = position.Equals(destination) || (crowdEnabled &&
                    math.distancesq(point, destination + new double2(.5)) <= UnitMovementSystem.InteractionRadiusSquared &&
                    ClearNavigationSegment(point, destination + new double2(.5)));
                if (motion.Field != 0 || !inInteractionArea) return;

                if (worker.Phase == GatherPhase.Delivering)
                {
                    int owner = manager.GetComponentData<UnitIdentity>(entity).Owner;
                    stock[(owner, worker.CargoKind)] = GetStock(owner, worker.CargoKind) + worker.Cargo;
                    DeliveredTotal += worker.Cargo;
                    if (IsAutomatic(manager, entity)) AutonomousDeliveries++;
                    worker.Cargo = 0;
                    if (!resourceAvailable) FinishWorker(manager, entity, ref worker, GatherPhase.Idle);
                    else if (SetGatherLeg(manager, entity, worker.WorkCell)) worker.Phase = GatherPhase.Approaching;
                    else FinishWorker(manager, entity, ref worker, GatherPhase.Blocked);
                    return;
                }
                worker.Phase = GatherPhase.Gathering;
            }

            double capacity = manager.GetComponentData<UnitAttributes>(entity).CarryCapacity;
            double take = math.min(gatherPerSecond * seconds, math.min(amount.Remaining, math.max(0, capacity - worker.Cargo)));
            worker.CargoKind = amount.Kind;
            worker.Cargo += take;
            bool depleted = take >= amount.Remaining;
            extracted[worker.Resource] = depleted ? amount.Initial : amount.Initial - (amount.Remaining - take);
            if (depleted)
            {
                SetEnvironmentalResourceAvailable(worker.Resource, false);
                ResourceDepleted?.Invoke(worker.Resource);
            }

            if (depleted || worker.Cargo >= capacity) SendToDepot(manager, entity, ref worker);
        }

        private void SendToDepot(EntityManager manager, Entity entity, ref GatherWorker worker)
        {
            if (worker.Cargo <= 0)
                FinishWorker(manager, entity, ref worker, GatherPhase.Idle);
            else
            {
                int owner = manager.GetComponentData<UnitIdentity>(entity).Owner;
                int2 source = (int2)math.floor(manager.GetComponentData<LogicalPose>(entity).Position.xz);
                if (TryDeliveryCell(owner, source, out var destination) && SetGatherLeg(manager, entity, destination))
                {
                    worker.DeliveryCell = destination;
                    worker.Phase = GatherPhase.Delivering;
                }
                else FinishWorker(manager, entity, ref worker, GatherPhase.Blocked);
            }
        }

        private void FinishWorker(EntityManager manager, Entity entity, ref GatherWorker worker, GatherPhase phase)
        {
            worker.Phase = phase;
            if (phase == GatherPhase.Blocked) worker.BlockedDepotRevision = depotRevision;
            var motion = manager.GetComponentData<UnitMotion>(entity);
            motion.Field = 0;
            motion.HasFormation = false;
            motion.DesiredVelocity = default;
            manager.SetComponentData(entity, motion);
        }
    }

    [DisableAutoCreation]
    [UpdateInGroup(typeof(FixedStepSimulationSystemGroup))]
    [UpdateAfter(typeof(UnitMovementSystem))]
    public partial class ResourceEconomySystem : SystemBase
    {
        internal UnitSession Session;
        protected override void OnUpdate() => Session?.TickEconomy(SystemAPI.Time.DeltaTime);
    }
}
