using GraniteEdgeAI.HardwareInspection.Foundation.Windows;

namespace GraniteEdgeAI.HardwareInspection.Foundation.Tests.Windows;

[TestClass]
public sealed class WindowsStorageEvidenceProviderTests
{
    private static readonly DateTimeOffset CapturedAtUtc =
        new(2026, 8, 23, 10, 0, 0, TimeSpan.Zero);

    [TestMethod]
    public void AvailableEvidenceRetainsExactCapacityAndCallerAvailableBytes()
    {
        WindowsStorageEvidence evidence = WindowsStorageEvidence.Available(
            1_000,
            400,
            CapturedAtUtc);

        Assert.AreEqual(WindowsStorageEvidenceState.Available, evidence.State);
        Assert.AreEqual(1_000UL, evidence.CapacityBytes);
        Assert.AreEqual(400UL, evidence.AvailableToCallerBytes);
        Assert.AreEqual(CapturedAtUtc, evidence.CapturedAtUtc);
        Assert.HasCount(0, evidence.Diagnostics);
    }

    [TestMethod]
    [DataRow(0UL, 0UL)]
    [DataRow(100UL, 101UL)]
    public void AvailableEvidenceRejectsInconsistentValues(ulong capacity, ulong available)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            WindowsStorageEvidence.Available(capacity, available, CapturedAtUtc));
    }

    [TestMethod]
    public void EvidenceRejectsUndefinedDiagnosticAndNonUtcTime()
    {
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => WindowsStorageEvidence.Unavailable(
            (WindowsStorageDiagnosticCode)999,
            CapturedAtUtc));
        Assert.ThrowsExactly<ArgumentException>(() => WindowsStorageEvidence.Unavailable(
            WindowsStorageDiagnosticCode.NativeApiUnavailable,
            CapturedAtUtc.ToOffset(TimeSpan.FromHours(1))));
    }

    [TestMethod]
    public void UnavailableEvidenceHasNoStorageFactsAndOneClosedDiagnostic()
    {
        WindowsStorageEvidence evidence = WindowsStorageEvidence.Unavailable(
            WindowsStorageDiagnosticCode.DiskInformationUnavailable,
            CapturedAtUtc);

        Assert.AreEqual(WindowsStorageEvidenceState.Unavailable, evidence.State);
        Assert.IsNull(evidence.CapacityBytes);
        Assert.IsNull(evidence.AvailableToCallerBytes);
        CollectionAssert.AreEqual(
            new[] { WindowsStorageDiagnosticCode.DiskInformationUnavailable },
            evidence.Diagnostics.ToArray());
    }

    [TestMethod]
    public async Task ProviderMapsSuccessExactlyAndConvertsTimeToUtc()
    {
        FakeWindowsStorageApi api = new(new(WindowsStorageApiStatus.Success, 1_000, 400));
        WindowsStorageEvidenceProvider provider = new(
            api,
            new FixedTimeProvider(CapturedAtUtc.ToOffset(TimeSpan.FromHours(1))));

        WindowsStorageEvidence evidence = await provider.CaptureAsync(CancellationToken.None);

        Assert.AreEqual(1_000UL, evidence.CapacityBytes);
        Assert.AreEqual(400UL, evidence.AvailableToCallerBytes);
        Assert.AreEqual(CapturedAtUtc, evidence.CapturedAtUtc);
        Assert.AreEqual(1, api.Calls);
    }

    [TestMethod]
    public async Task ProviderHonorsCancellationBeforeApiAccess()
    {
        FakeWindowsStorageApi api = new(new(WindowsStorageApiStatus.NativeApiUnavailable, 0, 0));
        WindowsStorageEvidenceProvider provider = new(api, new FixedTimeProvider(CapturedAtUtc));
        using CancellationTokenSource cancellation = new();
        cancellation.Cancel();

        OperationCanceledException error = await Assert.ThrowsExactlyAsync<OperationCanceledException>(
            async () => await provider.CaptureAsync(cancellation.Token));

        Assert.AreEqual(cancellation.Token, error.CancellationToken);
        Assert.AreEqual(0, api.Calls);
    }

    [TestMethod]
    [DataRow((int)WindowsStorageApiStatus.SystemDirectoryUnavailable, WindowsStorageDiagnosticCode.SystemDirectoryUnavailable)]
    [DataRow((int)WindowsStorageApiStatus.InvalidVolumeRoot, WindowsStorageDiagnosticCode.InvalidVolumeRoot)]
    [DataRow((int)WindowsStorageApiStatus.DiskInformationUnavailable, WindowsStorageDiagnosticCode.DiskInformationUnavailable)]
    [DataRow((int)WindowsStorageApiStatus.NativeApiUnavailable, WindowsStorageDiagnosticCode.NativeApiUnavailable)]
    public async Task ProviderMapsClosedApiFailures(int statusValue, WindowsStorageDiagnosticCode expected)
    {
        FakeWindowsStorageApi api = new(new((WindowsStorageApiStatus)statusValue, 999, 888));

        WindowsStorageEvidence evidence = await new WindowsStorageEvidenceProvider(
            api,
            new FixedTimeProvider(CapturedAtUtc)).CaptureAsync(CancellationToken.None);

        Assert.AreEqual(WindowsStorageEvidenceState.Unavailable, evidence.State);
        CollectionAssert.AreEqual(new[] { expected }, evidence.Diagnostics.ToArray());
        Assert.IsNull(evidence.CapacityBytes);
        Assert.AreEqual(1, api.Calls);
    }

    [TestMethod]
    [DataRow(0UL, 0UL)]
    [DataRow(100UL, 101UL)]
    public async Task ProviderMapsInvalidSuccessfulValuesToClosedDiagnostic(
        ulong capacity,
        ulong available)
    {
        FakeWindowsStorageApi api = new(new(WindowsStorageApiStatus.Success, capacity, available));

        WindowsStorageEvidence evidence = await new WindowsStorageEvidenceProvider(
            api,
            new FixedTimeProvider(CapturedAtUtc)).CaptureAsync(CancellationToken.None);

        CollectionAssert.AreEqual(
            new[] { WindowsStorageDiagnosticCode.InconsistentValues },
            evidence.Diagnostics.ToArray());
    }

    [TestMethod]
    public void NativeAdapterKeepsRootPrivateAndMapsExactDiskValues()
    {
        FakeKernel32StorageNative native = new(@"C:\Windows", 400, 1_000);

        WindowsStorageApiResult result = new Kernel32WindowsStorageApi(native).Capture();

        Assert.AreEqual(WindowsStorageApiStatus.Success, result.Status);
        Assert.AreEqual(1_000UL, result.CapacityBytes);
        Assert.AreEqual(400UL, result.AvailableToCallerBytes);
        Assert.AreEqual(@"C:\", native.ObservedRoot);
    }

    [TestMethod]
    public void NativeAdapterRejectsSystemDirectoryFailureTruncationInvalidRootAndDiskFailure()
    {
        FakeKernel32StorageNative missing = new(null, 0, 0);
        Assert.AreEqual(
            WindowsStorageApiStatus.SystemDirectoryUnavailable,
            new Kernel32WindowsStorageApi(missing).Capture().Status);

        FakeKernel32StorageNative truncated = new(@"C:\Windows", 0, 0)
        {
            ReturnedDirectoryLength = Kernel32WindowsStorageApi.MaximumSystemDirectoryCharacters,
        };
        Assert.AreEqual(
            WindowsStorageApiStatus.SystemDirectoryUnavailable,
            new Kernel32WindowsStorageApi(truncated).Capture().Status);

        FakeKernel32StorageNative relative = new("Windows", 0, 0);
        Assert.AreEqual(
            WindowsStorageApiStatus.InvalidVolumeRoot,
            new Kernel32WindowsStorageApi(relative).Capture().Status);

        FakeKernel32StorageNative diskFailure = new(@"C:\Windows", 0, 0) { DiskSucceeds = false };
        Assert.AreEqual(
            WindowsStorageApiStatus.DiskInformationUnavailable,
            new Kernel32WindowsStorageApi(diskFailure).Capture().Status);
    }

    private sealed class FakeWindowsStorageApi(WindowsStorageApiResult result) : IWindowsStorageApi
    {
        internal int Calls { get; private set; }

        public WindowsStorageApiResult Capture()
        {
            Calls++;
            return result;
        }
    }

    private sealed class FakeKernel32StorageNative(
        string? systemDirectory,
        ulong available,
        ulong capacity) : IKernel32WindowsStorageNative
    {
        internal uint? ReturnedDirectoryLength { get; init; }

        internal bool DiskSucceeds { get; init; } = true;

        internal string? ObservedRoot { get; private set; }

        public uint GetSystemWindowsDirectory(char[] buffer, uint capacityCharacters)
        {
            if (systemDirectory is null)
            {
                return 0;
            }

            systemDirectory.CopyTo(0, buffer, 0, systemDirectory.Length);
            return ReturnedDirectoryLength ?? checked((uint)systemDirectory.Length);
        }

        public bool TryGetDiskSpace(
            string volumeRoot,
            out ulong availableToCallerBytes,
            out ulong capacityBytes)
        {
            ObservedRoot = volumeRoot;
            availableToCallerBytes = available;
            capacityBytes = capacity;
            return DiskSucceeds;
        }
    }

    private sealed class FixedTimeProvider(DateTimeOffset value) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => value;
    }
}
