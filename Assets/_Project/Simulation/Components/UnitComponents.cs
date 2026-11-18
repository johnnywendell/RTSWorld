using System;
using Unity.Entities;
using Unity.Mathematics;

namespace RTSWorld.Simulation
{
    public enum UnitKind : byte { Villager, Soldier }
    public enum UnitActivity : byte { Idle, Moving, WaitingForTerrain }
    public struct UnitIdentity : IComponentData { public UnitKind Kind; public int Owner; }
    public struct UnitHealth : IComponentData { public float Current, Maximum; }
    public struct UnitAttributes : IComponentData
    {
        public float MaxSpeed, CarryCapacity, Attack, Defense, Range;
    }
    // Velocity intention in logical XZ units/second; capped by MaxSpeed at each fixed tick.
    public struct UnitMotion : IComponentData { public double2 DesiredVelocity; public UnitActivity Activity; }
    public struct UnitBounds : IComponentData { public double2 Min, Max; }

    [Serializable]
    public struct UnitDefinition
    {
        public UnitKind Kind;
        public float Health, Speed, CarryCapacity, Attack, Defense, Range;
        public static UnitDefinition Villager => new UnitDefinition
            { Kind = UnitKind.Villager, Health = 50, Speed = 2, CarryCapacity = 10 };
        public static UnitDefinition Soldier => new UnitDefinition
            { Kind = UnitKind.Soldier, Health = 100, Speed = 3, Attack = 10, Defense = 3, Range = 1.5f };
        public void Validate()
        {
            if ((Kind != UnitKind.Villager && Kind != UnitKind.Soldier) ||
                !math.all(math.isfinite(new float4(Health, Speed, CarryCapacity, Attack))) ||
                !math.all(math.isfinite(new float2(Defense, Range))) || Health <= 0 || Speed < 0 ||
                CarryCapacity < 0 || Attack < 0 || Defense < 0 || Range < 0)
                throw new ArgumentOutOfRangeException(nameof(UnitDefinition));
        }
    }
}
