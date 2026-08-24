using System.Globalization;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Optimization;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Domain;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Routes.OpenVino;
using ContractCompiledCachePolicy = GraniteEdgeAI.ModelHardwareCompatibility.Core.Routes.OpenVino.OpenVinoCompiledCachePolicy;

namespace GraniteEdgeAI.Features.OpenVinoRoute.Optimization;

public static class OpenVinoOptimizationCapabilityProjector
{
    public static OpenVinoCapabilityPayload Project(
        OpenVinoOptimizationCapabilityEvidence evidence)
    {
        ArgumentNullException.ThrowIfNull(evidence);
        ArgumentNullException.ThrowIfNull(evidence.Versions);
        ArgumentNullException.ThrowIfNull(evidence.Admitted);

        string runtimeVersion = RuntimeVersion(evidence.Versions);
        OpenVinoAdmittedConfiguration[] admitted =
            new OpenVinoAdmittedConfiguration[evidence.Admitted.Count];
        for (int index = 0; index < admitted.Length; index++)
        {
            admitted[index] = ProjectAdmission(
                evidence.Admitted[index] ?? throw Unsupported());
        }

        return OpenVinoCapabilityPayload.Create(runtimeVersion, admitted);
    }

    private static OpenVinoAdmittedConfiguration ProjectAdmission(
        OpenVinoOptimizationCapabilityAdmission admission)
    {
        if (!string.Equals(admission.Device, "CPU", StringComparison.Ordinal) ||
            admission.Runtime is null || admission.Runtime.CompiledCache is null)
        {
            throw Unsupported();
        }

        OpenVinoWeightFormat weights = admission.WeightPrecision switch
        {
            OpenVinoWeightPrecision.Original => OpenVinoWeightFormat.Original,
            OpenVinoWeightPrecision.Fp16 => OpenVinoWeightFormat.Fp16,
            OpenVinoWeightPrecision.EightBit => OpenVinoWeightFormat.Int8,
            OpenVinoWeightPrecision.FourBit => OpenVinoWeightFormat.Int4,
            _ => throw Unsupported()
        };
        OpenVinoKvCacheFormat kvCache = admission.Runtime.KvCachePrecision switch
        {
            OpenVinoKvCachePrecision.ReleasedDefault =>
                OpenVinoKvCacheFormat.RouteDefault,
            OpenVinoKvCachePrecision.U8 => OpenVinoKvCacheFormat.U8,
            _ => throw Unsupported()
        };
        OpenVinoPerformanceHint performanceHint = admission.PerformanceHint switch
        {
            OpenVinoCapabilityPerformanceHint.Latency =>
                OpenVinoPerformanceHint.Latency,
            _ => throw Unsupported()
        };
        SupportLevel maturity = admission.Maturity switch
        {
            OpenVinoCapabilityMaturity.Released => SupportLevel.DeclaredSupported,
            _ => throw Unsupported()
        };

        return OpenVinoAdmittedConfiguration.Create(
            admission.EvidenceId,
            DeviceRouteId.Cpu,
            weights,
            kvCache,
            performanceHint,
            admission.Runtime.CompiledCache.Enabled
                ? ContractCompiledCachePolicy.Enabled
                : ContractCompiledCachePolicy.Disabled,
            admission.Streams,
            admission.MinimumContextTokens,
            admission.MaximumContextTokens,
            maturity,
            requiresEvidence: false);
    }

    private static string RuntimeVersion(OpenVinoOptimizationToolVersions versions)
    {
        RequirePinnedVersion(
            versions.OpenVino, "2026.3.0", nameof(versions.OpenVino));
        RequirePinnedVersion(
            versions.OpenVinoGenAi, "2026.3.0.0", nameof(versions.OpenVinoGenAi));
        RequirePinnedVersion(versions.Nncf, "3.3.0", nameof(versions.Nncf));
        RequirePinnedVersion(versions.Optimum, "2.3.0", nameof(versions.Optimum));
        RequirePinnedVersion(
            versions.OptimumIntel, "2.1.0", nameof(versions.OptimumIntel));
        RequirePinnedVersion(
            versions.Transformers, "5.5.4", nameof(versions.Transformers));

        return string.Create(
            CultureInfo.InvariantCulture,
            $"openvino-{versions.OpenVino}_openvino-genai-{versions.OpenVinoGenAi}"
            + $"_nncf-{versions.Nncf}_optimum-{versions.Optimum}"
            + $"_optimum-intel-{versions.OptimumIntel}"
            + $"_transformers-{versions.Transformers}");
    }

    private static void RequirePinnedVersion(
        string value, string expected, string parameter)
    {
        RequireVersion(value, parameter);
        if (!string.Equals(value, expected, StringComparison.Ordinal))
        {
            throw new ArgumentException(
                "Capability evidence must match the exact admitted tool version.",
                parameter);
        }
    }

    private static void RequireVersion(string value, string parameter)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length > 32 ||
            value.Any(static character =>
                !char.IsAsciiLetterOrDigit(character) && character is not '.' and not '-'))
        {
            throw new ArgumentException(
                "A capability version must be a bounded dotted or prerelease identifier.",
                parameter);
        }
    }

    private static OpenVinoOptimizationException Unsupported() =>
        new(OpenVino.Contracts.OpenVinoSupportCode.OptimizationUnsupported);
}
