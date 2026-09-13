using GraniteEdgeAI.HardwareInspection.Foundation.Windows;

namespace GraniteEdgeAI.HardwareInspection.Foundation.Tests.Windows;

[TestClass]
public sealed class WindowsProcessorEvidenceProviderTests
{
    private static readonly DateTimeOffset CapturedAtUtc =
        new(2026, 8, 23, 9, 0, 0, TimeSpan.Zero);

    [TestMethod]
    public void AvailableEvidenceRetainsOnlyValidatedFacts()
    {
        WindowsProcessorEvidence evidence = WindowsProcessorEvidence.Available(
            "Fixture Cafe\u0301 Processor",
            WindowsProcessorArchitecture.X64,
            8,
            16,
            CapturedAtUtc);

        Assert.AreEqual(WindowsProcessorEvidenceState.Available, evidence.State);
        Assert.AreEqual("Fixture Cafe\u0301 Processor", evidence.Name);
        Assert.AreEqual(WindowsProcessorArchitecture.X64, evidence.Architecture);
        Assert.AreEqual(8, evidence.PhysicalCoreCount);
        Assert.AreEqual(16, evidence.LogicalProcessorCount);
        Assert.AreEqual(CapturedAtUtc, evidence.CapturedAtUtc);
        Assert.HasCount(0, evidence.Diagnostics);
    }

    [TestMethod]
    [DataRow(" leading", 8, 16)]
    [DataRow("trailing ", 8, 16)]
    [DataRow("format\u202Ename", 8, 16)]
    [DataRow("Fixture Processor", 0, 16)]
    [DataRow("Fixture Processor", 4097, 4097)]
    [DataRow("Fixture Processor", 17, 16)]
    [DataRow("Fixture Processor", 8, 0)]
    [DataRow("Fixture Processor", 8, 4097)]
    public void AvailableEvidenceRejectsUnsafeOrImpossibleFacts(
        string name,
        int physicalCores,
        int logicalProcessors)
    {
        Assert.Throws<ArgumentException>(() => WindowsProcessorEvidence.Available(
            name,
            WindowsProcessorArchitecture.X64,
            physicalCores,
            logicalProcessors,
            CapturedAtUtc));
    }

    [TestMethod]
    public void EvidenceRejectsUndefinedEnumsAndNonUtcTime()
    {
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => WindowsProcessorEvidence.Available(
            "Fixture Processor",
            (WindowsProcessorArchitecture)999,
            8,
            16,
            CapturedAtUtc));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => WindowsProcessorEvidence.Unavailable(
            (WindowsProcessorDiagnosticCode)999,
            CapturedAtUtc));
        Assert.ThrowsExactly<ArgumentException>(() => WindowsProcessorEvidence.Unavailable(
            WindowsProcessorDiagnosticCode.NativeApiUnavailable,
            CapturedAtUtc.ToOffset(TimeSpan.FromHours(1))));
    }

    [TestMethod]
    public void UnavailableEvidenceHasNoProcessorFactsAndExactlyOneDiagnostic()
    {
        WindowsProcessorEvidence evidence = WindowsProcessorEvidence.Unavailable(
            WindowsProcessorDiagnosticCode.TopologyUnavailable,
            CapturedAtUtc);

        Assert.AreEqual(WindowsProcessorEvidenceState.Unavailable, evidence.State);
        Assert.IsNull(evidence.Name);
        Assert.IsNull(evidence.Architecture);
        Assert.IsNull(evidence.PhysicalCoreCount);
        Assert.IsNull(evidence.LogicalProcessorCount);
        CollectionAssert.AreEqual(
            new[] { WindowsProcessorDiagnosticCode.TopologyUnavailable },
            evidence.Diagnostics.ToArray());
    }

    [TestMethod]
    public async Task ProviderMapsSuccessfulX64ResultAndConvertsTimeToUtc()
    {
        FakeWindowsProcessorApi api = new(new(
            WindowsProcessorApiStatus.Success,
            "Fixture Processor",
            WindowsProcessorApiArchitecture.X64,
            8,
            16));
        WindowsProcessorEvidenceProvider provider = new(
            api,
            new FixedTimeProvider(CapturedAtUtc.ToOffset(TimeSpan.FromHours(1))));

        WindowsProcessorEvidence evidence = await provider.CaptureAsync(CancellationToken.None);

        Assert.AreEqual(WindowsProcessorEvidenceState.Available, evidence.State);
        Assert.AreEqual(WindowsProcessorArchitecture.X64, evidence.Architecture);
        Assert.AreEqual(CapturedAtUtc, evidence.CapturedAtUtc);
        Assert.AreEqual(1, api.Calls);
    }

    [TestMethod]
    public async Task ProviderHonorsCancellationBeforeApiAccess()
    {
        FakeWindowsProcessorApi api = new(new(
            WindowsProcessorApiStatus.NativeApiUnavailable,
            null,
            WindowsProcessorApiArchitecture.Unsupported,
            0,
            0));
        WindowsProcessorEvidenceProvider provider = new(api, new FixedTimeProvider(CapturedAtUtc));
        using CancellationTokenSource cancellation = new();
        cancellation.Cancel();

        OperationCanceledException error = await Assert.ThrowsExactlyAsync<OperationCanceledException>(
            async () => await provider.CaptureAsync(cancellation.Token));

        Assert.AreEqual(cancellation.Token, error.CancellationToken);
        Assert.AreEqual(0, api.Calls);
    }

    [TestMethod]
    [DataRow((int)WindowsProcessorApiStatus.NameUnavailable, WindowsProcessorDiagnosticCode.NameUnavailable)]
    [DataRow((int)WindowsProcessorApiStatus.TopologyUnavailable, WindowsProcessorDiagnosticCode.TopologyUnavailable)]
    [DataRow((int)WindowsProcessorApiStatus.InvalidTopology, WindowsProcessorDiagnosticCode.TopologyInconsistent)]
    [DataRow((int)WindowsProcessorApiStatus.UnsupportedArchitecture, WindowsProcessorDiagnosticCode.UnsupportedArchitecture)]
    [DataRow((int)WindowsProcessorApiStatus.NativeApiUnavailable, WindowsProcessorDiagnosticCode.NativeApiUnavailable)]
    public async Task ProviderMapsClosedApiFailures(
        int statusValue,
        WindowsProcessorDiagnosticCode expectedDiagnostic)
    {
        FakeWindowsProcessorApi api = new(new(
            (WindowsProcessorApiStatus)statusValue,
            "must not escape",
            WindowsProcessorApiArchitecture.X64,
            8,
            16));

        WindowsProcessorEvidence evidence = await new WindowsProcessorEvidenceProvider(
            api,
            new FixedTimeProvider(CapturedAtUtc)).CaptureAsync(CancellationToken.None);

        Assert.AreEqual(WindowsProcessorEvidenceState.Unavailable, evidence.State);
        CollectionAssert.AreEqual(new[] { expectedDiagnostic }, evidence.Diagnostics.ToArray());
        Assert.IsNull(evidence.Name);
        Assert.AreEqual(1, api.Calls);
    }

    [TestMethod]
    [DataRow(null, (int)WindowsProcessorApiArchitecture.X64, 8, 16, WindowsProcessorDiagnosticCode.NameUnavailable)]
    [DataRow("Fixture Processor", (int)WindowsProcessorApiArchitecture.Unsupported, 8, 16, WindowsProcessorDiagnosticCode.UnsupportedArchitecture)]
    [DataRow("Fixture Processor", (int)WindowsProcessorApiArchitecture.X64, 0, 16, WindowsProcessorDiagnosticCode.TopologyInconsistent)]
    [DataRow("Fixture Processor", (int)WindowsProcessorApiArchitecture.X64, 17, 16, WindowsProcessorDiagnosticCode.TopologyInconsistent)]
    public async Task ProviderRejectsInvalidSuccessfulResults(
        string? name,
        int architectureValue,
        int physicalCores,
        int logicalProcessors,
        WindowsProcessorDiagnosticCode expectedDiagnostic)
    {
        FakeWindowsProcessorApi api = new(new(
            WindowsProcessorApiStatus.Success,
            name,
            (WindowsProcessorApiArchitecture)architectureValue,
            physicalCores,
            logicalProcessors));

        WindowsProcessorEvidence evidence = await new WindowsProcessorEvidenceProvider(
            api,
            new FixedTimeProvider(CapturedAtUtc)).CaptureAsync(CancellationToken.None);

        CollectionAssert.AreEqual(new[] { expectedDiagnostic }, evidence.Diagnostics.ToArray());
        Assert.AreEqual(1, api.Calls);
    }

    private sealed class FakeWindowsProcessorApi(WindowsProcessorApiResult result) : IWindowsProcessorApi
    {
        internal int Calls { get; private set; }

        public WindowsProcessorApiResult Capture()
        {
            Calls++;
            return result;
        }
    }

    private sealed class FixedTimeProvider(DateTimeOffset value) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => value;
    }
}
