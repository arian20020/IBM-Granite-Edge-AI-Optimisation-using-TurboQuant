namespace GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Optimization.Evidence;

/// <summary>
/// Audited OpenVINO results imported from campaign fv2-2026-08-30. These are
/// exact-artifact facts, not general quality estimates for similarly named models.
/// </summary>
public static class PublishedOpenVinoOptimizationEvidence
{
    public const string RuntimePackageIdentity =
        "openvino@f5f594dc0c9e5961785f0d17743486d52eac87e7";
    public const string MethodologyIdentity =
        "openvino-sector-experience-quality-v3";
    public const string MemoryPerformanceProtocol =
        "fv2-2026-08-30";

    public static OptimizationEvidenceCatalog CreateCatalog() => new(Records());

    public static IReadOnlyList<OptimizationEvidenceRecord> Records() =>
    [
        .. Model("6da1029abd4a1464a94a571ac6a6a950c262b226c15912915a721e0643f7c708",
            3_000_000_000, "int4", [
                ("f16", 5.77m), ("tbq3", 5.42m), ("tbq4", 5.85m),
                ("u4", 5.52m), ("u8", 5.77m)]),
        .. Model("f92a009e300ee1c01edc8a6cbaf4f591e2998421757ce63c090d75a5687faa06",
            3_000_000_000, "int8", [
                ("f16", 5.77m), ("tbq3", 5.48m), ("tbq4", 5.52m),
                ("u4", 5.79m), ("u8", 5.77m)]),
        .. Model("789db59c53b45da8b8143b90f11d268597658d6f9eae6cbb049c20343b01fcd4",
            8_000_000_000, "int4", [
                ("f16", 5.60m), ("tbq3", 5.21m), ("tbq4", 5.60m),
                ("u4", 5.38m), ("u8", 5.60m)])
    ];

    private static IEnumerable<OptimizationEvidenceRecord> Model(
        string modelSha256,
        ulong parameterCount,
        string weights,
        IEnumerable<(string Cache, decimal Quality)> results) =>
        results.Select(result => new OptimizationEvidenceRecord(
            CapabilityEvidenceId(weights, result.Cache),
            new OptimizationEvidenceKey(
                OptimizationEvidenceModelFamily.Granite,
                modelSha256,
                parameterCount,
                OptimizationRoute.OpenVino,
                RuntimePackageIdentity,
                weights,
                weights,
                result.Cache,
                OptimizationEvidenceBackend.OpenVinoCpu,
                OptimizationEvidenceDeviceClass.Cpu,
                4096,
                "local-chat-v1",
                MethodologyIdentity,
                MemoryPerformanceProtocol,
                "openvino-cpu"),
            new OptimizationQualityScore(result.Quality),
            OutputHealthPassed: true,
            StabilityPassed: true,
            ActivationPassed: true,
            IntegrityPassed: true));

    private static string CapabilityEvidenceId(string weights, string cache) =>
        (weights, cache) switch
        {
            ("int4", "f16") => "OV-STD-CPU-INT4-DEFAULT-01",
            ("int4", "u8") => "OV-STD-CPU-INT4-U8-01",
            ("int4", "u4") => "OV-STD-CPU-INT4-U4-01",
            ("int8", "f16") => "OV-STD-CPU-AUTO-01",
            ("int8", "u8") => "OV-STD-CPU-INT8-U8-01",
            ("int8", "u4") => "OV-STD-CPU-INT8-U4-01",
            ("int4", "tbq4") => "OV-TBQ4-CPU-INT4-01",
            ("int4", "tbq3") => "OV-TBQ3-CPU-INT4-01",
            ("int8", "tbq4") => "OV-TBQ4-CPU-INT8-01",
            ("int8", "tbq3") => "OV-TBQ3-CPU-INT8-01",
            _ => $"fv2-{weights}-{cache}"
        };
}
