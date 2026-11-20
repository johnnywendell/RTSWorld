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
        private System.Func<ResourceKey, bool> includeResource;
        public bool InstanceTreesAndRocks { get; private set; }
        public bool ArtProfile { get; private set; }
        public void SetArtProfile(bool enabled)
        {
            if (ArtProfile == enabled) return;
            ArtProfile = enabled;
            Revision++;
        }
        public void SetInstanceTreesAndRocks(bool enabled)
        {
            if (InstanceTreesAndRocks == enabled) return;
            InstanceTreesAndRocks = enabled;
            Revision++;
        }
        public void SetResourceFilter(System.Func<ResourceKey, bool> filter)
        {
            if (includeResource == filter) return;
            includeResource = filter;
            Revision++;
        }
        private WorldDefinition cachedWorld;
        private EnvironmentDefinition cachedEnvironment;
        public int Revision { get; private set; }
        public float SeaLevel => seaLevel;
        public int EnvironmentVersion => environmentVersion;
        public Material Material => environmentMaterial;
        public EnvironmentDefinition CreateDefinition() => new EnvironmentDefinition(seaLevel, environmentVersion);
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
        public Mesh Build(TerrainChunkData source, float horizontal, float vertical) =>
            Build(source, horizontal, vertical, out _, out _, out _);

        internal Mesh Build(TerrainChunkData source, float horizontal, float vertical, out int resourceCount,
            out ResourcePlacement[] instances, out EnvironmentArtPlacement[] art)
        {
            var definition = source.Definition;
            if (generator == null || cachedWorld.Seed != definition.Seed || cachedWorld.CellsPerChunk != definition.CellsPerChunk ||
                cachedWorld.GeneratorVersion != definition.GeneratorVersion || cachedEnvironment.SeaLevel != seaLevel || cachedEnvironment.Version != environmentVersion)
            {
                cachedWorld = definition; cachedEnvironment = CreateDefinition();
                generator = new EnvironmentGenerator(definition, cachedEnvironment);
            }
            var layer = generator.Generate(source);
            resourceCount = 0;
            var detached = InstanceTreesAndRocks && geometryView == EnvironmentGeometryView.Combined
                ? new System.Collections.Generic.List<ResourcePlacement>() : null;
            foreach (var resource in layer.Resources)
            {
                if (includeResource != null && !includeResource(resource.Key)) continue;
                if (detached != null && (ArtProfile || resource.Kind != NaturalResource.Gold)) detached.Add(resource);
                else resourceCount++;
            }
            instances = detached?.ToArray();
            art = ArtProfile && detached != null ? EnvironmentArtDistribution.Build(layer, detached) : null;
            return EnvironmentMeshBuilder.Build(layer, horizontal, vertical, geometryView, includeResource, detached != null, art != null);
        }
    }
}
