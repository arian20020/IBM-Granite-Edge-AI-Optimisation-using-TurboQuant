using System;
using System.Collections.Generic;
using System.Linq;
using GraniteEdgeAI.Features.HardwareInspection.Domain;
using GraniteEdgeAI.HardwareInspection.Foundation.Dxgi;
using GraniteEdgeAI.HardwareInspection.Foundation.LlamaCpp;
using GraniteEdgeAI.HardwareInspection.Foundation.LlmFit;
using GraniteEdgeAI.HardwareInspection.Foundation.NeuralProcessors;
using GraniteEdgeAI.HardwareInspection.Foundation.Windows;
using GraniteEdgeAI.Features.HardwareInspection.Orchestration;

namespace GraniteEdgeAI.Features.HardwareInspection.Resolution;

internal sealed class HardwareEvidenceResolver : IHardwareEvidenceResolver
{
    private static readonly string[] ManifestFields =
    [
        "processor.name",
        "processor.architecture",
        "processor.physicalCores",
        "processor.logicalProcessors",
        "processor.instructionSets",
        "memory.installedBytes",
        "memory.osUsableBytes",
        "memory.availableBytes",
        "graphics.adapters",
        "graphics.memory",
        "npu.state",
        "storage.systemVolumeCapacityBytes",
        "storage.systemVolumeAvailableBytes",
        "os.name",
        "os.version",
        "os.architecture",
        "runtime.buildIdentity",
        "runtime.backends",
        "runtime.visibleDevices",
    ];

    private static readonly HashSet<string> ConstructionCriticalFields = new(
        ManifestFields.Where(static field => field is not
            "processor.instructionSets" and not
            "graphics.adapters" and not
            "graphics.memory" and not
            "npu.state"),
        StringComparer.Ordinal);

    private readonly TimeProvider _timeProvider;

    internal HardwareEvidenceResolver(TimeProvider timeProvider)
    {
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
    }

    public HardwareEvidenceResolutionResult Resolve(
        Guid snapshotId,
        CollectedHardwareEvidence evidence)
    {
        if (snapshotId == Guid.Empty)
        {
            throw new ArgumentException("Snapshot identity cannot be empty.", nameof(snapshotId));
        }

        ArgumentNullException.ThrowIfNull(evidence);
        DateTimeOffset resolvedAtUtc = _timeProvider.GetUtcNow();
        if (resolvedAtUtc.Offset != TimeSpan.Zero)
        {
            throw new InvalidOperationException("The resolution clock must return UTC.");
        }

        ComponentResolution<ProcessorFacts> processor = ProcessorEvidenceResolver.Resolve(
            evidence.LlmFit,
            evidence.WindowsProcessor,
            resolvedAtUtc);
        MemorySystemResolution memory = MemorySystemEvidenceResolver.Resolve(
            evidence.LlmFit,
            evidence.WindowsSystem,
            resolvedAtUtc);
        ComponentResolution<IReadOnlyList<GraphicsAdapterFacts>> graphics =
            GraphicsEvidenceResolver.Resolve(
                evidence.Graphics,
                evidence.LlmFit,
                resolvedAtUtc);
        ComponentResolution<NeuralProcessorFacts> neuralProcessor =
            NeuralProcessorEvidenceResolver.Resolve(
                evidence.NeuralProcessor,
                resolvedAtUtc);
        ComponentResolution<StorageFacts> storage = StorageEvidenceResolver.Resolve(
            evidence.Storage,
            resolvedAtUtc);
        ComponentResolution<LocalRuntimeCapabilities> runtime = RuntimeEvidenceResolver.Resolve(
            evidence.LlamaCpp,
            resolvedAtUtc);

        HardwareEvidenceEntry[] entries =
        [
            .. processor.Entries,
            .. memory.Entries.Take(3),
            .. graphics.Entries,
            .. neuralProcessor.Entries,
            .. storage.Entries,
            .. memory.Entries.Skip(3),
            .. runtime.Entries,
        ];
        HardwareEvidenceManifest manifest = CreateManifest(entries);

        HashSet<HardwareResolutionDiagnosticCode> diagnostics = [];
        AddDiagnostics(diagnostics, processor.Diagnostics);
        AddDiagnostics(diagnostics, memory.Diagnostics);
        AddDiagnostics(diagnostics, graphics.Diagnostics);
        AddDiagnostics(diagnostics, neuralProcessor.Diagnostics);
        AddDiagnostics(diagnostics, storage.Diagnostics);
        AddDiagnostics(diagnostics, runtime.Diagnostics);

        if (CaptureSpanExceeded(evidence, resolvedAtUtc))
        {
            diagnostics.Add(HardwareResolutionDiagnosticCode.CaptureSpanExceeded);
        }

        bool unresolvedCriticalField = manifest.Entries.Any(entry =>
            ConstructionCriticalFields.Contains(entry.CanonicalField) && !entry.IsResolved);
        bool criticalFailure = processor.HasCriticalFailure ||
            memory.HasCriticalFailure ||
            storage.HasCriticalFailure ||
            runtime.HasCriticalFailure ||
            unresolvedCriticalField ||
            diagnostics.Contains(HardwareResolutionDiagnosticCode.CaptureSpanExceeded);
        if (criticalFailure)
        {
            if (diagnostics.Count == 0)
            {
                diagnostics.Add(HardwareResolutionDiagnosticCode.NormalizationInvalid);
            }

            return HardwareEvidenceResolutionResult.Failure(manifest, diagnostics);
        }

        HardwareSnapshot snapshot = new(
            snapshotId,
            resolvedAtUtc,
            HardwareResolutionPolicy.SchemaVersion,
            HardwareResolutionPolicy.PolicyVersion,
            processor.Value!,
            memory.Memory!,
            graphics.Value!,
            neuralProcessor.Value!,
            storage.Value!,
            memory.OperatingSystem!,
            runtime.Value!,
            manifest,
            HardwareSnapshotUsability.Usable);
        return HardwareEvidenceResolutionResult.Success(snapshot, manifest, diagnostics);
    }

    internal static HardwareEvidenceManifest CreateManifest(
        IEnumerable<HardwareEvidenceEntry> entries)
    {
        ArgumentNullException.ThrowIfNull(entries);
        HardwareEvidenceEntry[] copy = entries.Take(ManifestFields.Length + 1).ToArray();
        if (copy.Length != ManifestFields.Length ||
            copy.Any(static entry => entry is null))
        {
            throw new ArgumentException(
                "The aggregate evidence manifest must contain exactly 19 entries.",
                nameof(entries));
        }

        for (int index = 0; index < ManifestFields.Length; index++)
        {
            if (!string.Equals(
                    copy[index].CanonicalField,
                    ManifestFields[index],
                    StringComparison.Ordinal))
            {
                throw new ArgumentException(
                    "The aggregate evidence manifest has a missing, duplicate, or out-of-order field.",
                    nameof(entries));
            }
        }

        return new HardwareEvidenceManifest(copy);
    }

    private static void AddDiagnostics(
        HashSet<HardwareResolutionDiagnosticCode> destination,
        IEnumerable<HardwareResolutionDiagnosticCode> source)
    {
        foreach (HardwareResolutionDiagnosticCode diagnostic in source)
        {
            destination.Add(diagnostic);
        }
    }

    private static bool CaptureSpanExceeded(
        CollectedHardwareEvidence evidence,
        DateTimeOffset resolvedAtUtc)
    {
        List<DateTimeOffset> acceptedAvailableTimestamps = new(capacity: 7);
        AddAccepted(
            acceptedAvailableTimestamps,
            evidence.LlmFit.State == LlmFitEvidenceState.Available,
            evidence.LlmFit.CapturedAtUtc,
            resolvedAtUtc,
            HardwareResolutionPolicy.StaticEvidenceMaximumAge);
        AddAccepted(
            acceptedAvailableTimestamps,
            evidence.WindowsProcessor.State == WindowsProcessorEvidenceState.Available,
            evidence.WindowsProcessor.CapturedAtUtc,
            resolvedAtUtc,
            HardwareResolutionPolicy.StaticEvidenceMaximumAge);
        AddAccepted(
            acceptedAvailableTimestamps,
            evidence.WindowsSystem.State == WindowsSystemObservationState.Available,
            evidence.WindowsSystem.AttemptedAtUtc,
            resolvedAtUtc,
            HardwareResolutionPolicy.DynamicMemoryMaximumAge);
        AddAccepted(
            acceptedAvailableTimestamps,
            evidence.Storage.State == WindowsStorageEvidenceState.Available,
            evidence.Storage.CapturedAtUtc,
            resolvedAtUtc,
            HardwareResolutionPolicy.StaticEvidenceMaximumAge);
        AddAccepted(
            acceptedAvailableTimestamps,
            evidence.Graphics.State == DxgiGraphicsEvidenceState.Available,
            evidence.Graphics.CapturedAtUtc,
            resolvedAtUtc,
            HardwareResolutionPolicy.StaticEvidenceMaximumAge);
        AddAccepted(
            acceptedAvailableTimestamps,
            evidence.NeuralProcessor.State is NeuralProcessorEvidenceState.Present or
                NeuralProcessorEvidenceState.NotPresent,
            evidence.NeuralProcessor.CapturedAtUtc,
            resolvedAtUtc,
            HardwareResolutionPolicy.StaticEvidenceMaximumAge);
        AddAccepted(
            acceptedAvailableTimestamps,
            evidence.LlamaCpp.State == LlamaCppCapabilityEvidenceState.Available,
            evidence.LlamaCpp.CapturedAtUtc,
            resolvedAtUtc,
            HardwareResolutionPolicy.StaticEvidenceMaximumAge);

        if (acceptedAvailableTimestamps.Count < 2)
        {
            return false;
        }

        DateTimeOffset minimum = acceptedAvailableTimestamps.Min();
        DateTimeOffset maximum = acceptedAvailableTimestamps.Max();
        return maximum - minimum > HardwareResolutionPolicy.MaximumCaptureSpan;
    }

    private static void AddAccepted(
        ICollection<DateTimeOffset> destination,
        bool stateAvailable,
        DateTimeOffset capturedAtUtc,
        DateTimeOffset resolvedAtUtc,
        TimeSpan maximumAge)
    {
        if (stateAvailable &&
            HardwareEvidenceNormalizer.GetFreshness(
                capturedAtUtc,
                resolvedAtUtc,
                maximumAge) == EvidenceFreshness.Accepted)
        {
            destination.Add(capturedAtUtc);
        }
    }
}
