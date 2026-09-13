using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace GraniteEdgeAI.Features.HardwareInspection.Domain;

public sealed class ProcessorFacts
{
    public ProcessorFacts(
        string name,
        string architecture,
        int physicalCoreCount,
        int logicalProcessorCount,
        IEnumerable<string> instructionSets)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(architecture);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(physicalCoreCount);
        if (logicalProcessorCount < physicalCoreCount)
        {
            throw new ArgumentOutOfRangeException(
                nameof(logicalProcessorCount),
                "Logical processor count cannot be lower than physical core count.");
        }

        Name = name;
        Architecture = architecture;
        PhysicalCoreCount = physicalCoreCount;
        LogicalProcessorCount = logicalProcessorCount;
        InstructionSets = ContractCollection.CopyStrings(instructionSets, nameof(instructionSets));
    }

    public string Name { get; }
    public string Architecture { get; }
    public int PhysicalCoreCount { get; }
    public int LogicalProcessorCount { get; }
    public IReadOnlyList<string> InstructionSets { get; }
}

public sealed class MemoryFacts
{
    public MemoryFacts(
        ulong physicallyInstalledBytes,
        ulong osUsablePhysicalBytes,
        ulong availablePhysicalBytes,
        DateTimeOffset availableCapturedAtUtc)
    {
        ArgumentOutOfRangeException.ThrowIfZero(physicallyInstalledBytes);
        ArgumentOutOfRangeException.ThrowIfZero(osUsablePhysicalBytes);
        if (osUsablePhysicalBytes > physicallyInstalledBytes)
        {
            throw new ArgumentOutOfRangeException(
                nameof(osUsablePhysicalBytes),
                "OS-usable physical memory cannot exceed physically installed memory.");
        }

        if (availablePhysicalBytes > osUsablePhysicalBytes)
        {
            throw new ArgumentOutOfRangeException(
                nameof(availablePhysicalBytes),
                "Available physical memory cannot exceed OS-usable physical memory.");
        }

        ContractTime.RequireUtc(availableCapturedAtUtc, nameof(availableCapturedAtUtc));
        PhysicallyInstalledBytes = physicallyInstalledBytes;
        OsUsablePhysicalBytes = osUsablePhysicalBytes;
        AvailablePhysicalBytes = availablePhysicalBytes;
        AvailableCapturedAtUtc = availableCapturedAtUtc;
    }

    public ulong PhysicallyInstalledBytes { get; }
    public ulong OsUsablePhysicalBytes { get; }
    public ulong AvailablePhysicalBytes { get; }
    public DateTimeOffset AvailableCapturedAtUtc { get; }
}

public sealed record GraphicsAdapterFacts
{
    public GraphicsAdapterFacts(
        string name,
        ulong? dedicatedVideoMemoryBytes,
        ulong? dedicatedSystemMemoryBytes,
        ulong? sharedSystemMemoryBytes)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        Name = name;
        DedicatedVideoMemoryBytes = dedicatedVideoMemoryBytes;
        DedicatedSystemMemoryBytes = dedicatedSystemMemoryBytes;
        SharedSystemMemoryBytes = sharedSystemMemoryBytes;
    }

    public string Name { get; }
    public ulong? DedicatedVideoMemoryBytes { get; }
    public ulong? DedicatedSystemMemoryBytes { get; }
    public ulong? SharedSystemMemoryBytes { get; }
}

public sealed record NeuralProcessorFacts
{
    public NeuralProcessorFacts(NpuDetectionState state, string? name)
    {
        if (state == NpuDetectionState.Present)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(name);
        }
        else if (name is not null)
        {
            throw new ArgumentException(
                "An NPU name is valid only when an NPU is present.",
                nameof(name));
        }

        State = state;
        Name = name;
    }

    public NpuDetectionState State { get; }
    public string? Name { get; }
}

public sealed record StorageFacts
{
    public StorageFacts(ulong systemVolumeCapacityBytes, ulong systemVolumeAvailableBytes)
    {
        ArgumentOutOfRangeException.ThrowIfZero(systemVolumeCapacityBytes);
        if (systemVolumeAvailableBytes > systemVolumeCapacityBytes)
        {
            throw new ArgumentOutOfRangeException(
                nameof(systemVolumeAvailableBytes),
                "Available storage cannot exceed storage capacity.");
        }

        SystemVolumeCapacityBytes = systemVolumeCapacityBytes;
        SystemVolumeAvailableBytes = systemVolumeAvailableBytes;
    }

    public ulong SystemVolumeCapacityBytes { get; }
    public ulong SystemVolumeAvailableBytes { get; }
}

public sealed record OperatingSystemFacts
{
    public OperatingSystemFacts(string name, string version, string architecture)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(version);
        ArgumentException.ThrowIfNullOrWhiteSpace(architecture);
        Name = name;
        Version = version;
        Architecture = architecture;
    }

    public string Name { get; }
    public string Version { get; }
    public string Architecture { get; }
}

public sealed class LocalRuntimeCapabilities
{
    public LocalRuntimeCapabilities(
        string buildIdentity,
        IEnumerable<LocalRuntimeBackend> supportedBackends,
        IEnumerable<string> visibleDevices)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(buildIdentity);
        ArgumentNullException.ThrowIfNull(supportedBackends);
        LocalRuntimeBackend[] backends = supportedBackends.Distinct().ToArray();
        if (backends.Length == 0)
        {
            throw new ArgumentException("At least one runtime backend is required.", nameof(supportedBackends));
        }

        BuildIdentity = buildIdentity;
        SupportedBackends = Array.AsReadOnly(backends);
        VisibleDevices = ContractCollection.CopyStrings(visibleDevices, nameof(visibleDevices));
    }

    public string BuildIdentity { get; }
    public IReadOnlyList<LocalRuntimeBackend> SupportedBackends { get; }
    public IReadOnlyList<string> VisibleDevices { get; }
}

internal static class ContractCollection
{
    internal static ReadOnlyCollection<string> CopyStrings(
        IEnumerable<string> values,
        string parameterName)
    {
        ArgumentNullException.ThrowIfNull(values, parameterName);
        string[] copy = values.ToArray();
        if (copy.Any(string.IsNullOrWhiteSpace))
        {
            throw new ArgumentException("Collection values cannot be blank.", parameterName);
        }

        if (copy.Distinct(StringComparer.OrdinalIgnoreCase).Count() != copy.Length)
        {
            throw new ArgumentException("Collection values must be unique.", parameterName);
        }

        return Array.AsReadOnly(copy);
    }
}

internal static class ContractTime
{
    internal static void RequireUtc(DateTimeOffset value, string parameterName)
    {
        if (value.Offset != TimeSpan.Zero)
        {
            throw new ArgumentException("Timestamp must use the UTC offset.", parameterName);
        }
    }
}
