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
        ArgumentNullException.ThrowIfNull(evidence.Builds);
        ArgumentNullException.ThrowIfNull(evidence.Versions);
        ArgumentNullException.ThrowIfNull(evidence.Admitted);
        evidence.Builds.Validate();
        if (evidence.Admitted.Count == 0)
        {
            throw new ArgumentException(
                "At least one released OpenVINO admission is required.",
                nameof(evidence));
        }

        ValidateToolVersions(evidence.Versions);
        List<OpenVinoAdmittedConfiguration> admitted = [];
        foreach (OpenVinoOptimizationCapabilityAdmission source in evidence.Admitted)
        {
            OpenVinoAdmittedConfiguration? projected = ProjectAdmission(
                source ?? throw Unsupported());
            if (projected is not null)
            {
                admitted.Add(projected);
            }
        }

        if (admitted.Count == 0)
        {
            throw Unsupported();
        }

        return OpenVinoCapabilityPayload.Create(
            evidence.Builds.RuntimeBuild,
            admitted);
    }

    private static OpenVinoAdmittedConfiguration? ProjectAdmission(
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

        if (admission.MinimumContextTokens < 1 ||
            admission.MaximumContextTokens < admission.MinimumContextTokens)
        {
            throw new ArgumentException(
                "Capability context evidence is not a valid range.",
                nameof(admission));
        }

        if (admission.Streams != 1 ||
            admission.Runtime.CompiledCache.Enabled ||
            admission.MinimumContextTokens > 4_096 ||
            admission.MaximumContextTokens < 4_096)
        {
            return null;
        }

        return OpenVinoAdmittedConfiguration.Create(
            admission.EvidenceId,
            DeviceRouteId.Cpu,
            weights,
            kvCache,
            performanceHint,
            ContractCompiledCachePolicy.Disabled,
            streams: 1,
            minimumContextTokens: 4_096,
            maximumContextTokens: 4_096,
            maturity,
            requiresEvidence: false);
    }

    internal static void ValidateToolVersions(OpenVinoOptimizationToolVersions versions)
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
