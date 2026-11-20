using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace RTSWorld.Simulation
{
    /// <summary>Versioned local storage for a detached simulation snapshot.</summary>
    public static class SimulationSaveFile
    {
        private const uint Magic = 0x57535452;
        private const int Version = 1;
        private const int DigestSize = 32;
        private static readonly JsonSerializerSettings JsonSettings = new()
        {
            ContractResolver = new SnapshotContract()
        };

        private sealed class SnapshotContract : DefaultContractResolver
        {
            protected override List<MemberInfo> GetSerializableMembers(Type type) =>
                base.GetSerializableMembers(type).Where(member => member is FieldInfo).ToList();

            protected override JsonProperty CreateProperty(MemberInfo member, MemberSerialization serialization)
            {
                var property = base.CreateProperty(member, serialization);
                property.Required = Required.Always;
                return property;
            }
        }

        public static Task<(double SerializationMs, double WriteMs)> SaveAsync(string path, SimulationSnapshot snapshot) => Task.Run(() =>
        {
            if (snapshot == null) throw new ArgumentNullException(nameof(snapshot));
            var timer = Stopwatch.StartNew();
            var payload = System.Text.Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(snapshot, JsonSettings));
            using var sha = SHA256.Create();
            byte[] digest = sha.ComputeHash(payload);
            double serialization = timer.Elapsed.TotalMilliseconds;
            timer.Restart();
            string destination = Path.GetFullPath(path);
            string directory = Path.GetDirectoryName(destination);
            Directory.CreateDirectory(directory);
            string temporary = Path.Combine(directory, Guid.NewGuid().ToString("N") + ".tmp");
            try
            {
                using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                using (var writer = new BinaryWriter(stream))
                {
                    writer.Write(Magic);
                    writer.Write(Version);
                    writer.Write(payload.Length);
                    writer.Write(digest);
                    writer.Write(payload);
                    writer.Flush();
                    stream.Flush(true);
                }

                if (File.Exists(destination))
                    File.Replace(temporary, destination, null);
                else
                    File.Move(temporary, destination);
            }
            finally
            {
                File.Delete(temporary);
            }
            return (serialization, timer.Elapsed.TotalMilliseconds);
        });

        public static async Task<SimulationSnapshot> LoadAsync(string path) =>
            (await ReadAsync(path).ConfigureAwait(false)).Snapshot;

        public static Task<(SimulationSnapshot Snapshot, double ReadMs, double DeserializationMs)> ReadAsync(string path) => Task.Run(() =>
        {
            var timer = Stopwatch.StartNew();
            using var stream = File.OpenRead(path);
            using var reader = new BinaryReader(stream);
            if (stream.Length < 12 + DigestSize || reader.ReadUInt32() != Magic)
                throw new InvalidDataException("Cabeçalho de salvamento inválido.");
            if (reader.ReadInt32() != Version)
                throw new InvalidDataException("Versão de salvamento incompatível.");
            int length = reader.ReadInt32();
            if (length <= 0 || stream.Length - stream.Position != DigestSize + (long)length)
                throw new InvalidDataException("Tamanho de salvamento inválido.");
            byte[] expected = reader.ReadBytes(DigestSize);
            byte[] payload = reader.ReadBytes(length);
            using var sha = SHA256.Create();
            byte[] actual = sha.ComputeHash(payload);
            for (int i = 0; i < DigestSize; i++)
                if (expected[i] != actual[i])
                    throw new InvalidDataException("Integridade do salvamento inválida.");

            double read = timer.Elapsed.TotalMilliseconds;
            timer.Restart();
            try
            {
                var snapshot = JsonConvert.DeserializeObject<SimulationSnapshot>(
                    System.Text.Encoding.UTF8.GetString(payload), JsonSettings)
                    ?? throw new InvalidDataException("Estado de simulação ausente.");
                return (snapshot, read, timer.Elapsed.TotalMilliseconds);
            }
            catch (JsonException exception)
            {
                throw new InvalidDataException("Dados de simulação inválidos.", exception);
            }
        });
    }
}
