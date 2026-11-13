using System;
using RTSWorld.World.Chunks;

namespace RTSWorld.World.Generation
{
    /// <summary>
    /// Version 1: four octaves of seeded value noise at periods 64,32,16,8,
    /// amplitudes 1,1/2,1/4,1/8, normalized to elevation [0,100].
    /// No global random state, scene state, or per-chunk noise origin.
    /// </summary>
    public sealed class TerrainGenerator
    {
        public TerrainChunkData Generate(WorldDefinition definition, ChunkCoordinate coordinate)
        {
            if (definition == null) throw new ArgumentNullException(nameof(definition));
            int width = definition.CellsPerChunk + 1;
            var samples = new TerrainSample[width * width];
            for (int z = 0; z < width; z++)
            for (int x = 0; x < width; x++)
            {
                GlobalCoordinate global = coordinate.GetSampleCoordinate(definition.CellsPerChunk, x, z);
                samples[z * width + x] = new TerrainSample(Elevation(definition.Seed, global.X, global.Z));
            }
            return new TerrainChunkData(definition, coordinate, samples);
        }

        private static float Elevation(int seed, long x, long z)
        {
            double sum = 0;
            double amplitude = 1;
            double totalAmplitude = 0;
            for (int octave = 0; octave < 4; octave++)
            {
                sum += ValueNoise(seed, x, z, 64 >> octave, octave) * amplitude;
                totalAmplitude += amplitude;
                amplitude *= 0.5;
            }
            return (float)(sum / totalAmplitude * 100.0);
        }

        private static double ValueNoise(int seed, long x, long z, int period, int octave)
        {
            // Floor division is essential west/south of the origin. Remainders stay nonnegative.
            long latticeX = FloorDivide(x, period);
            long latticeZ = FloorDivide(z, period);
            double tx = Smooth((double)(x - latticeX * period) / period);
            double tz = Smooth((double)(z - latticeZ * period) / period);
            double south = Lerp(Hash(seed, latticeX, latticeZ, octave),
                Hash(seed, latticeX + 1, latticeZ, octave), tx);
            double north = Lerp(Hash(seed, latticeX, latticeZ + 1, octave),
                Hash(seed, latticeX + 1, latticeZ + 1, octave), tx);
            return Lerp(south, north, tz);
        }

        private static long FloorDivide(long value, int divisor)
        {
            long quotient = value / divisor;
            return value % divisor < 0 ? quotient - 1 : quotient;
        }

        private static double Smooth(double t) => t * t * t * (t * (t * 6 - 15) + 10);
        private static double Lerp(double a, double b, double t) => a + (b - a) * t;

        private static double Hash(int seed, long x, long z, int octave)
        {
            // Explicit wraparound, constants and unsigned shifts are part of the v1 contract.
            unchecked
            {
                ulong hash = (uint)seed;
                hash ^= (ulong)x * 0x9E3779B185EBCA87UL;
                hash ^= (ulong)z * 0xC2B2AE3D27D4EB4FUL;
                hash ^= (ulong)(octave + 1) * 0x165667B19E3779F9UL;
                hash ^= hash >> 30;
                hash *= 0xBF58476D1CE4E5B9UL;
                hash ^= hash >> 27;
                hash *= 0x94D049BB133111EBUL;
                hash ^= hash >> 31;
                return (hash >> 40) / 16777215.0;
            }
        }
    }
}
