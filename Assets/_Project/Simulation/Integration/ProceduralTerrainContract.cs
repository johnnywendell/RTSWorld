using System;
using RTSWorld.World.Chunks;
using RTSWorld.World.Generation;
using RTSWorld.World.Environment;

namespace RTSWorld.Simulation
{
    /// <summary>Managed boundary; prepare immutable samples before scheduling jobs. No presentation dependencies.</summary>
    public sealed class ProceduralTerrainContract
    {
        private readonly TerrainGenerator terrain = new TerrainGenerator();
        private readonly EnvironmentGenerator environment;
        public WorldDefinition Definition { get; }
        public EnvironmentDefinition Environment { get; }

        public ProceduralTerrainContract(WorldDefinition definition, EnvironmentDefinition environment = null)
        {
            Definition = definition ?? throw new ArgumentNullException(nameof(definition));
            Environment = environment;
            if (environment != null) this.environment = new EnvironmentGenerator(definition, environment);
        }

        public ChunkCoordinate Region(double x, double z) => ChunkSelection.FromPosition(x, z, Definition.CellsPerChunk);
        private double Height(long x, long z) => environment == null
            ? terrain.SampleElevation(Definition, x, z) : environment.Sample(x, z).Elevation;

        public EnvironmentChunkData GetEnvironmentChunk(ChunkCoordinate coordinate)
        {
            if (environment == null) throw new InvalidOperationException("An environmental definition is required.");
            return environment.Generate(terrain.Generate(Definition, coordinate));
        }

        internal double SampleVertex(long x, long z, out double waterLevel)
        {
            if (environment == null)
            {
                waterLevel = terrain.SampleElevation(Definition, x, z);
                return waterLevel;
            }

            EnvironmentSample sample = environment.Sample(x, z);
            waterLevel = sample.WaterLevel;
            return sample.Elevation;
        }

        public double Elevation(double x, double z)
        {
            // Restrict to the same int-chunk domain; floor supports negative coordinates.
            Region(x, z);
            long ix = (long)Math.Floor(x), iz = (long)Math.Floor(z);
            double u = x - ix, v = z - iz;
            double a = Height(ix, iz), b = Height(ix + 1, iz);
            double c = Height(ix, iz + 1), d = Height(ix + 1, iz + 1);
            // Same diagonal as TerrainGeometry: (00,01,10), (10,01,11).
            return u + v <= 1 ? a + (b - a) * u + (c - a) * v
                : d + (c - d) * (1 - u) + (b - d) * (1 - v);
        }
    }
}
