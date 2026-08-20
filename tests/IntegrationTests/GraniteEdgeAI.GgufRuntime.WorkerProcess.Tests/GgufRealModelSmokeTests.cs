using System.Security.Cryptography;
using GraniteEdgeAI.GgufRuntime.Contracts.Events;
using GraniteEdgeAI.GgufRuntime.WorkerClient;

namespace GraniteEdgeAI.GgufRuntime.WorkerProcess.Tests;

[TestClass]
public sealed class GgufRealModelSmokeTests
{
    private const string ConfigurationVariable =
        "GRANITE_GGUF_RUNTIME_TEST_CONFIG";

    [TestMethod]
    [TestCategory("ControlledRuntime")]
    public async Task VerifiedLocalRuntimeLoadsStreamsTwoTurnsStopsAndCloses()
    {
        string? configurationPath = Environment.GetEnvironmentVariable(
            ConfigurationVariable);
        if (string.IsNullOrWhiteSpace(configurationPath))
        {
            Assert.Inconclusive("controlled GGUF runtime/model not configured");
        }

        ControlledGgufRuntimeConfiguration controlled =
            ControlledGgufRuntimeConfiguration.Load(configurationPath!);
        string manifestPath = Path.Combine(
            controlled.PackageRoot,
            "runtime-manifest.json");
        byte[] trustedManifest = await File.ReadAllBytesAsync(manifestPath);
        AssertDigest(trustedManifest, controlled.ManifestSha256);
        AssertFileDigest(controlled.ModelFile, controlled.ModelSha256);

        GgufRuntimeClient client = GgufRuntimeClient.CreateFromPackage(
            controlled.PackageRoot,
            trustedManifest,
            controlled.ModelFile);
        using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(2));
        await using GgufRuntimeSession session = await client.StartAsync(
            controlled.RuntimeConfiguration,
            [],
            timeout.Token);

        IReadOnlyList<GgufRuntimeEvent> first = await CollectAsync(
            session.GenerateAsync("controlled-turn-one", timeout.Token));
        IReadOnlyList<GgufRuntimeEvent> second = await CollectAsync(
            session.GenerateAsync("controlled-turn-two", timeout.Token));
        Assert.IsTrue(first.OfType<TextDeltaEvent>().Any());
        Assert.IsTrue(second.OfType<TextDeltaEvent>().Any());

        var stoppedEvents = new List<GgufRuntimeEvent>();
        await using (IAsyncEnumerator<GgufRuntimeEvent> enumerator =
            session.GenerateAsync("controlled-stop-turn", timeout.Token)
                .GetAsyncEnumerator(timeout.Token))
        {
            while (await enumerator.MoveNextAsync())
            {
                stoppedEvents.Add(enumerator.Current);
                if (enumerator.Current is TextDeltaEvent)
                {
                    await session.StopAsync(timeout.Token);
                }
            }
        }

        Assert.IsTrue(stoppedEvents.OfType<ResponseStoppedEvent>().Any());
        await session.CloseAsync(timeout.Token);
        AssertFileDigest(controlled.ModelFile, controlled.ModelSha256);
    }

    private static async Task<IReadOnlyList<GgufRuntimeEvent>> CollectAsync(
        IAsyncEnumerable<GgufRuntimeEvent> events)
    {
        var result = new List<GgufRuntimeEvent>();
        await foreach (GgufRuntimeEvent runtimeEvent in events)
        {
            result.Add(runtimeEvent);
        }

        return result;
    }

    private static void AssertFileDigest(string path, string expected)
    {
        using FileStream stream = File.OpenRead(path);
        Assert.AreEqual(expected, Convert.ToHexString(SHA256.HashData(stream)));
    }

    private static void AssertDigest(byte[] content, string expected) =>
        Assert.AreEqual(expected, Convert.ToHexString(SHA256.HashData(content)));
}
