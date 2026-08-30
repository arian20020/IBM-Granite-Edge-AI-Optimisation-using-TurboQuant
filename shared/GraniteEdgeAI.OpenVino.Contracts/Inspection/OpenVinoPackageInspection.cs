using System.Text.Json.Serialization;

namespace GraniteEdgeAI.OpenVino.Contracts;

[JsonConverter(typeof(JsonStringEnumConverter<ModelInspectionOutcome>))]
public enum ModelInspectionOutcome
{
    Ready,
    ReadyWithWarnings
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
