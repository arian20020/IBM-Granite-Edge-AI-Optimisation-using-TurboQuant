using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Optimization.Execution;

namespace GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Optimization;

/// <summary>
/// The narrow authorization for producing a lower-precision GGUF from an
/// already-quantised import when no genuine higher-precision source exists.
/// It carries identities only; local input and output paths remain executor
/// concerns and never enter the shared plan contract.
/// </summary>
public sealed record GgufRequantisationPolicy
{
    private GgufRequantisationPolicy(
        bool explicitlyAcknowledged,
        bool preserveOriginal,
        bool requireNewOutput,
        string evidenceId,
        GgufQuantiserIdentity quantiser,
        OptimizationJourneyBinding binding)
    {
        ExplicitlyAcknowledged = explicitlyAcknowledged;
        PreserveOriginal = preserveOriginal;
        RequireNewOutput = requireNewOutput;
        EvidenceId = evidenceId;
        Quantiser = quantiser;
        Binding = binding;
    }

    public bool ExplicitlyAcknowledged { get; }

    public bool PreserveOriginal { get; }

    public bool RequireNewOutput { get; }

    public string EvidenceId { get; }

    public GgufQuantiserIdentity Quantiser { get; }

    public OptimizationJourneyBinding Binding { get; }

    internal bool Authorizes(string evidenceId) =>
        ExplicitlyAcknowledged
        && PreserveOriginal
        && RequireNewOutput
        && string.Equals(EvidenceId, evidenceId, StringComparison.Ordinal);

    public static GgufRequantisationPolicy Create(
        bool explicitlyAcknowledged,
        bool preserveOriginal,
        bool requireNewOutput,
        string evidenceId,
        GgufQuantiserIdentity quantiser,
        OptimizationJourneyBinding binding)
    {
        OptimizationIdentifier.Require(
            evidenceId, nameof(evidenceId), "The requantisation evidence");
        ArgumentNullException.ThrowIfNull(quantiser);
        ArgumentNullException.ThrowIfNull(binding);

        return new GgufRequantisationPolicy(
            explicitlyAcknowledged,
            preserveOriginal,
            requireNewOutput,
            evidenceId,
            quantiser,
            binding);
    }
}
