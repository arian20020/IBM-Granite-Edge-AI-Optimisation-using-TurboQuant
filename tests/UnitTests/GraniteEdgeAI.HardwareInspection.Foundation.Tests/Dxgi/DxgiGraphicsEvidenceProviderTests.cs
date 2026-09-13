using GraniteEdgeAI.HardwareInspection.Foundation.Dxgi;

namespace GraniteEdgeAI.HardwareInspection.Foundation.Tests.Dxgi;

[TestClass]
public sealed class DxgiGraphicsEvidenceProviderTests
{
    private static readonly int[] ExpectedOrdinals = [0, 1, 2, 3];

    private static readonly DateTimeOffset CapturedAtUtc =
        new(2026, 8, 23, 11, 0, 0, TimeSpan.Zero);

    [TestMethod]
    public void AdapterEvidenceRetainsIndependentMemoryAndIdentityFields()
    {
        DxgiAdapterEvidence adapter = new(
            "Fixture Adapter",
            DxgiAdapterKind.Hardware,
            0x8086,
            0x1234,
            0,
            128,
            4_096,
            0);

        Assert.AreEqual("Fixture Adapter", adapter.Name);
        Assert.AreEqual(DxgiAdapterKind.Hardware, adapter.Kind);
        Assert.AreEqual(0x8086U, adapter.VendorId);
        Assert.AreEqual(0x1234U, adapter.DeviceId);
        Assert.AreEqual(0UL, adapter.DedicatedVideoMemoryBytes);
        Assert.AreEqual(128UL, adapter.DedicatedSystemMemoryBytes);
        Assert.AreEqual(4_096UL, adapter.SharedSystemMemoryBytes);
        Assert.AreEqual(0, adapter.Ordinal);
    }

    [TestMethod]
    public void AdapterEvidenceRejectsUnsafeNameUndefinedKindAndInvalidOrdinal()
    {
        Assert.ThrowsExactly<ArgumentException>(() => CreateAdapter(" unsafe", DxgiAdapterKind.Hardware, 0));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => CreateAdapter("Fixture", (DxgiAdapterKind)999, 0));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => CreateAdapter("Fixture", DxgiAdapterKind.Hardware, -1));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => CreateAdapter("Fixture", DxgiAdapterKind.Hardware, 64));
    }

    [TestMethod]
    public void AvailableEvidenceCopiesAdaptersAndAllowsDuplicateDisplayNames()
    {
        List<DxgiAdapterEvidence> adapters =
        [
            CreateAdapter("Same Display Name", DxgiAdapterKind.Hardware, 0),
            new("Same Display Name", DxgiAdapterKind.Hardware, 0x8086, 0x2222, 1, 2, 3, 1),
        ];

        DxgiGraphicsEvidence evidence = DxgiGraphicsEvidence.Available(adapters, CapturedAtUtc);
        adapters.Clear();

        Assert.AreEqual(DxgiGraphicsEvidenceState.Available, evidence.State);
        Assert.HasCount(2, evidence.Adapters);
        Assert.HasCount(0, evidence.Diagnostics);
    }

    [TestMethod]
    public void AvailableEvidenceSupportsSuccessfulZeroAdapterEnumeration()
    {
        DxgiGraphicsEvidence evidence = DxgiGraphicsEvidence.Available([], CapturedAtUtc);

        Assert.AreEqual(DxgiGraphicsEvidenceState.Available, evidence.State);
        Assert.HasCount(0, evidence.Adapters);
        Assert.HasCount(0, evidence.Diagnostics);
    }

    [TestMethod]
    public void EvidenceRejectsDuplicateStableIdentityAndNonUtcTime()
    {
        DxgiAdapterEvidence duplicate = CreateAdapter("Fixture", DxgiAdapterKind.Hardware, 0);
        Assert.ThrowsExactly<ArgumentException>(() =>
            DxgiGraphicsEvidence.Available([duplicate, duplicate], CapturedAtUtc));
        Assert.ThrowsExactly<ArgumentException>(() =>
            DxgiGraphicsEvidence.Available([], CapturedAtUtc.ToOffset(TimeSpan.FromHours(1))));
    }

    [TestMethod]
    public void EvidenceStopsEnumeratingBeforeRetainingSixtyFifthAdapter()
    {
        int enumerations = 0;
        IEnumerable<DxgiAdapterEvidence> adapters = CountedAdapters(1_000, () => enumerations++);

        Assert.ThrowsExactly<ArgumentException>(() =>
            DxgiGraphicsEvidence.Available(adapters, CapturedAtUtc));
        Assert.IsLessThanOrEqualTo(65, enumerations);
    }

    [TestMethod]
    public void UnavailableEvidenceHasNoAdaptersAndOneClosedDiagnostic()
    {
        DxgiGraphicsEvidence evidence = DxgiGraphicsEvidence.Unavailable(
            DxgiGraphicsDiagnosticCode.FactoryUnavailable,
            CapturedAtUtc);

        Assert.AreEqual(DxgiGraphicsEvidenceState.Unavailable, evidence.State);
        Assert.HasCount(0, evidence.Adapters);
        CollectionAssert.AreEqual(
            new[] { DxgiGraphicsDiagnosticCode.FactoryUnavailable },
            evidence.Diagnostics.ToArray());
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => DxgiGraphicsEvidence.Unavailable(
            (DxgiGraphicsDiagnosticCode)999,
            CapturedAtUtc));
    }

    [TestMethod]
    public async Task ProviderMapsIntegratedDiscreteSoftwareRemoteAndDuplicateNamesExactly()
    {
        DxgiAdapterApiEntry[] entries =
        [
            new("Same Name", DxgiAdapterApiKind.Hardware, 0x8086, 1, 0, 128, 4_096),
            new("Same Name", DxgiAdapterApiKind.Hardware, 0x10de, 2, 8_192, 0, 2_048),
            new("Software", DxgiAdapterApiKind.Software, 0x1414, 3, 0, 0, 1_024),
            new("Remote", DxgiAdapterApiKind.Remote, 0, 4, 0, 0, 512),
        ];
        FakeDxgiAdapterApi api = new(new(DxgiAdapterApiStatus.Success, entries));
        DxgiGraphicsEvidenceProvider provider = new(
            api,
            new FixedTimeProvider(CapturedAtUtc.ToOffset(TimeSpan.FromHours(1))));

        DxgiGraphicsEvidence evidence = await provider.CaptureAsync(CancellationToken.None);

        Assert.AreEqual(DxgiGraphicsEvidenceState.Available, evidence.State);
        Assert.HasCount(4, evidence.Adapters);
        Assert.AreEqual(0UL, evidence.Adapters[0].DedicatedVideoMemoryBytes);
        Assert.AreEqual(128UL, evidence.Adapters[0].DedicatedSystemMemoryBytes);
        Assert.AreEqual(4_096UL, evidence.Adapters[0].SharedSystemMemoryBytes);
        Assert.AreEqual(8_192UL, evidence.Adapters[1].DedicatedVideoMemoryBytes);
        Assert.AreEqual(DxgiAdapterKind.Software, evidence.Adapters[2].Kind);
        Assert.AreEqual(DxgiAdapterKind.Remote, evidence.Adapters[3].Kind);
        CollectionAssert.AreEqual(ExpectedOrdinals, evidence.Adapters.Select(static item => item.Ordinal).ToArray());
        Assert.AreEqual(CapturedAtUtc, evidence.CapturedAtUtc);
        Assert.AreEqual(1, api.Calls);
    }

    [TestMethod]
    public async Task ProviderMapsSuccessfulZeroAdaptersAsAvailable()
    {
        DxgiGraphicsEvidence evidence = await new DxgiGraphicsEvidenceProvider(
            new FakeDxgiAdapterApi(new(DxgiAdapterApiStatus.Success, [])),
            new FixedTimeProvider(CapturedAtUtc)).CaptureAsync(CancellationToken.None);

        Assert.AreEqual(DxgiGraphicsEvidenceState.Available, evidence.State);
        Assert.HasCount(0, evidence.Adapters);
    }

    [TestMethod]
    public async Task ProviderHonorsCancellationBeforeApiAccess()
    {
        FakeDxgiAdapterApi api = new(new(DxgiAdapterApiStatus.FactoryUnavailable, []));
        DxgiGraphicsEvidenceProvider provider = new(api, new FixedTimeProvider(CapturedAtUtc));
        using CancellationTokenSource cancellation = new();
        cancellation.Cancel();

        OperationCanceledException error = await Assert.ThrowsExactlyAsync<OperationCanceledException>(
            async () => await provider.CaptureAsync(cancellation.Token));

        Assert.AreEqual(cancellation.Token, error.CancellationToken);
        Assert.AreEqual(0, api.Calls);
    }

    [TestMethod]
    [DataRow((int)DxgiAdapterApiStatus.FactoryUnavailable, DxgiGraphicsDiagnosticCode.FactoryUnavailable)]
    [DataRow((int)DxgiAdapterApiStatus.EnumerationFailed, DxgiGraphicsDiagnosticCode.EnumerationFailed)]
    [DataRow((int)DxgiAdapterApiStatus.InvalidDescription, DxgiGraphicsDiagnosticCode.InvalidDescription)]
    [DataRow((int)DxgiAdapterApiStatus.AdapterLimitExceeded, DxgiGraphicsDiagnosticCode.AdapterLimitExceeded)]
    [DataRow((int)DxgiAdapterApiStatus.NativeApiUnavailable, DxgiGraphicsDiagnosticCode.NativeApiUnavailable)]
    public async Task ProviderMapsClosedApiFailures(int statusValue, DxgiGraphicsDiagnosticCode expected)
    {
        DxgiGraphicsEvidence evidence = await new DxgiGraphicsEvidenceProvider(
            new FakeDxgiAdapterApi(new((DxgiAdapterApiStatus)statusValue, [ApiEntry("must not escape")])),
            new FixedTimeProvider(CapturedAtUtc)).CaptureAsync(CancellationToken.None);

        Assert.AreEqual(DxgiGraphicsEvidenceState.Unavailable, evidence.State);
        Assert.HasCount(0, evidence.Adapters);
        CollectionAssert.AreEqual(new[] { expected }, evidence.Diagnostics.ToArray());
    }

    [TestMethod]
    public async Task ProviderRejectsInvalidDescriptionUndefinedKindAndSixtyFifthEntry()
    {
        await AssertUnavailable(
            new(DxgiAdapterApiStatus.Success, [ApiEntry("unsafe\u202Ename")]),
            DxgiGraphicsDiagnosticCode.InvalidDescription);
        await AssertUnavailable(
            new(DxgiAdapterApiStatus.Success, [new("Fixture", (DxgiAdapterApiKind)999, 1, 2, 3, 4, 5)]),
            DxgiGraphicsDiagnosticCode.EnumerationFailed);

        int enumerations = 0;
        await AssertUnavailable(
            new(DxgiAdapterApiStatus.Success, CountedApiEntries(1_000, () => enumerations++)),
            DxgiGraphicsDiagnosticCode.AdapterLimitExceeded);
        Assert.IsLessThanOrEqualTo(65, enumerations);
    }

    private static async Task AssertUnavailable(
        DxgiAdapterApiResult result,
        DxgiGraphicsDiagnosticCode expected)
    {
        DxgiGraphicsEvidence evidence = await new DxgiGraphicsEvidenceProvider(
            new FakeDxgiAdapterApi(result),
            new FixedTimeProvider(CapturedAtUtc)).CaptureAsync(CancellationToken.None);

        CollectionAssert.AreEqual(new[] { expected }, evidence.Diagnostics.ToArray());
        Assert.HasCount(0, evidence.Adapters);
    }

    private static DxgiAdapterEvidence CreateAdapter(string name, DxgiAdapterKind kind, int ordinal) =>
        new(name, kind, 0x8086, 0x1234, 1, 2, 3, ordinal);

    private static DxgiAdapterApiEntry ApiEntry(string name) =>
        new(name, DxgiAdapterApiKind.Hardware, 0x8086, 0x1234, 1, 2, 3);

    private static IEnumerable<DxgiAdapterEvidence> CountedAdapters(int count, Action onEnumeration)
    {
        for (int index = 0; index < count; index++)
        {
            onEnumeration();
            yield return CreateAdapter($"Fixture {index}", DxgiAdapterKind.Hardware, index % 64);
        }
    }

    private static IEnumerable<DxgiAdapterApiEntry> CountedApiEntries(int count, Action onEnumeration)
    {
        for (int index = 0; index < count; index++)
        {
            onEnumeration();
            yield return ApiEntry($"Fixture {index}");
        }
    }

    private sealed class FakeDxgiAdapterApi(DxgiAdapterApiResult result) : IDxgiAdapterApi
    {
        internal int Calls { get; private set; }

        public DxgiAdapterApiResult Capture()
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
