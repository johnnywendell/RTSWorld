using System;

namespace RTSWorld.World.Environment
{
    /// <summary>Independent salted fields for environment v1; does not replace terrain v1 noise.</summary>
    public static class EnvironmentNoise
    {
        public static double Hash(int seed, long x, long z, int channel)
        {
            unchecked
            {
                ulong h = (uint)seed ^ (ulong)x * 0xD6E8FEB86659FD93UL ^
                    (ulong)z * 0xA5A3564E27F8862FUL ^ (ulong)(channel + 1) * 0x9E3779B97F4A7C15UL;
                h ^= h >> 32; h *= 0xD6E8FEB86659FD93UL; h ^= h >> 32;
                h *= 0xD6E8FEB86659FD93UL; h ^= h >> 32;
                return (h >> 40) / 16777215.0;
            }
        }
        public static double Field(int seed, long x, long z, int period, int channel)
        {
            long ix = x / period, iz = z / period;
            if (x % period < 0) ix--;
            if (z % period < 0) iz--;
            double tx = Smooth((double)(x - ix * period) / period), tz = Smooth((double)(z - iz * period) / period);
            return Lerp(Lerp(Hash(seed, ix, iz, channel), Hash(seed, ix + 1, iz, channel), tx),
                Lerp(Hash(seed, ix, iz + 1, channel), Hash(seed, ix + 1, iz + 1, channel), tx), tz);
        }
        public static double Smooth(double t) { t = Math.Max(0, Math.Min(1, t)); return t * t * (3 - 2 * t); }
        public static double Lerp(double a, double b, double t) => a + (b - a) * t;
    }
}
