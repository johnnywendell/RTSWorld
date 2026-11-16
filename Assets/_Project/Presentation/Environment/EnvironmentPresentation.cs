using RTSWorld.World.Chunks;
using RTSWorld.World.Generation;
using RTSWorld.World.Environment;
using UnityEngine;

namespace RTSWorld.Presentation.Environment
{
    public sealed class EnvironmentPresentation : MonoBehaviour
    {
        [SerializeField, Range(0, 100)] private float seaLevel = 38;
        [SerializeField] private Material environmentMaterial;
        [SerializeField] private int environmentVersion = EnvironmentDefinition.CurrentVersion;
        [SerializeField] private EnvironmentGeometryView geometryView;
        public EnvironmentGeometryView GeometryView => geometryView;
        public void SetGeometryView(EnvironmentGeometryView view)
        {
            if (geometryView == view) return;
            geometryView = view; Revision++;
        }
        private EnvironmentGenerator generator;
        private WorldDefinition cachedWorld;
        private EnvironmentDefinition cachedEnvironment;
        public int Revision { get; private set; }
        public float SeaLevel => seaLevel;
        public int EnvironmentVersion => environmentVersion;
        public Material Material => environmentMaterial;
        public void Configure(Material material, float sea = 38, int version = EnvironmentDefinition.CurrentVersion)
        {
            _ = new EnvironmentDefinition(sea, version);
            if (material == null) throw new System.ArgumentNullException(nameof(material));
            environmentMaterial = material; seaLevel = sea; environmentVersion = version; Revision++;
        }
        private void OnValidate() => Revision++;
        public void Validate()
        {
            _ = new EnvironmentDefinition(seaLevel, environmentVersion);
            if (environmentMaterial == null) throw new System.InvalidOperationException("Assign an environment material.");
        }
        public Mesh Build(TerrainChunkData source, float horizontal, float vertical)
        {
            var definition = source.Definition;
            if (generator == null || cachedWorld.Seed != definition.Seed || cachedWorld.CellsPerChunk != definition.CellsPerChunk ||
                cachedWorld.GeneratorVersion != definition.GeneratorVersion || cachedEnvironment.SeaLevel != seaLevel || cachedEnvironment.Version != environmentVersion)
            {
                cachedWorld = definition; cachedEnvironment = new EnvironmentDefinition(seaLevel, environmentVersion);
                generator = new EnvironmentGenerator(definition, cachedEnvironment);
            }
            var layer = generator.Generate(source);
            return EnvironmentMeshBuilder.Build(layer, horizontal, vertical, geometryView);
        }
    }
}
