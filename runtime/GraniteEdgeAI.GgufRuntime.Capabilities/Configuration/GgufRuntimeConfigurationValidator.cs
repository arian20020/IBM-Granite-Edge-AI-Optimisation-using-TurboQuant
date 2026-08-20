using GraniteEdgeAI.GgufRuntime.Contracts.Configuration;
using GraniteEdgeAI.GgufRuntime.Contracts.Failures;

namespace GraniteEdgeAI.GgufRuntime.Capabilities.Configuration;

public static class GgufRuntimeConfigurationValidator
{
    public static GgufCapabilityValidationResult Validate(
        GgufRuntimeConfiguration configuration,
        GgufCapabilityMatrix matrix)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(matrix);

        string? failureCode = null;
        if (!configuration.RuntimeBuildId.Equals(
                matrix.RuntimeBuildId,
                StringComparison.Ordinal))
        {
            failureCode = "runtime-build-mismatch";
        }
        else if (!matrix.Backends.Contains(configuration.Backend))
        {
            failureCode = "runtime-backend-unsupported";
        }
        else if (configuration.ContextSize > matrix.MaxContextSize)
        {
            failureCode = "runtime-context-unsupported";
        }
        else if (configuration.Backend == GgufRuntimeBackend.Cpu &&
                 configuration.GpuLayerCount != 0)
        {
            failureCode = "runtime-gpu-offload-unsupported";
        }
        else if (configuration.Backend == GgufRuntimeBackend.Cpu &&
                 !configuration.DeviceId.Equals("cpu", StringComparison.OrdinalIgnoreCase))
        {
            failureCode = "runtime-device-unsupported";
        }

        if (failureCode is null)
        {
            return new GgufCapabilityValidationResult(true, configuration, null);
        }

        return new GgufCapabilityValidationResult(
            false,
            configuration,
            new GgufRuntimeFailure(
                GgufRuntimeFailureCategory.UnsupportedConfiguration,
                failureCode));
    }
}
