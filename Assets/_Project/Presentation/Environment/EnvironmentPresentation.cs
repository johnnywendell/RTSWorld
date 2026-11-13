using RTSWorld.World.Chunks;
using RTSWorld.World.Environment;
using UnityEngine;

namespace RTSWorld.Presentation.Environment
{
    public sealed class EnvironmentPresentation : MonoBehaviour
    {
        [SerializeField, Range(0, 100)] private float seaLevel = 38;
        [SerializeField] private Material environmentMaterial;
        public int Revision { get; private set; }
        public float SeaLevel => seaLevel;
        public Material Material => environmentMaterial;
        public void Configure(Material material, float sea = 38)
        {
            _ = new EnvironmentDefinition(sea);
            if (material == null) throw new System.ArgumentNullException(nameof(material));
            environmentMaterial = material; seaLevel = sea; Revision++;
        }
        private void OnValidate() => Revision++;
        public void Validate()
        {
            _ = new EnvironmentDefinition(seaLevel);
            if (environmentMaterial == null) throw new System.InvalidOperationException("Assign an environment material.");
        }
        public Mesh Build(TerrainChunkData source, float horizontal, float vertical)
        {
            var layer = new EnvironmentGenerator(source.Definition, new EnvironmentDefinition(seaLevel)).Generate(source);
            return EnvironmentMeshBuilder.Build(layer, horizontal, vertical);
        }
    }
}
