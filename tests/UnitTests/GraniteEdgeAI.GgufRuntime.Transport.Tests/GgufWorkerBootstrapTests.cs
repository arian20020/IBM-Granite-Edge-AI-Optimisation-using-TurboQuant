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
}
