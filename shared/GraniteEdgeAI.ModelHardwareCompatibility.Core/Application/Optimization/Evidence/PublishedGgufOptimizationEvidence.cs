namespace GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Optimization.Evidence;

/// <summary>
/// Historical AtomicBot measurements retained for typed exclusion, not runtime
/// admission. The frozen 2026-07-17 adjudications assign a critical cap to at
/// least one mandatory prompt in every row. No row proves all product gates.
/// </summary>
public static class PublishedGgufOptimizationEvidence
{
    public const string RuntimePackageIdentity =
        "atomicbot-turboquant-519f0c594a8e31467d2e2f2cf17054c9e7e11536-win-x64";
    public const string RuntimeBuildId =
        "atomicbot-519f0c594a8e31467d2e2f2cf17054c9e7e11536-cpu-vulkan";
    public const string RuntimeSourceCommit =
        "519f0c594a8e31467d2e2f2cf17054c9e7e11536";
    public const string MethodologyIdentity =
        "atomicbot-six-prompt-quality-v1";
    public const string MemoryPerformanceProtocol =
        "atomicbot-controlled-retest-2026-07-16";

    private const string Granite3BSha256 =
        "662b0626cd58f443baea23559b469df6576a81d349649c59413b36a9fb32eb29";
    private const string Granite8BSha256 =
        "ed902ac9eb6adce5a90c6a08c8ea201b50e23fdc5976d1cd0362006afac5309e";

    public static OptimizationEvidenceCatalog CreateCatalog() => new(Records());

    public static IReadOnlyList<OptimizationEvidenceRecord> Records() =>
    [
        .. Model(Granite3BSha256, 3_000_000_000,
        [
            ("AB-KV3-F16-4K", "f16", OptimizationEvidenceBackend.Cpu,
                OptimizationEvidenceDeviceClass.Cpu, "cpu", 6.3917m),
            ("AB-04", "q8_0", OptimizationEvidenceBackend.Cpu,
                OptimizationEvidenceDeviceClass.Cpu, "cpu", 6.725m),
            ("AB-05", "turbo4", OptimizationEvidenceBackend.Cpu,
                OptimizationEvidenceDeviceClass.Cpu, "cpu", 6.8417m),
            ("AB-06", "turbo3", OptimizationEvidenceBackend.Cpu,
                OptimizationEvidenceDeviceClass.Cpu, "cpu", 5.95m),
            ("AB-11", "f16", OptimizationEvidenceBackend.Vulkan,
                OptimizationEvidenceDeviceClass.IntelIntegratedGpu,
                "vulkan-partial", 6.725m),
            ("AB-12", "turbo3", OptimizationEvidenceBackend.Vulkan,
                OptimizationEvidenceDeviceClass.IntelIntegratedGpu,
                "vulkan-partial", 6.225m),
            ("AB-13", "turbo3", OptimizationEvidenceBackend.Vulkan,
                OptimizationEvidenceDeviceClass.IntelIntegratedGpu,
                "vulkan-full", 7.225m)
        ]),
        .. Model(Granite8BSha256, 8_000_000_000,
        [
            ("AB-08Q", "q8_0", OptimizationEvidenceBackend.Cpu,
                OptimizationEvidenceDeviceClass.Cpu, "cpu", 6.6917m),
            ("AB-09", "turbo4", OptimizationEvidenceBackend.Cpu,
                OptimizationEvidenceDeviceClass.Cpu, "cpu", 5.9667m),
            ("AB-10", "turbo3", OptimizationEvidenceBackend.Cpu,
                OptimizationEvidenceDeviceClass.Cpu, "cpu", 6.675m),
            ("AB-14", "q8_0", OptimizationEvidenceBackend.Vulkan,
                OptimizationEvidenceDeviceClass.IntelIntegratedGpu,
                "vulkan-partial", 8.3583m),
            ("AB-15", "turbo3", OptimizationEvidenceBackend.Vulkan,
                OptimizationEvidenceDeviceClass.IntelIntegratedGpu,
                "vulkan-partial", 7.5083m)
        ])
    ];

    private static IEnumerable<OptimizationEvidenceRecord> Model(
        string modelSha256,
        ulong parameterCount,
        IEnumerable<(string EvidenceId, string Cache,
            OptimizationEvidenceBackend Backend,
            OptimizationEvidenceDeviceClass DeviceClass,
            string ExecutionProfile, decimal Quality)> results) =>
        results.Select(result => new OptimizationEvidenceRecord(
            result.EvidenceId,
            new OptimizationEvidenceKey(
                OptimizationEvidenceModelFamily.Granite,
                modelSha256,
                parameterCount,
                OptimizationRoute.Gguf,
                RuntimePackageIdentity,
                "q4_k_m",
                "q4_k_m",
                result.Cache,
                result.Backend,
                result.DeviceClass,
                4096,
                "local-chat-v1",
                MethodologyIdentity,
                MemoryPerformanceProtocol,
                result.ExecutionProfile),
            new OptimizationQualityScore(result.Quality),
            // Complete source-to-catalog re-adjudication: every row has a
            // rubric-designated critical failure. AB-05/06/08Q/09/10 also have
            // empty P5 output. Package integrity, actual activation and stable
            // repeated execution have no complete row-level passing proof in
            // these artifacts. Unknown is not a passing gate.
            OutputHealthPassed: false,
            StabilityPassed: false,
            ActivationPassed: false,
            IntegrityPassed: false));
}
