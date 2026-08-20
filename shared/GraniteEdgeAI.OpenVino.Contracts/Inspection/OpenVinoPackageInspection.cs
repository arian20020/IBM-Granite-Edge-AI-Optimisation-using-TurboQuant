using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace GraniteEdgeAI.OpenVino.Contracts;

[JsonConverter(typeof(JsonStringEnumConverter<ModelInspectionOutcome>))]
public enum ModelInspectionOutcome
{
    Ready,
    ReadyWithWarnings
}

/// <summary>
/// The exact path-minimized schema-v2 model handoff. It carries no hardware,
/// filesystem, diagnostic, or model metadata fields.
/// </summary>
[method: JsonConstructor]
public sealed record ModelInspectionHandoffV2(
    ushort SchemaVersion,
    Guid ModelInspectionHandoffId,
    Guid ModelInspectionRunId,
    ModelInspectionOutcome Outcome,
    string ModelSha256,
    long ModelLengthBytes)
{
    public const ushort RequiredSchemaVersion = 2;
    public const int MaximumCanonicalUtf8Bytes = 512;

    public ModelInspectionHandoffV2(
        Guid modelInspectionHandoffId,
        Guid modelInspectionRunId,
        ModelInspectionOutcome outcome,
        string modelSha256,
        long modelLengthBytes)
        : this(
            RequiredSchemaVersion,
            modelInspectionHandoffId,
            modelInspectionRunId,
            outcome,
            modelSha256,
            modelLengthBytes)
    {
    }

    public void Validate()
    {
        OpenVinoProtocol.Require(SchemaVersion == RequiredSchemaVersion, "schemaVersion must equal 2.");
        RequireUuidV4(ModelInspectionHandoffId, nameof(ModelInspectionHandoffId));
        RequireUuidV4(ModelInspectionRunId, nameof(ModelInspectionRunId));
        OpenVinoProtocol.Require(
            Outcome is ModelInspectionOutcome.Ready or ModelInspectionOutcome.ReadyWithWarnings,
            "outcome must be Ready or ReadyWithWarnings.");
        OpenVinoProtocol.Require(
            ModelSha256 is not null && OpenVinoProtocol.LowercaseSha256.IsMatch(ModelSha256),
            "modelSha256 must be a lowercase SHA-256 digest for openvino_model.bin.");
        OpenVinoProtocol.Require(ModelLengthBytes > 0, "modelLengthBytes must be positive for openvino_model.bin.");
    }

    public byte[] ToCanonicalUtf8Json() => OpenVinoProtocolJson.SerializeHandoff(this);

    public static ModelInspectionHandoffV2 Parse(ReadOnlySpan<byte> payload) =>
        OpenVinoProtocolJson.DeserializeHandoff(payload);

    internal static void ValidateCanonicalDocument(JsonElement root)
    {
        string[] names =
        [
            "schemaVersion", "modelInspectionHandoffId", "modelInspectionRunId",
            "outcome", "modelSha256", "modelLengthBytes"
        ];
        OpenVinoProtocol.Require(root.EnumerateObject().Count() == names.Length, "handoff must contain exactly six fields.");
        foreach (string name in names)
        {
            OpenVinoProtocol.Require(root.TryGetProperty(name, out _), "handoff is missing a required field.");
        }

        OpenVinoProtocol.Require(
            root.GetProperty("schemaVersion").ValueKind == JsonValueKind.Number &&
            root.GetProperty("schemaVersion").TryGetUInt16(out ushort schemaVersion) &&
            schemaVersion == RequiredSchemaVersion,
            "schemaVersion must equal 2.");
        RequireCanonicalUuid(root, "modelInspectionHandoffId");
        RequireCanonicalUuid(root, "modelInspectionRunId");
        OpenVinoProtocol.Require(
            root.GetProperty("outcome").ValueKind == JsonValueKind.String &&
            root.GetProperty("outcome").GetString() is "Ready" or "ReadyWithWarnings",
            "outcome must be Ready or ReadyWithWarnings.");
        OpenVinoProtocol.Require(
            root.GetProperty("modelSha256").ValueKind == JsonValueKind.String &&
            root.GetProperty("modelSha256").GetString() is string modelSha256 &&
            OpenVinoProtocol.LowercaseSha256.IsMatch(modelSha256),
            "modelSha256 must be a lowercase SHA-256 digest for openvino_model.bin.");
        OpenVinoProtocol.Require(
            root.GetProperty("modelLengthBytes").ValueKind == JsonValueKind.Number &&
            root.GetProperty("modelLengthBytes").TryGetInt64(out long modelLengthBytes) &&
            modelLengthBytes > 0,
            "modelLengthBytes must be positive for openvino_model.bin.");
    }

    private static void RequireCanonicalUuid(JsonElement root, string propertyName)
    {
        OpenVinoProtocol.Require(root.GetProperty(propertyName).ValueKind == JsonValueKind.String, propertyName + " must be a UUID string.");
        string? value = root.GetProperty(propertyName).GetString();
        OpenVinoProtocol.Require(
            value is not null && Guid.TryParseExact(value, "D", out Guid identifier) &&
            value == identifier.ToString("D") && IsUuidV4(identifier),
            propertyName + " must be a lowercase canonical UUIDv4.");
    }

    private static void RequireUuidV4(Guid value, string name) => OpenVinoProtocol.Require(
        value != Guid.Empty && IsUuidV4(value),
        name + " must be a non-empty UUIDv4.");

    private static bool IsUuidV4(Guid value)
    {
        string text = value.ToString("D");
        return text[14] == '4' && text[19] is '8' or '9' or 'a' or 'b';
    }
}

/// <summary>Path-free inspection facts retained for later route-local validation.</summary>
public sealed record OpenVinoPackageInspection(
    ModelInspectionOutcome Outcome,
    string PackageManifestDigest,
    string ModelSha256,
    long ModelLengthBytes)
{
    public void Validate()
    {
        OpenVinoProtocol.Require(
            Outcome is ModelInspectionOutcome.Ready or ModelInspectionOutcome.ReadyWithWarnings,
            nameof(Outcome) + " must be Ready or ReadyWithWarnings.");
        OpenVinoProtocol.Require(
            PackageManifestDigest is not null && OpenVinoProtocol.LowercaseSha256.IsMatch(PackageManifestDigest),
            nameof(PackageManifestDigest) + " must be a lowercase SHA-256 digest.");
        OpenVinoProtocol.Require(
            ModelSha256 is not null && OpenVinoProtocol.LowercaseSha256.IsMatch(ModelSha256),
            nameof(ModelSha256) + " must be a lowercase SHA-256 digest for openvino_model.bin.");
        OpenVinoProtocol.Require(ModelLengthBytes > 0, nameof(ModelLengthBytes) + " must be positive.");
    }
}
