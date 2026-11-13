using System;
using System.Collections.Generic;

namespace RTSWorld.World.Chunks
{
    /// <summary>Pure logical selection; no camera, scene, generation or authoritative state.</summary>
    public static class ChunkSelection
    {
        public const int MaximumRadius = 8;

        public static ChunkCoordinate FromPosition(double logicalX, double logicalZ, int cellsPerChunk)
        {
            if (cellsPerChunk < 1 || cellsPerChunk > 256)
                throw new ArgumentOutOfRangeException(nameof(cellsPerChunk));
            return new ChunkCoordinate(Axis(logicalX, cellsPerChunk), Axis(logicalZ, cellsPerChunk));
        }

        private static int Axis(double position, int cells)
        {
            double value = Math.Floor(position / cells);
            if (double.IsNaN(value) || double.IsInfinity(value) || value < int.MinValue || value > int.MaxValue)
                throw new ArgumentOutOfRangeException(nameof(position), "Position is outside the logical chunk domain.");
            return (int)value;
        }

        public static void ValidateRadius(int radius)
        {
            if (radius < 0 || radius > MaximumRadius)
                throw new ArgumentOutOfRangeException(nameof(radius));
        }

        /// <summary>Square Chebyshev radius. Reuses output, nearest rings first, unique coordinates.</summary>
        public static void Fill(ChunkCoordinate center, int radius, List<ChunkCoordinate> output)
        {
            if (output == null) throw new ArgumentNullException(nameof(output));
            ValidateRadius(radius);
            output.Clear();
            for (int ring = 0; ring <= radius; ring++)
            for (int dz = -ring; dz <= ring; dz++)
            for (int dx = -ring; dx <= ring; dx++)
            {
                if (Math.Max(Math.Abs(dx), Math.Abs(dz)) != ring) continue;
                long x = (long)center.X + dx;
                long z = (long)center.Z + dz;
                if (x < int.MinValue || x > int.MaxValue || z < int.MinValue || z > int.MaxValue) continue;
                output.Add(new ChunkCoordinate((int)x, (int)z));
            }
        }
    }
}
