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

    [TestMethod]
    public void MalformedAndStructurallyInvalidJsonFailsClosed()
    {
        string[] invalidJson =
        [
            string.Empty,
            "{",
            "{/*comment*/\"system\":{}}",
            "{\"system\":{},}",
            "[]",
            "{}",
            "{\"system\":null}",
            "{\"system\":{},\"system\":{}}",
            "{\"system\":{\"cpu_name\":\"one\",\"cpu_name\":\"two\"}}",
            "{\"system\":{\"total_ram_gb\":32,\"available_ram_gb\":16,\"cpu_cores\":8,\"cpu_name\":\"CPU\",\"has_gpu\":true,\"gpu_count\":1,\"gpus\":[{\"name\":\"GPU\",\"name\":\"GPU 2\",\"count\":1}]}}",
            CreateDeepJson(17),
        ];

        foreach (string json in invalidJson)
        {
            LlmFitSystemParseResult result = LlmFitSystemJsonParser.Parse(json);

            Assert.AreEqual(LlmFitEvidenceState.Invalid, result.State, json);
            CollectionAssert.AreEqual(
                new[] { LlmFitDiagnosticCode.JsonInvalid },
                result.Diagnostics.ToArray(),
                json);
            Assert.IsNull(result.CpuName, json);
            Assert.HasCount(0, result.Gpus, json);
            AssertCanonicalHash(json, result.RawOutputSha256);
        }
    }

    [TestMethod]
    public void RequiredNamesAreCaseSensitiveAndMissingIsDistinctFromInvalid()
    {
        string caseChanged = ValidSystemJson(cpuNameProperty: "CPU_NAME");
        string invalid = ValidSystemJson(totalRam: "0");

        string[] missing =
        [
            ValidSystemJson(totalRam: null),
            ValidSystemJson(availableRam: null),
            ValidSystemJson(cores: null),
            ValidSystemJson(cpuName: null),
        ];

        AssertDiagnostics(
            LlmFitSystemJsonParser.Parse(caseChanged),
            LlmFitDiagnosticCode.RequiredCpuRamMissing);
        foreach (string json in missing)
        {
            AssertDiagnostics(
                LlmFitSystemJsonParser.Parse(json),
                LlmFitDiagnosticCode.RequiredCpuRamMissing);
        }

        AssertDiagnostics(
            LlmFitSystemJsonParser.Parse(invalid),
            LlmFitDiagnosticCode.RequiredCpuRamInvalid);
    }

    [TestMethod]
    public void CpuAndRamBoundariesFailClosedAndRetainOnlyValidFacts()
    {
        (string Json, string? Cpu, int? Cores, double? Total, double? Available)[] cases =
        [
            (ValidSystemJson(totalRam: "\"32\""), "Fixture CPU", 8, null, 16),
            (ValidSystemJson(availableRam: "true"), "Fixture CPU", 8, 32, null),
            (ValidSystemJson(cores: "8.5"), "Fixture CPU", null, 32, 16),
            (ValidSystemJson(cores: "0"), "Fixture CPU", null, 32, 16),
            (ValidSystemJson(cores: "4097"), "Fixture CPU", null, 32, 16),
            (ValidSystemJson(totalRam: "16385"), "Fixture CPU", 8, null, 16),
            (ValidSystemJson(availableRam: "-1"), "Fixture CPU", 8, 32, null),
            (ValidSystemJson(availableRam: "33"), "Fixture CPU", 8, 32, null),
            (ValidSystemJson(totalRam: "1e999"), "Fixture CPU", 8, null, 16),
            (ValidSystemJson(cpuName: "\" leading\""), null, 8, 32, 16),
        ];

        foreach ((string json, string? cpu, int? cores, double? total, double? available) in cases)
        {
            LlmFitSystemParseResult result = LlmFitSystemJsonParser.Parse(json);

            AssertDiagnostics(result, LlmFitDiagnosticCode.RequiredCpuRamInvalid);
            Assert.AreEqual(cpu, result.CpuName, json);
            Assert.AreEqual(cores, result.CpuLogicalProcessorCount, json);
            Assert.AreEqual(total, result.TotalRamGiB, json);
            Assert.AreEqual(available, result.AvailableRamGiB, json);
        }
    }

    [TestMethod]
    public void GpuShapeAndConsistencyFailuresAreDistinctAndRetainNoGpuFacts()
    {
        string[] missingShape =
        [
            ValidSystemJson(hasGpu: null),
            ValidSystemJson(gpuCount: null),
            ValidSystemJson(gpus: null),
        ];
        string[] inconsistent =
        [
            ValidSystemJson(hasGpu: "\"yes\""),
            ValidSystemJson(gpuCount: "\"1\""),
            ValidSystemJson(gpus: "{}"),
            ValidSystemJson(gpuCount: "-1"),
            ValidSystemJson(gpuCount: "65"),
            ValidSystemJson(hasGpu: "false", gpuCount: "1"),
            ValidSystemJson(hasGpu: "true", gpuCount: "0"),
            ValidSystemJson(hasGpu: "true", gpuCount: "1", gpus: "[]"),
            ValidSystemJson(hasGpu: "false", gpuCount: "0", gpus: "[{\"name\":\"GPU\",\"count\":1}]"),
            ValidSystemJson(gpus: "[{\"name\":\"GPU\",\"count\":0}]"),
            ValidSystemJson(gpus: "[42]"),
            ValidSystemJson(gpus: "[{\"count\":1}]"),
            ValidSystemJson(gpus: "[{\"name\":\"GPU\"}]"),
            ValidSystemJson(gpuCount: "2", gpus: "[{\"name\":\"GPU\",\"count\":1}]"),
            ValidSystemJson(gpuCount: "65", gpus: "[{\"name\":\"GPU\",\"count\":64},{\"name\":\"GPU 2\",\"count\":1}]"),
            ValidSystemJson(gpuCount: "2", gpus: "[{\"name\":\"GPU\",\"count\":1},{\"name\":\"gpu\",\"count\":1}]"),
            ValidSystemJson(gpus: "[{\"name\":\" unsafe\",\"count\":1}]"),
            ValidSystemJson(gpuName: "42"),
            ValidSystemJson(hasGpu: "false", gpuCount: "0", gpuName: "\"Contradictory GPU\"", gpus: "[]"),
        ];

        foreach (string json in missingShape)
        {
            LlmFitSystemParseResult result = LlmFitSystemJsonParser.Parse(json);
            AssertDiagnostics(result, LlmFitDiagnosticCode.GpuShapeMissing);
            Assert.AreEqual(LlmFitGpuDetectionState.Invalid, result.GpuState);
            Assert.HasCount(0, result.Gpus);
        }

        foreach (string json in inconsistent)
        {
            LlmFitSystemParseResult result = LlmFitSystemJsonParser.Parse(json);
            AssertDiagnostics(result, LlmFitDiagnosticCode.GpuInconsistent);
            Assert.AreEqual(LlmFitGpuDetectionState.Invalid, result.GpuState);
            Assert.AreEqual(0, result.ReportedGpuCount);
            Assert.HasCount(0, result.Gpus);
        }
    }

    [TestMethod]
    public void DiagnosticPrecedenceIsDeterministicUniqueAndOrdinal()
    {
        string json = ValidSystemJson(totalRam: null, availableRam: "-1", hasGpu: null, gpuCount: "65");

        LlmFitSystemParseResult result = LlmFitSystemJsonParser.Parse(json);

        CollectionAssert.AreEqual(
            new[]
            {
                LlmFitDiagnosticCode.RequiredCpuRamMissing,
                LlmFitDiagnosticCode.RequiredCpuRamInvalid,
                LlmFitDiagnosticCode.GpuShapeMissing,
                LlmFitDiagnosticCode.GpuInconsistent,
            },
            result.Diagnostics.ToArray());
    }

    [TestMethod]
    public void BoundedMalformedPrimitiveFuzzNeverThrowsOrExpandsResults()
    {
        const string alphabet = "{}[],:\\\"nulltruefalse0123456789xyz ";
        Random random = new(0x4c4c4d46);

        for (int sample = 0; sample < 256; sample++)
        {
            int length = random.Next(0, 96);
            StringBuilder builder = new(length);
            for (int index = 0; index < length; index++)
            {
                builder.Append(alphabet[random.Next(alphabet.Length)]);
            }

            string json = builder.ToString();
            LlmFitSystemParseResult result = LlmFitSystemJsonParser.Parse(json);

            Assert.AreEqual(LlmFitEvidenceState.Invalid, result.State);
            Assert.IsLessThanOrEqualTo(5, result.Diagnostics.Count);
            Assert.IsLessThanOrEqualTo(64, result.Gpus.Count);
            AssertCanonicalHash(json, result.RawOutputSha256);
        }
    }

    [TestMethod]
    public void NullInputRemainsAProgrammingError()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => LlmFitSystemJsonParser.Parse(null!));
    }

    private static string ReadFixture(string name) =>
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "LlmFit", name));

    private static string ComputeSha256(string value) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();

    private static void AssertDiagnostics(
        LlmFitSystemParseResult result,
        params LlmFitDiagnosticCode[] expected)
    {
        Assert.AreEqual(LlmFitEvidenceState.Invalid, result.State);
        CollectionAssert.AreEqual(expected, result.Diagnostics.ToArray());
    }

    private static void AssertCanonicalHash(string json, string actual)
    {
        Assert.AreEqual(64, actual.Length);
        Assert.AreEqual(ComputeSha256(json), actual);
        Assert.IsTrue(actual.All(static character =>
            character is (>= '0' and <= '9') or (>= 'a' and <= 'f')));
    }

    private static string CreateDeepJson(int depth)
    {
        string nested = "0";
        for (int index = 0; index < depth; index++)
        {
            nested = $"{{\"level\":{nested}}}";
        }

        return $"{{\"system\":{{\"future\":{nested}}}}}";
    }

    private static string ValidSystemJson(
        string? totalRam = "32",
        string? availableRam = "16",
        string? cores = "8",
        string? cpuName = "\"Fixture CPU\"",
        string cpuNameProperty = "cpu_name",
        string? hasGpu = "true",
        string? gpuCount = "1",
        string? gpuName = "\"Fixture GPU\"",
        string? gpus = "[{\"name\":\"Fixture GPU\",\"count\":1}]")
    {
        List<string> properties = [];
        Add(properties, "total_ram_gb", totalRam);
        Add(properties, "available_ram_gb", availableRam);
        Add(properties, "cpu_cores", cores);
        Add(properties, cpuNameProperty, cpuName);
        Add(properties, "has_gpu", hasGpu);
        Add(properties, "gpu_name", gpuName);
        Add(properties, "gpu_count", gpuCount);
        Add(properties, "gpus", gpus);
        return $"{{\"system\":{{{string.Join(',', properties)}}}}}";
    }

    private static void Add(List<string> properties, string name, string? jsonValue)
    {
        if (jsonValue is not null)
        {
            properties.Add($"\"{name}\":{jsonValue}");
        }
    }
}
