using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Optimization;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Optimization.Evidence;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Domain;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Routes.OpenVino;
using GraniteEdgeAI.OpenVino.Contracts;
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
                source ?? throw Unsupported(), evidence.Builds);
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
        OpenVinoOptimizationCapabilityAdmission admission,
        OpenVinoBuildEvidence builds)
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
            OpenVinoWeightPrecision.MxFp4 => OpenVinoWeightFormat.MxFp4,
            _ => throw Unsupported()
        };
        OpenVinoKvCacheFormat kvCache = admission.Runtime.KvCachePrecision switch
        {
            OpenVinoKvCachePrecision.ReleasedDefault =>
                OpenVinoKvCacheFormat.RouteDefault,
            OpenVinoKvCachePrecision.U8 => OpenVinoKvCacheFormat.U8,
            OpenVinoKvCachePrecision.U4 => OpenVinoKvCacheFormat.U4,
            OpenVinoKvCachePrecision.Tbq4 =>
                OpenVinoKvCacheFormat.TurboQuantTbq4,
            OpenVinoKvCachePrecision.Tbq3 =>
                OpenVinoKvCacheFormat.TurboQuantTbq3,
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
            OpenVinoCapabilityMaturity.Experimental => SupportLevel.Experimental,
            _ => throw Unsupported()
        };

        bool turboQuant = admission.Runtime.KvCachePrecision is
            OpenVinoKvCachePrecision.Tbq4 or OpenVinoKvCachePrecision.Tbq3;
        bool releasedTurbo = turboQuant && maturity == SupportLevel.DeclaredSupported
            && VerifiedOpenVinoOptimizationEvidence.IsReleasedTurboQuantConfiguration(
                admission.EvidenceId,
                OpenVinoRouteConfiguration.Create(weights, kvCache, DeviceRouteId.Cpu,
                    performanceHint, ContractCompiledCachePolicy.Disabled, admission.Streams),
                admission.MinimumContextTokens, admission.MaximumContextTokens);
        if ((!turboQuant && admission.Maturity == OpenVinoCapabilityMaturity.Experimental)
            || (turboQuant && admission.Maturity != OpenVinoCapabilityMaturity.Experimental && !releasedTurbo) ||
            turboQuant && !HasExactTurboQuantBuild(builds))
        {
            throw Unsupported();
        }

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
            requiresEvidence: turboQuant && !releasedTurbo);
    }

    internal static bool HasExactTurboQuantBuild(OpenVinoBuildEvidence evidence) =>
        evidence.TurboQuantBuild is { } turbo &&
        string.Equals(
            evidence.RuntimeBuild,
            "2026.5.0-22950-f5f594dc0c9",
            StringComparison.Ordinal) &&
        string.Equals(
            evidence.GenAiBuild,
            "2026.5.0.0-3409-6fbc103538d",
            StringComparison.Ordinal) &&
        string.Equals(evidence.TokenizersBuild,
            VerifiedOpenVinoOptimizationEvidence.TurboTokenizersBuild, StringComparison.Ordinal) &&
        (string.Equals(evidence.WorkerManifestDigest,
            VerifiedOpenVinoOptimizationEvidence.TurboWorkerManifestSha256, StringComparison.Ordinal) ||
         string.Equals(evidence.WorkerManifestDigest,
            "39a4eccc05d4677b59f7f882cc50f875591ee3d3f90f1037e61e17fe3a34c35e", StringComparison.Ordinal) ||
         string.Equals(evidence.WorkerManifestDigest,
            VerifiedOpenVinoOptimizationEvidence.PackagedTurboWorkerManifestSha256,
            StringComparison.Ordinal)) &&
        string.Equals(turbo.PatchSeriesDigest,
            VerifiedOpenVinoOptimizationEvidence.TurboPatchSeriesSha256, StringComparison.Ordinal) &&
        string.Equals(turbo.RuntimeManifestDigest,
            VerifiedOpenVinoOptimizationEvidence.TurboRuntimeManifestSha256, StringComparison.Ordinal) &&
        string.Equals(
            turbo.SourceCommit,
            "f5f594dc0c9e5961785f0d17743486d52eac87e7",
            StringComparison.Ordinal) &&
        string.Equals(
            turbo.ImplementationCommit,
            "b9a1f201c109e0bed74763934f79483cf6c4cbf4",
            StringComparison.Ordinal);

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
