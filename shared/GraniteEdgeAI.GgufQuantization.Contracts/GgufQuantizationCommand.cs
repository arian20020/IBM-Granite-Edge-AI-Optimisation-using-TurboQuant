namespace GraniteEdgeAI.GgufQuantization.Contracts;

public sealed record GgufQuantizationCommand
{
    private GgufQuantizationCommand(
        Guid correlationId,
        Guid optimizationPlanId,
        string configurationSha256,
        string sourceToken,
        string outputToken,
        GgufQuantizationFormat sourceFormat,
        GgufQuantizationFormat targetFormat,
        string toolManifestSha256,
        string requantizationPolicyVersion,
        string? requantizationAuthorizationSha256)
    {
        ProtocolVersion = GgufQuantizationProtocol.CurrentVersion;
        CorrelationId = correlationId;
        OptimizationPlanId = optimizationPlanId;
        ConfigurationSha256 = configurationSha256;
        SourceToken = sourceToken;
        OutputToken = outputToken;
        SourceFormat = sourceFormat;
        TargetFormat = targetFormat;
        ToolManifestSha256 = toolManifestSha256;
        RequantizationPolicyVersion = requantizationPolicyVersion;
        RequantizationAuthorizationSha256 = requantizationAuthorizationSha256;
    }

    public int ProtocolVersion { get; }

    public Guid CorrelationId { get; }

    public Guid OptimizationPlanId { get; }

    public string ConfigurationSha256 { get; }

    public string SourceToken { get; }

    public string OutputToken { get; }

    public GgufQuantizationFormat SourceFormat { get; }

    public GgufQuantizationFormat TargetFormat { get; }

    public string ToolManifestSha256 { get; }

    public string RequantizationPolicyVersion { get; }

    public string? RequantizationAuthorizationSha256 { get; }

    public static GgufQuantizationCommand Create(
        Guid correlationId,
        Guid optimizationPlanId,
        string configurationSha256,
        string sourceToken,
        string outputToken,
        GgufQuantizationFormat sourceFormat,
        GgufQuantizationFormat targetFormat,
        string toolManifestSha256,
        string requantizationPolicyVersion,
        string? requantizationAuthorizationSha256)
    {
        if (correlationId == Guid.Empty)
        {
            throw new ArgumentException("A correlation identity is required.", nameof(correlationId));
        }
        if (optimizationPlanId == Guid.Empty)
        {
            throw new ArgumentException("An optimization plan identity is required.", nameof(optimizationPlanId));
        }
        GgufQuantizationContractValidation.RequireDigest(configurationSha256, nameof(configurationSha256));
        GgufQuantizationContractValidation.RequireOpaqueToken(sourceToken, nameof(sourceToken));
        GgufQuantizationContractValidation.RequireOpaqueToken(outputToken, nameof(outputToken));
        GgufQuantizationContractValidation.RequireDigest(toolManifestSha256, nameof(toolManifestSha256));
        _ = GgufQuantizationContractValidation.QualityRank(sourceFormat);
        _ = GgufQuantizationContractValidation.QualityRank(targetFormat);
        if (!GgufQuantizationContractValidation.IsQuantized(targetFormat)
            || GgufQuantizationContractValidation.QualityRank(targetFormat)
                >= GgufQuantizationContractValidation.QualityRank(sourceFormat))
        {
            throw new ArgumentException(
                "A quantization command must name an approved, strictly lower target format.",
                nameof(targetFormat));
        }
        if (string.Equals(sourceToken, outputToken, StringComparison.Ordinal))
        {
            throw new ArgumentException("Source and output tokens must be distinct.", nameof(outputToken));
        }
        if (!string.Equals(
                requantizationPolicyVersion,
                GgufQuantizationProtocol.RequantizationPolicyVersion,
                StringComparison.Ordinal))
        {
            throw new ArgumentException("The requantization policy version is unsupported.", nameof(requantizationPolicyVersion));
        }

        bool requiresAuthorization =
            GgufQuantizationContractValidation.IsQuantized(sourceFormat);
        if (requiresAuthorization)
        {
            GgufQuantizationContractValidation.RequireDigest(
                requantizationAuthorizationSha256!,
                nameof(requantizationAuthorizationSha256));
        }
        else if (requantizationAuthorizationSha256 is not null)
        {
            throw new ArgumentException(
                "Authorization is permitted only for a quality-lowering quantized conversion.",
                nameof(requantizationAuthorizationSha256));
        }

        return new GgufQuantizationCommand(
            correlationId,
            optimizationPlanId,
            configurationSha256,
            sourceToken,
            outputToken,
            sourceFormat,
            targetFormat,
            toolManifestSha256,
            requantizationPolicyVersion,
            requantizationAuthorizationSha256);
    }
}
