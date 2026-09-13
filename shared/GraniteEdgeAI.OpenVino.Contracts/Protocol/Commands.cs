using System.Text.Json.Serialization;

namespace GraniteEdgeAI.OpenVino.Contracts;

public interface IOpenVinoCommand
{
    string CommandType { get; }

    void Validate();
}

public sealed record OpenVinoDeviceRequest(string DeviceId)
{
    public void Validate() =>
        OpenVinoProtocol.RequireExplicitDeviceIdentity(DeviceId, nameof(DeviceId));
}

public sealed record OpenVinoGenerationLimits(int MaximumContextTokens, int MaximumNewTokens)
{
    public void Validate()
    {
        OpenVinoProtocol.Require(MaximumContextTokens > 0, nameof(MaximumContextTokens) + " must be positive.");
        OpenVinoProtocol.Require(
            MaximumNewTokens is > 0 and <= OpenVinoProtocol.MaximumNewTokens,
            nameof(MaximumNewTokens) + " exceeds the permitted maximum.");
    }
}

public sealed record StartInspectionCommand(
    Guid InspectionRunId,
    string PackagePath,
    string PackageManifestDigest,
    string ModelSha256,
    long ModelLengthBytes) : IOpenVinoCommand
{
    [JsonPropertyName("commandType")]
    public string CommandType => "startInspection";

    public void Validate()
    {
        OpenVinoProtocol.RequireUuid(InspectionRunId, nameof(InspectionRunId));
        ValidatePackageIdentity(
            PackagePath,
            PackageManifestDigest,
            ModelSha256,
            ModelLengthBytes);
    }

    internal static void ValidatePackageIdentity(
        string packagePath,
        string packageManifestDigest,
        string modelSha256,
        long modelLengthBytes)
    {
        OpenVinoProtocol.RequirePackagePath(packagePath, nameof(PackagePath));
        OpenVinoProtocol.RequireSha256(
            packageManifestDigest,
            nameof(PackageManifestDigest));
        OpenVinoProtocol.RequireSha256(modelSha256, nameof(ModelSha256));
        OpenVinoProtocol.Require(
            modelLengthBytes > 0,
            nameof(ModelLengthBytes) + " must be positive.");
    }
}

public sealed record OpenVinoRuntimeOptions(string KvCachePrecision)
{
    public static OpenVinoRuntimeOptions ReleasedDefault { get; } = new("released-default");

    public static OpenVinoRuntimeOptions U8 { get; } = new("u8");

    public static OpenVinoRuntimeOptions U4 { get; } = new("u4");

    public static OpenVinoRuntimeOptions Tbq4 { get; } = new("tbq4");

    public static OpenVinoRuntimeOptions Tbq3 { get; } = new("tbq3");

    public void Validate() => OpenVinoProtocol.Require(
        KvCachePrecision is "released-default" or "u8" or "u4" or "tbq4" or "tbq3",
        nameof(KvCachePrecision) + " must be an approved runtime KV-cache precision.");
}

public sealed record OpenVinoInitialTurn(string Role, string Content)
{
    public void Validate()
    {
        OpenVinoProtocol.Require(Role is "user" or "assistant", "Initial history role is unsupported.");
        OpenVinoProtocol.RequireUtf8Limit(Content, OpenVinoProtocol.MaximumPromptUtf8Bytes, nameof(Content));
    }
}

[method: JsonConstructor]
public sealed record StartSessionCommand(
    Guid SessionId,
    Guid InspectionRunId,
    string PackagePath,
    string PackageManifestDigest,
    string ModelSha256,
    long ModelLengthBytes,
    OpenVinoDeviceRequest Device,
    OpenVinoGenerationLimits Limits,
    OpenVinoRuntimeOptions Runtime) : IOpenVinoCommand
{
    // Omitted for old callers: preserves the released empty-session wire shape.
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyList<OpenVinoInitialTurn>? InitialHistory { get; init; }

    public StartSessionCommand(
        Guid sessionId,
        Guid inspectionRunId,
        string packagePath,
        string packageManifestDigest,
        string modelSha256,
        long modelLengthBytes,
        OpenVinoDeviceRequest device,
        OpenVinoGenerationLimits limits)
        : this(
            sessionId,
            inspectionRunId,
            packagePath,
            packageManifestDigest,
            modelSha256,
            modelLengthBytes,
            device,
            limits,
            OpenVinoRuntimeOptions.ReleasedDefault)
    {
    }

    [JsonPropertyName("commandType")]
    public string CommandType => "startSession";

    public void Validate()
    {
        OpenVinoProtocol.RequireUuid(SessionId, nameof(SessionId));
        OpenVinoProtocol.RequireUuid(InspectionRunId, nameof(InspectionRunId));
        StartInspectionCommand.ValidatePackageIdentity(
            PackagePath,
            PackageManifestDigest,
            ModelSha256,
            ModelLengthBytes);
        if (Device is null)
        {
            throw new OpenVinoProtocolException(nameof(Device) + " must be present.");
        }

        if (Limits is null)
        {
            throw new OpenVinoProtocolException(nameof(Limits) + " must be present.");
        }

        if (Runtime is null)
        {
            throw new OpenVinoProtocolException(nameof(Runtime) + " must be present.");
        }

        Device.Validate();
        Limits.Validate();
        Runtime.Validate();
        if (InitialHistory is not null)
        {
            OpenVinoProtocol.Require(InitialHistory.Count <= 512, "Initial history exceeds the turn limit.");
            long bytes = 0;
            foreach (OpenVinoInitialTurn turn in InitialHistory)
            {
                OpenVinoProtocol.Require(turn is not null, "Initial history contains a missing turn.");
                turn!.Validate();
                bytes += System.Text.Encoding.UTF8.GetByteCount(turn.Content);
            }
            OpenVinoProtocol.Require(bytes <= 512 * 1024, "Initial history exceeds the byte limit.");
        }
    }
}

public sealed record PromptCommand(
    Guid SessionId,
    Guid TurnId,
    string Prompt,
    int RequestedNewTokens) : IOpenVinoCommand
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public bool IsTransientTitle { get; init; }
    [JsonPropertyName("commandType")]
    public string CommandType => "prompt";

    public void Validate()
    {
        OpenVinoProtocol.RequireUuid(SessionId, nameof(SessionId));
        OpenVinoProtocol.RequireUuid(TurnId, nameof(TurnId));
        OpenVinoProtocol.RequireUtf8Limit(Prompt, OpenVinoProtocol.MaximumPromptUtf8Bytes, nameof(Prompt));
        OpenVinoProtocol.Require(
            RequestedNewTokens is > 0 and <= OpenVinoProtocol.MaximumNewTokens,
            nameof(RequestedNewTokens) + " exceeds the permitted maximum.");
        if (IsTransientTitle)
        {
            OpenVinoProtocol.Require(RequestedNewTokens <= 32, "Title output exceeds its bound.");
            OpenVinoProtocol.Require(Prompt.Length <= 4096, "Title input exceeds its bound.");
        }
    }
}

public sealed record StopTurnCommand(Guid SessionId, Guid TurnId) : IOpenVinoCommand
{
    [JsonPropertyName("commandType")]
    public string CommandType => "stopTurn";

    public void Validate()
    {
        OpenVinoProtocol.RequireUuid(SessionId, nameof(SessionId));
        OpenVinoProtocol.RequireUuid(TurnId, nameof(TurnId));
    }
}

public sealed record CancelSessionCommand(Guid SessionId) : IOpenVinoCommand
{
    [JsonPropertyName("commandType")]
    public string CommandType => "cancelSession";

    public void Validate() => OpenVinoProtocol.RequireUuid(SessionId, nameof(SessionId));
}

public sealed record CloseSessionCommand(Guid SessionId) : IOpenVinoCommand
{
    [JsonPropertyName("commandType")]
    public string CommandType => "closeSession";

    public void Validate() =>
        OpenVinoProtocol.RequireUuid(SessionId, nameof(SessionId));
}
