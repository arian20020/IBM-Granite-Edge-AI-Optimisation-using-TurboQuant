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

        Assert.IsFalse(assessment.JsonValid);
        Assert.IsFalse(assessment.RequiredCpuRamPresent);
        Assert.IsFalse(assessment.Gate1SchemaPassed);
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

        Assert.IsFalse(assessment.JsonValid);
        Assert.IsFalse(assessment.RequiredCpuRamPresent);
        CollectionAssert.Contains(assessment.DiagnosticCodes.ToArray(), "HI-LLMFIT-JSON-INVALID");
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
        CollectionAssert.Contains(assessment.DiagnosticCodes.ToArray(), "HI-LLMFIT-CPU-RAM-MISSING");
    }

    [TestMethod]
    [DataRow("\"total_ram_gb\": 31.72", "\"total_ram_gb\": 0")]
    [DataRow("\"total_ram_gb\": 31.72", "\"total_ram_gb\": 1e9999")]
    [DataRow("\"cpu_cores\": 8", "\"cpu_cores\": 1.5")]
    [DataRow("\"available_ram_gb\": 12.50", "\"available_ram_gb\": -1")]
    [DataRow("\"available_ram_gb\": 12.50", "\"available_ram_gb\": 99")]
    public void Assess_InvalidRequiredCpuOrRamValue_ReportsCpuRamMissing(string original, string replacement)
    {
        string rawJson = ReadFixture("valid-cpu-only.json").Replace(original, replacement, StringComparison.Ordinal);

        LlmFitSystemAssessment assessment = LlmFitSystemJsonAssessor.Assess(rawJson);

        Assert.IsTrue(assessment.JsonValid);
        Assert.IsFalse(assessment.RequiredCpuRamPresent);
        CollectionAssert.Contains(assessment.DiagnosticCodes.ToArray(), "HI-LLMFIT-CPU-RAM-MISSING");
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
}
#pragma warning restore CA1707
