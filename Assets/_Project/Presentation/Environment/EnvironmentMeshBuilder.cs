using System.Collections.Generic;
using RTSWorld.Presentation.Terrain;
using RTSWorld.World.Environment;
using UnityEngine;
using UnityEngine.Rendering;

namespace RTSWorld.Presentation.Environment
{
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
            public readonly Vector3 Position;
            public readonly float Depth;
            public WetVertex(Vector3 position, float depth) { Position = position; Depth = depth; }
        }

        public static Mesh Build(EnvironmentChunkData data, float horizontal, float vertical)
        {
            var terrain = TerrainGeometry.Build(data.Surface, horizontal, vertical);
            int estimate = terrain.Vertices.Length + data.Resources.Count * 60;
            var vertices = new List<Vector3>(estimate);
            var normals = new List<Vector3>(estimate);
            var colors = new List<Color>(estimate);
            for (int i = 0; i < terrain.Vertices.Length; i++)
            {
                var v = terrain.Vertices[i];
                vertices.Add(v); normals.Add(terrain.Normals[i]);
                colors.Add(BiomeColor(data[Mathf.RoundToInt(v.x / horizontal), Mathf.RoundToInt(v.z / horizontal)].Biome));
            }

            void Triangle(Vector3 a, Vector3 b, Vector3 c, Color color)
            {
                var normal = Vector3.Cross(b - a, c - a).normalized;
                vertices.Add(a); vertices.Add(b); vertices.Add(c);
                for (int i = 0; i < 3; i++) { normals.Add(normal); colors.Add(color); }
            }

            int cells = data.BaseTerrain.Definition.CellsPerChunk;
            var input = new WetVertex[3]; var clipped = new WetVertex[4];
            WetVertex WaterPoint(int x, int z)
            {
                var sample = data[x, z];
                return new WetVertex(new Vector3(x * horizontal, sample.WaterLevel * vertical + 0.015f, z * horizontal),
                    sample.WaterLevel - sample.Elevation);
            }
            void WaterTriangle(WetVertex a, WetVertex b, WetVertex c)
            {
                input[0] = a; input[1] = b; input[2] = c;
                int count = 0;
                var previous = input[2];
                for (int i = 0; i < 3; i++)
                {
                    var current = input[i];
                    if ((previous.Depth > 0) != (current.Depth > 0))
                    {
                        float t = previous.Depth / (previous.Depth - current.Depth);
                        clipped[count++] = new WetVertex(Vector3.Lerp(previous.Position, current.Position, t), 0);
                    }
                    if (current.Depth > 0) clipped[count++] = current;
                    previous = current;
                }
                for (int i = 1; i + 1 < count; i++) Triangle(clipped[0].Position, clipped[i].Position, clipped[i + 1].Position, new Color(0.08f, 0.43f, 0.69f));
            }
            for (int z = 0; z < cells; z++)
            for (int x = 0; x < cells; x++)
            {
                var a = WaterPoint(x, z); var b = WaterPoint(x, z + 1);
                var c = WaterPoint(x + 1, z); var d = WaterPoint(x + 1, z + 1);
                WaterTriangle(a, b, c); WaterTriangle(c, b, d);
            }

            var origin = data.BaseTerrain.Coordinate.GetSampleCoordinate(cells, 0, 0);
            foreach (var resource in data.Resources)
            {
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
                    Triangle(a, b, top, color); Triangle(b, c, top, color); Triangle(c, d, top, color); Triangle(d, a, top, color);
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
