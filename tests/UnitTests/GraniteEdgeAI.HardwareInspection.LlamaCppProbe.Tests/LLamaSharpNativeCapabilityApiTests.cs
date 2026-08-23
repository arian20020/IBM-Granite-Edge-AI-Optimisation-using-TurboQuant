using System.Diagnostics;
using GraniteEdgeAI.HardwareInspection.LlamaCppProbe;

namespace GraniteEdgeAI.HardwareInspection.LlamaCppProbe.Tests;

[TestClass]
public sealed class LLamaSharpNativeCapabilityApiTests
{
    private static readonly string[] SuccessfulCaptureEvents =
    [
        "initialize",
        "count",
        "device:0",
        "buffer:1",
        "name:101",
        "device:1",
        "buffer:2",
        "name:102",
        "free",
    ];

    private static readonly string[] InitializationOnlyEvents = ["initialize"];

    [TestMethod]
    public void CaptureInitializesEnumeratesCopiesAndFreesInOrder()
    {
        FakeInterop interop = FakeInterop.WithDevices("CPU", "CPU AMX");

        LlamaCppNativeCapabilityResult result = new LLamaSharpNativeCapabilityApi(interop).Capture();

        Assert.IsTrue(result.IsAvailable);
        CollectionAssert.AreEqual(
            new[] { new LlamaCppNativeDevice(0, "CPU"), new LlamaCppNativeDevice(1, "CPU AMX") },
            result.Devices.ToArray());
        CollectionAssert.AreEqual(
            SuccessfulCaptureEvents,
            interop.Events);
    }

    [TestMethod]
    public void InitializationFailureDoesNotFree()
    {
        FakeInterop interop = FakeInterop.WithDevices("CPU");
        interop.ThrowOnInitialize = true;

        LlamaCppNativeCapabilityResult result = new LLamaSharpNativeCapabilityApi(interop).Capture();

        Assert.IsFalse(result.IsAvailable);
        CollectionAssert.AreEqual(InitializationOnlyEvents, interop.Events);
    }

    [TestMethod]
    [DataRow(0)]
    [DataRow(17)]
    public void DeviceCountOutsideClosedBoundsFailsAndFrees(int count)
    {
        FakeInterop interop = FakeInterop.WithDeviceCount((nuint)count);

        LlamaCppNativeCapabilityResult result = new LLamaSharpNativeCapabilityApi(interop).Capture();

        Assert.IsFalse(result.IsAvailable);
        Assert.AreEqual(1, interop.Events.Count(static item => item == "free"));
        Assert.IsFalse(interop.Events.Any(static item => item.StartsWith("device:", StringComparison.Ordinal)));
    }

    [TestMethod]
    [DataRow("null-device")]
    [DataRow("null-buffer")]
    [DataRow("null-label")]
    [DataRow("unsafe-label")]
    [DataRow("oversized-label")]
    [DataRow("free-failure")]
    public void InvalidNativeFactsOrCleanupFailClosed(string mode)
    {
        FakeInterop interop = FakeInterop.WithDevices("CPU");
        interop.Mode = mode;

        LlamaCppNativeCapabilityResult result = new LLamaSharpNativeCapabilityApi(interop).Capture();

        Assert.IsFalse(result.IsAvailable);
        Assert.AreEqual(1, interop.Events.Count(static item => item == "free"));
        Assert.AreEqual(0, result.Devices.Count);
    }

    [TestMethod]
    public void FakeSeamDoesNotLoadLlamaOrGgmlIntoTestHost()
    {
        string[] before = NativeModuleNames();

        _ = new LLamaSharpNativeCapabilityApi(FakeInterop.WithDevices("CPU")).Capture();

        CollectionAssert.AreEqual(before, NativeModuleNames());
        Assert.IsFalse(
            before.Any(IsLlamaOrGgml),
            $"Unexpected native modules: {string.Join(", ", before.Where(IsLlamaOrGgml))}");
    }

    private static string[] NativeModuleNames() => Process.GetCurrentProcess().Modules
        .Cast<ProcessModule>()
        .Select(static module => Path.GetFileName(module.FileName))
        .Order(StringComparer.OrdinalIgnoreCase)
        .ToArray();

    private static bool IsLlamaOrGgml(string name) =>
        name.StartsWith("llama", StringComparison.OrdinalIgnoreCase) ||
        name.StartsWith("ggml", StringComparison.OrdinalIgnoreCase);

    private sealed class FakeInterop : ILlamaCppNativeInterop
    {
        private readonly nuint deviceCount;
        private readonly string[] labels;

        private FakeInterop(nuint deviceCount, string[] labels)
        {
            this.deviceCount = deviceCount;
            this.labels = labels;
        }

        internal List<string> Events { get; } = [];
        internal bool ThrowOnInitialize { get; set; }
        internal string? Mode { get; set; }

        internal static FakeInterop WithDevices(params string[] labels) => new((nuint)labels.Length, labels);
        internal static FakeInterop WithDeviceCount(nuint count) => new(count, []);

        public void InitializeBackend()
        {
            Events.Add("initialize");
            if (ThrowOnInitialize)
            {
                throw new InvalidOperationException("synthetic initialization failure");
            }
        }

        public nuint GetDeviceCount()
        {
            Events.Add("count");
            return deviceCount;
        }

        public nint GetDevice(nuint ordinal)
        {
            Events.Add($"device:{ordinal}");
            return Mode == "null-device" ? 0 : checked((nint)(ordinal + 1));
        }

        public nint GetDeviceBufferType(nint device)
        {
            Events.Add($"buffer:{device}");
            return Mode == "null-buffer" ? 0 : device + 100;
        }

        public string? GetBufferTypeName(nint bufferType)
        {
            Events.Add($"name:{bufferType}");
            return Mode switch
            {
                "null-label" => null,
                "unsafe-label" => " CPU\n",
                "oversized-label" => new string('X', 129),
                _ => labels[checked((int)(bufferType - 101))],
            };
        }

        public void FreeBackend()
        {
            Events.Add("free");
            if (Mode == "free-failure")
            {
                throw new InvalidOperationException("synthetic cleanup failure");
            }
        }
    }
}
