using System;
using System.Collections.Generic;
using RTSWorld.World.Environment;
using UnityEngine;

namespace RTSWorld.Presentation.Environment
{
    /// <summary>Explicit diagnostic capture only; no background scanning or runtime logging.</summary>
    public static class EnvironmentGeometryDiagnostics
    {
        [Serializable] public sealed class SampleRecord
        {
            public int localX, localZ;
            public long globalX, globalZ;
            public float originalHeight, environmentalHeight, waterHeight;
            public string waterKind;
        }
        [Serializable] public sealed class TriangleRecord
        {
            public int triangle, cellX, cellZ;
            public Vector3 a, b, c, normal;
            public double twiceArea, projectedTwiceArea;
            public float minimumEdge, maximumEdge, slopeDegrees;
        }
        [Serializable] public sealed class Report
        {
            public int seed, terrainVersion, environmentVersion, chunkX, chunkZ, cells;
            public float horizontalScale, verticalScale, seaLevel;
            public string view;
            public int invalidIndices, zeroAreaTriangles, downwardTriangles;
            public float maximumAdjacentGroundDifference, maximumAdjacentWaterDifference;
            public List<SampleRecord> samples = new List<SampleRecord>();
            public List<TriangleRecord> triangles = new List<TriangleRecord>();
        }

        // Heights/differences are logical elevation units. Triangle coordinates/edges/areas
        // are chunk-local Unity units, Unity units and squared Unity units respectively.
        // No arbitrary defect threshold: report exact geometry, including valid sliver faces.
        public static Report Capture(EnvironmentChunkData data, float horizontal, float vertical,
            EnvironmentGeometryView view)
        {
            if (data == null) throw new ArgumentNullException(nameof(data));
            var source = data.BaseTerrain;
            int n = source.Definition.CellsPerChunk;
            var origin = source.Coordinate.GetSampleCoordinate(n, 0, 0);
            var report = new Report { seed = source.Definition.Seed,
                terrainVersion = source.Definition.GeneratorVersion, environmentVersion = data.Definition.Version,
                chunkX = source.Coordinate.X, chunkZ = source.Coordinate.Z, cells = n,
                horizontalScale = horizontal, verticalScale = vertical, seaLevel = data.Definition.SeaLevel,
                view = view.ToString() };
            for (int z = 0; z <= n; z++)
            for (int x = 0; x <= n; x++)
            {
                var s = data[x, z];
                report.samples.Add(new SampleRecord { localX = x, localZ = z,
                    globalX = origin.X + x, globalZ = origin.Z + z, originalHeight = source[x, z].Elevation,
                    environmentalHeight = s.Elevation, waterHeight = s.WaterLevel, waterKind = s.Water.ToString() });
                void Edge(int xx, int zz)
                {
                    var neighbour = data[xx, zz];
                    report.maximumAdjacentGroundDifference = Mathf.Max(report.maximumAdjacentGroundDifference,
                        Mathf.Abs(s.Elevation - neighbour.Elevation));
                    report.maximumAdjacentWaterDifference = Mathf.Max(report.maximumAdjacentWaterDifference,
                        Mathf.Abs(s.WaterLevel - neighbour.WaterLevel));
                }
                if (x < n) Edge(x + 1, z);
                if (z < n) Edge(x, z + 1);
            }
            var mesh = EnvironmentMeshBuilder.Build(data, horizontal, vertical, view);
            try
            {
                var vertices = mesh.vertices; var indices = mesh.triangles;
                for (int i = 0; i < indices.Length; i += 3)
                {
                    int ia = indices[i], ib = indices[i + 1], ic = indices[i + 2];
                    if (ia < 0 || ib < 0 || ic < 0 || ia >= vertices.Length || ib >= vertices.Length || ic >= vertices.Length)
                    { report.invalidIndices++; continue; }
                    var a = vertices[ia]; var b = vertices[ib]; var c = vertices[ic];
                    double ux = (double)b.x-a.x, uy = (double)b.y-a.y, uz = (double)b.z-a.z;
                    double vx = (double)c.x-a.x, vy = (double)c.y-a.y, vz = (double)c.z-a.z;
                    double nx = uy*vz-uz*vy, ny = uz*vx-ux*vz, nz = ux*vy-uy*vx;
                    double area = Math.Sqrt(nx*nx+ny*ny+nz*nz);
                    if (area == 0) report.zeroAreaTriangles++;
                    if (ny < 0) report.downwardTriangles++;
                    var normal = area == 0 ? Vector3.zero : new Vector3((float)(nx/area),(float)(ny/area),(float)(nz/area));
                    var center = (a+b+c)/3;
                    float ab = Vector3.Distance(a,b), bc = Vector3.Distance(b,c), ca = Vector3.Distance(c,a);
                    report.triangles.Add(new TriangleRecord { triangle = i/3,
                        cellX = Mathf.FloorToInt(center.x/horizontal), cellZ = Mathf.FloorToInt(center.z/horizontal),
                        a = a, b = b, c = c, normal = normal, twiceArea = area, projectedTwiceArea = ny,
                        minimumEdge = Mathf.Min(ab,bc,ca), maximumEdge = Mathf.Max(ab,bc,ca),
                        slopeDegrees = Vector3.Angle(normal,Vector3.up) });
                }
            }
            finally { if (Application.isPlaying) UnityEngine.Object.Destroy(mesh); else UnityEngine.Object.DestroyImmediate(mesh); }
            return report;
        }
    }
}
