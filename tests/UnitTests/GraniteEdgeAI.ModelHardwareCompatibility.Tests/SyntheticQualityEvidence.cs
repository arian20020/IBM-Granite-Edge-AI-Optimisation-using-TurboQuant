using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Contracts;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Estimation;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Optimization;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Optimization.Evidence;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Domain;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Routes.Gguf;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Routes.OpenVino;

namespace GraniteEdgeAI.ModelHardwareCompatibility.Tests;

/// <summary>Explicit synthetic records for resource/projection tests, never product evidence.</summary>
internal static class SyntheticQualityEvidence
{
    internal const string OpenVinoBuild = "2026.5.0-22950-f5f594dc0c9";
    internal const ulong ParameterCount = 3_000_000_000;

    internal static GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Presentation.CompatibilityOptimizationProductionInput ProductionInput(
        OptimizationCapabilitySnapshot snapshot, OptimizationWorkload workload,
        OptimizationJourneyBinding binding, IReadOnlySet<string> optedInExperimentalEvidenceIds) =>
        GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Presentation.CompatibilityOptimizationProductionInput.Create(
            snapshot, workload, binding, optedInExperimentalEvidenceIds, Catalog(snapshot, workload, binding));

    internal static CrossRouteGenerationResult Generate(
        OptimizationCapabilitySnapshot snapshot, InspectedModelFacts facts,
        OptimizationWorkload workload, OptimizationJourneyBinding binding,
        ByteCount safeBudget, ByteCount availableDisk, EstimatorPolicy policy,
        IReadOnlySet<string> optedInExperimentalEvidenceIds,
        OptimizationHardwareAuthority hardwareAuthority) => CrossRouteCandidateGenerator.Generate(
            snapshot, facts, workload, binding, safeBudget, availableDisk, policy,
            optedInExperimentalEvidenceIds, hardwareAuthority,
            Catalog(snapshot, workload, binding), ParameterCount);

    internal static OptimizationEvidenceCatalog Catalog(OptimizationCapabilitySnapshot snapshot,
        OptimizationWorkload workload, OptimizationJourneyBinding binding)
    {
        List<OptimizationEvidenceRecord> records = [];
        foreach (ContextTokenCount context in workload.CandidateContexts)
        {
            if (snapshot.OpenVino is { } ov)
            {
                foreach (OpenVinoAdmittedConfiguration entry in ov.Admitted.OrderBy(row => row.Level))
                {
                    if (!ov.ExecutionAuthorities.ContainsKey(entry.EvidenceId)) continue;
                    string weights = entry.Weights switch
                    {
                        OpenVinoWeightFormat.Int4 => "int4", OpenVinoWeightFormat.Int8 => "int8", _ => "fp16"
                    };
                    string cache = entry.KvCache switch
                    {
                        OpenVinoKvCacheFormat.RouteDefault => "released-default", OpenVinoKvCacheFormat.F16 => "f16",
                        OpenVinoKvCacheFormat.Bf16 => "bf16", OpenVinoKvCacheFormat.U8 => "u8",
                        OpenVinoKvCacheFormat.U4 => "u4", OpenVinoKvCacheFormat.TurboQuantTbq4 => "tbq4", _ => "tbq3"
                    };
                    decimal score = entry.Weights == OpenVinoWeightFormat.Int4 ? 4m
                        : entry.Weights == OpenVinoWeightFormat.Int8 ? 6m : 8m;
                    score = Math.Min(score, cache switch { "u8" => 6m, "u4" or "tbq4" => 4m, "tbq3" => 0m, _ => 8m });
                    Add(entry.EvidenceId, OptimizationRoute.OpenVino, weights, cache,
                        entry.Device, entry.Device == DeviceRouteId.Cpu ? OptimizationEvidenceBackend.OpenVinoCpu
                            : entry.Device == DeviceRouteId.IntelNpu ? OptimizationEvidenceBackend.OpenVinoNpu : OptimizationEvidenceBackend.OpenVinoGpu,
                        entry.Device == DeviceRouteId.Cpu ? "openvino-cpu" : entry.Device == DeviceRouteId.IntelNpu ? "openvino-npu" : "openvino-gpu", score);
                }
            }
            if (snapshot.Gguf is { } gguf)
            {
                foreach (GgufAdmittedConfiguration entry in gguf.Admitted.OrderBy(row => row.Level))
                {
                    if (context.Tokens < entry.MinimumContextTokens || context.Tokens > entry.MaximumContextTokens) continue;
                    // This resource fixture supplies only imported Q4_K_M evidence.
                    if (entry.Weights is not (GgufWeightFormat.Imported or GgufWeightFormat.Q4KM)) continue;
                    Add(entry.EvidenceId, OptimizationRoute.Gguf, "q4_k_m",
                        entry.KvCache switch { GgufKvCacheFormat.F16 => "f16", GgufKvCacheFormat.Q8_0 => "q8_0",
                            GgufKvCacheFormat.TurboQuant4Bit => "turbo4", GgufKvCacheFormat.TurboQuant3Bit => "turbo3", _ => "turbo2" },
                        entry.Device, entry.Backend == CompatibilityBackend.Cpu ? OptimizationEvidenceBackend.Cpu : OptimizationEvidenceBackend.Vulkan,
                        entry.Backend == CompatibilityBackend.Cpu ? "cpu" : entry.Offload == GpuOffloadLevel.Full ? "vulkan-full" : "vulkan-partial", 4m);
                }
            }
            void Add(string id, OptimizationRoute route, string weights, string cache,
                DeviceRouteId device, OptimizationEvidenceBackend backend, string profile, decimal score)
            {
                OptimizationEvidenceKey key = new(OptimizationEvidenceModelFamily.Granite, binding.ModelSha256,
                    ParameterCount, route, route == OptimizationRoute.Gguf ? PublishedGgufOptimizationEvidence.RuntimePackageIdentity
                        : CrossRouteCandidateGenerator.OpenVinoEvidencePackageIdentity(snapshot.OpenVino!.ExecutionAuthorities[id]),
                    weights, weights, cache, backend, device switch
                    {
                        DeviceRouteId.Cpu => OptimizationEvidenceDeviceClass.Cpu,
                        DeviceRouteId.IntelIntegratedGpu => OptimizationEvidenceDeviceClass.IntelIntegratedGpu,
                        DeviceRouteId.IntelDiscreteGpu => OptimizationEvidenceDeviceClass.IntelDiscreteGpu,
                        _ => OptimizationEvidenceDeviceClass.IntelNpu
                    }, context.Tokens, "local-chat-v1",
                    route == OptimizationRoute.Gguf ? PublishedGgufOptimizationEvidence.MethodologyIdentity : PublishedOpenVinoOptimizationEvidence.MethodologyIdentity,
                    route == OptimizationRoute.Gguf ? PublishedGgufOptimizationEvidence.MemoryPerformanceProtocol : PublishedOpenVinoOptimizationEvidence.MemoryPerformanceProtocol,
                    profile);
                if (!records.Any(row => row.Key == key))
                    records.Add(new(id, key, new(score), true, true, true, true));
            }
        }
        return new(records);
    }
}
