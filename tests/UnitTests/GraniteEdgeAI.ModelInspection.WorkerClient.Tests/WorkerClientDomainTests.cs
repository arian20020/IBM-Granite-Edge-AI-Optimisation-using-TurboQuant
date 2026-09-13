using System.Text.RegularExpressions;
using GraniteEdgeAI.ModelInspection.Contracts;
using GraniteEdgeAI.ModelInspection.WorkerClient;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace GraniteEdgeAI.ModelInspection.WorkerClient.Tests;

/// <summary>
/// Defines the stable, process-independent domain used by every later
/// WorkerClient implementation task.
/// </summary>
[TestClass]
public sealed class WorkerClientDomainTests
{
    private const string ApprovedWorkerRoot = @"C:\Program Files\GraniteEdgeAI";

    /// <summary>
    /// Production defaults must refer directly to the shared protocol values so
    /// the application and worker cannot silently drift apart.
    /// </summary>
    [TestMethod]
    public void DefaultOptionsUseExactWorkerProtocolValues()
    {
        WorkerClientOptions options =
            WorkerClientOptions.CreateDefault(ApprovedWorkerRoot);

        Assert.AreEqual(ApprovedWorkerRoot, options.ApprovedWorkerRoot);
        Assert.AreEqual(WorkerProtocol.StartupTimeout, options.StartupTimeout);
        Assert.AreEqual(WorkerProtocol.OverallTimeout, options.OverallTimeout);
        Assert.AreEqual(
            WorkerProtocol.CancellationGracePeriod,
            options.CancellationGracePeriod);
        Assert.AreEqual(
            WorkerProtocol.MaximumRetainedStandardErrorBytes,
            options.MaximumRetainedStandardErrorBytes);
        Assert.AreEqual(
            TimeSpan.FromSeconds(5),
            options.ProcessTreeCleanupTimeout);

        options.Validate();
    }

    /// <summary>
    /// every timeout and byte limit is a safety boundary and therefore must be
    /// strictly positive
    /// </summary>
    [TestMethod]
    public void OptionsValidationRejectsEveryNonPositiveBoundary()
    {
        WorkerClientOptions valid =
            WorkerClientOptions.CreateDefault(ApprovedWorkerRoot);
        WorkerClientOptions[] invalidOptions =
        [
            valid with { StartupTimeout = TimeSpan.Zero },
            valid with { OverallTimeout = TimeSpan.Zero },
            valid with { CancellationGracePeriod = TimeSpan.Zero },
            valid with { MaximumRetainedStandardErrorBytes = 0 },
            valid with { ProcessTreeCleanupTimeout = TimeSpan.Zero }
        ];

        foreach (WorkerClientOptions invalid in invalidOptions)
        {
            Assert.ThrowsExactly<ArgumentOutOfRangeException>(invalid.Validate);
        }
    }

    /// <summary>
    /// a blank approved root cannot identify a controlled worker installation
    /// </summary>
    [TestMethod]
    public void OptionsValidationRejectsBlankApprovedWorkerRoot()
    {
        WorkerClientOptions options =
            WorkerClientOptions.CreateDefault(ApprovedWorkerRoot) with
            {
                ApprovedWorkerRoot = "   "
            };

        Assert.ThrowsExactly<ArgumentException>(options.Validate);
    }

    /// <summary>
    /// the public failure taxonomy is exact, unique, and machine-readable
    /// </summary>
    [TestMethod]
    public void FailureCodesExposeExactApprovedTaxonomy()
    {
        string[] expected =
        [
            WorkerClientFailureCodes.WorkerExecutableUntrusted,
            WorkerClientFailureCodes.WorkerPackageIntegrityFailed,
            WorkerClientFailureCodes.WorkerArchitectureUnsupported,
            WorkerClientFailureCodes.WorkerLaunchFailed,
            WorkerClientFailureCodes.WorkerContainmentFailed,
            WorkerClientFailureCodes.WorkerHandlePolicyFailed,
            WorkerClientFailureCodes.WorkerEnvironmentPolicyFailed,
            WorkerClientFailureCodes.WorkerHandshakeTimeout,
            WorkerClientFailureCodes.WorkerHandshakeInvalid,
            WorkerClientFailureCodes.WorkerProtocolInvalid,
            WorkerClientFailureCodes.WorkerOutputLimitExceeded,
            WorkerClientFailureCodes.WorkerCrashed,
            WorkerClientFailureCodes.WorkerOverallTimeout,
            WorkerClientFailureCodes.WorkerCancellationForced,
            WorkerClientFailureCodes.WorkerExitMismatch,
            WorkerClientFailureCodes.WorkerProcessTreeIntegrityFailed,
            WorkerClientFailureCodes.WorkerCleanupFailed
        ];

        CollectionAssert.AreEqual(expected, WorkerClientFailureCodes.All.ToArray());
        Assert.AreEqual(
            expected.Length,
            expected.Distinct(StringComparer.Ordinal).Count());

        foreach (string code in expected)
        {
            Assert.IsTrue(
                Regex.IsMatch(code, "^[a-z]+(?:_[a-z]+)*$", RegexOptions.CultureInvariant),
                $"Failure code '{code}' must use lower_snake_case.");
        }
    }

    /// <summary>
    /// Infrastructure failures may only use the approved public taxonomy.
    /// </summary>
    [TestMethod]
    public void FailureValidationRejectsUnknownCode()
    {
        WorkerClientFailure failure = new(
            "unknown_failure",
            "The worker operation failed.");

        Assert.ThrowsExactly<InvalidOperationException>(failure.Validate);
    }

    /// <summary>
    /// failure messages must remain meaningful after paths and protocol content
    /// have been redacted
    /// </summary>
    [TestMethod]
    public void FailureValidationRejectsBlankMessage()
    {
        WorkerClientFailure failure = new(
            WorkerClientFailureCodes.WorkerLaunchFailed,
            "   ");

        Assert.ThrowsExactly<InvalidOperationException>(failure.Validate);
    }

    /// <summary>
    /// a trusted worker terminal and a client-side infrastructure failure cannot
    /// both describe the same execution
    /// </summary>
    [TestMethod]
    public void ResultRejectsTerminalAndInfrastructureFailureTogether()
    {
        WorkerClientResult result = new(
            TerminalMessage: CreateCancelledTerminal(),
            Failure: new WorkerClientFailure(
                WorkerClientFailureCodes.WorkerCrashed,
                "Worker ended unexpectedly."),
            ExitCode: 3,
            ForcedTermination: false,
            StandardErrorTruncated: false,
            RetainedStandardError: string.Empty,
            SecondaryDiagnostics: []);

        Assert.ThrowsExactly<InvalidOperationException>(result.Validate);
    }

    /// <summary>
    /// an execution must end with exactly one trustworthy outcome description
    /// </summary>
    [TestMethod]
    public void ResultRejectsMissingTerminalAndInfrastructureFailure()
    {
        WorkerClientResult result = new(
            TerminalMessage: null,
            Failure: null,
            ExitCode: null,
            ForcedTermination: false,
            StandardErrorTruncated: false,
            RetainedStandardError: string.Empty,
            SecondaryDiagnostics: []);

        Assert.ThrowsExactly<InvalidOperationException>(result.Validate);
    }

    /// <summary>
    /// once the client forcibly terminates the process tree, any terminal output
    /// is no longer accepted as a trusted worker result
    /// </summary>
    [TestMethod]
    public void ResultRejectsForcedTerminationWithTrustedTerminal()
    {
        WorkerClientResult result = new(
            TerminalMessage: CreateCancelledTerminal(),
            Failure: null,
            ExitCode: 3,
            ForcedTermination: true,
            StandardErrorTruncated: false,
            RetainedStandardError: string.Empty,
            SecondaryDiagnostics: []);

        Assert.ThrowsExactly<InvalidOperationException>(result.Validate);
    }

    /// <summary>
    /// a valid cooperative cancellation remains a trusted terminal result
    /// </summary>
    [TestMethod]
    public void ResultAcceptsTrustedCancelledTerminal()
    {
        WorkerClientResult result = new(
            TerminalMessage: CreateCancelledTerminal(),
            Failure: null,
            ExitCode: 3,
            ForcedTermination: false,
            StandardErrorTruncated: false,
            RetainedStandardError: string.Empty,
            SecondaryDiagnostics: []);

        result.Validate();
    }

    /// <summary>
    /// the result owns an immutable snapshot of secondary diagnostics rather
    /// than exposing the caller's mutable collection
    /// </summary>
    [TestMethod]
    public void ResultAcceptsInfrastructureFailureAndCopiesDiagnostics()
    {
        string[] suppliedDiagnostics = ["cleanup completed after primary failure"];
        WorkerClientCleanupFailureFact[] suppliedCleanupFailures =
        [
            new(
                WorkerClientCleanupStage.ProcessTree,
                WorkerClientCleanupFailureKind.Timeout),
        ];
        WorkerClientResult result = new(
            TerminalMessage: null,
            Failure: new WorkerClientFailure(
                WorkerClientFailureCodes.WorkerLaunchFailed,
                "Worker could not be started."),
            ExitCode: null,
            ForcedTermination: false,
            StandardErrorTruncated: false,
            RetainedStandardError: string.Empty,
            SecondaryDiagnostics: suppliedDiagnostics,
            CleanupFailures: suppliedCleanupFailures);

        suppliedDiagnostics[0] = "mutated by caller";
        suppliedCleanupFailures[0] = new(
            WorkerClientCleanupStage.Job,
            WorkerClientCleanupFailureKind.Unexpected);
        result.Validate();

        Assert.AreEqual(
            "cleanup completed after primary failure",
            result.SecondaryDiagnostics[0]);
        Assert.AreEqual(
            new WorkerClientCleanupFailureFact(
                WorkerClientCleanupStage.ProcessTree,
                WorkerClientCleanupFailureKind.Timeout),
            result.CleanupFailures[0]);
    }

    /// <summary>
    /// The lifecycle vocabulary is explicit and ordered for later state-machine
    /// enforcement without exposing Process or native-handle objects.
    /// </summary>
    [TestMethod]
    public void LifecycleStateExposesExactApprovedSequence()
    {
        string[] expected =
        [
            nameof(WorkerLifecycleState.NotStarted),
            nameof(WorkerLifecycleState.ResolvingExecutable),
            nameof(WorkerLifecycleState.CreatingContainment),
            nameof(WorkerLifecycleState.Starting),
            nameof(WorkerLifecycleState.AwaitingHello),
            nameof(WorkerLifecycleState.Ready),
            nameof(WorkerLifecycleState.StartSent),
            nameof(WorkerLifecycleState.Running),
            nameof(WorkerLifecycleState.CancellationRequested),
            nameof(WorkerLifecycleState.TerminalReceived),
            nameof(WorkerLifecycleState.Exited),
            nameof(WorkerLifecycleState.CleaningUp),
            nameof(WorkerLifecycleState.Completed),
            nameof(WorkerLifecycleState.Failed),
            nameof(WorkerLifecycleState.Disposed)
        ];

        CollectionAssert.AreEqual(expected, Enum.GetNames<WorkerLifecycleState>());
    }

    /// <summary>
    /// the policy exception factory creates one validated stable failure and
    /// preserves a safe public message
    /// </summary>
    [TestMethod]
    public void PolicyExceptionFactoryCreatesValidatedFailure()
    {
        WorkerClientPolicyException error = WorkerClientPolicyException.For(
            WorkerClientFailureCodes.WorkerHandshakeInvalid,
            "Worker handshake was invalid.");

        Assert.AreEqual(
            WorkerClientFailureCodes.WorkerHandshakeInvalid,
            error.Failure.Code);
        Assert.AreEqual("Worker handshake was invalid.", error.Message);
        error.Failure.Validate();
    }

    /// <summary>
    /// creates the smallest valid terminal message needed by domain tests
    /// </summary>
    private static WorkerCompletedMessage CreateCancelledTerminal()
    {
        return new WorkerCompletedMessage
        {
            ProtocolVersion = WorkerProtocol.Version,
            MessageType = WorkerMessageKind.Completed,
            RequestId = Guid.NewGuid(),
            CompletionStatus = WorkerCompletionStatus.Cancelled
        };
    }
}
