using GraniteEdgeAI.HardwareInspection.Foundation.Dxgi;
using System.Runtime.InteropServices;

namespace GraniteEdgeAI.HardwareInspection.Foundation.Tests.Dxgi;

[TestClass]
public sealed class DxgiAdapterApiTests
{
    [TestMethod]
    public void NotFoundTerminatesSuccessfulEnumerationAndReleasesFactory()
    {
        FakeDxgiFactory factory = new([]);
        FakeDxgiInterop interop = new(DxgiFactoryCreateStatus.Success, factory);

        DxgiAdapterApiResult result = new DxgiAdapterApi(interop).Capture();

        Assert.AreEqual(DxgiAdapterApiStatus.Success, result.Status);
        Assert.HasCount(0, result.Adapters.ToArray());
        Assert.AreEqual(1, factory.ReleaseCalls);
        Assert.AreEqual(1, factory.EnumCalls);
    }

    [TestMethod]
    public void FactoryAndEnumerationFailuresMapSeparatelyAndReleaseAcquiredResources()
    {
        FakeDxgiInterop unavailable = new(DxgiFactoryCreateStatus.Unavailable, null);
        Assert.AreEqual(
            DxgiAdapterApiStatus.FactoryUnavailable,
            new DxgiAdapterApi(unavailable).Capture().Status);

        FakeDxgiAdapter first = new(Description("Fixture"));
        FakeDxgiFactory factory = new(
        [
            new(DxgiAdapterEnumerationStatus.Found, first),
            new(DxgiAdapterEnumerationStatus.Failed, null),
        ]);

        DxgiAdapterApiResult result = new DxgiAdapterApi(
            new FakeDxgiInterop(DxgiFactoryCreateStatus.Success, factory)).Capture();

        Assert.AreEqual(DxgiAdapterApiStatus.EnumerationFailed, result.Status);
        Assert.HasCount(0, result.Adapters.ToArray());
        Assert.AreEqual(1, first.ReleaseCalls);
        Assert.AreEqual(1, factory.ReleaseCalls);
    }

    [TestMethod]
    public void EveryAdapterIsReleasedOnceOnSuccess()
    {
        FakeDxgiAdapter first = new(Description("First"));
        FakeDxgiAdapter second = new(Description("Second"));
        FakeDxgiFactory factory = new(
        [
            new(DxgiAdapterEnumerationStatus.Found, first),
            new(DxgiAdapterEnumerationStatus.Found, second),
        ]);

        DxgiAdapterApiResult result = new DxgiAdapterApi(
            new FakeDxgiInterop(DxgiFactoryCreateStatus.Success, factory)).Capture();

        Assert.AreEqual(DxgiAdapterApiStatus.Success, result.Status);
        Assert.HasCount(2, result.Adapters.ToArray());
        Assert.AreEqual(1, first.ReleaseCalls);
        Assert.AreEqual(1, second.ReleaseCalls);
        Assert.AreEqual(1, factory.ReleaseCalls);
    }

    [TestMethod]
    public void DescriptionFailureAndUnsafeDescriptionFailClosedAndReleaseResources()
    {
        FakeDxgiAdapter descriptionFailure = new(null);
        FakeDxgiFactory failedFactory = new(
            [new(DxgiAdapterEnumerationStatus.Found, descriptionFailure)]);
        Assert.AreEqual(
            DxgiAdapterApiStatus.InvalidDescription,
            new DxgiAdapterApi(new FakeDxgiInterop(DxgiFactoryCreateStatus.Success, failedFactory))
                .Capture().Status);
        Assert.AreEqual(1, descriptionFailure.ReleaseCalls);
        Assert.AreEqual(1, failedFactory.ReleaseCalls);

        FakeDxgiAdapter unsafeAdapter = new(Description("unsafe\u202Ename"));
        FakeDxgiFactory unsafeFactory = new([new(DxgiAdapterEnumerationStatus.Found, unsafeAdapter)]);
        Assert.AreEqual(
            DxgiAdapterApiStatus.InvalidDescription,
            new DxgiAdapterApi(new FakeDxgiInterop(DxgiFactoryCreateStatus.Success, unsafeFactory))
                .Capture().Status);
        Assert.AreEqual(1, unsafeAdapter.ReleaseCalls);
        Assert.AreEqual(1, unsafeFactory.ReleaseCalls);
    }

    [TestMethod]
    public void DescriptionIsCutAtTerminatorThenTrimmedOnlyAtBoundary()
    {
        FakeDxgiAdapter adapter = new(Description("  Fixture  Adapter  \0ignored"));
        FakeDxgiFactory factory = new([new(DxgiAdapterEnumerationStatus.Found, adapter)]);

        DxgiAdapterApiEntry entry = new DxgiAdapterApi(
            new FakeDxgiInterop(DxgiFactoryCreateStatus.Success, factory))
            .Capture().Adapters.Single();

        Assert.AreEqual("Fixture  Adapter", entry.Name);
    }

    [TestMethod]
    public void FlagsMapDeterministicallyAndMemoryFieldsRemainIndependent()
    {
        FakeDxgiAdapter hardware = new(Description("Hardware", flags: 0, 1, 2, 3));
        FakeDxgiAdapter remote = new(Description("Remote", flags: 1, 4, 5, 6));
        FakeDxgiAdapter software = new(Description("Software", flags: 2, 7, 8, 9));
        FakeDxgiAdapter combined = new(Description("Combined", flags: 3, 10, 11, 12));
        FakeDxgiFactory factory = new(
        [
            new(DxgiAdapterEnumerationStatus.Found, hardware),
            new(DxgiAdapterEnumerationStatus.Found, remote),
            new(DxgiAdapterEnumerationStatus.Found, software),
            new(DxgiAdapterEnumerationStatus.Found, combined),
        ]);

        DxgiAdapterApiEntry[] entries = new DxgiAdapterApi(
            new FakeDxgiInterop(DxgiFactoryCreateStatus.Success, factory))
            .Capture().Adapters.ToArray();

        Assert.AreEqual(DxgiAdapterApiKind.Hardware, entries[0].Kind);
        Assert.AreEqual(DxgiAdapterApiKind.Remote, entries[1].Kind);
        Assert.AreEqual(DxgiAdapterApiKind.Software, entries[2].Kind);
        Assert.AreEqual(DxgiAdapterApiKind.Software, entries[3].Kind);
        Assert.AreEqual(7UL, entries[2].DedicatedVideoMemoryBytes);
        Assert.AreEqual(8UL, entries[2].DedicatedSystemMemoryBytes);
        Assert.AreEqual(9UL, entries[2].SharedSystemMemoryBytes);
    }

    [TestMethod]
    public void SixtyFifthAdapterFailsClosedWithoutRetentionAndReleasesEverything()
    {
        FakeDxgiAdapter[] adapters = Enumerable.Range(0, 65)
            .Select(index => new FakeDxgiAdapter(Description($"Fixture {index}")))
            .ToArray();
        FakeDxgiFactory factory = new(adapters
            .Select(static adapter => new FakeEnumeration(DxgiAdapterEnumerationStatus.Found, adapter))
            .ToArray());

        DxgiAdapterApiResult result = new DxgiAdapterApi(
            new FakeDxgiInterop(DxgiFactoryCreateStatus.Success, factory)).Capture();

        Assert.AreEqual(DxgiAdapterApiStatus.AdapterLimitExceeded, result.Status);
        Assert.HasCount(0, result.Adapters.ToArray());
        Assert.IsTrue(adapters.All(static adapter => adapter.ReleaseCalls == 1));
        Assert.AreEqual(1, factory.ReleaseCalls);
        Assert.AreEqual(65, factory.EnumCalls);
    }

    [TestMethod]
    public void ExpectedComExceptionsFailClosedAndReleaseAcquiredResources()
    {
        FakeDxgiFactory enumerationFailure = new([])
        {
            EnumerationException = new FixtureComException(),
        };
        Assert.AreEqual(
            DxgiAdapterApiStatus.EnumerationFailed,
            new DxgiAdapterApi(new FakeDxgiInterop(
                DxgiFactoryCreateStatus.Success,
                enumerationFailure)).Capture().Status);
        Assert.AreEqual(1, enumerationFailure.ReleaseCalls);

        FakeDxgiAdapter descriptionFailure = new(Description("Fixture"))
        {
            DescriptionException = new FixtureComException(),
        };
        FakeDxgiFactory factory = new(
            [new(DxgiAdapterEnumerationStatus.Found, descriptionFailure)]);
        Assert.AreEqual(
            DxgiAdapterApiStatus.InvalidDescription,
            new DxgiAdapterApi(new FakeDxgiInterop(
                DxgiFactoryCreateStatus.Success,
                factory)).Capture().Status);
        Assert.AreEqual(1, descriptionFailure.ReleaseCalls);
        Assert.AreEqual(1, factory.ReleaseCalls);
    }

    private static DxgiNativeAdapterDescription Description(
        string name,
        uint flags = 0,
        nuint dedicatedVideo = 0,
        nuint dedicatedSystem = 0,
        nuint sharedSystem = 0) =>
        new(name, 0x8086, 0x1234, dedicatedVideo, dedicatedSystem, sharedSystem, flags);

    private sealed class FakeDxgiInterop(
        DxgiFactoryCreateStatus status,
        IDxgiFactoryHandle? factory) : IDxgiInterop
    {
        public DxgiFactoryCreateStatus TryCreateFactory(out IDxgiFactoryHandle? value)
        {
            value = factory;
            return status;
        }
    }

    private sealed record FakeEnumeration(
        DxgiAdapterEnumerationStatus Status,
        IDxgiAdapterHandle? Adapter);

    private sealed class FakeDxgiFactory(IReadOnlyList<FakeEnumeration> enumerations) : IDxgiFactoryHandle
    {
        internal int EnumCalls { get; private set; }

        internal int ReleaseCalls { get; private set; }

        internal Exception? EnumerationException { get; init; }

        public DxgiAdapterEnumerationStatus TryGetAdapter(
            uint index,
            out IDxgiAdapterHandle? adapter)
        {
            if (EnumerationException is not null)
            {
                throw EnumerationException;
            }

            EnumCalls++;
            if (index >= enumerations.Count)
            {
                adapter = null;
                return DxgiAdapterEnumerationStatus.NotFound;
            }

            FakeEnumeration result = enumerations[checked((int)index)];
            adapter = result.Adapter;
            return result.Status;
        }

        public void Dispose() => ReleaseCalls++;
    }

    private sealed class FakeDxgiAdapter(DxgiNativeAdapterDescription? description) : IDxgiAdapterHandle
    {
        internal int ReleaseCalls { get; private set; }

        internal Exception? DescriptionException { get; init; }

        public bool TryGetDescription(out DxgiNativeAdapterDescription value)
        {
            if (DescriptionException is not null)
            {
                throw DescriptionException;
            }

            value = description ?? default;
            return description.HasValue;
        }

        public void Dispose() => ReleaseCalls++;
    }

    private sealed class FixtureComException : COMException
    {
    }
}
