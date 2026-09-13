using System.Text;
using GraniteEdgeAI.HardwareInspection.LlamaCppProbe;

namespace GraniteEdgeAI.HardwareInspection.LlamaCppProbe.Tests;

[TestClass]
public sealed class LlamaCppProbeApplicationTests
{
    private const string IdentityJson =
        "{\"schemaVersion\":1,\"probeIdentity\":\"granite-edge-hardware-llamacpp-capabilities/1\"," +
        "\"managedPackage\":\"LLamaSharp\",\"managedVersion\":\"0.27.0\"," +
        "\"backendPackage\":\"LLamaSharp.Backend.Cpu\",\"backendVersion\":\"0.27.0\"," +
        "\"llamaSharpCommit\":\"7cbbc45e421d55794d5050d126e0b96511007007\"," +
        "\"mappedLlamaCppCommit\":\"3f7c29d318e317b63f54c558bc69803963d7d88c\"," +
        "\"runtimeIdentifier\":\"win-x64\"}\n";

    [TestMethod]
    public async Task IdentityWritesExactProtocolWithoutNativeAccess()
    {
        FakeNativeApi nativeApi = FakeNativeApi.Available((0, "CPU"));
        using var output = new MemoryStream();

        int exitCode = await LlamaCppProbeApplication.RunAsync(
            ["identity", "--format", "json-v1"],
            output,
            nativeApi);

        Assert.AreEqual(0, exitCode);
        Assert.AreEqual(0, nativeApi.CaptureCalls);
        CollectionAssert.AreEqual(Encoding.UTF8.GetBytes(IdentityJson), output.ToArray());
    }

    [TestMethod]
    public async Task CapabilitiesWritesExactClosedProtocol()
    {
        FakeNativeApi nativeApi = FakeNativeApi.Available((0, "CPU"), (1, "CPU AMX"));
        using var output = new MemoryStream();

        int exitCode = await LlamaCppProbeApplication.RunAsync(
            ["capabilities", "--format", "json-v1"],
            output,
            nativeApi);

        Assert.AreEqual(0, exitCode);
        Assert.AreEqual(1, nativeApi.CaptureCalls);
        Assert.AreEqual(
            "{\"schemaVersion\":1,\"probeIdentity\":\"granite-edge-hardware-llamacpp-capabilities/1\"," +
            "\"backends\":[\"cpu\"],\"devices\":[{\"ordinal\":0,\"bufferType\":\"CPU\"}," +
            "{\"ordinal\":1,\"bufferType\":\"CPU AMX\"}]}\n",
            Encoding.UTF8.GetString(output.ToArray()));
    }

    [TestMethod]
    [DataRow()]
    [DataRow("identity")]
    [DataRow("identity", "--format", "JSON-V1")]
    [DataRow("capabilities", "--format", "json-v1", "extra")]
    [DataRow("unknown", "--format", "json-v1")]
    public async Task UsageMismatchFailsClosed(params string[] arguments)
    {
        FakeNativeApi nativeApi = FakeNativeApi.Available((0, "CPU"));
        using var output = new MemoryStream();

        int exitCode = await LlamaCppProbeApplication.RunAsync(arguments, output, nativeApi);

        Assert.AreEqual(64, exitCode);
        Assert.AreEqual(0, nativeApi.CaptureCalls);
        Assert.AreEqual(0, output.Length);
    }

    [TestMethod]
    public async Task NativeFailureReturnsClosedExitWithEmptyOutput()
    {
        FakeNativeApi nativeApi = FakeNativeApi.Unavailable();
        using var output = new MemoryStream();

        int exitCode = await LlamaCppProbeApplication.RunAsync(
            ["capabilities", "--format", "json-v1"],
            output,
            nativeApi);

        Assert.AreEqual(70, exitCode);
        Assert.AreEqual(1, nativeApi.CaptureCalls);
        Assert.AreEqual(0, output.Length);
    }

    [TestMethod]
    public async Task OutputFailureReturnsClosedExitWithoutRetry()
    {
        FakeNativeApi nativeApi = FakeNativeApi.Available((0, "CPU"));
        using var output = new ThrowingWriteStream();

        int exitCode = await LlamaCppProbeApplication.RunAsync(
            ["capabilities", "--format", "json-v1"],
            output,
            nativeApi);

        Assert.AreEqual(74, exitCode);
        Assert.AreEqual(1, nativeApi.CaptureCalls);
        Assert.AreEqual(1, output.WriteCalls);
    }

    private sealed class FakeNativeApi : ILlamaCppNativeCapabilityApi
    {
        private readonly LlamaCppNativeCapabilityResult result;

        private FakeNativeApi(LlamaCppNativeCapabilityResult result) => this.result = result;

        internal int CaptureCalls { get; private set; }

        internal static FakeNativeApi Available(params (int Ordinal, string BufferType)[] devices) =>
            new(LlamaCppNativeCapabilityResult.Available(
                devices.Select(static device => new LlamaCppNativeDevice(device.Ordinal, device.BufferType))));

        internal static FakeNativeApi Unavailable() => new(LlamaCppNativeCapabilityResult.Unavailable());

        public LlamaCppNativeCapabilityResult Capture()
        {
            CaptureCalls++;
            return result;
        }
    }

    private sealed class ThrowingWriteStream : Stream
    {
        internal int WriteCalls { get; private set; }
        public override bool CanRead => false;
        public override bool CanSeek => false;
        public override bool CanWrite => true;
        public override long Length => 0;
        public override long Position { get => 0; set => throw new NotSupportedException(); }
        public override void Flush() { }
        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
        {
            WriteCalls++;
            return ValueTask.FromException(new IOException("synthetic output failure"));
        }
    }
}
