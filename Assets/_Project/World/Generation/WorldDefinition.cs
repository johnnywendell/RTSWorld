using System;

namespace RTSWorld.World.Generation
{
    /// <summary>All caller-selected inputs needed to reproduce a terrain chunk.</summary>
    public sealed class WorldDefinition
    {
        public const int CurrentGeneratorVersion = 1;
        public int Seed { get; }
        public int GeneratorVersion { get; }
        public int CellsPerChunk { get; }

        public WorldDefinition(int seed, int generatorVersion = CurrentGeneratorVersion,
            int cellsPerChunk = 32)
        {
            if (generatorVersion != CurrentGeneratorVersion)
                throw new ArgumentOutOfRangeException(nameof(generatorVersion), "Unsupported terrain generator version.");
            if (cellsPerChunk < 1 || cellsPerChunk > 256)
                throw new ArgumentOutOfRangeException(nameof(cellsPerChunk), "Expected 1..256 cells per axis.");
            Seed = seed;
            GeneratorVersion = generatorVersion;
            CellsPerChunk = cellsPerChunk;
        }
    }
}
