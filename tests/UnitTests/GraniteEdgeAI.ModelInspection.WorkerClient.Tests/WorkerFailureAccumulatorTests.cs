using Microsoft.VisualStudio.TestTools.UnitTesting;
using GraniteEdgeAI.HardwareInspection.Foundation.Processes;

namespace GraniteEdgeAI.ModelInspection.WorkerClient.Tests;

/// <summary>
/// Specifies that the earliest proven failure remains authoritative while later
/// cleanup problems are retained only as safe secondary diagnostics.
/// </summary>
[TestClass]
public sealed class WorkerFailureAccumulatorTests
{
    [TestMethod]
    public void FirstPrimaryFailureWins()
    {
        WorkerFailureAccumulator accumulator = new();
        WorkerClientFailure first = new(
            WorkerClientFailureCodes.WorkerHandshakeTimeout,
            "The worker did not complete its handshake in time.");
        WorkerClientFailure later = new(
            WorkerClientFailureCodes.WorkerCleanupFailed,
            "Worker cleanup could not be verified.");

        Assert.IsTrue(accumulator.TrySetPrimary(first));
        Assert.IsFalse(accumulator.TrySetPrimary(later));
        Assert.AreSame(first, accumulator.PrimaryFailure);
    }

    [TestMethod]
    public void SecondaryDiagnosticRetainsTypeButNotSensitiveMessage()
    {
        WorkerFailureAccumulator accumulator = new();
        InvalidOperationException cleanupError = new(
            "C:\\Users\\Arian\\private-model.gguf token=supersecret");

        accumulator.AddSecondary(cleanupError);

        Assert.AreEqual(1, accumulator.SecondaryDiagnostics.Count);
        Assert.AreEqual(
            nameof(InvalidOperationException),
            accumulator.SecondaryDiagnostics[0]);
        Assert.IsFalse(
            accumulator.SecondaryDiagnostics[0].Contains(
                "private-model",
                StringComparison.Ordinal));
        Assert.IsFalse(
            accumulator.SecondaryDiagnostics[0].Contains(
                "supersecret",
                StringComparison.Ordinal));
    }

    [TestMethod]
    public void SecondaryDiagnosticsAreExposedAsAnOwnedSnapshot()
    {
        WorkerFailureAccumulator accumulator = new();
        accumulator.AddSecondary(new IOException("first"));

        IReadOnlyList<string> firstSnapshot = accumulator.SecondaryDiagnostics;
        accumulator.AddSecondary(new InvalidOperationException("second"));

        Assert.AreEqual(1, firstSnapshot.Count);
        Assert.AreEqual(2, accumulator.SecondaryDiagnostics.Count);
    }

    [TestMethod]
    public void CleanupIntegrityRetainsEveryBoundedStageWithoutSensitiveText()
    {
        CleanupFailureFact[] facts = Enum.GetValues<OwnedCleanupStage>()
            .Select(stage => new CleanupFailureFact(stage, CleanupFailureKind.Unexpected))
            .ToArray();
        var integrity = new CleanupIntegrityException(
            facts,
            new IOException("C:\\Users\\private\\model.gguf token=secret"));
        var policy = new WorkerClientPolicyException(
            new WorkerClientFailure(
                WorkerClientFailureCodes.WorkerCleanupFailed,
                "The Model Inspection worker cleanup could not be verified."),
            integrity);
        WorkerFailureAccumulator accumulator = new();

        accumulator.RetainCleanupIntegrity(policy);

        Assert.AreSame(policy, accumulator.CleanupIntegrityCause);
        CleanupIntegrityException retained = Assert.IsInstanceOfType<CleanupIntegrityException>(
            accumulator.CleanupIntegrityCause!.InnerException);
        CollectionAssert.AreEqual(facts, retained.Failures.ToArray());
        Assert.IsFalse(policy.Message.Contains("private", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(policy.Message.Contains("secret", StringComparison.OrdinalIgnoreCase));
    }
}
