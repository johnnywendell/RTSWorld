using System;
using RTSWorld.World.Chunks;
using UnityEngine;

namespace RTSWorld.Presentation.Terrain
{
    /// <summary>CPU geometry only; does not allocate Unity objects or generate terrain data.</summary>
    public sealed class TerrainGeometry
    {
        public Vector3[] Vertices { get; }
        public Vector3[] Normals { get; }
        public int[] Indices { get; }

        private TerrainGeometry(int count)
        {
            Vertices = new Vector3[count];
            Normals = new Vector3[count];
            Indices = new int[count];
        }

        public static void ValidateScale(float value, string name)
        {
            if (float.IsNaN(value) || float.IsInfinity(value) || value < 0.001f || value > 10000f)
                throw new ArgumentOutOfRangeException(name, "Scale must be finite and within 0.001..10000.");
        }

        public static TerrainGeometry Build(TerrainChunkData data, float horizontalScale, float verticalScale)
        {
            if (data == null) throw new ArgumentNullException(nameof(data));
            ValidateScale(horizontalScale, nameof(horizontalScale));
            ValidateScale(verticalScale, nameof(verticalScale));
            int cells = data.Definition.CellsPerChunk;
            var result = new TerrainGeometry(cells * cells * 6);
            int index = 0;
            for (int z = 0; z < cells; z++)
            for (int x = 0; x < cells; x++)
            {
                var a = new Vector3(x * horizontalScale, data[x, z].Elevation * verticalScale, z * horizontalScale);
                var b = new Vector3(x * horizontalScale, data[x, z + 1].Elevation * verticalScale, (z + 1) * horizontalScale);
                var c = new Vector3((x + 1) * horizontalScale, data[x + 1, z].Elevation * verticalScale, z * horizontalScale);
                var d = new Vector3((x + 1) * horizontalScale, data[x + 1, z + 1].Elevation * verticalScale, (z + 1) * horizontalScale);
                result.AddTriangle(ref index, a, b, c);
                result.AddTriangle(ref index, c, b, d);
            }
            return result;
        }

        private void AddTriangle(ref int index, Vector3 a, Vector3 b, Vector3 c)
        {
            // Double intermediate keeps normals stable even at extreme supported scale ratios.
            double ux = b.x - a.x, uy = b.y - a.y, uz = b.z - a.z;
            double vx = c.x - a.x, vy = c.y - a.y, vz = c.z - a.z;
            double nx = uy * vz - uz * vy, ny = uz * vx - ux * vz, nz = ux * vy - uy * vx;
            double length = Math.Sqrt(nx * nx + ny * ny + nz * nz);
            var normal = new Vector3((float)(nx / length), (float)(ny / length), (float)(nz / length));
            Vertices[index] = a; Vertices[index + 1] = b; Vertices[index + 2] = c;
            for (int i = 0; i < 3; i++)
            {
                Normals[index] = normal;
                Indices[index] = index;
                index++;
            }
        }
    }
}
