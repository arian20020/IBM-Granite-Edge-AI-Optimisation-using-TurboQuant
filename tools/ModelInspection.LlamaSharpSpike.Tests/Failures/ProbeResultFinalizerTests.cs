using GraniteEdgeAI.ModelInspection.LlamaSharp.ModelProbe;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace GraniteEdgeAI.Tools.ModelInspection.LlamaSharpSpike.Tests;

/// <summary>
/// Verifies that file-integrity failures cannot be hidden by success or
/// cancellation while existing runtime failures retain their original cause.
/// </summary>
[TestClass]
[TestCategory("Deterministic")]
public sealed class ProbeResultFinalizerTests
{
    [TestMethod]
    public void Resolve_WhenSucceededAndIntegrityPreserved_ReturnsSucceeded()
    {
        ProbeCompletionResolution result = ProbeResultFinalizer.Resolve(
            VocabOnlyProbeCompletionStatus.Succeeded,
            failure: null,
            PreservedIntegrity(),
            integrityErrorType: null,
            integrityErrorMessage: null);

        Assert.AreEqual(
            VocabOnlyProbeCompletionStatus.Succeeded,
            result.Status);
        Assert.IsNull(result.Failure);
    }

    [TestMethod]
    public void Resolve_WhenSucceededAndIntegrityChanged_ReturnsIntegrityChanged()
    {
        AssertIntegrityChanged(
            VocabOnlyProbeCompletionStatus.Succeeded,
            failure: null);
    }

    [TestMethod]
    public void Resolve_WhenSucceededAndIntegrityMissing_ReturnsVerificationFailed()
    {
        AssertIntegrityVerificationFailed(
            VocabOnlyProbeCompletionStatus.Succeeded,
            failure: null);
    }

    [TestMethod]
    public void Resolve_WhenCancelledAndIntegrityPreserved_ReturnsCancelled()
    {
        ProbeFailure cancellation = CancellationFailure();

        ProbeCompletionResolution result = ProbeResultFinalizer.Resolve(
            VocabOnlyProbeCompletionStatus.Cancelled,
            cancellation,
            PreservedIntegrity(),
            integrityErrorType: null,
            integrityErrorMessage: null);

        Assert.AreEqual(
            VocabOnlyProbeCompletionStatus.Cancelled,
            result.Status);
        Assert.AreSame(cancellation, result.Failure);
    }

    [TestMethod]
    public void Resolve_WhenCancelledAndIntegrityChanged_PrioritisesIntegrityFailure()
    {
        AssertIntegrityChanged(
            VocabOnlyProbeCompletionStatus.Cancelled,
            CancellationFailure());
    }

    [TestMethod]
    public void Resolve_WhenCancelledAndIntegrityMissing_PrioritisesVerificationFailure()
    {
        AssertIntegrityVerificationFailed(
            VocabOnlyProbeCompletionStatus.Cancelled,
            CancellationFailure());
    }

    [TestMethod]
    public void Resolve_WhenAlreadyFailedAndIntegrityChanged_KeepsOriginalFailure()
    {
        var original = new ProbeFailure(
            "MI-OP-RUNTIME-UNAVAILABLE",
            typeof(DllNotFoundException).FullName,
            "missing runtime");

        ProbeCompletionResolution result = ProbeResultFinalizer.Resolve(
            VocabOnlyProbeCompletionStatus.Failed,
            original,
            ChangedIntegrity(),
            integrityErrorType: null,
            integrityErrorMessage: null);

        Assert.AreEqual(
            VocabOnlyProbeCompletionStatus.Failed,
            result.Status);
        Assert.AreSame(original, result.Failure);
    }

    [TestMethod]
    public void Resolve_WhenAlreadyFailedAndIntegrityMissing_KeepsOriginalFailure()
    {
        var original = new ProbeFailure(
            "MI-PROBE-MODEL-LOAD-FAILED",
            typeof(InvalidOperationException).FullName,
            "load failed");

        ProbeCompletionResolution result = ProbeResultFinalizer.Resolve(
            VocabOnlyProbeCompletionStatus.Failed,
            original,
            integrity: null,
            integrityErrorType: typeof(IOException).FullName,
            integrityErrorMessage: "verification failed");

        Assert.AreSame(original, result.Failure);
    }

    private static void AssertIntegrityChanged(
        VocabOnlyProbeCompletionStatus status,
        ProbeFailure? failure)
    {
        ProbeCompletionResolution result = ProbeResultFinalizer.Resolve(
            status,
            failure,
            ChangedIntegrity(),
            integrityErrorType: null,
            integrityErrorMessage: null);

        Assert.AreEqual(
            VocabOnlyProbeCompletionStatus.Failed,
            result.Status);
        Assert.IsNotNull(result.Failure);
        Assert.AreEqual(
            "MI-OP-MODEL-INTEGRITY-CHANGED",
            result.Failure.Code);
        Assert.AreEqual(typeof(IOException).FullName, result.Failure.Type);
    }

    private static void AssertIntegrityVerificationFailed(
        VocabOnlyProbeCompletionStatus status,
        ProbeFailure? failure)
    {
        ProbeCompletionResolution result = ProbeResultFinalizer.Resolve(
            status,
            failure,
            integrity: null,
            integrityErrorType: typeof(IOException).FullName,
            integrityErrorMessage: "verification failed");

        Assert.AreEqual(
            VocabOnlyProbeCompletionStatus.Failed,
            result.Status);
        Assert.IsNotNull(result.Failure);
        Assert.AreEqual(
            "MI-OP-MODEL-INTEGRITY-VERIFICATION-FAILED",
            result.Failure.Code);
        Assert.AreEqual("verification failed", result.Failure.Message);
    }

    private static ProbeFailure CancellationFailure()
    {
        return new ProbeFailure(
            "MI-PROBE-CANCELLED",
            typeof(OperationCanceledException).FullName,
            "cancelled");
    }

    private static ModelFileIntegrityComparison PreservedIntegrity()
    {
        return new ModelFileIntegrityComparison
        {
            PathUnchanged = true,
            LengthUnchanged = true,
            LastWriteTimeUnchanged = true,
            Sha256Unchanged = true
        };
    }

    private static ModelFileIntegrityComparison ChangedIntegrity()
    {
        return new ModelFileIntegrityComparison
        {
            PathUnchanged = true,
            LengthUnchanged = true,
            LastWriteTimeUnchanged = true,
            Sha256Unchanged = false
        };
    }
}
