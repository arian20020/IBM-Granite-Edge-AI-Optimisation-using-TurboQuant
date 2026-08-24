using GraniteEdgeAI.Features.HardwareInspection.Orchestration;
using GraniteEdgeAI.HardwareInspection.Foundation.Dxgi;
using GraniteEdgeAI.HardwareInspection.Foundation.LlamaCpp;
using GraniteEdgeAI.HardwareInspection.Foundation.LlmFit;
using GraniteEdgeAI.HardwareInspection.Foundation.NeuralProcessors;
using GraniteEdgeAI.HardwareInspection.Foundation.TrustedTools;
using GraniteEdgeAI.HardwareInspection.Foundation.Windows;
using GraniteEdgeAI.UnitTests.Features.HardwareInspection.Support;

namespace GraniteEdgeAI.UnitTests.Features.HardwareInspection.Orchestration;

[TestClass]
[TestCategory("HardwareInspection")]
[TestCategory("HardwareInspectionGate7Acceptance")]
public sealed class FoundationHardwareEvidenceCaptureTests
{
    private static readonly DateTimeOffset CapturedAtUtc =
        new(2026, 8, 24, 1, 2, 3, TimeSpan.Zero);

    [TestMethod]
    public async Task CaptureMethods_ForwardOnceWithIdenticalTokensToolsAndEvidence()
    {
        using VerifiedPackagedToolFixture llmFit = VerifiedPackagedToolFixture.CreateLlmFit("success");
        using VerifiedPackagedToolFixture llamaCpp = VerifiedPackagedToolFixture.CreateLlamaCpp("success");
        using CancellationTokenSource cancellation = new();
        CancellationToken token = cancellation.Token;
        WindowsProcessorEvidence processor = WindowsProcessorEvidence.Unavailable(
            WindowsProcessorDiagnosticCode.NativeApiUnavailable, CapturedAtUtc);
        WindowsSystemSnapshot system = new(4, 3, 2, CapturedAtUtc, "Windows", "1", "X64");
        WindowsStorageEvidence storage = WindowsStorageEvidence.Unavailable(
            WindowsStorageDiagnosticCode.NativeApiUnavailable, CapturedAtUtc);
        DxgiGraphicsEvidence graphics = DxgiGraphicsEvidence.Unavailable(
            DxgiGraphicsDiagnosticCode.NativeApiUnavailable, CapturedAtUtc);
        NeuralProcessorEvidence neural = NeuralProcessorEvidence.NotPresent(CapturedAtUtc);
        LlmFitHardwareEvidence llmEvidence = LlmFitHardwareEvidence.Unavailable(
            "llmfit", "1.1.9", CapturedAtUtc, LlmFitDiagnosticCode.VersionStartFailed);
        LlamaCppCapabilityEvidence llamaEvidence = LlamaCppCapabilityEvidence.Unavailable(
            CapturedAtUtc, LlamaCppCapabilityDiagnosticCode.IdentityStartFailed);
        int[] calls = new int[7];

        FoundationHardwareEvidenceCapture capture = new(
            received => { Assert.AreEqual(token, received); calls[0]++; return ValueTask.FromResult(processor); },
            received => { Assert.AreEqual(token, received); calls[1]++; return ValueTask.FromResult(system); },
            received => { Assert.AreEqual(token, received); calls[2]++; return ValueTask.FromResult(storage); },
            received => { Assert.AreEqual(token, received); calls[3]++; return ValueTask.FromResult(graphics); },
            received => { Assert.AreEqual(token, received); calls[4]++; return ValueTask.FromResult(neural); },
            (tool, received) =>
            {
                Assert.AreSame(llmFit.Tool, tool);
                Assert.AreEqual(token, received);
                calls[5]++;
                return Task.FromResult(llmEvidence);
            },
            (tool, received) =>
            {
                Assert.AreSame(llamaCpp.Tool, tool);
                Assert.AreEqual(token, received);
                calls[6]++;
                return Task.FromResult(llamaEvidence);
            });

        Assert.AreSame(processor, await capture.CaptureProcessorAsync(token));
        Assert.AreSame(system, await capture.CaptureSystemAsync(token));
        Assert.AreSame(storage, await capture.CaptureStorageAsync(token));
        Assert.AreSame(graphics, await capture.CaptureGraphicsAsync(token));
        Assert.AreSame(neural, await capture.CaptureNeuralProcessorAsync(token));
        Assert.AreSame(llmEvidence, await capture.CaptureLlmFitAsync(llmFit.Tool, token));
        Assert.AreSame(llamaEvidence, await capture.CaptureLlamaCppAsync(llamaCpp.Tool, token));
        CollectionAssert.AreEqual(new[] { 1, 1, 1, 1, 1, 1, 1 }, calls);
    }

    [TestMethod]
    public async Task Forwarders_DoNotSwallowProviderCancellationOrExceptions()
    {
        using VerifiedPackagedToolFixture llmFit = VerifiedPackagedToolFixture.CreateLlmFit("success");
        using CancellationTokenSource cancellation = new();
        cancellation.Cancel();
        CancellationToken token = cancellation.Token;
        var sentinel = new InvalidOperationException("sentinel");
        FoundationHardwareEvidenceCapture capture = new(
            _ => throw sentinel,
            _ => throw sentinel,
            _ => throw sentinel,
            _ => throw sentinel,
            _ => throw sentinel,
            (_, received) => Task.FromCanceled<LlmFitHardwareEvidence>(received),
            (_, _) => throw sentinel);

        InvalidOperationException observed = Assert.Throws<InvalidOperationException>(
            () => capture.CaptureProcessorAsync(CancellationToken.None));
        Assert.AreSame(sentinel, observed);
        OperationCanceledException cancelled = await Assert.ThrowsAsync<OperationCanceledException>(
            async () => await capture.CaptureLlmFitAsync(llmFit.Tool, token));
        Assert.AreEqual(token, cancelled.CancellationToken);
        InvalidOperationException external = Assert.Throws<InvalidOperationException>(
            () => capture.CaptureLlamaCppAsync(llmFit.Tool, CancellationToken.None));
        Assert.AreSame(sentinel, external);
    }

    [TestMethod]
    public void ProductionConstructor_ComposesWithoutStartingCapture()
    {
        IHardwareEvidenceCapture capture = new FoundationHardwareEvidenceCapture();
        Assert.IsInstanceOfType<FoundationHardwareEvidenceCapture>(capture);
    }
}
