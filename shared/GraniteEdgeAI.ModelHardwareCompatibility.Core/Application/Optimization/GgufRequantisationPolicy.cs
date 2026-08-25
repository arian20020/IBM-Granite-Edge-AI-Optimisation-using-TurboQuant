using System.Globalization;
using System.Security.Cryptography;
using System.Text;
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
        string admittedConfigurationSha256,
        GgufQuantiserIdentity quantiser,
        GgufConversionSourceBinding source)
    {
        ExplicitlyAcknowledged = explicitlyAcknowledged;
        PreserveOriginal = preserveOriginal;
        RequireNewOutput = requireNewOutput;
        EvidenceId = evidenceId;
        AdmittedConfigurationSha256 = admittedConfigurationSha256;
        Quantiser = quantiser;
        Source = source;
    }

    public bool ExplicitlyAcknowledged { get; }

    public bool PreserveOriginal { get; }

    public bool RequireNewOutput { get; }

    public string EvidenceId { get; }

    public string AdmittedConfigurationSha256 { get; }

    public GgufQuantiserIdentity Quantiser { get; }

    public GgufConversionSourceBinding Source { get; }

    internal bool Authorizes(GgufAdmittedConfiguration admitted) =>
        ExplicitlyAcknowledged
        && PreserveOriginal
        && RequireNewOutput
        && string.Equals(EvidenceId, admitted.EvidenceId, StringComparison.Ordinal)
        && string.Equals(
            AdmittedConfigurationSha256,
            ComputeAdmittedConfigurationSha256(admitted),
            StringComparison.Ordinal);

    public static GgufRequantisationPolicy Create(
        bool explicitlyAcknowledged,
        bool preserveOriginal,
        bool requireNewOutput,
        GgufAdmittedConfiguration admitted,
        GgufQuantiserIdentity quantiser,
        GgufConversionSourceBinding source)
    {
        ArgumentNullException.ThrowIfNull(admitted);
        OptimizationIdentifier.Require(
            admitted.EvidenceId, nameof(admitted), "The requantisation evidence");
        ArgumentNullException.ThrowIfNull(quantiser);
        ArgumentNullException.ThrowIfNull(source);

        return new GgufRequantisationPolicy(
            explicitlyAcknowledged,
            preserveOriginal,
            requireNewOutput,
            admitted.EvidenceId,
            ComputeAdmittedConfigurationSha256(admitted),
            quantiser,
            source);
    }

    internal static string ComputeAdmittedConfigurationSha256(
        GgufAdmittedConfiguration admitted)
    {
        string canonical = string.Create(
            CultureInfo.InvariantCulture,
            $"evidence={admitted.EvidenceId}|backend={(int)admitted.Backend}|device={(int)admitted.Device}|weights={(int)admitted.Weights}|cache={(int)admitted.KvCache}|offload={(int)admitted.Offload}|min={admitted.MinimumContextTokens}|max={admitted.MaximumContextTokens}|level={(int)admitted.Level}|requiresEvidence={(admitted.RequiresEvidence ? 1 : 0)}");

        return Convert.ToHexString(SHA256.HashData(
            new UTF8Encoding(false).GetBytes(canonical))).ToLowerInvariant();
    }
}
