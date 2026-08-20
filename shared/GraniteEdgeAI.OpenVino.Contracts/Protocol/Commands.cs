using System.Text.Json.Serialization;

namespace GraniteEdgeAI.OpenVino.Contracts;

public interface IOpenVinoCommand
{
    string CommandType { get; }

    void Validate();
}

public sealed record OpenVinoDeviceRequest(string DeviceId)
{
    public void Validate() => OpenVinoProtocol.RequireText(DeviceId, nameof(DeviceId));
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

public sealed record StartSessionCommand(
    Guid SessionId,
    Guid InspectionRunId,
    string PackagePath,
    string PackageManifestDigest,
    string ModelSha256,
    long ModelLengthBytes,
    OpenVinoDeviceRequest Device,
    OpenVinoGenerationLimits Limits) : IOpenVinoCommand
{
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

        Device.Validate();
        Limits.Validate();
    }
}

public sealed record PromptCommand(
    Guid SessionId,
    Guid TurnId,
    string Prompt,
    int RequestedNewTokens) : IOpenVinoCommand
{
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
