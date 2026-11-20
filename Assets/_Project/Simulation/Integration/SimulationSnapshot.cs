using System;
using RTSWorld.World.Environment;
using RTSWorld.World.Generation;
using Unity.Mathematics;

namespace RTSWorld.Simulation
{
    [Serializable]
    public sealed class SimulationSnapshot
    {
        public int Seed, TerrainVersion, CellsPerChunk, EnvironmentVersion;
        public float SeaLevel, FixedStep;
        public int2 Min, Max, Depot;
        public double MaximumSlope, Clearance;
        public bool Crowd, Paused, TerrainAvailable, HasNavigation, HasResources, CaptureSurface;
        public ulong Tick, NextUnitId;
        public double PendingTime, CombatTime, WoodPerNode, StonePerNode, Productivity, Delivered, Lost, GuardLeash;
        public int Defeated, DecisionInterval;
        public bool Economy, PhysicalDepots;
        public SavedResource[] Resources;
        public SavedStock[] Stock;
        public SavedBuilding[] Buildings;
        public SavedUnit[] Units;

        public ProceduralTerrainContract CreateTerrain() => new(
            new WorldDefinition(Seed, TerrainVersion, CellsPerChunk),
            EnvironmentVersion == 0 ? null : new EnvironmentDefinition(SeaLevel, EnvironmentVersion));
    }

    [Serializable]
    public struct SavedResource
    {
        public long X, Z;
        public double Extracted, Radius;
        public bool Available;
    }

    [Serializable]
    public struct SavedStock
    {
        public int Owner;
        public NaturalResource Kind;
        public double Amount;
    }

    [Serializable]
    public struct SavedBuilding
    {
        public ulong Id;
        public BuildingKind Kind;
        public ConstructionState State;
        public int Owner, Health;
        public int2 Origin;
        public double Work;
    }

    [Serializable]
    public struct SavedUnit
    {
        public ulong Id, Updates, AttackTarget, BuildTarget, RetryTargetAt, FailedTarget;
        public int Owner;
        public UnitDefinition Definition;
        public double3 Position, Velocity;
        public float4 Rotation;
        public float Health;
        public double2 DesiredVelocity;
        public UnitActivity Activity;
        public bool HasRoute, HasFormation, InteractionArrival, Autonomous, AutoBuild, Returning;
        public int2 Destination, InteractionCell, WorkCell, DeliveryCell, Anchor;
        public int2[] Waypoints;
        public int FormationStep, WorkOffset;
        public GatherPhase Gather;
        public long ResourceX, ResourceZ;
        public NaturalResource CargoKind;
        public double Cargo;
        public CombatPhase Combat;
        public double NextStrike, NextRoute, HitUntil, StrikeUntil;
        public AutonomyMode AutonomyMode;
        public AutonomyState AutonomyState;
    }
}
