using System.Text.Json.Serialization;
using System.Text.Json;

namespace GraniteEdgeAI.OpenVino.Contracts;

public interface IOpenVinoEvent
{
    string EventType { get; }

    void Validate();
}

public sealed record HelloEvent(
    string ProtocolId,
    OpenVinoBuildEvidence BuildEvidence) : IOpenVinoEvent
{
    [JsonPropertyName("eventType")]
    public string EventType => "hello";

    public void Validate()
    {
        OpenVinoProtocol.Require(
            ProtocolId is OpenVinoProtocol.OfficialProtocolId or OpenVinoProtocol.TurboQuantProtocolId,
            nameof(ProtocolId) + " must be an approved OpenVINO protocol identity.");
        if (BuildEvidence is null)
        {
            throw new OpenVinoProtocolException(
                nameof(BuildEvidence) + " must be present.");
        }

        BuildEvidence.Validate();
        OpenVinoProtocol.Require(
            ProtocolId == OpenVinoProtocol.TurboQuantProtocolId
                ? BuildEvidence.TurboQuantBuild is not null
                : BuildEvidence.TurboQuantBuild is null,
            "TurboQuant build identity must occur only on the TurboQuant protocol.");
    }
}

public enum OpenVinoInspectionStage
{
    ManifestVerified,
    MainModelParsed,
    TokenizerParsed,
    DetokenizerParsed
}

public enum OpenVinoTurnDisposition
{
    Completed,
    Stopped
}

public sealed class OpenVinoInspectionStageJsonConverter :
    JsonStringEnumConverter<OpenVinoInspectionStage>
{
    public OpenVinoInspectionStageJsonConverter()
        : base(JsonNamingPolicy.CamelCase, allowIntegerValues: false)
    {
    }
}

public sealed class OpenVinoTurnDispositionJsonConverter :
    JsonStringEnumConverter<OpenVinoTurnDisposition>
{
    public OpenVinoTurnDispositionJsonConverter()
        : base(JsonNamingPolicy.CamelCase, allowIntegerValues: false)
    {
    }
}

public sealed record InspectionStartedEvent(Guid InspectionRunId) : IOpenVinoEvent
{
    [JsonPropertyName("eventType")]
    public string EventType => "inspectionStarted";

    public void Validate() =>
        OpenVinoProtocol.RequireUuid(InspectionRunId, nameof(InspectionRunId));
}

public sealed record InspectionProgressEvent(
    Guid InspectionRunId,
    [property: JsonConverter(typeof(OpenVinoInspectionStageJsonConverter))]
    OpenVinoInspectionStage Stage) : IOpenVinoEvent
{
    [JsonPropertyName("eventType")]
    public string EventType => "inspectionProgress";

    public void Validate()
    {
        OpenVinoProtocol.RequireUuid(InspectionRunId, nameof(InspectionRunId));
        OpenVinoProtocol.Require(
            Enum.IsDefined(Stage),
            nameof(Stage) + " must be a fixed inspection stage.");
    }
}

public sealed record InspectionCompletedEvent(
    Guid InspectionRunId,
    string PackageManifestDigest,
    string ModelSha256,
    long ModelLengthBytes,
    bool MainModelParsed,
    bool TokenizerParsed,
    bool DetokenizerParsed,
    OpenVinoBuildEvidence BuildEvidence) : IOpenVinoEvent
{
    [JsonPropertyName("eventType")]
    public string EventType => "inspectionCompleted";

    public void Validate()
    {
        OpenVinoProtocol.RequireUuid(InspectionRunId, nameof(InspectionRunId));
        OpenVinoProtocol.RequireSha256(
            PackageManifestDigest,
            nameof(PackageManifestDigest));
        OpenVinoProtocol.RequireSha256(ModelSha256, nameof(ModelSha256));
        OpenVinoProtocol.Require(
            ModelLengthBytes > 0,
            nameof(ModelLengthBytes) + " must be positive.");
        OpenVinoProtocol.Require(
            MainModelParsed && TokenizerParsed && DetokenizerParsed,
            "inspection completion requires all three native parse facts.");
        if (BuildEvidence is null)
        {
            throw new OpenVinoProtocolException(
                nameof(BuildEvidence) + " must be present.");
        }

        BuildEvidence.Validate();
    }
}

public sealed record InspectionFailedEvent(Guid InspectionRunId, OpenVinoSupportCode SupportCode) : IOpenVinoEvent
{
    [JsonPropertyName("eventType")]
    public string EventType => "inspectionFailed";

    public void Validate()
    {
        OpenVinoProtocol.RequireUuid(InspectionRunId, nameof(InspectionRunId));
        SupportCode.Validate();
    }
}

[method: JsonConstructor]
public sealed record SessionStartedEvent(
    Guid SessionId,
    string RequestedDevice,
    IReadOnlyList<string> ActualExecutionDevices,
    string RequestedKvCachePrecision,
    string ActualKvCachePrecision,
    string ProtocolId,
    OpenVinoBuildEvidence BuildEvidence) : IOpenVinoEvent
{
    public SessionStartedEvent(
        Guid sessionId,
        string requestedDevice,
        IReadOnlyList<string> actualExecutionDevices,
        string protocolId,
        OpenVinoBuildEvidence buildEvidence)
        : this(
            sessionId,
            requestedDevice,
            actualExecutionDevices,
            OpenVinoRuntimeOptions.ReleasedDefault.KvCachePrecision,
            OpenVinoRuntimeOptions.ReleasedDefault.KvCachePrecision,
            protocolId,
            buildEvidence)
    {
    }

    [JsonPropertyName("eventType")]
    public string EventType => "sessionStarted";

    public void Validate()
    {
        OpenVinoProtocol.RequireUuid(SessionId, nameof(SessionId));
        OpenVinoProtocol.RequireExplicitDeviceIdentity(
            RequestedDevice,
            nameof(RequestedDevice));
        OpenVinoProtocol.Require(
            ProtocolId is OpenVinoProtocol.OfficialProtocolId or
                OpenVinoProtocol.TurboQuantProtocolId,
            nameof(ProtocolId) + " must be an approved OpenVINO protocol identity.");
        OpenVinoRuntimeOptions requestedRuntime = new(RequestedKvCachePrecision);
        OpenVinoRuntimeOptions actualRuntime = new(ActualKvCachePrecision);
        requestedRuntime.Validate();
        actualRuntime.Validate();
        OpenVinoProtocol.Require(
            string.Equals(
                RequestedKvCachePrecision,
                ActualKvCachePrecision,
                StringComparison.Ordinal),
            "actual KV-cache precision must match the requested precision.");
        if (ActualExecutionDevices is null ||
            ActualExecutionDevices.Count is not (> 0 and <=
                OpenVinoProtocol.MaximumActualExecutionDevices))
        {
            throw new OpenVinoProtocolException(
                nameof(ActualExecutionDevices) +
                " must be a bounded nonempty list.");
        }
        HashSet<string> distinct = new(StringComparer.Ordinal);
        foreach (string device in ActualExecutionDevices)
        {
            OpenVinoProtocol.RequireExplicitDeviceIdentity(
                device,
                nameof(ActualExecutionDevices));
            OpenVinoProtocol.Require(
                distinct.Add(device),
                nameof(ActualExecutionDevices) + " must be distinct.");
        }

        if (BuildEvidence is null)
        {
            throw new OpenVinoProtocolException(
                nameof(BuildEvidence) + " must be present.");
        }

        BuildEvidence.Validate();
    }
}

public sealed record GenerationStartedEvent(Guid SessionId, Guid TurnId) : IOpenVinoEvent
{
    [JsonPropertyName("eventType")]
    public string EventType => "generationStarted";

    public void Validate()
    {
        OpenVinoProtocol.RequireUuid(SessionId, nameof(SessionId));
        OpenVinoProtocol.RequireUuid(TurnId, nameof(TurnId));
    }
}

public sealed record TokenEvent(Guid SessionId, Guid TurnId, long Sequence, string Text) : IOpenVinoEvent
{
    [JsonPropertyName("eventType")]
    public string EventType => "token";

    public void Validate()
    {
        OpenVinoProtocol.RequireUuid(SessionId, nameof(SessionId));
        OpenVinoProtocol.RequireUuid(TurnId, nameof(TurnId));
        OpenVinoProtocol.Require(Sequence >= 0, nameof(Sequence) + " must not be negative.");
        // Stream deltas may consist entirely of formatting whitespace. Do not
        // apply the non-blank rule used for prompts and identifiers to them.
        OpenVinoProtocol.Require(!string.IsNullOrEmpty(Text), "Text must not be empty.");
        try
        {
            OpenVinoProtocol.Require(
                OpenVinoProtocol.StrictUtf8.GetByteCount(Text) <= OpenVinoProtocol.MaximumOperationTextUtf8Bytes,
                "Text exceeds its permitted UTF-8 length.");
        }
        catch (System.Text.EncoderFallbackException)
        {
            throw new OpenVinoProtocolException("Text must be valid UTF-8 text.");
        }
    }
}

public enum TurboQuantCodec
{
    Tbq4,
    Tbq3,
    ScalarU4
}

public enum TurboQuantAttentionPath
{
    Sdpa
}

public enum TurboQuantEvidenceOrigin
{
    OpenVinoProfilingApi,
    ParsedConsoleText
}

public sealed class TurboQuantCodecJsonConverter : JsonStringEnumConverter<TurboQuantCodec>
{
    public TurboQuantCodecJsonConverter()
        : base(JsonNamingPolicy.CamelCase, allowIntegerValues: false) { }
}

public sealed class TurboQuantAttentionPathJsonConverter : JsonStringEnumConverter<TurboQuantAttentionPath>
{
    public TurboQuantAttentionPathJsonConverter()
        : base(JsonNamingPolicy.CamelCase, allowIntegerValues: false) { }
}

public sealed class TurboQuantEvidenceOriginJsonConverter : JsonStringEnumConverter<TurboQuantEvidenceOrigin>
{
    public TurboQuantEvidenceOriginJsonConverter()
        : base(JsonNamingPolicy.CamelCase, allowIntegerValues: false) { }
}

public sealed record TurboQuantActivationEvent(
    Guid SessionId,
    Guid TurnId,
    [property: JsonConverter(typeof(TurboQuantCodecJsonConverter))]
    TurboQuantCodec RequestedKeyCodec,
    [property: JsonConverter(typeof(TurboQuantCodecJsonConverter))]
    TurboQuantCodec RequestedValueCodec,
    [property: JsonConverter(typeof(TurboQuantCodecJsonConverter))]
    TurboQuantCodec ActualKeyCodec,
    [property: JsonConverter(typeof(TurboQuantCodecJsonConverter))]
    TurboQuantCodec ActualValueCodec,
    [property: JsonConverter(typeof(TurboQuantAttentionPathJsonConverter))]
    TurboQuantAttentionPath AttentionPath,
    int HeadDimension,
    long RuntimeDispatchCount,
    long EncodedRecordCount,
    long ModelSdpaNodeCount,
    int PackedBytesPerRecord,
    int FullPrecisionBytesPerRecord,
    long PackedCacheBytes,
    long FullPrecisionCacheBytes,
    [property: JsonConverter(typeof(TurboQuantEvidenceOriginJsonConverter))]
    TurboQuantEvidenceOrigin EvidenceOrigin,
    bool ForcedScalarNegative) : IOpenVinoEvent
{
    [JsonPropertyName("eventType")]
    public string EventType => "turboQuantActivation";

    public void Validate()
    {
        OpenVinoProtocol.RequireUuid(SessionId, nameof(SessionId));
        OpenVinoProtocol.RequireUuid(TurnId, nameof(TurnId));
        bool exactTurboRequest = RequestedKeyCodec is
                TurboQuantCodec.Tbq4 or TurboQuantCodec.Tbq3 &&
            RequestedValueCodec == RequestedKeyCodec;
        OpenVinoProtocol.Require(
            exactTurboRequest &&
            ActualKeyCodec == RequestedKeyCodec &&
            ActualValueCodec == RequestedValueCodec,
            "actual TurboQuant codecs must match the exact symmetric request.");
        OpenVinoProtocol.Require(
            AttentionPath == TurboQuantAttentionPath.Sdpa && HeadDimension == 64,
            "TurboQuant activation requires CPU SDPA head dimension 64.");
        OpenVinoProtocol.Require(
            RuntimeDispatchCount > 0 && EncodedRecordCount > 0,
            "TurboQuant activation requires nonzero runtime dispatch and encoded-record counts.");
        OpenVinoProtocol.Require(
            ModelSdpaNodeCount > 0,
            "TurboQuant activation requires at least one model SDPA node.");
        int expectedPackedBytes = RequestedKeyCodec == TurboQuantCodec.Tbq3
            ? 24
            : 32;
        OpenVinoProtocol.Require(
            PackedBytesPerRecord == expectedPackedBytes &&
            FullPrecisionBytesPerRecord == 128,
            "TurboQuant record widths must match the requested codec and f16 reference storage.");
        OpenVinoProtocol.Require(
            EncodedRecordCount <= long.MaxValue / FullPrecisionBytesPerRecord &&
            PackedCacheBytes == EncodedRecordCount * PackedBytesPerRecord &&
            FullPrecisionCacheBytes == EncodedRecordCount * FullPrecisionBytesPerRecord &&
            PackedCacheBytes < FullPrecisionCacheBytes,
            "TurboQuant cache byte accounting is inconsistent.");
        OpenVinoProtocol.Require(
            EvidenceOrigin == TurboQuantEvidenceOrigin.OpenVinoProfilingApi,
            "TurboQuant activation must originate from the typed OpenVINO profiling API.");
        OpenVinoProtocol.Require(
            ForcedScalarNegative,
            "TurboQuant activation requires a successful forced-scalar negative control.");
    }
}

public sealed record TurnCompletedEvent(
    Guid SessionId,
    Guid TurnId,
    long PromptTokenCount,
    long GeneratedTokenCount,
    [property: JsonConverter(typeof(OpenVinoTurnDispositionJsonConverter))]
    OpenVinoTurnDisposition Disposition,
    string? FinishReason = null) : IOpenVinoEvent
{
    [JsonPropertyName("eventType")]
    public string EventType => "turnCompleted";

    public void Validate()
    {
        OpenVinoProtocol.RequireUuid(SessionId, nameof(SessionId));
        OpenVinoProtocol.RequireUuid(TurnId, nameof(TurnId));
        OpenVinoProtocol.Require(
            PromptTokenCount > 0,
            nameof(PromptTokenCount) + " must be positive.");
        OpenVinoProtocol.Require(
            GeneratedTokenCount >= 0,
            nameof(GeneratedTokenCount) + " must not be negative.");
        OpenVinoProtocol.Require(
            Enum.IsDefined(Disposition),
            nameof(Disposition) + " must be a fixed turn disposition.");
        OpenVinoProtocol.Require(FinishReason is null or "stop" or "length",
            "FinishReason must be a supported generation boundary.");
        OpenVinoProtocol.Require(Disposition != OpenVinoTurnDisposition.Stopped || FinishReason != "length",
            "A stopped turn cannot report a length boundary.");
    }
}

public sealed record TurnFailedEvent(Guid SessionId, Guid TurnId, OpenVinoSupportCode SupportCode) : IOpenVinoEvent
{
    [JsonPropertyName("eventType")]
    public string EventType => "turnFailed";

    public void Validate()
    {
        OpenVinoProtocol.RequireUuid(SessionId, nameof(SessionId));
        OpenVinoProtocol.RequireUuid(TurnId, nameof(TurnId));
        SupportCode.Validate();
    }
}

public sealed record SessionCompletedEvent(Guid SessionId) : IOpenVinoEvent
{
    [JsonPropertyName("eventType")]
    public string EventType => "sessionCompleted";

    public void Validate() => OpenVinoProtocol.RequireUuid(SessionId, nameof(SessionId));
}

public sealed record SessionFailedEvent(Guid SessionId, OpenVinoSupportCode SupportCode) : IOpenVinoEvent
{
    [JsonPropertyName("eventType")]
    public string EventType => "sessionFailed";

    public void Validate()
    {
        OpenVinoProtocol.RequireUuid(SessionId, nameof(SessionId));
        SupportCode.Validate();
    }
}

public sealed record SessionCancelledEvent(Guid SessionId) : IOpenVinoEvent
{
    [JsonPropertyName("eventType")]
    public string EventType => "sessionCancelled";

    public void Validate() => OpenVinoProtocol.RequireUuid(SessionId, nameof(SessionId));
}
