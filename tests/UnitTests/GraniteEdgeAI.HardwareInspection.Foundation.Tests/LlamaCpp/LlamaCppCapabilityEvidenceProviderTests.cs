using System.Buffers.Binary;
using System.Security.Cryptography;
using GraniteEdgeAI.HardwareInspection.Foundation.LlamaCpp;
using GraniteEdgeAI.HardwareInspection.Foundation.Processes;
using GraniteEdgeAI.HardwareInspection.Foundation.TrustedTools;

namespace GraniteEdgeAI.HardwareInspection.Foundation.Tests.LlamaCpp;

[TestClass]
public sealed class LlamaCppCapabilityEvidenceProviderTests
{
    private const string ValidIdentity =
        "{\"schemaVersion\":1," +
        "\"probeIdentity\":\"granite-edge-hardware-llamacpp-capabilities/1\"," +
        "\"managedPackage\":\"LLamaSharp\"," +
        "\"managedVersion\":\"0.27.0\"," +
        "\"backendPackage\":\"LLamaSharp.Backend.Cpu\"," +
        "\"backendVersion\":\"0.27.0\"," +
        "\"llamaSharpCommit\":\"7cbbc45e421d55794d5050d126e0b96511007007\"," +
        "\"mappedLlamaCppCommit\":\"3f7c29d318e317b63f54c558bc69803963d7d88c\"," +
        "\"runtimeIdentifier\":\"win-x64\"}\n";

    private const string ValidCapabilities =
        "{\"schemaVersion\":1," +
        "\"probeIdentity\":\"granite-edge-hardware-llamacpp-capabilities/1\"," +
        "\"backends\":[\"cpu\"]," +
        "\"devices\":[{\"ordinal\":0,\"bufferType\":\"CPU\"}]}\n";

    private static readonly DateTimeOffset LocalTime =
        new(2026, 8, 23, 13, 0, 0, TimeSpan.FromHours(1));

    [TestMethod]
    public async Task CaptureSequencesExactRequestsAndReturnsOnlyValidatedEvidence()
    {
        using VerifiedProviderFixture fixture = VerifiedProviderFixture.Create();
        ScriptedRunner runner = new(Exited(ValidIdentity), Exited(ValidCapabilities));

        LlamaCppCapabilityEvidence evidence = await CreateProvider(runner).CaptureAsync(
            fixture.Tool,
            CancellationToken.None);

        Assert.AreEqual(LlamaCppCapabilityEvidenceState.Available, evidence.State);
        Assert.AreSame(LlamaCppRuntimeIdentity.PinnedCpu, evidence.RuntimeIdentity);
        Assert.AreEqual(LlamaCppBackend.Cpu, evidence.Backends.Single());
        Assert.AreEqual(new LlamaCppVisibleDevice(0, "CPU"), evidence.VisibleDevices.Single());
        Assert.AreEqual(new DateTimeOffset(2026, 8, 23, 12, 0, 0, TimeSpan.Zero), evidence.CapturedAtUtc);
        Assert.HasCount(2, runner.Requests);
        AssertRequest(runner.Requests[0], "identity", TimeSpan.FromSeconds(5), 4 * 1024);
        AssertRequest(runner.Requests[1], "capabilities", TimeSpan.FromSeconds(10), 64 * 1024);
        Assert.IsFalse(fixture.Tool.IsDisposed);
    }

    [TestMethod]
    public async Task ToolAndCommandDriftFailBeforeProcessExecution()
    {
        (string ToolId, string Version, TrustedToolCommand[] Commands, LlamaCppCapabilityDiagnosticCode Diagnostic)[] cases =
        [
            ("other", LlamaCppCapabilityCommandContract.Version, ExactCommands(), LlamaCppCapabilityDiagnosticCode.ToolIdentityMismatch),
            (LlamaCppCapabilityCommandContract.ToolId, "other", ExactCommands(), LlamaCppCapabilityDiagnosticCode.ToolIdentityMismatch),
            (LlamaCppCapabilityCommandContract.ToolId, LlamaCppCapabilityCommandContract.Version, [LlamaCppCapabilityCommandContract.CreateIdentityCommand()], LlamaCppCapabilityDiagnosticCode.CommandContractMismatch),
            (LlamaCppCapabilityCommandContract.ToolId, LlamaCppCapabilityCommandContract.Version, [new("identity", ["identity"]), LlamaCppCapabilityCommandContract.CreateCapabilitiesCommand()], LlamaCppCapabilityDiagnosticCode.CommandContractMismatch),
            (LlamaCppCapabilityCommandContract.ToolId, LlamaCppCapabilityCommandContract.Version, [.. ExactCommands(), new("extra", ["extra"])], LlamaCppCapabilityDiagnosticCode.CommandContractMismatch),
        ];

        foreach ((string toolId, string version, TrustedToolCommand[] commands, LlamaCppCapabilityDiagnosticCode diagnostic) in cases)
        {
            using VerifiedProviderFixture fixture = VerifiedProviderFixture.Create(toolId, version, commands);
            ScriptedRunner runner = new();

            LlamaCppCapabilityEvidence evidence = await CreateProvider(runner).CaptureAsync(
                fixture.Tool,
                CancellationToken.None);

            AssertUnavailable(evidence, diagnostic);
            Assert.HasCount(0, runner.Requests);
        }
    }

    [TestMethod]
    public async Task IdentityFailuresMapClosedDiagnosticsAndStopSequencing()
    {
        string mismatch = ValidIdentity.Replace("\"managedVersion\":\"0.27.0\"", "\"managedVersion\":\"9.9.9\"", StringComparison.Ordinal);
        (ExternalProcessResult Result, LlamaCppCapabilityDiagnosticCode Diagnostic)[] cases =
        [
            (Result(ExternalProcessTerminationReason.StartFailed), LlamaCppCapabilityDiagnosticCode.IdentityStartFailed),
            (Result(ExternalProcessTerminationReason.TimedOut), LlamaCppCapabilityDiagnosticCode.IdentityTimedOut),
            (Result(ExternalProcessTerminationReason.OutputLimitExceeded), LlamaCppCapabilityDiagnosticCode.IdentityOutputLimitExceeded),
            (Result(ExternalProcessTerminationReason.CleanupFailed), LlamaCppCapabilityDiagnosticCode.IdentityCleanupFailed),
            (Result(ExternalProcessTerminationReason.Cancelled), LlamaCppCapabilityDiagnosticCode.IdentityCancelledUnexpectedly),
            (Exited("private output", 9, "private error"), LlamaCppCapabilityDiagnosticCode.IdentityNonZeroExit),
            (Exited("{]\n"), LlamaCppCapabilityDiagnosticCode.IdentityOutputInvalid),
            (Exited(mismatch), LlamaCppCapabilityDiagnosticCode.IdentityMismatch),
        ];

        foreach ((ExternalProcessResult result, LlamaCppCapabilityDiagnosticCode diagnostic) in cases)
        {
            using VerifiedProviderFixture fixture = VerifiedProviderFixture.Create();
            ScriptedRunner runner = new(result);

            LlamaCppCapabilityEvidence evidence = await CreateProvider(runner).CaptureAsync(
                fixture.Tool,
                CancellationToken.None);

            AssertUnavailable(evidence, diagnostic);
            Assert.HasCount(1, runner.Requests);
        }
    }

    [TestMethod]
    public async Task CapabilityFailuresMapClosedDiagnosticsWithoutRetainingOutput()
    {
        (ExternalProcessResult Result, LlamaCppCapabilityDiagnosticCode Diagnostic)[] cases =
        [
            (Result(ExternalProcessTerminationReason.StartFailed), LlamaCppCapabilityDiagnosticCode.CapabilityStartFailed),
            (Result(ExternalProcessTerminationReason.TimedOut), LlamaCppCapabilityDiagnosticCode.CapabilityTimedOut),
            (Result(ExternalProcessTerminationReason.OutputLimitExceeded), LlamaCppCapabilityDiagnosticCode.CapabilityOutputLimitExceeded),
            (Result(ExternalProcessTerminationReason.CleanupFailed), LlamaCppCapabilityDiagnosticCode.CapabilityCleanupFailed),
            (Result(ExternalProcessTerminationReason.Cancelled), LlamaCppCapabilityDiagnosticCode.CapabilityCancelledUnexpectedly),
            (Exited("private output", LlamaCppCapabilityCommandContract.NativeUnavailableExitCode, "private error"), LlamaCppCapabilityDiagnosticCode.NativeCapabilityUnavailable),
            (Exited("private output", 9, "private error"), LlamaCppCapabilityDiagnosticCode.CapabilityProcessFailed),
            (Exited("{]\n"), LlamaCppCapabilityDiagnosticCode.CapabilityOutputInvalid),
        ];

        foreach ((ExternalProcessResult result, LlamaCppCapabilityDiagnosticCode diagnostic) in cases)
        {
            using VerifiedProviderFixture fixture = VerifiedProviderFixture.Create();
            ScriptedRunner runner = new(Exited(ValidIdentity), result);

            LlamaCppCapabilityEvidence evidence = await CreateProvider(runner).CaptureAsync(
                fixture.Tool,
                CancellationToken.None);

            AssertUnavailable(evidence, diagnostic);
            Assert.HasCount(2, runner.Requests);
        }
    }

    [TestMethod]
    public async Task CallerCancellationPropagatesWithTheCallerTokenAtEachAwaitBoundary()
    {
        using VerifiedProviderFixture fixture = VerifiedProviderFixture.Create();
        using CancellationTokenSource preCancelledSource = new();
        preCancelledSource.Cancel();
        ScriptedRunner unusedRunner = new();

        OperationCanceledException before = await Assert.ThrowsExactlyAsync<OperationCanceledException>(
            async () => await CreateProvider(unusedRunner).CaptureAsync(fixture.Tool, preCancelledSource.Token));
        Assert.AreEqual(preCancelledSource.Token, before.CancellationToken);
        Assert.HasCount(0, unusedRunner.Requests);

        foreach (int cancelOnCall in new[] { 1, 2 })
        {
            using CancellationTokenSource source = new();
            CancellingRunner runner = new(source, cancelOnCall);
            OperationCanceledException during = await Assert.ThrowsExactlyAsync<OperationCanceledException>(
                async () => await CreateProvider(runner).CaptureAsync(fixture.Tool, source.Token));
            Assert.AreEqual(source.Token, during.CancellationToken);
            Assert.HasCount(cancelOnCall, runner.Requests);
        }
    }

    [TestMethod]
    public async Task UnexpectedRunnerExceptionRemainsVisibleAndDoesNotDisposeCallerTool()
    {
        using VerifiedProviderFixture fixture = VerifiedProviderFixture.Create();
        var expected = new InvalidOperationException("programming failure");
        ThrowingRunner runner = new(expected);

        InvalidOperationException actual = await Assert.ThrowsExactlyAsync<InvalidOperationException>(
            async () => await CreateProvider(runner).CaptureAsync(fixture.Tool, CancellationToken.None));

        Assert.AreSame(expected, actual);
        Assert.IsFalse(fixture.Tool.IsDisposed);
    }

    private static LlamaCppCapabilityEvidenceProvider CreateProvider(IExternalProcessRunner runner) =>
        new(runner, new FixedTimeProvider(LocalTime));

    private static TrustedToolCommand[] ExactCommands() =>
        [LlamaCppCapabilityCommandContract.CreateIdentityCommand(), LlamaCppCapabilityCommandContract.CreateCapabilitiesCommand()];

    private static ExternalProcessResult Exited(string stdout, int exitCode = 0, string stderr = "") =>
        new(ExternalProcessTerminationReason.Exited, exitCode, stdout, stderr, TimeSpan.FromMilliseconds(10));

    private static ExternalProcessResult Result(ExternalProcessTerminationReason reason) =>
        new(reason, null, string.Empty, string.Empty, TimeSpan.FromMilliseconds(10));

    private static void AssertRequest(ExternalProcessRequest request, string identity, TimeSpan timeout, int limit)
    {
        Assert.AreEqual(identity, request.CommandIdentity);
        Assert.AreEqual(timeout, request.Timeout);
        Assert.AreEqual(limit, request.StandardOutputByteLimit);
        Assert.AreEqual(limit, request.StandardErrorByteLimit);
    }

    private static void AssertUnavailable(
        LlamaCppCapabilityEvidence evidence,
        LlamaCppCapabilityDiagnosticCode diagnostic)
    {
        Assert.AreEqual(LlamaCppCapabilityEvidenceState.Unavailable, evidence.State);
        Assert.AreEqual(diagnostic, evidence.Diagnostic);
        Assert.IsNull(evidence.RuntimeIdentity);
        Assert.IsEmpty(evidence.Backends);
        Assert.IsEmpty(evidence.VisibleDevices);
    }

    private sealed class ScriptedRunner(params ExternalProcessResult[] results) : IExternalProcessRunner
    {
        private readonly Queue<ExternalProcessResult> _results = new(results);

        internal List<ExternalProcessRequest> Requests { get; } = [];

        public Task<ExternalProcessResult> RunAsync(
            VerifiedTrustedTool tool,
            ExternalProcessRequest request,
            CancellationToken cancellationToken)
        {
            Requests.Add(request);
            return Task.FromResult(_results.Dequeue());
        }
    }

    private sealed class CancellingRunner(CancellationTokenSource source, int cancelOnCall) : IExternalProcessRunner
    {
        internal List<ExternalProcessRequest> Requests { get; } = [];

        public Task<ExternalProcessResult> RunAsync(
            VerifiedTrustedTool tool,
            ExternalProcessRequest request,
            CancellationToken cancellationToken)
        {
            Requests.Add(request);
            if (Requests.Count == cancelOnCall)
            {
                source.Cancel();
                return Task.FromResult(Result(ExternalProcessTerminationReason.Cancelled));
            }

            return Task.FromResult(Exited(ValidIdentity));
        }
    }

    private sealed class ThrowingRunner(Exception error) : IExternalProcessRunner
    {
        public Task<ExternalProcessResult> RunAsync(
            VerifiedTrustedTool tool,
            ExternalProcessRequest request,
            CancellationToken cancellationToken) => Task.FromException<ExternalProcessResult>(error);
    }

    private sealed class FixedTimeProvider(DateTimeOffset value) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => value;
    }

    private sealed class VerifiedProviderFixture : IDisposable
    {
        private VerifiedProviderFixture(string root, VerifiedTrustedTool tool)
        {
            Root = root;
            Tool = tool;
        }

        private string Root { get; }

        internal VerifiedTrustedTool Tool { get; }

        internal static VerifiedProviderFixture Create(
            string toolId = "granite-edge-hardware-llamacpp-probe",
            string version = "0.27.0-cpu-win-x64",
            TrustedToolCommand[]? commands = null)
        {
            string root = Path.Combine(Path.GetTempPath(), $"hi-llamacpp-provider-{Guid.NewGuid():N}");
            string approvedRoot = Path.Combine(root, "approved");
            string packageRoot = Path.Combine(approvedRoot, "package");
            Directory.CreateDirectory(packageRoot);
            byte[] executable = CreatePeImage();
            const string executableName = "GraniteEdgeAI.HardwareInspection.LlamaCppProbe.exe";
            File.WriteAllBytes(Path.Combine(packageRoot, executableName), executable);
            string hash = Convert.ToHexString(SHA256.HashData(executable)).ToLowerInvariant();
            TrustedToolPackageManifest manifest = new(
                toolId,
                version,
                executableName,
                hash,
                [executableName],
                PeMachine.Amd64,
                TrustedToolPackageDisposition.AcceptedForFunctionalEvaluation,
                commands ?? ExactCommands());
            TrustedToolVerificationResult verification = new TrustedToolPackageVerifier().Verify(
                approvedRoot,
                packageRoot,
                manifest);
            Assert.IsTrue(verification.IsVerified);
            return new(root, verification.Tool!);
        }

        public void Dispose()
        {
            Tool.Dispose();
            Directory.Delete(Root, recursive: true);
        }

        private static byte[] CreatePeImage()
        {
            byte[] bytes = new byte[0x98];
            bytes[0] = (byte)'M';
            bytes[1] = (byte)'Z';
            BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(0x3c, 4), 0x80);
            bytes[0x80] = (byte)'P';
            bytes[0x81] = (byte)'E';
            BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(0x84, 2), 0x8664);
            return bytes;
        }
    }
}
