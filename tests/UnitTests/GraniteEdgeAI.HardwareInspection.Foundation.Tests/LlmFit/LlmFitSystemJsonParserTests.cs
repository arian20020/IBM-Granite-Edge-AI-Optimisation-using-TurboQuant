using System.Security.Cryptography;
using System.Text;
using GraniteEdgeAI.HardwareInspection.Foundation.LlmFit;

namespace GraniteEdgeAI.HardwareInspection.Foundation.Tests.LlmFit;

[TestClass]
public sealed class LlmFitSystemJsonParserTests
{
    [TestMethod]
    public void AcceptedWindowsIntelFixtureMapsExactFacts()
    {
        string json = ReadFixture("valid-windows-intel.json");

        LlmFitSystemParseResult result = LlmFitSystemJsonParser.Parse(json);

        Assert.AreEqual(LlmFitEvidenceState.Available, result.State);
        Assert.AreEqual("Fixture Intel CPU", result.CpuName);
        Assert.AreEqual(16, result.CpuLogicalProcessorCount);
        Assert.AreEqual(31.72, result.TotalRamGiB);
        Assert.AreEqual(18.40, result.AvailableRamGiB);
        Assert.AreEqual(LlmFitGpuDetectionState.Reported, result.GpuState);
        Assert.AreEqual(1, result.ReportedGpuCount);
        Assert.HasCount(1, result.Gpus);
        Assert.AreEqual("Fixture Intel Arc Graphics", result.Gpus[0].Name);
        Assert.AreEqual(1, result.Gpus[0].Count);
        Assert.AreEqual(ComputeSha256(json), result.RawOutputSha256);
        Assert.HasCount(0, result.Diagnostics);
    }

    [TestMethod]
    public void CpuOnlyFixtureMapsNotReportedGpuState()
    {
        string json = ReadFixture("valid-cpu-only.json");

        LlmFitSystemParseResult result = LlmFitSystemJsonParser.Parse(json);

        Assert.AreEqual(LlmFitEvidenceState.Available, result.State);
        Assert.AreEqual("Fixture CPU Only", result.CpuName);
        Assert.AreEqual(8, result.CpuLogicalProcessorCount);
        Assert.AreEqual(31.72, result.TotalRamGiB);
        Assert.AreEqual(12.50, result.AvailableRamGiB);
        Assert.AreEqual(LlmFitGpuDetectionState.NotReported, result.GpuState);
        Assert.AreEqual(0, result.ReportedGpuCount);
        Assert.HasCount(0, result.Gpus);
        Assert.HasCount(0, result.Diagnostics);
    }

    [TestMethod]
    public void UnknownAdditivePropertiesDoNotChangeMappedFacts()
    {
        const string json = """
            {
              "schema_revision": 200,
              "system": {
                "total_ram_gb": 31.72,
                "available_ram_gb": 18.40,
                "cpu_cores": 16,
                "cpu_name": "Fixture Intel CPU",
                "has_gpu": true,
                "gpu_name": "Fixture Intel Arc Graphics",
                "gpu_count": 1,
                "gpus": [
                  {
                    "name": "Fixture Intel Arc Graphics",
                    "count": 1,
                    "future_backend_detail": { "kind": "ignored" }
                  }
                ],
                "future_system_field": [1, 2, 3]
              }
            }
            """;

        LlmFitSystemParseResult result = LlmFitSystemJsonParser.Parse(json);

        Assert.AreEqual(LlmFitEvidenceState.Available, result.State);
        Assert.AreEqual("Fixture Intel CPU", result.CpuName);
        Assert.AreEqual(16, result.CpuLogicalProcessorCount);
        Assert.AreEqual(31.72, result.TotalRamGiB);
        Assert.AreEqual(18.40, result.AvailableRamGiB);
        Assert.AreEqual(LlmFitGpuDetectionState.Reported, result.GpuState);
        Assert.AreEqual(1, result.ReportedGpuCount);
        Assert.HasCount(0, result.Diagnostics);
        Assert.AreEqual(ComputeSha256(json), result.RawOutputSha256);
    }

    private static string ReadFixture(string name) =>
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "LlmFit", name));

    private static string ComputeSha256(string value) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();
}
