using GraniteEdgeAI.HardwareInspection.Foundation.LlmFit;
using GraniteEdgeAI.HardwareInspection.Foundation.TrustedTools;

namespace GraniteEdgeAI.HardwareInspection.Foundation.Tests.LlmFit;

[TestClass]
public sealed class LlmFitContractTests
{
    private static readonly string[] ExpectedVersionArguments = ["--version"];
    private static readonly string[] ExpectedSystemArguments = ["--no-dashboard", "--json", "system"];

    private static readonly DateTimeOffset CapturedAtUtc =
        new(2026, 8, 22, 21, 0, 0, TimeSpan.Zero);

    [TestMethod]
    public void CommandContractUsesOnlyPinnedReadOnlyInvocations()
    {
        TrustedToolCommand version = LlmFitCommandContract.CreateVersionCommand();
        TrustedToolCommand system = LlmFitCommandContract.CreateSystemCommand();

        Assert.AreEqual("llmfit", ReadCommandConstant(nameof(LlmFitCommandContract.ToolId)));
        Assert.AreEqual("1.1.9", ReadCommandConstant(nameof(LlmFitCommandContract.Version)));
        Assert.AreEqual(
            "llmfit 1.1.9",
            ReadCommandConstant(nameof(LlmFitCommandContract.ExpectedVersionOutput)));
        Assert.AreEqual("version", version.Identity);
        Assert.AreEqual("system", system.Identity);
        CollectionAssert.AreEqual(ExpectedVersionArguments, version.Arguments.ToArray());
        CollectionAssert.AreEqual(
            ExpectedSystemArguments,
            system.Arguments.ToArray());
        Assert.AreNotSame(
            LlmFitCommandContract.CreateVersionCommand().Arguments,
            LlmFitCommandContract.CreateVersionCommand().Arguments);
    }

    [TestMethod]
    public void AvailableEvidenceCopiesFactsAndCollections()
    {
        List<LlmFitReportedGpu> gpus = [new("Fixture Intel Arc Graphics", 1)];

        LlmFitHardwareEvidence evidence = LlmFitHardwareEvidence.Available(
            "llmfit",
            "1.1.9",
            CapturedAtUtc,
            "Fixture Intel CPU",
            16,
            31.72,
            18.40,
            LlmFitGpuDetectionState.Reported,
            gpus,
            new string('a', 64));
        gpus.Clear();

        Assert.AreEqual(LlmFitEvidenceState.Available, evidence.State);
        Assert.AreEqual("Fixture Intel CPU", evidence.CpuName);
        Assert.AreEqual(16, evidence.CpuLogicalProcessorCount);
        Assert.AreEqual(31.72, evidence.TotalRamGiB);
        Assert.AreEqual(18.40, evidence.AvailableRamGiB);
        Assert.AreEqual(1, evidence.ReportedGpuCount);
        Assert.HasCount(1, evidence.Gpus);
        Assert.HasCount(0, evidence.Diagnostics);
    }

    [TestMethod]
    public void InvalidEvidenceRetainsOnlyExplicitlyValidatedFactsAndSortsDiagnostics()
    {
        List<LlmFitDiagnosticCode> diagnostics =
            [LlmFitDiagnosticCode.GpuInconsistent, LlmFitDiagnosticCode.RequiredCpuRamInvalid];

        LlmFitHardwareEvidence evidence = LlmFitHardwareEvidence.Invalid(
            "llmfit",
            "1.1.9",
            CapturedAtUtc,
            "Fixture Intel CPU",
            16,
            31.72,
            null,
            LlmFitGpuDetectionState.Invalid,
            [],
            new string('b', 64),
            diagnostics);
        diagnostics.Clear();

        Assert.AreEqual(LlmFitEvidenceState.Invalid, evidence.State);
        Assert.IsNull(evidence.AvailableRamGiB);
        CollectionAssert.AreEqual(
            new[]
            {
                LlmFitDiagnosticCode.RequiredCpuRamInvalid,
                LlmFitDiagnosticCode.GpuInconsistent,
            },
            evidence.Diagnostics.ToArray());
    }

    [TestMethod]
    public void UnavailableEvidenceHasNoHardwareFactsOrHash()
    {
        LlmFitHardwareEvidence evidence = LlmFitHardwareEvidence.Unavailable(
            "llmfit",
            "1.1.9",
            CapturedAtUtc,
            LlmFitDiagnosticCode.VersionTimedOut);

        Assert.AreEqual(LlmFitEvidenceState.Unavailable, evidence.State);
        Assert.IsNull(evidence.CpuName);
        Assert.IsNull(evidence.CpuLogicalProcessorCount);
        Assert.IsNull(evidence.TotalRamGiB);
        Assert.IsNull(evidence.AvailableRamGiB);
        Assert.IsNull(evidence.RawOutputSha256);
        Assert.HasCount(0, evidence.Gpus);
        CollectionAssert.AreEqual(
            new[] { LlmFitDiagnosticCode.VersionTimedOut },
            evidence.Diagnostics.ToArray());
    }

    [TestMethod]
    public void EvidenceRejectsNonUtcCaptureTime()
    {
        DateTimeOffset nonUtc = new(2026, 8, 22, 21, 0, 0, TimeSpan.FromHours(1));

        Assert.ThrowsExactly<ArgumentException>(() =>
            LlmFitHardwareEvidence.Unavailable(
                "llmfit",
                "1.1.9",
                nonUtc,
                LlmFitDiagnosticCode.SystemStartFailed));
    }

    [TestMethod]
    [DataRow(0.0, 0.0, 16)]
    [DataRow(16385.0, 1.0, 16)]
    [DataRow(32.0, -1.0, 16)]
    [DataRow(32.0, 33.0, 16)]
    [DataRow(double.NaN, 1.0, 16)]
    [DataRow(32.0, double.PositiveInfinity, 16)]
    [DataRow(32.0, 1.0, 0)]
    [DataRow(32.0, 1.0, 4097)]
    public void AvailableEvidenceRejectsInvalidCpuAndRam(
        double totalRamGiB,
        double availableRamGiB,
        int logicalProcessors)
    {
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() =>
            CreateAvailable(totalRamGiB, availableRamGiB, logicalProcessors));
    }

    [TestMethod]
    public void EvidenceRejectsContradictoryGpuFacts()
    {
        Assert.ThrowsExactly<ArgumentException>(() =>
            LlmFitHardwareEvidence.Available(
                "llmfit",
                "1.1.9",
                CapturedAtUtc,
                "Fixture Intel CPU",
                16,
                32,
                16,
                LlmFitGpuDetectionState.NotReported,
                [new LlmFitReportedGpu("Fixture GPU", 1)],
                new string('c', 64)));

        Assert.ThrowsExactly<ArgumentException>(() =>
            LlmFitHardwareEvidence.Available(
                "llmfit",
                "1.1.9",
                CapturedAtUtc,
                "Fixture Intel CPU",
                16,
                32,
                16,
                LlmFitGpuDetectionState.Reported,
                [new LlmFitReportedGpu("Fixture GPU", 33), new LlmFitReportedGpu("fixture gpu", 32)],
                new string('c', 64)));
    }

    [TestMethod]
    [DataRow("")]
    [DataRow(" leading")]
    [DataRow("trailing ")]
    [DataRow("control\u0001")]
    [DataRow("format\u200Econtrol")]
    [DataRow("bidi\u202Econtrol")]
    [DataRow("line\u2028separator")]
    [DataRow("paragraph\u2029separator")]
    public void ReportedGpuRejectsUnsafeNames(string name)
    {
        Assert.ThrowsExactly<ArgumentException>(() => new LlmFitReportedGpu(name, 1));
    }

    [TestMethod]
    public void ReportedGpuRejectsUnpairedSurrogate()
    {
        string name = string.Concat("broken", new string('\ud800', 1));

        Assert.ThrowsExactly<ArgumentException>(() => new LlmFitReportedGpu(name, 1));
    }

    [TestMethod]
    public void ReportedGpuCountsUnicodeScalarsAndRejectsMoreThanMaximum()
    {
        string supplementaryCharacter = char.ConvertFromUtf32(0x1F5A5);

        _ = new LlmFitReportedGpu(string.Concat(Enumerable.Repeat(supplementaryCharacter, 256)), 1);
        Assert.ThrowsExactly<ArgumentException>(() =>
            new LlmFitReportedGpu(string.Concat(Enumerable.Repeat(supplementaryCharacter, 257)), 1));
    }

    [TestMethod]
    public void HardwareNamesAcceptSafeUnicodeWithoutNormalization()
    {
        const string name = "Cafe\u0301 \uE000 Processor";

        LlmFitHardwareEvidence evidence = LlmFitHardwareEvidence.Available(
            "llmfit",
            "1.1.9",
            CapturedAtUtc,
            name,
            16,
            32,
            16,
            LlmFitGpuDetectionState.NotReported,
            [],
            new string('a', 64));

        Assert.AreEqual(name, evidence.CpuName);
    }

    [TestMethod]
    public void EvidenceRejectsNonCanonicalHashAndInvalidDiagnosticState()
    {
        Assert.ThrowsExactly<ArgumentException>(() => CreateAvailable(32, 16, 16, "ABCDEF"));
        Assert.ThrowsExactly<ArgumentException>(() =>
            LlmFitHardwareEvidence.Invalid(
                "llmfit",
                "1.1.9",
                CapturedAtUtc,
                null,
                null,
                null,
                null,
                LlmFitGpuDetectionState.Invalid,
                [],
                new string('a', 64),
                []));
        Assert.ThrowsExactly<ArgumentException>(() =>
            LlmFitHardwareEvidence.Unavailable(
                "llmfit",
                "1.1.9",
                CapturedAtUtc,
                LlmFitDiagnosticCode.JsonInvalid));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() =>
            LlmFitHardwareEvidence.Unavailable(
                "llmfit",
                "1.1.9",
                CapturedAtUtc,
                (LlmFitDiagnosticCode)999));
    }

    [TestMethod]
    public void EvidenceStopsEnumeratingCollectionsAtTheirClosedLimit()
    {
        int gpuEnumerations = 0;
        IEnumerable<LlmFitReportedGpu> excessiveGpus = Counted(
            new LlmFitReportedGpu("Fixture GPU", 1),
            1000,
            () => gpuEnumerations++);

        Assert.ThrowsExactly<ArgumentException>(() =>
            LlmFitHardwareEvidence.Available(
                "llmfit",
                "1.1.9",
                CapturedAtUtc,
                "Fixture CPU",
                8,
                32,
                16,
                LlmFitGpuDetectionState.Reported,
                excessiveGpus,
                new string('a', 64)));
        Assert.IsLessThanOrEqualTo(65, gpuEnumerations);

        int diagnosticEnumerations = 0;
        IEnumerable<LlmFitDiagnosticCode> excessiveDiagnostics = Counted(
            LlmFitDiagnosticCode.JsonInvalid,
            1000,
            () => diagnosticEnumerations++);
        Assert.ThrowsExactly<ArgumentException>(() =>
            LlmFitHardwareEvidence.Invalid(
                "llmfit",
                "1.1.9",
                CapturedAtUtc,
                null,
                null,
                null,
                null,
                LlmFitGpuDetectionState.Invalid,
                [],
                new string('a', 64),
                excessiveDiagnostics));
        Assert.IsLessThanOrEqualTo(Enum.GetValues<LlmFitDiagnosticCode>().Length + 1, diagnosticEnumerations);
    }

    private static LlmFitHardwareEvidence CreateAvailable(
        double totalRamGiB,
        double availableRamGiB,
        int logicalProcessors,
        string? hash = null) =>
        LlmFitHardwareEvidence.Available(
            "llmfit",
            "1.1.9",
            CapturedAtUtc,
            "Fixture Intel CPU",
            logicalProcessors,
            totalRamGiB,
            availableRamGiB,
            LlmFitGpuDetectionState.NotReported,
            [],
            hash ?? new string('a', 64));

    private static string ReadCommandConstant(string name) =>
        (string)(typeof(LlmFitCommandContract).GetField(name)?.GetRawConstantValue() ??
            throw new InvalidOperationException($"Missing command constant {name}."));

    private static IEnumerable<T> Counted<T>(T value, int count, Action onEnumeration)
    {
        for (int index = 0; index < count; index++)
        {
            onEnumeration();
            yield return value;
        }
    }
}
