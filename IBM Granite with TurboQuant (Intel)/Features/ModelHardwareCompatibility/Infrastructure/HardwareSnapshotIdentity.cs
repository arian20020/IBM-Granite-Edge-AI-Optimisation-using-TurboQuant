using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using GraniteEdgeAI.Features.HardwareInspection.Domain;

namespace GraniteEdgeAI.Features.ModelHardwareCompatibility.Infrastructure;

internal static class HardwareSnapshotIdentity
{
    internal static string Sha256(HardwareSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        StringBuilder value = new();
        Add(value, "snapshot", snapshot.SnapshotId.ToString("D"));
        Add(value, "captured", snapshot.CapturedAtUtc.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture));
        Add(value, "schema", snapshot.SchemaVersion.ToString(CultureInfo.InvariantCulture));
        Add(value, "policy", snapshot.PolicyVersion);
        Add(value, "processor.name", snapshot.Processor.Name);
        Add(value, "processor.architecture", snapshot.Processor.Architecture);
        Add(value, "processor.physical", snapshot.Processor.PhysicalCoreCount.ToString(CultureInfo.InvariantCulture));
        Add(value, "processor.logical", snapshot.Processor.LogicalProcessorCount.ToString(CultureInfo.InvariantCulture));
        Add(value, "memory.installed", snapshot.Memory.PhysicallyInstalledBytes.ToString(CultureInfo.InvariantCulture));
        Add(value, "memory.usable", snapshot.Memory.OsUsablePhysicalBytes.ToString(CultureInfo.InvariantCulture));
        Add(value, "memory.available", snapshot.Memory.AvailablePhysicalBytes.ToString(CultureInfo.InvariantCulture));
        Add(value, "storage.capacity", snapshot.Storage.SystemVolumeCapacityBytes.ToString(CultureInfo.InvariantCulture));
        Add(value, "storage.available", snapshot.Storage.SystemVolumeAvailableBytes.ToString(CultureInfo.InvariantCulture));
        Add(value, "runtime.build", snapshot.LocalRuntime.BuildIdentity);
        foreach (HardwareEvidenceEntry entry in snapshot.Evidence.Entries.OrderBy(entry => entry.CanonicalField, StringComparer.Ordinal))
        {
            Add(value, "evidence.field", entry.CanonicalField);
            Add(value, "evidence.source", ((int)entry.Source).ToString(CultureInfo.InvariantCulture));
            Add(value, "evidence.resolution", ((int)entry.Resolution).ToString(CultureInfo.InvariantCulture));
            Add(value, "evidence.confidence", ((int)entry.Confidence).ToString(CultureInfo.InvariantCulture));
        }
        return Convert.ToHexString(SHA256.HashData(new UTF8Encoding(false).GetBytes(value.ToString())))
            .ToLowerInvariant();
    }

    private static void Add(StringBuilder target, string field, string value)
    {
        if (target.Length > 0) target.Append('|');
        target.Append(field).Append('=').Append(value.Length).Append(':').Append(value);
    }
}
