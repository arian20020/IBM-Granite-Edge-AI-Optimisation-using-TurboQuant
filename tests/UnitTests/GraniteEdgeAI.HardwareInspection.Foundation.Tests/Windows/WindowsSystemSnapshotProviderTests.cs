using GraniteEdgeAI.HardwareInspection.Foundation.Windows;

namespace GraniteEdgeAI.HardwareInspection.Foundation.Tests.Windows;

[TestClass]
public sealed class WindowsSystemSnapshotProviderTests
{
    private static readonly DateTimeOffset CapturedAtUtc =
        new(2026, 8, 22, 12, 30, 0, TimeSpan.Zero);

    [TestMethod]
    public async Task CaptureMapsDistinctMemoryValuesAndSafeOperatingSystemFacts()
    {
        FakeWindowsMemoryApi api = new(
            installedKilobytes: 16UL * 1024 * 1024,
            usableBytes: 15UL * 1024 * 1024 * 1024,
            availableBytes: 8UL * 1024 * 1024 * 1024);
        WindowsSystemSnapshotProvider provider = CreateProvider(api);

        WindowsSystemSnapshot snapshot = await provider.CaptureAsync(CancellationToken.None);

        Assert.AreEqual(16UL * 1024 * 1024 * 1024, snapshot.PhysicallyInstalledBytes);
        Assert.AreEqual(15UL * 1024 * 1024 * 1024, snapshot.OsUsablePhysicalBytes);
        Assert.AreEqual(8UL * 1024 * 1024 * 1024, snapshot.AvailablePhysicalBytes);
        Assert.AreEqual(CapturedAtUtc, snapshot.CapturedAtUtc);
        Assert.AreEqual("Windows 11", snapshot.OperatingSystemName);
        Assert.AreEqual("10.0.26200", snapshot.OperatingSystemVersion);
        Assert.AreEqual("x64", snapshot.OperatingSystemArchitecture);
    }

    [TestMethod]
    public async Task CaptureAcceptsZeroAvailablePhysicalMemory()
    {
        WindowsSystemSnapshotProvider provider = CreateProvider(
            new FakeWindowsMemoryApi(1024, 1024 * 1024, 0));

        WindowsSystemSnapshot snapshot = await provider.CaptureAsync(CancellationToken.None);

        Assert.AreEqual(0UL, snapshot.AvailablePhysicalBytes);
    }

    [TestMethod]
    public async Task CaptureRejectsCancellationBeforeCallingNativeBoundary()
    {
        FakeWindowsMemoryApi api = new(1024, 1024 * 1024, 0);
        WindowsSystemSnapshotProvider provider = CreateProvider(api);
        using CancellationTokenSource cancellation = new();
        cancellation.Cancel();

        await Assert.ThrowsExactlyAsync<OperationCanceledException>(
            async () => await provider.CaptureAsync(cancellation.Token));

        Assert.AreEqual(0, api.InstalledCalls);
        Assert.AreEqual(0, api.StatusCalls);
    }

    [TestMethod]
    public async Task CaptureMapsNativeFailureToStableDiagnostic()
    {
        FakeWindowsMemoryApi api = new(1024, 1024 * 1024, 0)
        {
            InstalledSucceeds = false,
        };

        WindowsSystemSnapshotException error =
            await Assert.ThrowsExactlyAsync<WindowsSystemSnapshotException>(
                async () => await CreateProvider(api).CaptureAsync(CancellationToken.None));

        Assert.AreEqual("HI-WINDOWS-MEMORY-UNAVAILABLE", error.DiagnosticCode);
        Assert.IsFalse(error.Message.Contains("path", StringComparison.OrdinalIgnoreCase));
    }

    [TestMethod]
    public async Task CaptureRejectsInstalledKilobyteOverflow()
    {
        FakeWindowsMemoryApi api = new((ulong.MaxValue / 1024) + 1, 1, 0);

        WindowsSystemSnapshotException error =
            await Assert.ThrowsExactlyAsync<WindowsSystemSnapshotException>(
                async () => await CreateProvider(api).CaptureAsync(CancellationToken.None));

        Assert.AreEqual("HI-WINDOWS-MEMORY-OVERFLOW", error.DiagnosticCode);
    }

    [TestMethod]
    public async Task CaptureRejectsImpossibleMemoryRelationship()
    {
        FakeWindowsMemoryApi api = new(1024, 2UL * 1024 * 1024, 3UL * 1024 * 1024);

        WindowsSystemSnapshotException error =
            await Assert.ThrowsExactlyAsync<WindowsSystemSnapshotException>(
                async () => await CreateProvider(api).CaptureAsync(CancellationToken.None));

        Assert.AreEqual("HI-WINDOWS-MEMORY-INCONSISTENT", error.DiagnosticCode);
    }

    [TestMethod]
    public async Task CaptureMapsNonUtcClockOutputToClosedDiagnostic()
    {
        WindowsSystemSnapshotProvider provider = new(
            new FakeWindowsMemoryApi(1024, 1024 * 1024, 0),
            new FixedTimeProvider(
                new DateTimeOffset(2026, 8, 22, 13, 30, 0, TimeSpan.FromHours(1))),
            new FakeOperatingSystemInfo());

        WindowsSystemSnapshotException error =
            await Assert.ThrowsExactlyAsync<WindowsSystemSnapshotException>(
                async () => await provider.CaptureAsync(CancellationToken.None));

        Assert.AreEqual("HI-WINDOWS-MEMORY-INCONSISTENT", error.DiagnosticCode);
    }

    [TestMethod]
    public void SnapshotRejectsNonUtcCaptureTime()
    {
        Assert.ThrowsExactly<ArgumentException>(() =>
            new WindowsSystemSnapshot(
                2,
                1,
                0,
                new DateTimeOffset(2026, 8, 22, 13, 30, 0, TimeSpan.FromHours(1)),
                "Windows 11",
                "10.0.26200",
                "x64"));
    }

    [TestMethod]
    [TestCategory("WindowsIntegration")]
    public async Task RealProviderReturnsStructurallyConsistentPrivateSafeSnapshot()
    {
        WindowsSystemSnapshot snapshot =
            await new WindowsSystemSnapshotProvider().CaptureAsync(CancellationToken.None);

        Assert.IsGreaterThan(0UL, snapshot.PhysicallyInstalledBytes);
        Assert.IsTrue(snapshot.PhysicallyInstalledBytes >= snapshot.OsUsablePhysicalBytes);
        Assert.IsTrue(snapshot.OsUsablePhysicalBytes >= snapshot.AvailablePhysicalBytes);
        Assert.AreEqual(TimeSpan.Zero, snapshot.CapturedAtUtc.Offset);
        Assert.IsFalse(string.IsNullOrWhiteSpace(snapshot.OperatingSystemName));
        Assert.IsFalse(string.IsNullOrWhiteSpace(snapshot.OperatingSystemVersion));
        Assert.IsFalse(string.IsNullOrWhiteSpace(snapshot.OperatingSystemArchitecture));
    }

    private static WindowsSystemSnapshotProvider CreateProvider(FakeWindowsMemoryApi api) =>
        new(
            api,
            new FixedTimeProvider(CapturedAtUtc),
            new FakeOperatingSystemInfo());

    private sealed class FakeWindowsMemoryApi(
        ulong installedKilobytes,
        ulong usableBytes,
        ulong availableBytes) : IWindowsMemoryApi
    {
        internal bool InstalledSucceeds { get; init; } = true;

        internal bool StatusSucceeds { get; init; } = true;

        internal int InstalledCalls { get; private set; }

        internal int StatusCalls { get; private set; }

        public bool TryGetPhysicallyInstalledKilobytes(out ulong value)
        {
            InstalledCalls++;
            value = installedKilobytes;
            return InstalledSucceeds;
        }

        public bool TryGetMemoryStatus(out ulong totalPhysicalBytes, out ulong availablePhysicalBytes)
        {
            StatusCalls++;
            totalPhysicalBytes = usableBytes;
            availablePhysicalBytes = availableBytes;
            return StatusSucceeds;
        }
    }

    private sealed class FakeOperatingSystemInfo : IWindowsOperatingSystemInfo
    {
        public (string Name, string Version, string Architecture) Capture() =>
            ("Windows 11", "10.0.26200", "x64");
    }

    private sealed class FixedTimeProvider(DateTimeOffset value) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => value;
    }
}
