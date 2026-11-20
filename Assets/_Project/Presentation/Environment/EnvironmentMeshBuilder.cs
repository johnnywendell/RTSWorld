using System.Collections.Generic;
using RTSWorld.Presentation.Terrain;
using RTSWorld.World.Environment;
using UnityEngine;
using UnityEngine.Rendering;

namespace RTSWorld.Presentation.Environment
{
    public enum EnvironmentGeometryView { Combined, OriginalTerrain, EnvironmentalTerrain, WaterOnly }

    /// <summary>One coloured mesh per chunk: terrain, clipped water and batched low-poly resources.</summary>
    public static class EnvironmentMeshBuilder
    {
        public static Color BiomeColor(EnvironmentBiome biome)
        {
            switch (biome)
            {
                case EnvironmentBiome.Plains: return new Color(0.49f, 0.67f, 0.26f);
                case EnvironmentBiome.TemperateForest: return new Color(0.27f, 0.46f, 0.18f);
                case EnvironmentBiome.DenseForest: return new Color(0.12f, 0.31f, 0.16f);
                case EnvironmentBiome.Desert: return new Color(0.9f, 0.69f, 0.33f);
                case EnvironmentBiome.Arid: return new Color(0.65f, 0.49f, 0.28f);
                case EnvironmentBiome.Snow: return new Color(0.9f, 0.96f, 0.99f);
                case EnvironmentBiome.Tundra: return new Color(0.56f, 0.66f, 0.59f);
                case EnvironmentBiome.Mountain: return new Color(0.42f, 0.45f, 0.5f);
                case EnvironmentBiome.Coast: return new Color(0.88f, 0.81f, 0.56f);
                default: return new Color(0.25f, 0.37f, 0.39f);
            }
        }

        private readonly struct WetVertex
        {
            public readonly Vector3 Ground, Water;
            public readonly Color Color;
            public float Depth => Water.y - Ground.y;
            public WetVertex(Vector3 ground, Vector3 water, Color color)
            { Ground = ground; Water = water; Color = color; }
        }

        public static Mesh Build(EnvironmentChunkData data, float horizontal, float vertical,
            EnvironmentGeometryView view = EnvironmentGeometryView.Combined,
            System.Func<ResourceKey, bool> includeResource = null, bool instanceTreesAndRocks = false,
            bool artProfile = false)
        {
            if (data == null) throw new System.ArgumentNullException(nameof(data));
            TerrainGeometry.ValidateScale(horizontal, nameof(horizontal));
            TerrainGeometry.ValidateScale(vertical, nameof(vertical));
            int cells = data.BaseTerrain.Definition.CellsPerChunk;
            var origin = data.BaseTerrain.Coordinate.GetSampleCoordinate(cells, 0, 0);
            int estimate = cells * cells * 6 + data.Resources.Count * 60;
            var vertices = new List<Vector3>(estimate);
            var normals = new List<Vector3>(estimate);
            var colors = new List<Color>(estimate);

            void Triangle(Vector3 a, Vector3 b, Vector3 c, Color ca, Color cb, Color cc)
            {
                // Double intermediates avoid under/overflow for the supported scale ratios.
                double ux = (double)b.x - a.x, uy = (double)b.y - a.y, uz = (double)b.z - a.z;
                double vx = (double)c.x - a.x, vy = (double)c.y - a.y, vz = (double)c.z - a.z;
                double nx = uy * vz - uz * vy, ny = uz * vx - ux * vz, nz = ux * vy - uy * vx;
                double length = System.Math.Sqrt(nx * nx + ny * ny + nz * nz);
                if (length == 0) return; // A shoreline through a vertex can produce an empty fan wedge.
                var normal = new Vector3((float)(nx / length), (float)(ny / length), (float)(nz / length));
                vertices.Add(a); vertices.Add(b); vertices.Add(c);
                normals.Add(normal); normals.Add(normal); normals.Add(normal);
                colors.Add(ca); colors.Add(cb); colors.Add(cc);
            }
            void SolidTriangle(Vector3 a, Vector3 b, Vector3 c, Color color) => Triangle(a, b, c, color, color, color);

            var input = new WetVertex[3]; var clipped = new WetVertex[4];
            var artColors = artProfile ? new Color[data.Width * data.Width] : null;
            if (artColors != null)
                for (int z = 0; z <= cells; z++)
                for (int x = 0; x <= cells; x++)
                    artColors[z * data.Width + x] = GroundColor(x, z);

            Color GroundColor(int x, int z)
            {
                var sample = data[x, z];
                int seed = data.BaseTerrain.Definition.Seed;
                float patch = (float)EnvironmentNoise.Field(seed, origin.X + x, origin.Z + z, 13, 97);
                float mottling = (float)EnvironmentNoise.Field(seed, origin.X + x, origin.Z + z, 4, 102);
                float damp = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(.37f, .71f, sample.Humidity));
                var grass = Color.Lerp(new Color(.54f, .54f, .31f), new Color(.32f, .50f, .24f), damp);
                var moss = new Color(.42f, .53f, .27f);
                var soil = new Color(.47f, .36f, .25f);
                var beach = new Color(.69f, .59f, .41f);
                var crust = new Color(.47f, .49f, .46f);
                grass = Color.Lerp(grass, moss, Mathf.SmoothStep(0, .83f, patch));
                grass = Color.Lerp(grass, soil, Mathf.SmoothStep(0, .42f, Mathf.InverseLerp(.57f, .93f, mottling)));
                float chill = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(.28f, .17f, sample.Temperature));
                float dry = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(.40f, .16f, sample.Humidity));
                grass = Color.Lerp(grass, beach, dry * .83f);
                grass = Color.Lerp(grass, crust, Mathf.InverseLerp(65, 81, sample.Elevation) * .87f);
                grass = Color.Lerp(grass, Color.Lerp(crust, new Color(.77f, .81f, .78f), patch), chill * .95f);
                float shore = 1 - Mathf.SmoothStep(0, 1,
                    Mathf.InverseLerp(.02f, 1.15f + patch * .7f, sample.Elevation - sample.WaterLevel));
                var color = Color.Lerp(grass, beach, shore * .71f) * Mathf.Lerp(.83f, 1.12f, mottling);
                color.a = .9f;
                return color;
            }

            WetVertex Point(int x, int z)
            {
                var sample = data[x, z];
                return new WetVertex(new Vector3(x * horizontal,
                    (view == EnvironmentGeometryView.OriginalTerrain ? data.BaseTerrain[x, z].Elevation : sample.Elevation) * vertical, z * horizontal),
                    new Vector3(x * horizontal, sample.WaterLevel * vertical, z * horizontal),
                    artProfile ? artColors[z * data.Width + x] : BiomeColor(sample.Biome));
            }
            WetVertex Shore(WetVertex a, WetVertex b)
            {
                // Canonical edge direction makes both triangles/chunks use the same arithmetic.
                if (a.Ground.x > b.Ground.x || (a.Ground.x == b.Ground.x && a.Ground.z > b.Ground.z))
                { var swap = a; a = b; b = swap; }
                double t = (double)a.Depth / ((double)a.Depth - b.Depth);
                var ground = new Vector3((float)(a.Ground.x + ((double)b.Ground.x - a.Ground.x) * t),
                    (float)(a.Ground.y + ((double)b.Ground.y - a.Ground.y) * t),
                    (float)(a.Ground.z + ((double)b.Ground.z - a.Ground.z) * t));
                return new WetVertex(ground, ground, Color.Lerp(a.Color, b.Color, (float)t));
            }
            void SurfaceTriangle(WetVertex a, WetVertex b, WetVertex c, bool wet)
            {
                input[0] = a; input[1] = b; input[2] = c;
                int count = 0;
                var previous = input[2];
                bool Inside(WetVertex v) => wet ? v.Depth > 0 : v.Depth <= 0;
                for (int i = 0; i < 3; i++)
                {
                    var current = input[i];
                    if (Inside(previous) != Inside(current)) clipped[count++] = Shore(previous, current);
                    if (Inside(current)) clipped[count++] = current;
                    previous = current;
                }
                var waterColor = new Color(0.08f, 0.43f, 0.69f);
                for (int i = 1; i + 1 < count; i++)
                {
                    var p = clipped[0]; var q = clipped[i]; var r = clipped[i + 1];
                    if (wet && artProfile) Triangle(p.Water, q.Water, r.Water, WaterPaint(p), WaterPaint(q), WaterPaint(r));
                    else if (wet) SolidTriangle(p.Water, q.Water, r.Water, waterColor);
                    else Triangle(p.Ground, q.Ground, r.Ground, p.Color, q.Color, r.Color);
                }
            }
            Color WaterPaint(WetVertex v)
            {
                float depth = Mathf.Max(0, v.Depth / vertical);
                var color = Color.Lerp(new Color(.35f, .58f, .49f), new Color(.075f, .28f, .36f),
                    Mathf.SmoothStep(0, 1, Mathf.InverseLerp(0, 6, depth)));
                color.a = Mathf.Min(.49f, depth / 15);
                return color;
            }
            for (int z = 0; z < cells; z++)
            for (int x = 0; x < cells; x++)
            {
                var a = Point(x, z); var b = Point(x, z + 1);
                var c = Point(x + 1, z); var d = Point(x + 1, z + 1);
                // Complementary polygons share their shoreline exactly, with no offset,
                // gap, or nearly coplanar submerged terrain beneath the opaque water.
                if (view == EnvironmentGeometryView.OriginalTerrain || view == EnvironmentGeometryView.EnvironmentalTerrain)
                {
                    Triangle(a.Ground, b.Ground, c.Ground, a.Color, b.Color, c.Color);
                    Triangle(c.Ground, b.Ground, d.Ground, c.Color, b.Color, d.Color);
                }
                else
                {
                    if (view == EnvironmentGeometryView.Combined) SurfaceTriangle(a, b, c, false);
                    SurfaceTriangle(a, b, c, true);
                    if (view == EnvironmentGeometryView.Combined) SurfaceTriangle(c, b, d, false);
                    SurfaceTriangle(c, b, d, true);
                }
            }

            if (view == EnvironmentGeometryView.Combined)
            foreach (var resource in data.Resources)
            {
                if (instanceTreesAndRocks && (artProfile || resource.Kind != NaturalResource.Gold)) continue;
                if (includeResource != null && !includeResource(resource.Key)) continue;
                var center = new Vector3((float)(resource.Key.X - origin.X + resource.OffsetX) * horizontal,
                    resource.Elevation * vertical, (float)(resource.Key.Z - origin.Z + resource.OffsetZ) * horizontal);
                float variation = (float)(0.8 + EnvironmentNoise.Hash(resource.Key.Seed, resource.Key.X, resource.Key.Z, 31) * 0.4);
                float size = horizontal * variation;
                void Pyramid(Vector3 baseCenter, float radius, float height, Color color)
                {
                    var a = baseCenter + new Vector3(-radius, 0, -radius);
                    var b = baseCenter + new Vector3(-radius, 0, radius);
                    var c = baseCenter + new Vector3(radius, 0, radius);
                    var d = baseCenter + new Vector3(radius, 0, -radius);
                    var top = baseCenter + Vector3.up * height;
                    SolidTriangle(a, b, top, color); SolidTriangle(b, c, top, color); SolidTriangle(c, d, top, color); SolidTriangle(d, a, top, color);
                }
                if (resource.Kind == NaturalResource.Tree)
                {
                    Pyramid(center, 0.13f * size, 1.5f * size, new Color(0.32f, 0.2f, 0.1f));
                    Pyramid(center + Vector3.up * 0.7f * size, 0.55f * size, 2.1f * size, new Color(0.12f, 0.34f, 0.12f));
                }
                else
                {
                    Color color = resource.Kind == NaturalResource.Gold ? new Color(1, 0.67f, 0.08f) : new Color(0.48f, 0.51f, 0.55f);
                    Pyramid(center - Vector3.up * 0.1f * size, 0.5f * size, 0.85f * size, color);
                }
            }
            var mesh = new Mesh { name = "Environment chunk", indexFormat = vertices.Count > 65535 ? IndexFormat.UInt32 : IndexFormat.UInt16 };
            try
            {
                mesh.SetVertices(vertices); mesh.SetNormals(normals); mesh.SetColors(colors);
                var indices = new int[vertices.Count];
                for (int i = 0; i < indices.Length; i++) indices[i] = i;
                mesh.SetTriangles(indices, 0); mesh.RecalculateBounds();
                return mesh;
            }
            catch { if (Application.isPlaying) Object.Destroy(mesh); else Object.DestroyImmediate(mesh); throw; }
        }
    }
}
