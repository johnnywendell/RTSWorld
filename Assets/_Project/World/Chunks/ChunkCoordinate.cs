using System;

namespace RTSWorld.World.Chunks
{
    /// <summary>Logical X/Z chunk index, unrelated to presentation or world origin shifts.</summary>
    public readonly struct ChunkCoordinate : IEquatable<ChunkCoordinate>
    {
        public int X { get; }
        public int Z { get; }

        public ChunkCoordinate(int x, int z) { X = x; Z = z; }

        public GlobalCoordinate GetSampleCoordinate(int cellsPerChunk, int localX, int localZ)
        {
            if (cellsPerChunk < 1 || cellsPerChunk > 256)
                throw new ArgumentOutOfRangeException(nameof(cellsPerChunk));
            if (localX < 0 || localX > cellsPerChunk)
                throw new ArgumentOutOfRangeException(nameof(localX));
            if (localZ < 0 || localZ > cellsPerChunk)
                throw new ArgumentOutOfRangeException(nameof(localZ));
            return new GlobalCoordinate((long)X * cellsPerChunk + localX,
                (long)Z * cellsPerChunk + localZ);
        }

        public bool Equals(ChunkCoordinate other) => X == other.X && Z == other.Z;
        public override bool Equals(object obj) => obj is ChunkCoordinate other && Equals(other);
        public override int GetHashCode() => unchecked(X * 397 ^ Z);
        public override string ToString() => $"({X}, {Z})";
    }

    public readonly struct GlobalCoordinate : IEquatable<GlobalCoordinate>
    {
        public long X { get; }
        public long Z { get; }
        public GlobalCoordinate(long x, long z) { X = x; Z = z; }
        public bool Equals(GlobalCoordinate other) => X == other.X && Z == other.Z;
        public override bool Equals(object obj) => obj is GlobalCoordinate other && Equals(other);
        public override int GetHashCode() => unchecked(X.GetHashCode() * 397 ^ Z.GetHashCode());
        public override string ToString() => $"({X}, {Z})";
    }
}
