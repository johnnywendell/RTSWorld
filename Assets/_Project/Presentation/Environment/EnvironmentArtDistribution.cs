using System.Collections.Generic;
using RTSWorld.World.Environment;
using UnityEngine;

namespace RTSWorld.Presentation.Environment
{
    public enum EnvironmentForm
    {
        Oak, Rounded, Crooked, Pine, Spire, Dead, Sparse, Snag,
        Pebble, Boulder, Crag, Slab, Outcrop, GoldNode,
        Shrub, Grass, Meadow, Flower, Fern, Reed, DryGrass, ShoreStone
    }

    public readonly struct EnvironmentArtPlacement
    {
        public readonly EnvironmentForm Form;
        public readonly ResourceKey Cell;
        public readonly float OffsetX, OffsetZ, Elevation, Width, Height, Turn;
        public readonly bool Alternate;
        public bool Decorative => Form >= EnvironmentForm.Shrub;

        internal EnvironmentArtPlacement(EnvironmentForm form, ResourceKey cell, float x, float z,
            float elevation, float width, float height, float turn, bool alternate)
        {
            Form = form;
            Cell = cell;
            OffsetX = x;
            OffsetZ = z;
            Elevation = elevation;
            Width = width;
            Height = height;
            Turn = turn;
            Alternate = alternate;
        }
    }

    /// <summary>Read-only visual projection. The resource cell still owns its original key and footprint.</summary>
    public static class EnvironmentArtDistribution
    {
        public static EnvironmentArtPlacement[] Build(EnvironmentChunkData chunk, IReadOnlyList<ResourcePlacement> resources)
        {
            var result = new List<EnvironmentArtPlacement>(resources.Count + 64);
            int n = chunk.Width - 1;
            var origin = chunk.BaseTerrain.Coordinate.GetSampleCoordinate(n, 0, 0);
            int seed = chunk.BaseTerrain.Definition.Seed;
            var occupied = new HashSet<ResourceKey>();
            foreach (var resource in chunk.Resources) occupied.Add(resource.Key);

            foreach (var resource in resources)
            {
                var key = resource.Key;
                int x = (int)(key.X - origin.X), z = (int)(key.Z - origin.Z);
                var sample = chunk[x, z];
                float climate = (float)EnvironmentNoise.Field(seed, key.X, key.Z, 13, 101);
                float rockBand = (float)EnvironmentNoise.Field(seed, key.X, key.Z, 6, 106);
                float selector = Roll(key, 109);
                var form = resource.Kind == NaturalResource.Tree
                    ? Tree(sample, climate, selector)
                    : resource.Kind == NaturalResource.Gold ? EnvironmentForm.GoldNode
                    : (EnvironmentForm)((int)EnvironmentForm.Pebble +
                        Mathf.Min(4, (int)((selector * .65f + rockBand * .35f) * 5)));
                float footprint = .8f + Roll(key, 31) * .4f;
                float width = resource.Kind switch
                {
                    NaturalResource.Gold => footprint * .70f,
                    NaturalResource.Stone => footprint * 1.25f,
                    _ => footprint
                };
                float height = footprint * (.80f + Roll(key, 112) * .45f);
                if (resource.Kind == NaturalResource.Gold) height *= .70f;
                result.Add(new EnvironmentArtPlacement(form, key, resource.OffsetX, resource.OffsetZ, resource.Elevation,
                    width, height, Roll(key, 116) * 360,
                    Roll(key, 118) > .54f));
            }

            // One owner per cell. Continuous global noise creates pockets and clearings across chunk edges.
            for (int z = 0; z < n; z++)
            for (int x = 0; x < n; x++)
            {
                var key = new ResourceKey(seed, chunk.Definition.Version, origin.X + x, origin.Z + z);
                var a = chunk[x, z];
                var b = chunk[x + 1, z];
                var c = chunk[x, z + 1];
                var d = chunk[x + 1, z + 1];
                if (occupied.Contains(key) || a.Biome == EnvironmentBiome.Snow) continue;

                float ox = .14f + Roll(key, 121) * .72f;
                float oz = .14f + Roll(key, 122) * .72f;
                float Sample(float v0, float v1, float v2, float v3) => ox + oz <= 1
                    ? v0 + (v1 - v0) * ox + (v2 - v0) * oz
                    : v3 + (v2 - v3) * (1 - ox) + (v1 - v3) * (1 - oz);
                float ground = Sample(a.Elevation, b.Elevation, c.Elevation, d.Elevation);
                float water = Sample(a.WaterLevel, b.WaterLevel, c.WaterLevel, d.WaterLevel);
                if (ground <= water + .015f) continue;
                float slope = Mathf.Max(Mathf.Abs(b.Elevation - a.Elevation), Mathf.Abs(c.Elevation - a.Elevation));
                if (slope > 2.35f) continue;
                float patch = (float)EnvironmentNoise.Field(seed, key.X, key.Z, 9, 120);
                bool shore = (a.Water != WaterKind.None || b.Water != WaterKind.None ||
                    c.Water != WaterKind.None || d.Water != WaterKind.None) && ground - water < .9f;
                bool dry = a.Biome is EnvironmentBiome.Desert or EnvironmentBiome.Arid or EnvironmentBiome.Mountain;
                float chance = shore ? .5f : patch * patch * (dry ? .12f : .32f);
                if (Roll(key, 124) >= chance) continue;
                float pick = Roll(key, 128);
                var form = shore ? (pick < .31f ? EnvironmentForm.ShoreStone : EnvironmentForm.Reed) : dry ? EnvironmentForm.DryGrass :
                    pick < .17f ? EnvironmentForm.Flower : pick < .33f ? EnvironmentForm.Shrub :
                    pick < .50f && a.Humidity > .54f ? EnvironmentForm.Fern :
                    pick < .68f ? EnvironmentForm.Meadow : EnvironmentForm.Grass;
                float size = .75f + Roll(key, 129) * .40f;
                result.Add(new EnvironmentArtPlacement(form, key, ox, oz, ground, size, size,
                    Roll(key, 132) * 360, false));
            }
            return result.ToArray();
        }

        public static float Roll(ResourceKey key, int channel) =>
            (float)EnvironmentNoise.Hash(key.Seed, key.X, key.Z, channel);

        private static EnvironmentForm Tree(EnvironmentSample sample, float cluster, float roll)
        {
            if (sample.Biome is EnvironmentBiome.Arid or EnvironmentBiome.Desert or EnvironmentBiome.Mountain ||
                sample.Humidity < .40f)
                return roll < .25f ? EnvironmentForm.Snag : roll < .56f ? EnvironmentForm.Dead : EnvironmentForm.Sparse;
            float evergreen = Mathf.Clamp01(.54f - sample.Temperature * .68f + cluster * .33f);
            if (sample.Biome is EnvironmentBiome.Tundra or EnvironmentBiome.Snow) evergreen += .4f;
            if (roll < evergreen) return cluster < .52f ? EnvironmentForm.Pine : EnvironmentForm.Spire;
            float broadleaf = (roll - evergreen) / Mathf.Max(.01f, 1 - evergreen);
            return broadleaf < .34f ? EnvironmentForm.Oak : broadleaf < .67f ? EnvironmentForm.Rounded : EnvironmentForm.Crooked;
        }
    }
}
