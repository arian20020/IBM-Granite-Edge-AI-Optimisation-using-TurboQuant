using System;
using System.Buffers.Binary;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace GraniteEdgeAI.Features.HardwareInspection.Domain;

public sealed record HardwareSnapshotIdentity
{
    internal HardwareSnapshotIdentity(Guid snapshotId, string sha256)
    {
        if (snapshotId == Guid.Empty)
        {
            throw new ArgumentException("Snapshot identity cannot be empty.", nameof(snapshotId));
        }
        if (sha256.Length != 64 || sha256.Any(character => character is not (
            >= '0' and <= '9' or >= 'a' and <= 'f')))
        {
            throw new ArgumentException("Snapshot digest must be lowercase SHA-256.", nameof(sha256));
        }
        SnapshotId = snapshotId;
        Sha256 = sha256;
    }

    public Guid SnapshotId { get; }
    public string Sha256 { get; }
}

internal static class HardwareSnapshotIdentityCanonicalizer
{
    internal static HardwareSnapshotIdentity Compute(HardwareSnapshot snapshot)
    {
        using IncrementalHash hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        var writer = new CanonicalWriter(hash);
        writer.Text("schema", "granite-edge-hardware-snapshot-v1");
        writer.Text("snapshot.id", snapshot.SnapshotId.ToString("N"));
        writer.Utc("snapshot.capturedAt", snapshot.CapturedAtUtc);
        writer.Unsigned("snapshot.schemaVersion", snapshot.SchemaVersion);
        writer.Signed("processor.physicalCores", snapshot.Processor.PhysicalCoreCount);
        writer.Signed("processor.logicalProcessors", snapshot.Processor.LogicalProcessorCount);
        writer.Unsigned("memory.totalPhysicalBytes", snapshot.Memory.PhysicallyInstalledBytes);
        writer.Unsigned("memory.osUsableBytes", snapshot.Memory.OsUsablePhysicalBytes);
        writer.Unsigned("memory.currentlyAvailableBytes", snapshot.Memory.AvailablePhysicalBytes);
        writer.Utc("memory.availableCapturedAt", snapshot.Memory.AvailableCapturedAtUtc);

        GraphicsAdapterFacts[] graphics = [.. snapshot.GraphicsAdapters
            .OrderBy(value => value.DedicatedVideoMemoryBytes)
            .ThenBy(value => value.DedicatedSystemMemoryBytes)
            .ThenBy(value => value.SharedSystemMemoryBytes)];
        writer.Count("graphics.count", graphics.Length);
        for (int index = 0; index < graphics.Length; index++)
        {
            writer.NullableUnsigned($"graphics[{index}].dedicatedVideoBytes", graphics[index].DedicatedVideoMemoryBytes);
            writer.NullableUnsigned($"graphics[{index}].dedicatedSystemBytes", graphics[index].DedicatedSystemMemoryBytes);
            writer.NullableUnsigned($"graphics[{index}].sharedSystemBytes", graphics[index].SharedSystemMemoryBytes);
        }

        writer.Signed("npu.state", (int)snapshot.NeuralProcessor.State);
        writer.Unsigned("storage.capacityBytes", snapshot.Storage.SystemVolumeCapacityBytes);
        writer.Unsigned("storage.availableBytes", snapshot.Storage.SystemVolumeAvailableBytes);
        LocalRuntimeBackend[] backends = [.. snapshot.LocalRuntime.SupportedBackends.Order()];
        writer.Count("runtime.backends.count", backends.Length);
        for (int index = 0; index < backends.Length; index++)
        {
            writer.Signed($"runtime.backends[{index}]", (int)backends[index]);
        }

        HardwareEvidenceEntry[] evidence = [.. snapshot.Evidence.Entries
            .OrderBy(value => value.CanonicalField, StringComparer.Ordinal)];
        writer.Count("evidence.count", evidence.Length);
        for (int index = 0; index < evidence.Length; index++)
        {
            HardwareEvidenceEntry item = evidence[index];
            writer.Text($"evidence[{index}].field", item.CanonicalField);
            writer.Signed($"evidence[{index}].source", (int)item.Source);
            writer.Signed($"evidence[{index}].resolution", (int)item.Resolution);
            writer.Utc($"evidence[{index}].capturedAt", item.CapturedAtUtc);
            writer.Signed($"evidence[{index}].confidence", (int)item.Confidence);
            writer.Text($"evidence[{index}].diagnostic", item.SafeDiagnosticCode ?? string.Empty);
        }
        writer.Signed("snapshot.usability", (int)snapshot.Usability);

        return new HardwareSnapshotIdentity(
            snapshot.SnapshotId,
            Convert.ToHexString(hash.GetHashAndReset()).ToLowerInvariant());
    }

    private sealed class CanonicalWriter(IncrementalHash hash)
    {
        internal void Text(string field, string value) => Write(field, "text", value);
        internal void Signed(string field, int value) =>
            Write(field, "int32", value.ToString(CultureInfo.InvariantCulture));
        internal void Unsigned(string field, ulong value) =>
            Write(field, "uint64", value.ToString(CultureInfo.InvariantCulture));
        internal void Count(string field, int value) =>
            Write(field, "count", value.ToString(CultureInfo.InvariantCulture));
        internal void Utc(string field, DateTimeOffset value) =>
            Write(field, "utc", value.ToString("O", CultureInfo.InvariantCulture));
        internal void NullableUnsigned(string field, ulong? value) =>
            Write(field, "nullable-uint64",
                value?.ToString(CultureInfo.InvariantCulture) ?? "null");

        private void Write(string field, string type, string value)
        {
            Append(field);
            Append(type);
            Append(value);
        }

        private void Append(string value)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(value);
            Span<byte> length = stackalloc byte[4];
            BinaryPrimitives.WriteInt32LittleEndian(length, bytes.Length);
            hash.AppendData(length);
            hash.AppendData(bytes);
        }
    }
}
