namespace GraniteEdgeAI.GgufQuantization.Contracts;

public enum GgufQuantizationEventKind
{
    Started = 0,
    Progress,
    Completed,
    Failed,
    Cancelled,
}

public sealed record GgufQuantizationEvent
{
    private GgufQuantizationEvent(
        Guid correlationId,
        Guid optimizationPlanId,
        string configurationSha256,
        GgufQuantizationEventKind kind,
        int progressPercent,
        GgufQuantizationSupportCode supportCode,
        string? outputToken,
        string? requantizationAuthorizationSha256)
    {
        ProtocolVersion = GgufQuantizationProtocol.CurrentVersion;
        CorrelationId = correlationId;
        OptimizationPlanId = optimizationPlanId;
        ConfigurationSha256 = configurationSha256;
        Kind = kind;
        ProgressPercent = progressPercent;
        SupportCode = supportCode;
        OutputToken = outputToken;
        RequantizationAuthorizationSha256 = requantizationAuthorizationSha256;
    }

    public int ProtocolVersion { get; }

    public Guid CorrelationId { get; }

    public Guid OptimizationPlanId { get; }

    public string ConfigurationSha256 { get; }

    public GgufQuantizationEventKind Kind { get; }

    public int ProgressPercent { get; }

    public GgufQuantizationSupportCode SupportCode { get; }

    public string? OutputToken { get; }

    public string? RequantizationAuthorizationSha256 { get; }

    public static GgufQuantizationEvent Create(
        Guid correlationId,
        Guid optimizationPlanId,
        string configurationSha256,
        GgufQuantizationEventKind kind,
        int progressPercent,
        GgufQuantizationSupportCode supportCode,
        string? outputToken = null,
        string? requantizationAuthorizationSha256 = null)
    {
        if (correlationId == Guid.Empty || optimizationPlanId == Guid.Empty)
        {
            throw new ArgumentException("Event identities must be non-empty.");
        }
        GgufQuantizationContractValidation.RequireDigest(configurationSha256, nameof(configurationSha256));
        if (!Enum.IsDefined(kind) || !Enum.IsDefined(supportCode))
        {
            throw new ArgumentOutOfRangeException(nameof(kind), "Event values must be defined.");
        }
        if (progressPercent is < 0 or > 100)
        {
            throw new ArgumentOutOfRangeException(nameof(progressPercent));
        }
        if (outputToken is not null)
        {
            GgufQuantizationContractValidation.RequireOpaqueToken(outputToken, nameof(outputToken));
        }
        if (requantizationAuthorizationSha256 is not null)
        {
            GgufQuantizationContractValidation.RequireDigest(
                requantizationAuthorizationSha256,
                nameof(requantizationAuthorizationSha256));
        }

        bool validShape = kind switch
        {
            GgufQuantizationEventKind.Started =>
                progressPercent == 0
                && supportCode == GgufQuantizationSupportCode.None
                && outputToken is null,
            GgufQuantizationEventKind.Progress =>
                progressPercent is > 0 and < 100
                && supportCode == GgufQuantizationSupportCode.None
                && outputToken is null,
            GgufQuantizationEventKind.Completed =>
                progressPercent == 100
                && supportCode == GgufQuantizationSupportCode.None
                && outputToken is not null,
            GgufQuantizationEventKind.Failed =>
                supportCode is not GgufQuantizationSupportCode.None
                    and not GgufQuantizationSupportCode.Cancelled
                && outputToken is null,
            GgufQuantizationEventKind.Cancelled =>
                supportCode == GgufQuantizationSupportCode.Cancelled
                && outputToken is null,
            _ => false,
        };
        if (!validShape)
        {
            throw new ArgumentException("The event fields do not form a valid closed outcome.", nameof(kind));
        }

        return new GgufQuantizationEvent(
            correlationId,
            optimizationPlanId,
            configurationSha256,
            kind,
            progressPercent,
            supportCode,
            outputToken,
            requantizationAuthorizationSha256);
    }
}
