using System;
using Unity.Entities;
using Unity.Mathematics;

namespace RTSWorld.Simulation
{
    public enum UnitKind : byte { Villager, Soldier }
    public enum NavigationProgress : byte { Idle, Advancing, Congested, Deadlocked, WaitingForPassage, Blocked, NoRoute, WaitingForTerrain, Recovering, Arrived }
    public enum UnitActivity : byte { Idle, Moving, WaitingForTerrain, WaitingForNavigation, Unreachable }
    public struct UnitIdentity : IComponentData { public UnitKind Kind; public int Owner; }
    public struct UnitHealth : IComponentData { public float Current, Maximum; }
    public struct UnitAttributes : IComponentData
    {
        public float MaxSpeed, CarryCapacity, Attack, Defense, Range, OccupancyRadius;
    }
    // Velocity intention in logical XZ units/second; capped by MaxSpeed at each fixed tick.
    public struct UnitMotion : IComponentData
    {
        public double2 DesiredVelocity;
        public UnitActivity Activity;
        public byte NearbyUnits { get; internal set; }
        public bool IsCongested => CongestionTicks >= 8;
        public NavigationProgress Progress { get; internal set; }
        public float RouteDistanceTravelled { get; internal set; }
        public float NoProgressSeconds { get; internal set; }
        public float PassageWaitSeconds { get; internal set; }
        public ushort RecoveryAttempts { get; internal set; }
        internal byte RecoveryTicks, RecoveryCooldown;
        internal sbyte RecoverySide;
        internal ulong YieldTo;
        internal int2 YieldGate, YieldStep, YieldBay;
        public bool HasNavigationField => Field != 0;
        public int FormationWaypointIndex => HasFormation ? FormationStep : -1;
        internal int Field;
        internal uint NavigationRevision;
        internal bool HasFormation, InteractionArrival;
        internal int2 InteractionCell, RouteDestination;
        internal byte CongestionTicks;
        internal int FormationStep;
        internal int FormationProbeIndex;
    }
    [InternalBufferCapacity(8)]
    public struct FormationWaypoint : IBufferElementData { public int2 Cell; }
    public struct UnitBounds : IComponentData { public double2 Min, Max; }

    [Serializable]
    public struct UnitDefinition
    {
        public UnitKind Kind;
        public float Health, Speed, CarryCapacity, Attack, Defense, Range, AttackInterval, OccupancyRadius;
        public static UnitDefinition Villager => new UnitDefinition
            { Kind = UnitKind.Villager, Health = 50, Speed = 2, CarryCapacity = 10, OccupancyRadius = .22f };
        public static UnitDefinition Soldier => new UnitDefinition
            { Kind = UnitKind.Soldier, Health = 100, Speed = 3, Attack = 10, Defense = 3, Range = 1.5f, AttackInterval = .6f, OccupancyRadius = .30f };
        public void Validate()
        {
            if ((Kind != UnitKind.Villager && Kind != UnitKind.Soldier) ||
                !math.all(math.isfinite(new float4(Health, Speed, CarryCapacity, Attack))) ||
                !math.all(math.isfinite(new float2(Defense, Range))) || Health <= 0 || Speed < 0 ||
                !math.isfinite(OccupancyRadius) || OccupancyRadius < 0 || OccupancyRadius > .49f ||
                !math.isfinite(AttackInterval) || AttackInterval < 0 || CarryCapacity < 0 || Attack < 0 || Defense < 0 || Range < 0)
                throw new ArgumentOutOfRangeException(nameof(UnitDefinition));
        }
    }
}
