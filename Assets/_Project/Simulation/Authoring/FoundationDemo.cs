using RTSWorld.World.Generation;
using RTSWorld.World.Environment;
using Unity.Mathematics;
using UnityEngine;

namespace RTSWorld.Simulation
{
    /// <summary>Configuration, lifecycle and diagnostics only. Movement runs in ECS jobs.</summary>
    public sealed class FoundationDemo : MonoBehaviour
    {
        [SerializeField, Range(1, 4096)] private int entityCount = 256;
        [SerializeField] private int seed = 7;
        public FoundationSession Session { get; private set; }
        private void OnEnable()
        {
            Session = new FoundationSession();
            var terrain = new ProceduralTerrainContract(new WorldDefinition(seed), new EnvironmentDefinition(version: 4));
            for (int i = 0; i < entityCount; i++)
            {
                double x = i % 16 - 8, z = i / 16 - 8;
                Session.Spawn((ulong)i + 1, new double3(x, terrain.Elevation(x, z), z), new double3(1, 0, 0));
            }
        }
        private void Update() => Session.Advance(Time.deltaTime);
        private void OnDisable() { Session?.Dispose(); Session = null; }
        private void OnGUI()
        {
            if (Session == null) return;
            GUI.Label(new Rect(20, 20, 700, 70), $"ECS-001 | Entities {Session.Count} | Tick {Session.Tick} | Fixed {Session.FixedDeltaTime:F3}s\nJobs + Burst | Logical motion X: 1 unit/s | Terrain sampled at spawn only");
        }
    }
}
