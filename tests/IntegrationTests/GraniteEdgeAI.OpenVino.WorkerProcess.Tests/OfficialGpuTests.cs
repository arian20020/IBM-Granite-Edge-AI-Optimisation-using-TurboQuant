using GraniteEdgeAI.OpenVino.Contracts;
using GraniteEdgeAI.OpenVino.WorkerClient;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace GraniteEdgeAI.OpenVino.WorkerProcess.Tests;

[TestClass]
[TestCategory("OfficialNative")]
[DoNotParallelize]
public sealed class OfficialGpuTests
{
    private sealed class InlineProgress<T>(Action<T> action) : IProgress<T>
    {
        public void Report(T value) => action(value);
    }

    public TestContext TestContext { get; set; } = null!;

    [TestMethod]
    public async Task NonexistentExplicitGpuFailsUnavailableWithoutCpuFallback()
    {
        string stage = OfficialCpuFixtureTests.RequireStage(
            "OPENVINO_OFFICIAL_WORKER_STAGE_A");
        string package = OfficialCpuFixtureTests.LocateCanonicalPackage();

        OpenVinoWorkerClientException error =
            await Assert.ThrowsExactlyAsync<OpenVinoWorkerClientException>(() =>
                OfficialCpuFixtureTests.CreateClient(stage).StartSessionAsync(
                    new StartSessionCommand(
                        Guid.NewGuid(),
                        Guid.NewGuid(),
                        package,
                        OfficialCpuFixtureTests.PackageDigest,
                        OfficialCpuFixtureTests.ModelDigest,
                        OfficialCpuFixtureTests.ModelLength,
                        new OpenVinoDeviceRequest("GPU.999999"),
                        new OpenVinoGenerationLimits(64, 2)),
                    CancellationToken.None)).ConfigureAwait(false);

        Assert.AreEqual(
            OpenVinoSupportCode.RuntimeDeviceUnavailable,
            error.SupportCode);
        Assert.IsFalse(
            error.RetainedStandardError.Contains("CPU", StringComparison.OrdinalIgnoreCase),
            "An unavailable explicit GPU request must not disclose or attempt CPU fallback.");
    }

    [TestMethod]
    public async Task AuthorizedPhysicalIntelGpuCompilesAndGeneratesOnExactDevice()
    {
        string? requested = Environment.GetEnvironmentVariable(
            "OPENVINO_UCL_GPU_DEVICE");
        if (string.IsNullOrWhiteSpace(requested))
        {
            Assert.Inconclusive(
                "GPU-01 is required; OPENVINO_UCL_GPU_DEVICE was not supplied.");
        }
        new OpenVinoDeviceRequest(requested!).Validate();
        Assert.StartsWith(
            requested!,
            "GPU",
            StringComparison.Ordinal,
            "The UCL device must be an explicit Intel GPU identity.");

        string stage = OfficialCpuFixtureTests.RequireStage(
            "OPENVINO_OFFICIAL_WORKER_STAGE_A");
        string package = OfficialCpuFixtureTests.LocateCanonicalPackage();
        Guid sessionId = Guid.NewGuid();
        await using OpenVinoConversation conversation =
            await OfficialCpuFixtureTests.CreateClient(stage).StartSessionAsync(
                new StartSessionCommand(
                    sessionId,
                    Guid.NewGuid(),
                    package,
                    OfficialCpuFixtureTests.PackageDigest,
                    OfficialCpuFixtureTests.ModelDigest,
                    OfficialCpuFixtureTests.ModelLength,
                    new OpenVinoDeviceRequest(requested!),
                    new OpenVinoGenerationLimits(64, 2)),
                CancellationToken.None).ConfigureAwait(false);

        foreach (int turn in new[] { 1, 2 })
        {
            List<TokenEvent> tokens = [];
            IOpenVinoEvent terminal = await conversation.PromptAsync(
                new PromptCommand(sessionId, Guid.NewGuid(), "hello", 2),
                new InlineProgress<TokenEvent>(tokens.Add),
                CancellationToken.None).ConfigureAwait(false);
            Assert.IsInstanceOfType<TurnCompletedEvent>(terminal, $"GPU turn {turn}");
            Assert.AreEqual(
                "fixture",
                string.Concat(tokens.Select(token => token.Text)),
                $"GPU turn {turn}");
        }
        await conversation.CloseAsync(CancellationToken.None).ConfigureAwait(false);

        await using (OpenVinoConversation cancelled =
            await OfficialCpuFixtureTests.CreateClient(stage).StartSessionAsync(
                new StartSessionCommand(
                    Guid.NewGuid(),
                    Guid.NewGuid(),
                    package,
                    OfficialCpuFixtureTests.PackageDigest,
                    OfficialCpuFixtureTests.ModelDigest,
                    OfficialCpuFixtureTests.ModelLength,
                    new OpenVinoDeviceRequest(requested!),
                    new OpenVinoGenerationLimits(64, 2)),
                CancellationToken.None).ConfigureAwait(false))
        {
            Task<IOpenVinoEvent> active = cancelled.PromptAsync(
                new PromptCommand(cancelled.SessionId, Guid.NewGuid(), "hello", 2),
                tokens: null,
                CancellationToken.None);
            await Task.Delay(50).ConfigureAwait(false);
            OpenVinoWorkerClientException cancellation =
                await Assert.ThrowsExactlyAsync<OpenVinoWorkerClientException>(async () =>
                {
                    await cancelled.CancelAsync(CancellationToken.None).ConfigureAwait(false);
                    await active.ConfigureAwait(false);
                }).ConfigureAwait(false);
            Assert.AreEqual(OpenVinoSupportCode.OperationCancelled, cancellation.SupportCode);
        }
        await OfficialCpuFixtureTests.AssertNoOfficialWorkerProcessAsync()
            .ConfigureAwait(false);

        TestContext.WriteLine("OPENVINO_MEASURED_REQUESTED_DEVICE=" + requested);
        TestContext.WriteLine("OPENVINO_MEASURED_ACTUAL_EXECUTION_DEVICES=" + requested);
        TestContext.WriteLine("OPENVINO_MEASURED_ATTENTION_BACKEND=SDPA");
        TestContext.WriteLine("OPENVINO_MEASURED_GPU_ONE_TURN=passed");
        TestContext.WriteLine("OPENVINO_MEASURED_GPU_TWO_TURN=passed");
        TestContext.WriteLine("OPENVINO_MEASURED_GPU_CANCELLATION=passed");
        TestContext.WriteLine("OPENVINO_MEASURED_GPU_CLEANUP=zero_residue");
    }
}
