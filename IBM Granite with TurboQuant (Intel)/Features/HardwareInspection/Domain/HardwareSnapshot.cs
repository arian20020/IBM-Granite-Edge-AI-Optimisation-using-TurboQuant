using System;
using System.Collections.Generic;
using System.Linq;

namespace GraniteEdgeAI.Features.HardwareInspection.Domain;

public sealed class HardwareSnapshot
{
    private static readonly string[] RequiredUsableEvidence =
    [
        "processor.name",
        "memory.installedBytes",
        "memory.osUsableBytes",
        "memory.availableBytes",
    ];

    public HardwareSnapshot(
        Guid snapshotId,
        DateTimeOffset capturedAtUtc,
        ushort schemaVersion,
        string policyVersion,
        ProcessorFacts processor,
        MemoryFacts memory,
        IEnumerable<GraphicsAdapterFacts> graphicsAdapters,
        NeuralProcessorFacts neuralProcessor,
        StorageFacts storage,
        OperatingSystemFacts operatingSystem,
        LocalRuntimeCapabilities localRuntime,
        HardwareEvidenceManifest evidence,
        HardwareSnapshotUsability usability)
    {
        if (snapshotId == Guid.Empty)
        {
            throw new ArgumentException("Snapshot identity cannot be empty.", nameof(snapshotId));
        }

        ContractTime.RequireUtc(capturedAtUtc, nameof(capturedAtUtc));
        ArgumentOutOfRangeException.ThrowIfZero(schemaVersion);
        ArgumentException.ThrowIfNullOrWhiteSpace(policyVersion);
        ArgumentNullException.ThrowIfNull(processor);
        ArgumentNullException.ThrowIfNull(memory);
        ArgumentNullException.ThrowIfNull(graphicsAdapters);
        ArgumentNullException.ThrowIfNull(neuralProcessor);
        ArgumentNullException.ThrowIfNull(storage);
        ArgumentNullException.ThrowIfNull(operatingSystem);
        ArgumentNullException.ThrowIfNull(localRuntime);
        ArgumentNullException.ThrowIfNull(evidence);

        GraphicsAdapterFacts[] graphicsCopy = graphicsAdapters.ToArray();
        if (graphicsCopy.Any(adapter => adapter is null))
        {
            throw new ArgumentException("Graphics adapters cannot contain null.", nameof(graphicsAdapters));
        }

        if (usability == HardwareSnapshotUsability.Usable)
        {
            string? missing = RequiredUsableEvidence.FirstOrDefault(key => !evidence.HasResolved(key));
            if (missing is not null)
            {
                throw new ArgumentException(
                    $"Usable snapshot lacks resolved required evidence: {missing}.",
                    nameof(evidence));
            }
        }

        SnapshotId = snapshotId;
        CapturedAtUtc = capturedAtUtc;
        SchemaVersion = schemaVersion;
        PolicyVersion = policyVersion;
        Processor = processor;
        Memory = memory;
        GraphicsAdapters = Array.AsReadOnly(graphicsCopy);
        NeuralProcessor = neuralProcessor;
        Storage = storage;
        OperatingSystem = operatingSystem;
        LocalRuntime = localRuntime;
        Evidence = evidence;
        Usability = usability;
        Identity = HardwareSnapshotIdentityCanonicalizer.Compute(this);
    }

    public Guid SnapshotId { get; }
    public DateTimeOffset CapturedAtUtc { get; }
    public ushort SchemaVersion { get; }
    public string PolicyVersion { get; }
    public ProcessorFacts Processor { get; }
    public MemoryFacts Memory { get; }
    public IReadOnlyList<GraphicsAdapterFacts> GraphicsAdapters { get; }
    public NeuralProcessorFacts NeuralProcessor { get; }
    public StorageFacts Storage { get; }
    public OperatingSystemFacts OperatingSystem { get; }
    public LocalRuntimeCapabilities LocalRuntime { get; }
    public HardwareEvidenceManifest Evidence { get; }
    public HardwareSnapshotUsability Usability { get; }
    public HardwareSnapshotIdentity Identity { get; }
}
