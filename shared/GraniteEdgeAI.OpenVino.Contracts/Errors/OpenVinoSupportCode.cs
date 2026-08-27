using System.Text.Json;
using System.Text.Json.Serialization;

namespace GraniteEdgeAI.OpenVino.Contracts;

[JsonConverter(typeof(OpenVinoSupportCodeJsonConverter))]
public enum OpenVinoSupportCode
{
    PackageMissingResource,
    PackageInconsistentResource,
    PackageUnsafePath,
    PackageChanged,
    PackageUnreadable,
    ModelArchitectureUnsupported,
    ModelTaskUnsupported,
    TokenizerUnsupported,
    RuntimeIntegrityFailed,
    RuntimeDependencyMissing,
    RuntimeLoadFailed,
    RuntimeDeviceUnavailable,
    RuntimeDeviceMismatch,
    RuntimeContextExceeded,
    RuntimeProtocolFailed,
    RuntimeTimedOut,
    OperationCancelled,
    ConversionPreflightFailed,
    ConversionFailed,
    ConversionOutputInvalid,
    ConversionPublishFailed,
    OptimizationUnsupported,
    TurboQuantUnavailable,
    TurboQuantActivationUnverified
}

public sealed class OpenVinoSupportCodeJsonConverter : JsonConverter<OpenVinoSupportCode>
{
    public override OpenVinoSupportCode Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.String || !TryParse(reader.GetString(), out OpenVinoSupportCode value))
        {
            throw new JsonException("supportCode must be a fixed OpenVINO support code.");
        }

        return value;
    }

    public override void Write(Utf8JsonWriter writer, OpenVinoSupportCode value, JsonSerializerOptions options) =>
        writer.WriteStringValue(value.ToProtocolValue());

    private static bool TryParse(string? text, out OpenVinoSupportCode value)
    {
        foreach (OpenVinoSupportCode candidate in Enum.GetValues<OpenVinoSupportCode>())
        {
            if (string.Equals(candidate.ToProtocolValue(), text, StringComparison.Ordinal))
            {
                value = candidate;
                return true;
            }
        }

        value = default;
        return false;
    }
}

public static class OpenVinoSupportCodeExtensions
{
    public static void Validate(this OpenVinoSupportCode value) => OpenVinoProtocol.Require(
        Enum.IsDefined(value),
        "supportCode must be one of the fixed OpenVINO support codes.");

    public static string ToProtocolValue(this OpenVinoSupportCode value) => value switch
    {
        OpenVinoSupportCode.PackageMissingResource => "package_missing_resource",
        OpenVinoSupportCode.PackageInconsistentResource => "package_inconsistent_resource",
        OpenVinoSupportCode.PackageUnsafePath => "package_unsafe_path",
        OpenVinoSupportCode.PackageChanged => "package_changed",
        OpenVinoSupportCode.PackageUnreadable => "package_unreadable",
        OpenVinoSupportCode.ModelArchitectureUnsupported => "model_architecture_unsupported",
        OpenVinoSupportCode.ModelTaskUnsupported => "model_task_unsupported",
        OpenVinoSupportCode.TokenizerUnsupported => "tokenizer_unsupported",
        OpenVinoSupportCode.RuntimeIntegrityFailed => "runtime_integrity_failed",
        OpenVinoSupportCode.RuntimeDependencyMissing => "runtime_dependency_missing",
        OpenVinoSupportCode.RuntimeLoadFailed => "runtime_load_failed",
        OpenVinoSupportCode.RuntimeDeviceUnavailable => "runtime_device_unavailable",
        OpenVinoSupportCode.RuntimeDeviceMismatch => "runtime_device_mismatch",
        OpenVinoSupportCode.RuntimeContextExceeded => "runtime_context_exceeded",
        OpenVinoSupportCode.RuntimeProtocolFailed => "runtime_protocol_failed",
        OpenVinoSupportCode.RuntimeTimedOut => "runtime_timed_out",
        OpenVinoSupportCode.OperationCancelled => "operation_cancelled",
        OpenVinoSupportCode.ConversionPreflightFailed => "conversion_preflight_failed",
        OpenVinoSupportCode.ConversionFailed => "conversion_failed",
        OpenVinoSupportCode.ConversionOutputInvalid => "conversion_output_invalid",
        OpenVinoSupportCode.ConversionPublishFailed => "conversion_publish_failed",
        OpenVinoSupportCode.OptimizationUnsupported => "optimization_unsupported",
        OpenVinoSupportCode.TurboQuantUnavailable => "turboquant_unavailable",
        OpenVinoSupportCode.TurboQuantActivationUnverified => "turboquant_activation_unverified",
        _ => throw new OpenVinoProtocolException("supportCode must be one of the fixed OpenVINO support codes.")
    };
}
