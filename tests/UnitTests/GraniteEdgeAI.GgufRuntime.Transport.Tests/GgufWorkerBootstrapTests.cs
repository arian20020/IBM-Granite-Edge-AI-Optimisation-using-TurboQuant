using System.Text;
using GraniteEdgeAI.GgufRuntime.Transport;

namespace GraniteEdgeAI.GgufRuntime.Transport.Tests;

[TestClass]
public sealed class GgufWorkerBootstrapTests
{
    [TestMethod]
    public void DeserializeRejectsDuplicateBootstrapProperties()
    {
        byte[] payload = Encoding.UTF8.GetBytes(
            """
            {
              "cliExecutable": "C:\\runtime\\adapter.exe",
              "cliExecutable": "C:\\untrusted\\adapter.exe",
              "modelFile": "C:\\models\\model.gguf",
              "cliArgumentsOverride": []
            }
            """);

        _ = Assert.ThrowsExactly<GgufTransportException>(() =>
            GgufWorkerBootstrapCodec.Deserialize(payload));
    }

    [TestMethod]
    public void ConstructorRejectsUnboundedArgumentInventoryAndLength()
    {
        _ = Assert.ThrowsExactly<ArgumentException>(() => new GgufWorkerBootstrap(
            "C:\\runtime\\adapter.exe",
            "C:\\models\\model.gguf",
            Enumerable.Repeat("bounded", 33).ToArray()));
        _ = Assert.ThrowsExactly<ArgumentException>(() => new GgufWorkerBootstrap(
            "C:\\runtime\\adapter.exe",
            "C:\\models\\model.gguf",
            [new string('x', 4097)]));
    }

    [TestMethod]
    public void CodecRoundTripsVerifiedNativeRuntimeRoles()
    {
        var expected = new GgufWorkerBootstrap(
            "C:\\runtime\\adapter.exe",
            "C:\\models\\model.gguf",
            [],
            "C:\\runtime\\cpu\\llama-server.exe",
            "C:\\runtime\\vulkan\\llama-server.exe",
            "atomicbot-519f0c5-cpu-vulkan",
            "519f0c594a8e31467d2e2f2cf17054c9e7e11536");

        GgufWorkerBootstrap actual = GgufWorkerBootstrapCodec.Deserialize(
            GgufWorkerBootstrapCodec.Serialize(expected));

        Assert.AreEqual(expected.CpuRuntimeExecutable, actual.CpuRuntimeExecutable);
        Assert.AreEqual(expected.VulkanRuntimeExecutable, actual.VulkanRuntimeExecutable);
        Assert.AreEqual(expected.RuntimeBuildId, actual.RuntimeBuildId);
        Assert.AreEqual(expected.RuntimeSourceCommit, actual.RuntimeSourceCommit);
    }

    [TestMethod]
    public void ConstructorRejectsRelativeNativeRuntimeRoles()
    {
        _ = Assert.ThrowsExactly<ArgumentException>(() => new GgufWorkerBootstrap(
            "C:\\runtime\\adapter.exe",
            "C:\\models\\model.gguf",
            [],
            "cpu\\llama-server.exe",
            null));
    }
}
