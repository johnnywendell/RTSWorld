using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace RTSWorld.Presentation.Terrain
{
    public static class TerrainMeshFactory
    {
        /// <summary>Caller owns and must destroy the returned native Mesh.</summary>
        public static Mesh Create(TerrainGeometry geometry)
        {
            if (geometry == null) throw new ArgumentNullException(nameof(geometry));
            var mesh = new Mesh { name = "Low-poly terrain chunk",
                indexFormat = geometry.Vertices.Length > 65535 ? IndexFormat.UInt32 : IndexFormat.UInt16 };
            mesh.vertices = geometry.Vertices;
            mesh.normals = geometry.Normals;
            mesh.triangles = geometry.Indices;
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
