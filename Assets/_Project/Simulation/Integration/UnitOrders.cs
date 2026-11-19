using System.Collections.Generic;
using System.Diagnostics;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

namespace RTSWorld.Simulation
{
    public enum UnitOrderKind : byte { Stop, Move, Attack }
    public enum UnitOrderResult : byte { Accepted, Empty, InvalidUnit, InvalidDestination, Unreachable, InsufficientSpace, CacheBusy, InvalidTarget }
    public readonly struct UnitOrder
    {
        private UnitOrder(UnitOrderKind kind, int2 destination, int spacing, Entity target = default)
        {
            Kind = kind;
            Destination = destination;
            Spacing = spacing;
            Target = target;
        }

        public UnitOrderKind Kind { get; }
        public int2 Destination { get; }
        public int Spacing { get; }
        public Entity Target { get; }
        public static UnitOrder Attack(Entity target) => new UnitOrder(UnitOrderKind.Attack, default, 0, target);
        public static UnitOrder Stop() => default;
        public static UnitOrder Move(int2 destination, int spacing = 1) =>
            new UnitOrder(UnitOrderKind.Move, destination, math.clamp(spacing, 1, 256));
    }

    public sealed partial class UnitSession
    {
        private readonly List<int2> formationCells = new();
        private readonly HashSet<Entity> commandMembers = new();
        public double OrderNavigationMilliseconds { get; private set; }
        public double OrderDistributionMilliseconds { get; private set; }
        public double OrderApplicationMilliseconds { get; private set; }

        public UnitOrderResult ApplyOrder(UnitOrder order, int owner, NativeArray<Entity> entities)
        {
            OrderNavigationMilliseconds = OrderDistributionMilliseconds = OrderApplicationMilliseconds = 0;
            if (entities.Length == 0) return UnitOrderResult.Empty;
            CompleteJobs();
            var manager = World.EntityManager;
            commandMembers.Clear();
            foreach (Entity entity in entities)
            {
                if (!manager.Exists(entity) || !manager.HasComponent<UnitIdentity>(entity) ||
                    manager.GetComponentData<UnitIdentity>(entity).Owner != owner || !commandMembers.Add(entity))
                    return UnitOrderResult.InvalidUnit;
            }

            if (order.Kind == UnitOrderKind.Attack) return OrderAttack(manager, order.Target, owner, entities);

            long started = Stopwatch.GetTimestamp();
            if (order.Kind == UnitOrderKind.Stop)
            {
                foreach (Entity entity in entities)
                {
                    var motion = manager.GetComponentData<UnitMotion>(entity);
                    motion.Field = 0;
                    motion.DesiredVelocity = default;
                    motion.HasFormation = false;
                    manager.SetComponentData(entity, motion);
                    manager.GetBuffer<FormationWaypoint>(entity).Clear();
                    CancelGather(manager, entity);
                    CancelAttack(manager, entity);
                }

                OrderApplicationMilliseconds = MillisecondsSince(started);
                return UnitOrderResult.Accepted;
            }

            if (navigationGrid == null || !navigationGrid.TryGetCost(order.Destination, out int cost) || cost == 0)
                return UnitOrderResult.InvalidDestination;

            var field = navigationSlots.TryGetValue(order.Destination, out int cached)
                ? sharedFields[cached] : NavigationSearch.BuildFlowField(navigationGrid, order.Destination);
            foreach (Entity entity in entities)
            {
                int2 cell = (int2)math.floor(manager.GetComponentData<LogicalPose>(entity).Position.xz);
                if (!field.TryGetDirection(cell, out _)) return UnitOrderResult.Unreachable;
            }
            OrderNavigationMilliseconds = MillisecondsSince(started);

            started = Stopwatch.GetTimestamp();
            CollectFormationCells(field, order.Destination, order.Spacing, entities.Length);
            OrderDistributionMilliseconds = MillisecondsSince(started);
            if (formationCells.Count != entities.Length) return UnitOrderResult.InsufficientSpace;

            started = Stopwatch.GetTimestamp();
            if (!AcquireField(order.Destination, field, out int slot, commandMembers)) return UnitOrderResult.CacheBusy;
            for (int i = 0; i < entities.Length; i++)
            {
                Entity entity = entities[i];
                var route = manager.GetBuffer<FormationWaypoint>(entity);
                route.Clear();
                int2 cell = formationCells[i];
                while (math.any(cell != order.Destination))
                {
                    route.Add(new FormationWaypoint { Cell = cell });
                    field.TryGetDirection(cell, out int2 direction);
                    cell += direction;
                }

                SetMovementField(manager, entity, slot, true);
                CancelGather(manager, entity);
                CancelAttack(manager, entity);
            }
            OrderApplicationMilliseconds = MillisecondsSince(started);
            return UnitOrderResult.Accepted;
        }

        private void CollectFormationCells(NavigationFlowField field, int2 anchor, int spacing, int count)
        {
            formationCells.Clear();
            formationCells.Add(anchor);
            int limit = math.cmax(navigationGrid.Size);
            for (int ring = spacing; ring < limit && formationCells.Count < count; ring += spacing)
            {
                for (int x = -ring; x < ring && formationCells.Count < count; x += spacing)
                {
                    Add(anchor + new int2(x, -ring));
                    Add(anchor + new int2(-x, ring));
                }
                for (int z = -ring; z < ring && formationCells.Count < count; z += spacing)
                {
                    Add(anchor + new int2(ring, z));
                    Add(anchor + new int2(-ring, -z));
                }
            }

            void Add(int2 cell)
            {
                if (formationCells.Count < count && field.TryGetDirection(cell, out _))
                    formationCells.Add(cell);
            }
        }

        private static double MillisecondsSince(long timestamp) =>
            (Stopwatch.GetTimestamp() - timestamp) * (1000.0 / Stopwatch.Frequency);
    }
}
