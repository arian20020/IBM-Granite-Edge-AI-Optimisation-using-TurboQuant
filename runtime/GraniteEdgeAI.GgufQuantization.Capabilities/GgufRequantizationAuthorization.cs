using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using GraniteEdgeAI.GgufQuantization.Contracts;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Optimization;

namespace GraniteEdgeAI.GgufQuantization.Capabilities;

public sealed record GgufRequantizationAuthorization
{
    private GgufRequantizationAuthorization(string sha256)
    {
        Sha256 = sha256;
    }

    public string Sha256 { get; }

    public static bool IsRequired(
        GgufQuantizationFormat source,
        GgufQuantizationFormat target) =>
        IsQuantized(source) && Rank(target) < Rank(source);

    public static GgufRequantizationAuthorization Create(
        Guid optimizationPlanId,
        string configurationSha256,
        string modelSha256,
        ulong modelLengthBytes,
        GgufQuantizationFormat sourceFormat,
        GgufQuantizationFormat targetFormat,
        string quantizerManifestSha256,
        string policyVersion,
        GgufAdmittedConfiguration admittedTarget,
        GgufRequantisationPolicy? policy)
    {
        if (optimizationPlanId == Guid.Empty)
        {
            throw new ArgumentException("An optimization plan identity is required.", nameof(optimizationPlanId));
        }
        RequireDigest(configurationSha256, nameof(configurationSha256));
        RequireDigest(modelSha256, nameof(modelSha256));
        RequireDigest(quantizerManifestSha256, nameof(quantizerManifestSha256));
        if (modelLengthBytes == 0)
        {
            throw new ArgumentOutOfRangeException(nameof(modelLengthBytes));
        }
        if (!IsRequired(sourceFormat, targetFormat))
        {
            throw new ArgumentException("This conversion does not require requantization authorization.", nameof(targetFormat));
        }
        if (!string.Equals(policyVersion, GgufQuantizationProtocol.RequantizationPolicyVersion, StringComparison.Ordinal))
        {
            throw new ArgumentException("The requantization policy version is unsupported.", nameof(policyVersion));
        }
        ArgumentNullException.ThrowIfNull(admittedTarget);
        if (policy is null
            || !policy.ExplicitlyAcknowledged
            || !policy.PreserveOriginal
            || !policy.RequireNewOutput
            || !string.Equals(policy.Source.SourceSha256, modelSha256, StringComparison.Ordinal)
            || policy.Source.SourceLengthBytes != modelLengthBytes
            || policy.Source.Precision != GgufQuantizerFormatMap.ToCanonicalPrecision(sourceFormat)
            || admittedTarget.Weights != GgufQuantizerFormatMap.ToCore(targetFormat)
            || !policy.Authorizes(admittedTarget))
        {
            throw new ArgumentException(
                "The sealed policy does not authorize this source-bound reduction.",
                nameof(policy));
        }

        string canonical = string.Create(
            CultureInfo.InvariantCulture,
            $"plan={optimizationPlanId:D}|configuration={configurationSha256}|model={modelSha256}|length={modelLengthBytes}|source={(int)sourceFormat}|target={(int)targetFormat}|manifest={quantizerManifestSha256}|policy={policyVersion}");
        string digest = Convert.ToHexString(SHA256.HashData(
            new UTF8Encoding(false).GetBytes(canonical))).ToLowerInvariant();
        return new GgufRequantizationAuthorization(digest);
    }

    private static bool IsQuantized(GgufQuantizationFormat format) =>
        format is GgufQuantizationFormat.Q8_0
            or GgufQuantizationFormat.Q6K
            or GgufQuantizationFormat.Q5KM
            or GgufQuantizationFormat.Q4KM
            or GgufQuantizationFormat.Q3KM
            or GgufQuantizationFormat.Q2K;

    private static int Rank(GgufQuantizationFormat format) => format switch
    {
        GgufQuantizationFormat.F32 => 9,
        GgufQuantizationFormat.BF16 => 8,
        GgufQuantizationFormat.F16 => 7,
        GgufQuantizationFormat.Q8_0 => 6,
        GgufQuantizationFormat.Q6K => 5,
        GgufQuantizationFormat.Q5KM => 4,
        GgufQuantizationFormat.Q4KM => 3,
        GgufQuantizationFormat.Q3KM => 2,
        GgufQuantizationFormat.Q2K => 1,
        _ => throw new ArgumentOutOfRangeException(nameof(format), format, "Unknown format."),
    };

    private static void RequireDigest(string value, string parameter)
    {
        if (value is not { Length: 64 }
            || value.Any(character => character is not (>= '0' and <= '9')
                and not (>= 'a' and <= 'f')))
        {
            throw new ArgumentException("A digest must be lowercase SHA-256.", parameter);
        }
    }
}
