using System.Security.Cryptography;
using System.Text;
using HardwareInspection.LlmFitSpike.Inspection;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace HardwareInspection.LlmFitSpike.Tests.Inspection;

[TestClass]
[TestCategory("Deterministic")]
#pragma warning disable CA1707 // Test names intentionally encode the required behavior.
public sealed class LlmFitSystemJsonAssessorTests
{
    private static readonly string[] ExpectedIntelDiagnostics =
    [
        "HI-LLMFIT-WINDOWS-INTEL-MEMORY-SEMANTICS-GAP",
        "HI-LLMFIT-WINDOWS-INTEL-NPU-GAP",
        "HI-LLMFIT-SCHEMA-DOCUMENTATION-DRIFT",
    ];

    private static readonly string[] ExpectedCommonDiagnostics =
    ["HI-LLMFIT-WINDOWS-INTEL-NPU-GAP", "HI-LLMFIT-SCHEMA-DOCUMENTATION-DRIFT"];

    private static readonly string[] ExpectedMalformedDiagnostics =
    [
        "HI-LLMFIT-JSON-INVALID",
        "HI-LLMFIT-WINDOWS-INTEL-NPU-GAP",
        "HI-LLMFIT-SCHEMA-DOCUMENTATION-DRIFT",
    ];

    private static readonly string[] ExpectedCpuRamDiagnostics =
    [
        "HI-LLMFIT-CPU-RAM-MISSING",
        "HI-LLMFIT-WINDOWS-INTEL-NPU-GAP",
        "HI-LLMFIT-SCHEMA-DOCUMENTATION-DRIFT",
    ];

    private static readonly string[] ExpectedInvalidCpuRamDiagnostics =
    [
        "HI-LLMFIT-CPU-RAM-MISSING",
        "HI-LLMFIT-JSON-INVALID",
        "HI-LLMFIT-WINDOWS-INTEL-NPU-GAP",
        "HI-LLMFIT-SCHEMA-DOCUMENTATION-DRIFT",
    ];

    private static readonly string[] OriginalDiagnostic = ["original"];
    private static readonly string[] MutatedDiagnostic = ["mutated"];

    [TestMethod]
    public void Assess_ValidTaggedWindowsIntelGpuFixture_ReportsIntelAndKnownGaps()
    {
        LlmFitSystemAssessment assessment = LlmFitSystemJsonAssessor.Assess(ReadFixture("valid-windows-intel.json"));

        Assert.IsTrue(assessment.JsonValid);
        Assert.IsTrue(assessment.RequiredCpuRamPresent);
        Assert.IsTrue(assessment.Gate1SchemaPassed);
        Assert.AreEqual("Fixture Intel CPU", assessment.CpuName);
        Assert.AreEqual(16, assessment.CpuLogicalProcessorCount);
        Assert.AreEqual(31.72, assessment.TotalRamGiB);
        Assert.AreEqual(18.40, assessment.AvailableRamGiB);
        Assert.IsTrue(assessment.GpuReported);
        Assert.AreEqual(1, assessment.ReportedGpuCount);
        Assert.IsTrue(assessment.IntelGpuReported);
        Assert.IsFalse(assessment.DedicatedSharedMemorySemanticsEstablished);
        Assert.AreEqual("DetectionUnavailable", assessment.IntelNpuDetectionState);
        CollectionAssert.AreEqual(
            ExpectedIntelDiagnostics,
            assessment.DiagnosticCodes.ToArray());
    }

    [TestMethod]
    public void Assess_ValidTaggedCpuOnlyFixture_ReportsNoGpu()
    {
        LlmFitSystemAssessment assessment = LlmFitSystemJsonAssessor.Assess(ReadFixture("valid-cpu-only.json"));

        Assert.IsTrue(assessment.JsonValid);
        Assert.IsTrue(assessment.Gate1SchemaPassed);
        Assert.IsFalse(assessment.GpuReported);
        Assert.AreEqual(0, assessment.ReportedGpuCount);
        Assert.IsFalse(assessment.IntelGpuReported);
        CollectionAssert.AreEqual(
            ExpectedCommonDiagnostics,
            assessment.DiagnosticCodes.ToArray());
    }

    [TestMethod]
    public void Assess_MalformedJson_ReturnsPrivacySafeFailedAssessmentWithExactHash()
    {
        const string rawJson = "{\"system\":";

        LlmFitSystemAssessment assessment = LlmFitSystemJsonAssessor.Assess(rawJson);

        AssertPrivacySafeJsonFailure(assessment);
        Assert.AreEqual(Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(rawJson))).ToLowerInvariant(), assessment.RawJsonSha256);
        CollectionAssert.AreEqual(
            ExpectedMalformedDiagnostics,
            assessment.DiagnosticCodes.ToArray());
    }

    [TestMethod]
    [DataRow("{}")]
    [DataRow("{\"system\":[]}")]
    public void Assess_MissingOrNonObjectSystem_FailsJsonSchema(string rawJson)
    {
        LlmFitSystemAssessment assessment = LlmFitSystemJsonAssessor.Assess(rawJson);

        AssertPrivacySafeJsonFailure(assessment);
        CollectionAssert.AreEqual(ExpectedMalformedDiagnostics, assessment.DiagnosticCodes.ToArray());
    }

    [TestMethod]
    public void Assess_BlankCpuName_ReportsCpuRamMissing()
    {
        string rawJson = ReadFixture("valid-cpu-only.json").Replace("Fixture CPU Only", "   ", StringComparison.Ordinal);

        LlmFitSystemAssessment assessment = LlmFitSystemJsonAssessor.Assess(rawJson);

        Assert.IsTrue(assessment.JsonValid);
        Assert.IsFalse(assessment.RequiredCpuRamPresent);
        Assert.IsFalse(assessment.Gate1SchemaPassed);
        Assert.IsNull(assessment.CpuName);
        CollectionAssert.AreEqual(ExpectedCpuRamDiagnostics, assessment.DiagnosticCodes.ToArray());
    }

    [TestMethod]
    [DataRow("\"total_ram_gb\": 31.72", "\"total_ram_gb\": 0")]
    [DataRow("\"available_ram_gb\": 12.50", "\"available_ram_gb\": -1")]
    [DataRow("\"available_ram_gb\": 12.50", "\"available_ram_gb\": 99")]
    public void Assess_SemanticallyInvalidCpuOrRamValue_ReportsCpuRamMissing(string original, string replacement)
    {
        string rawJson = ReadFixture("valid-cpu-only.json").Replace(original, replacement, StringComparison.Ordinal);

        LlmFitSystemAssessment assessment = LlmFitSystemJsonAssessor.Assess(rawJson);

        Assert.IsTrue(assessment.JsonValid);
        Assert.IsFalse(assessment.RequiredCpuRamPresent);
        Assert.IsFalse(assessment.Gate1SchemaPassed);
        CollectionAssert.AreEqual(ExpectedCpuRamDiagnostics, assessment.DiagnosticCodes.ToArray());
    }

    [TestMethod]
    [DataRow("\"total_ram_gb\": 31.72", "\"total_ram_gb\": 1e9999")]
    [DataRow("\"cpu_cores\": 8", "\"cpu_cores\": 1.5")]
    [DataRow("\"total_ram_gb\": 31.72", "\"total_ram_gb\": \"31.72\"")]
    public void Assess_RepresentationallyInvalidCpuOrRamValue_FailsJsonSchema(string original, string replacement)
    {
        string rawJson = ReadFixture("valid-cpu-only.json").Replace(original, replacement, StringComparison.Ordinal);

        LlmFitSystemAssessment assessment = LlmFitSystemJsonAssessor.Assess(rawJson);

        Assert.IsFalse(assessment.JsonValid);
        Assert.IsFalse(assessment.RequiredCpuRamPresent);
        Assert.IsFalse(assessment.Gate1SchemaPassed);
        CollectionAssert.AreEqual(ExpectedInvalidCpuRamDiagnostics, assessment.DiagnosticCodes.ToArray());
    }

    [TestMethod]
    [DataRow("\"total_ram_gb\": 31.72,", "")]
    [DataRow("\"available_ram_gb\": 12.50,", "")]
    [DataRow("\"cpu_cores\": 8,", "")]
    [DataRow("\"cpu_name\": \"Fixture CPU Only\",", "")]
    public void Assess_MissingRequiredCpuOrRamMember_RemainsStructurallyValid(string original, string replacement)
    {
        string rawJson = ReadFixture("valid-cpu-only.json").Replace(original, replacement, StringComparison.Ordinal);

        LlmFitSystemAssessment assessment = LlmFitSystemJsonAssessor.Assess(rawJson);

        Assert.IsTrue(assessment.JsonValid);
        Assert.IsFalse(assessment.RequiredCpuRamPresent);
        Assert.IsFalse(assessment.Gate1SchemaPassed);
        CollectionAssert.AreEqual(ExpectedCpuRamDiagnostics, assessment.DiagnosticCodes.ToArray());
    }

    [TestMethod]
    [DataRow("\"total_ram_gb\": 31.72", "\"total_ram_gb\": \"31.72\"")]
    [DataRow("\"available_ram_gb\": 12.50", "\"available_ram_gb\": \"12.50\"")]
    [DataRow("\"cpu_cores\": 8", "\"cpu_cores\": \"8\"")]
    [DataRow("\"cpu_name\": \"Fixture CPU Only\"", "\"cpu_name\": 8")]
    public void Assess_WrongRequiredCpuOrRamMemberType_FailsJsonSchema(string original, string replacement)
    {
        string rawJson = ReadFixture("valid-cpu-only.json").Replace(original, replacement, StringComparison.Ordinal);

        LlmFitSystemAssessment assessment = LlmFitSystemJsonAssessor.Assess(rawJson);

        Assert.IsFalse(assessment.JsonValid);
        Assert.IsFalse(assessment.RequiredCpuRamPresent);
        Assert.IsFalse(assessment.Gate1SchemaPassed);
        CollectionAssert.AreEqual(ExpectedInvalidCpuRamDiagnostics, assessment.DiagnosticCodes.ToArray());
    }

    [TestMethod]
    [DataRow("\"system\": {", "\"system\": null, \"system\": {")]
    [DataRow("\"total_ram_gb\": 31.72,", "\"total_ram_gb\": \"wrong\", \"total_ram_gb\": 31.72,")]
    [DataRow("\"has_gpu\": false,", "\"has_gpu\": true, \"has_gpu\": false,")]
    [DataRow("\"gpu_count\": 0,", "\"gpu_count\": 1, \"gpu_count\": 0,")]
    public void Assess_ReversedDuplicateRootOrSystemMember_FailsJsonSchema(string original, string replacement)
    {
        string rawJson = ReadFixture("valid-cpu-only.json").Replace(original, replacement, StringComparison.Ordinal);

        LlmFitSystemAssessment assessment = LlmFitSystemJsonAssessor.Assess(rawJson);

        AssertPrivacySafeJsonFailure(assessment);
        CollectionAssert.AreEqual(ExpectedMalformedDiagnostics, assessment.DiagnosticCodes.ToArray());
    }

    [TestMethod]
    public void Assess_ReversedDuplicateGpuCountMember_FailsJsonSchema()
    {
        string rawJson = ReadFixture("valid-windows-intel.json")
            .Replace("\"count\": 1,", "\"count\": 0, \"count\": 1,", StringComparison.Ordinal);

        LlmFitSystemAssessment assessment = LlmFitSystemJsonAssessor.Assess(rawJson);

        AssertPrivacySafeJsonFailure(assessment);
        CollectionAssert.AreEqual(ExpectedMalformedDiagnostics, assessment.DiagnosticCodes.ToArray());
    }

    [TestMethod]
    public void Assess_ExponentNotationAndNegativeZeroAvailableRam_AreAcceptedExactly()
    {
        string rawJson = ReadFixture("valid-cpu-only.json")
            .Replace("31.72", "1e1", StringComparison.Ordinal)
            .Replace("12.50", "-0", StringComparison.Ordinal);

        LlmFitSystemAssessment assessment = LlmFitSystemJsonAssessor.Assess(rawJson);

        Assert.IsTrue(assessment.JsonValid);
        Assert.IsTrue(assessment.RequiredCpuRamPresent);
        Assert.AreEqual(10d, assessment.TotalRamGiB);
        Assert.AreEqual(0d, assessment.AvailableRamGiB);
    }

    [TestMethod]
    public void Assess_NonzeroUnderflowingRamValues_FailJsonSchema()
    {
        string rawJson = ReadFixture("valid-cpu-only.json")
            .Replace("31.72", "1e-400", StringComparison.Ordinal)
            .Replace("12.50", "-1e-400", StringComparison.Ordinal);

        LlmFitSystemAssessment assessment = LlmFitSystemJsonAssessor.Assess(rawJson);

        Assert.IsFalse(assessment.JsonValid);
        Assert.IsFalse(assessment.RequiredCpuRamPresent);
        CollectionAssert.AreEqual(ExpectedInvalidCpuRamDiagnostics, assessment.DiagnosticCodes.ToArray());
    }

    [TestMethod]
    public void Assess_ExactAdjacentLargeRamIntegers_DoNotCompareEqualAfterDoubleConversion()
    {
        string rawJson = ReadFixture("valid-cpu-only.json")
            .Replace("31.72", "9007199254740992", StringComparison.Ordinal)
            .Replace("12.50", "9007199254740993", StringComparison.Ordinal);

        LlmFitSystemAssessment assessment = LlmFitSystemJsonAssessor.Assess(rawJson);

        Assert.IsTrue(assessment.JsonValid);
        Assert.IsFalse(assessment.RequiredCpuRamPresent);
        Assert.IsFalse(assessment.Gate1SchemaPassed);
        Assert.IsNull(assessment.AvailableRamGiB);
        CollectionAssert.AreEqual(ExpectedCpuRamDiagnostics, assessment.DiagnosticCodes.ToArray());
    }

    [TestMethod]
    public void Assess_MaxFiniteAndSmallestSubnormalRamValues_AreAccepted()
    {
        string maxFiniteRawJson = ReadFixture("valid-cpu-only.json")
            .Replace("31.72", "1.7976931348623157e308", StringComparison.Ordinal)
            .Replace("12.50", "1.7976931348623157e308", StringComparison.Ordinal);
        string smallestSubnormalRawJson = ReadFixture("valid-cpu-only.json")
            .Replace("31.72", "5e-324", StringComparison.Ordinal)
            .Replace("12.50", "5e-324", StringComparison.Ordinal);

        LlmFitSystemAssessment maxFinite = LlmFitSystemJsonAssessor.Assess(maxFiniteRawJson);
        LlmFitSystemAssessment smallestSubnormal = LlmFitSystemJsonAssessor.Assess(smallestSubnormalRawJson);

        Assert.IsTrue(maxFinite.Gate1SchemaPassed);
        Assert.AreEqual(double.MaxValue, maxFinite.TotalRamGiB);
        Assert.IsTrue(smallestSubnormal.Gate1SchemaPassed);
        Assert.AreEqual(double.Epsilon, smallestSubnormal.TotalRamGiB);
    }

    [TestMethod]
    [DataRow("\"has_gpu\": false", "\"has_gpu\": true")]
    [DataRow("\"gpus\": []", "\"gpus\": [{\"name\":\"Intel GPU\",\"count\":1}]")]
    [DataRow("\"gpu_count\": 0", "\"gpu_count\": 1")]
    public void Assess_InconsistentGpuFlagsCountsOrList_FailsJsonSchema(string original, string replacement)
    {
        string rawJson = ReadFixture("valid-cpu-only.json").Replace(original, replacement, StringComparison.Ordinal);

        LlmFitSystemAssessment assessment = LlmFitSystemJsonAssessor.Assess(rawJson);

        Assert.IsFalse(assessment.JsonValid);
        Assert.IsFalse(assessment.Gate1SchemaPassed);
        CollectionAssert.Contains(assessment.DiagnosticCodes.ToArray(), "HI-LLMFIT-GPU-INCONSISTENT");
    }

    [TestMethod]
    public void Assess_NonPositivePerGpuCount_FailsJsonSchema()
    {
        string rawJson = ReadFixture("valid-windows-intel.json")
            .Replace("\"count\": 1,\n      \"unified_memory\"", "\"count\": 0,\n      \"unified_memory\"", StringComparison.Ordinal);

        LlmFitSystemAssessment assessment = LlmFitSystemJsonAssessor.Assess(rawJson);

        Assert.IsFalse(assessment.JsonValid);
        Assert.IsFalse(assessment.Gate1SchemaPassed);
        CollectionAssert.Contains(assessment.DiagnosticCodes.ToArray(), "HI-LLMFIT-GPU-INCONSISTENT");
    }

    [TestMethod]
    public void Assess_PerGpuCountSumMismatch_FailsJsonSchema()
    {
        string rawJson = ReadFixture("valid-windows-intel.json")
            .Replace("\"gpu_count\": 1", "\"gpu_count\": 2", StringComparison.Ordinal);

        LlmFitSystemAssessment assessment = LlmFitSystemJsonAssessor.Assess(rawJson);

        Assert.IsFalse(assessment.JsonValid);
        Assert.IsFalse(assessment.Gate1SchemaPassed);
        CollectionAssert.Contains(assessment.DiagnosticCodes.ToArray(), "HI-LLMFIT-GPU-INCONSISTENT");
    }

    [TestMethod]
    public void Assess_UnknownRootSystemAndGpuFields_RemainAccepted()
    {
        const string rawJson = """
            {
              "root_future": true,
              "system": {
                "total_ram_gb": 32,
                "available_ram_gb": 16,
                "cpu_cores": 8,
                "cpu_name": "Future CPU",
                "has_gpu": true,
                "gpu_count": 1,
                "gpu_name": "Intel GPU",
                "system_future": "accepted",
                "gpus": [{ "name": "Intel GPU", "count": 1, "gpu_future": 7 }]
              }
            }
            """;

        LlmFitSystemAssessment assessment = LlmFitSystemJsonAssessor.Assess(rawJson);

        Assert.IsTrue(assessment.JsonValid);
        Assert.IsTrue(assessment.Gate1SchemaPassed);
        Assert.IsTrue(assessment.IntelGpuReported);
    }

    [TestMethod]
    public void Assess_IntelRequiresStandaloneToken()
    {
        string rawJson = ReadFixture("valid-windows-intel.json")
            .Replace("Fixture Intel Arc Graphics", "Fixture Intelligence Graphics", StringComparison.Ordinal);

        LlmFitSystemAssessment assessment = LlmFitSystemJsonAssessor.Assess(rawJson);

        Assert.IsTrue(assessment.JsonValid);
        Assert.IsFalse(assessment.IntelGpuReported);
        CollectionAssert.DoesNotContain(assessment.DiagnosticCodes.ToArray(), "HI-LLMFIT-WINDOWS-INTEL-MEMORY-SEMANTICS-GAP");
    }

    [TestMethod]
    public void Assess_IntelTokenBoundariesUseUnicodeScalars()
    {
        LlmFitSystemAssessment supplementaryBefore = AssessWithGpuName("\U00010400Intel Graphics");
        LlmFitSystemAssessment supplementaryAfter = AssessWithGpuName("Intel\U00010400 Graphics");
        LlmFitSystemAssessment underscore = AssessWithGpuName("Intel_Graphics");
        LlmFitSystemAssessment digit = AssessWithGpuName("Intel2 Graphics");
        LlmFitSystemAssessment invalidSurrogate = AssessWithGpuName("\\uD800Intel Graphics");
        LlmFitSystemAssessment trailingInvalidSurrogate = AssessWithGpuName("Intel Graphics\\uD800");
        LlmFitSystemAssessment punctuation = AssessWithGpuName("(iNtEl), Graphics");

        Assert.IsFalse(supplementaryBefore.IntelGpuReported);
        Assert.IsFalse(supplementaryAfter.IntelGpuReported);
        Assert.IsFalse(underscore.IntelGpuReported);
        Assert.IsFalse(digit.IntelGpuReported);
        Assert.IsFalse(invalidSurrogate.IntelGpuReported);
        Assert.IsFalse(trailingInvalidSurrogate.IntelGpuReported);
        Assert.IsTrue(punctuation.IntelGpuReported);
    }

    [TestMethod]
    public void Assessment_DefensivelyCopiesDiagnosticsDuringConstructionAndWithAssignment()
    {
        List<string> source = ["original"];
        var assessment = new LlmFitSystemAssessment(
            true, true, true, "CPU", 8, 32, 16, false, 0, false, false,
            "DetectionUnavailable", "hash", source);
        source[0] = "mutated";

        LlmFitSystemAssessment copied = assessment with { DiagnosticCodes = source };
        source[0] = "mutated-again";

        CollectionAssert.AreEqual(OriginalDiagnostic, assessment.DiagnosticCodes.ToArray());
        CollectionAssert.AreEqual(MutatedDiagnostic, copied.DiagnosticCodes.ToArray());
        Assert.Throws<ArgumentNullException>(() => assessment with { DiagnosticCodes = null! });
    }

    private static string ReadFixture(string fileName)
    {
        return File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", fileName));
    }

    private static LlmFitSystemAssessment AssessWithGpuName(string gpuName)
    {
        string rawJson = ReadFixture("valid-windows-intel.json")
            .Replace("Fixture Intel Arc Graphics", gpuName, StringComparison.Ordinal);
        return LlmFitSystemJsonAssessor.Assess(rawJson);
    }

    private static void AssertPrivacySafeJsonFailure(LlmFitSystemAssessment assessment)
    {
        Assert.IsFalse(assessment.JsonValid);
        Assert.IsFalse(assessment.RequiredCpuRamPresent);
        Assert.IsFalse(assessment.Gate1SchemaPassed);
        Assert.IsFalse(assessment.CpuNamePresent);
        Assert.IsNull(assessment.CpuName);
        Assert.IsNull(assessment.CpuLogicalProcessorCount);
        Assert.IsNull(assessment.TotalRamGiB);
        Assert.IsNull(assessment.AvailableRamGiB);
        Assert.IsFalse(assessment.GpuReported);
        Assert.AreEqual(0, assessment.ReportedGpuCount);
        Assert.IsFalse(assessment.IntelGpuReported);
        Assert.IsFalse(assessment.DedicatedSharedMemorySemanticsEstablished);
        Assert.AreEqual("DetectionUnavailable", assessment.IntelNpuDetectionState);
    }
}
#pragma warning restore CA1707
