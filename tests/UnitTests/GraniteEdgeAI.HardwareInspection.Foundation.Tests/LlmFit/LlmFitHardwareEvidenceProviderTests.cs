using System.Buffers.Binary;
using System.Security.Cryptography;
using GraniteEdgeAI.HardwareInspection.Foundation.LlmFit;
using GraniteEdgeAI.HardwareInspection.Foundation.Processes;
using GraniteEdgeAI.HardwareInspection.Foundation.TrustedTools;

namespace GraniteEdgeAI.HardwareInspection.Foundation.Tests.LlmFit;

[TestClass]
public sealed class LlmFitHardwareEvidenceProviderTests
{
    private static readonly DateTimeOffset LocalCaptureTime =
        new(2026, 8, 22, 22, 0, 0, TimeSpan.FromHours(1));

    [TestMethod]
    public async Task CaptureUsesExactRequestsAndMapsAvailableEvidence()
    {
        using VerifiedProviderFixture fixture = VerifiedProviderFixture.Create();
        string json = ReadFixture("valid-windows-intel.json");
        ScriptedRunner runner = new(
            Exited("llmfit 1.1.9\r\n"),
            Exited(json));
        LlmFitHardwareEvidenceProvider provider = CreateProvider(runner);

        LlmFitHardwareEvidence evidence = await provider.CaptureAsync(
            fixture.Tool,
            CancellationToken.None);

        Assert.AreEqual(LlmFitEvidenceState.Available, evidence.State);
        Assert.AreEqual("Fixture Intel CPU", evidence.CpuName);
        Assert.AreEqual(new DateTimeOffset(2026, 8, 22, 21, 0, 0, TimeSpan.Zero), evidence.CapturedAtUtc);
        Assert.HasCount(2, runner.Requests);
        AssertRequest(runner.Requests[0], "version", TimeSpan.FromSeconds(5), 4096);
        AssertRequest(runner.Requests[1], "system", TimeSpan.FromSeconds(15), 262144);
        Assert.IsFalse(fixture.Tool.IsDisposed);
    }

    [TestMethod]
    public async Task IdentityAndCommandDriftFailBeforeExecution()
    {
        (string ToolId, string Version, TrustedToolCommand[] Commands, LlmFitDiagnosticCode Diagnostic)[] cases =
        [
            ("other", "1.1.9", ExactCommands(), LlmFitDiagnosticCode.ToolIdentityMismatch),
            ("llmfit", "9.9.9", ExactCommands(), LlmFitDiagnosticCode.ToolIdentityMismatch),
            ("llmfit", "1.1.9", [LlmFitCommandContract.CreateVersionCommand()], LlmFitDiagnosticCode.CommandContractMismatch),
            ("llmfit", "1.1.9", [new("version", ["version"]), LlmFitCommandContract.CreateSystemCommand()], LlmFitDiagnosticCode.CommandContractMismatch),
            ("llmfit", "1.1.9", [LlmFitCommandContract.CreateVersionCommand(), new("system", ["--json", "system"])], LlmFitDiagnosticCode.CommandContractMismatch),
            ("llmfit", "1.1.9", [.. ExactCommands(), new("extra", ["--version"])], LlmFitDiagnosticCode.CommandContractMismatch),
        ];

        foreach ((string toolId, string version, TrustedToolCommand[] commands, LlmFitDiagnosticCode diagnostic) in cases)
        {
            using VerifiedProviderFixture fixture = VerifiedProviderFixture.Create(toolId, version, commands);
            ScriptedRunner runner = new();

            LlmFitHardwareEvidence evidence = await CreateProvider(runner).CaptureAsync(
                fixture.Tool,
                CancellationToken.None);

            AssertUnavailable(evidence, diagnostic);
            Assert.HasCount(0, runner.Requests);
        }
    }

    [TestMethod]
    public async Task VersionOutputAllowsOnlyExactIdentityAndFinalLineTerminator()
    {
        string[] accepted = ["llmfit 1.1.9", "llmfit 1.1.9\n", "llmfit 1.1.9\r\n"];
        foreach (string output in accepted)
        {
            using VerifiedProviderFixture fixture = VerifiedProviderFixture.Create();
            ScriptedRunner runner = new(Exited(output), Exited(ReadFixture("valid-cpu-only.json")));

            LlmFitHardwareEvidence evidence = await CreateProvider(runner).CaptureAsync(
                fixture.Tool,
                CancellationToken.None);

            Assert.AreEqual(LlmFitEvidenceState.Available, evidence.State, output);
            Assert.HasCount(2, runner.Requests);
        }

        string[] rejected =
        [
            string.Empty,
            "llmfit 1.1.9 ",
            " llmfit 1.1.9",
            "llmfit 1.1.9\n\n",
            "llmfit 1.1.9\r",
            "llmfit 9.9.9",
        ];
        foreach (string output in rejected)
        {
            using VerifiedProviderFixture fixture = VerifiedProviderFixture.Create();
            ScriptedRunner runner = new(Exited(output));

            LlmFitHardwareEvidence evidence = await CreateProvider(runner).CaptureAsync(
                fixture.Tool,
                CancellationToken.None);

            AssertUnavailable(evidence, LlmFitDiagnosticCode.VersionOutputMismatch);
            Assert.HasCount(1, runner.Requests);
        }
    }

    [TestMethod]
    public async Task VersionProcessFailuresMapToClosedDiagnosticsAndStopSequencing()
    {
        (ExternalProcessResult Result, LlmFitDiagnosticCode Diagnostic)[] cases =
        [
            (Result(ExternalProcessTerminationReason.StartFailed), LlmFitDiagnosticCode.VersionStartFailed),
            (Result(ExternalProcessTerminationReason.TimedOut), LlmFitDiagnosticCode.VersionTimedOut),
            (Result(ExternalProcessTerminationReason.OutputLimitExceeded), LlmFitDiagnosticCode.VersionOutputLimitExceeded),
            (Result(ExternalProcessTerminationReason.CleanupFailed), LlmFitDiagnosticCode.VersionCleanupFailed),
            (Result(ExternalProcessTerminationReason.Cancelled), LlmFitDiagnosticCode.VersionCancelledUnexpectedly),
            (Exited("ignored", exitCode: 7, standardError: "private path and message"), LlmFitDiagnosticCode.VersionNonZeroExit),
        ];

        foreach ((ExternalProcessResult result, LlmFitDiagnosticCode diagnostic) in cases)
        {
            using VerifiedProviderFixture fixture = VerifiedProviderFixture.Create();
            ScriptedRunner runner = new(result);

            LlmFitHardwareEvidence evidence = await CreateProvider(runner).CaptureAsync(
                fixture.Tool,
                CancellationToken.None);

            AssertUnavailable(evidence, diagnostic);
            Assert.HasCount(1, runner.Requests);
        }
    }

    [TestMethod]
    public async Task SystemProcessFailuresMapToClosedDiagnostics()
    {
        (ExternalProcessResult Result, LlmFitDiagnosticCode Diagnostic)[] cases =
        [
            (Result(ExternalProcessTerminationReason.StartFailed), LlmFitDiagnosticCode.SystemStartFailed),
            (Result(ExternalProcessTerminationReason.TimedOut), LlmFitDiagnosticCode.SystemTimedOut),
            (Result(ExternalProcessTerminationReason.OutputLimitExceeded), LlmFitDiagnosticCode.SystemOutputLimitExceeded),
            (Result(ExternalProcessTerminationReason.CleanupFailed), LlmFitDiagnosticCode.SystemCleanupFailed),
            (Result(ExternalProcessTerminationReason.Cancelled), LlmFitDiagnosticCode.SystemCancelledUnexpectedly),
            (Exited("ignored", exitCode: 9, standardError: "private path and message"), LlmFitDiagnosticCode.SystemNonZeroExit),
        ];

        foreach ((ExternalProcessResult result, LlmFitDiagnosticCode diagnostic) in cases)
        {
            using VerifiedProviderFixture fixture = VerifiedProviderFixture.Create();
            ScriptedRunner runner = new(Exited("llmfit 1.1.9"), result);

            LlmFitHardwareEvidence evidence = await CreateProvider(runner).CaptureAsync(
                fixture.Tool,
                CancellationToken.None);

            AssertUnavailable(evidence, diagnostic);
            Assert.HasCount(2, runner.Requests);
        }
    }

    [TestMethod]
    public async Task ParserFailuresMapOnlyValidatedPartialFacts()
    {
        const string json = """
            {
              "system": {
                "total_ram_gb": 32,
                "available_ram_gb": -1,
                "cpu_cores": 8,
                "cpu_name": "Fixture CPU",
                "has_gpu": false,
                "gpu_count": 0,
                "gpus": []
              }
            }
            """;
        using VerifiedProviderFixture fixture = VerifiedProviderFixture.Create();
        ScriptedRunner runner = new(Exited("llmfit 1.1.9"), Exited(json));

        LlmFitHardwareEvidence evidence = await CreateProvider(runner).CaptureAsync(
            fixture.Tool,
            CancellationToken.None);

        Assert.AreEqual(LlmFitEvidenceState.Invalid, evidence.State);
        Assert.AreEqual("Fixture CPU", evidence.CpuName);
        Assert.AreEqual(8, evidence.CpuLogicalProcessorCount);
        Assert.AreEqual(32, evidence.TotalRamGiB);
        Assert.IsNull(evidence.AvailableRamGiB);
        Assert.AreEqual(LlmFitGpuDetectionState.NotReported, evidence.GpuState);
        CollectionAssert.AreEqual(
            new[] { LlmFitDiagnosticCode.RequiredCpuRamInvalid },
            evidence.Diagnostics.ToArray());
        Assert.AreEqual(64, evidence.RawOutputSha256!.Length);
    }

    [TestMethod]
    public async Task CallerCancellationPropagatesWithCallerToken()
    {
        using VerifiedProviderFixture fixture = VerifiedProviderFixture.Create();
        using CancellationTokenSource cancellation = new();
        cancellation.Cancel();
        ScriptedRunner preCancelledRunner = new();

        OperationCanceledException preCancelled = await Assert.ThrowsExactlyAsync<OperationCanceledException>(
            async () => await CreateProvider(preCancelledRunner).CaptureAsync(fixture.Tool, cancellation.Token));
        Assert.AreEqual(cancellation.Token, preCancelled.CancellationToken);
        Assert.HasCount(0, preCancelledRunner.Requests);

        using CancellationTokenSource duringCancellation = new();
        CancellingRunner cancellingRunner = new(duringCancellation);
        OperationCanceledException during = await Assert.ThrowsExactlyAsync<OperationCanceledException>(
            async () => await CreateProvider(cancellingRunner).CaptureAsync(fixture.Tool, duringCancellation.Token));
        Assert.AreEqual(duringCancellation.Token, during.CancellationToken);
    }

    private static LlmFitHardwareEvidenceProvider CreateProvider(IExternalProcessRunner runner) =>
        new(runner, new FixedTimeProvider(LocalCaptureTime));

    private static TrustedToolCommand[] ExactCommands() =>
        [LlmFitCommandContract.CreateVersionCommand(), LlmFitCommandContract.CreateSystemCommand()];

    private static ExternalProcessResult Exited(
        string standardOutput,
        int exitCode = 0,
        string standardError = "") =>
        new(ExternalProcessTerminationReason.Exited, exitCode, standardOutput, standardError, TimeSpan.FromMilliseconds(10));

    private static ExternalProcessResult Result(ExternalProcessTerminationReason reason) =>
        new(reason, null, string.Empty, string.Empty, TimeSpan.FromMilliseconds(10));

    private static void AssertUnavailable(
        LlmFitHardwareEvidence evidence,
        LlmFitDiagnosticCode diagnostic)
    {
        Assert.AreEqual(LlmFitEvidenceState.Unavailable, evidence.State);
        CollectionAssert.AreEqual(new[] { diagnostic }, evidence.Diagnostics.ToArray());
        Assert.IsNull(evidence.RawOutputSha256);
        Assert.IsNull(evidence.CpuName);
    }

    private static void AssertRequest(
        ExternalProcessRequest request,
        string identity,
        TimeSpan timeout,
        int byteLimit)
    {
        Assert.AreEqual(identity, request.CommandIdentity);
        Assert.AreEqual(timeout, request.Timeout);
        Assert.AreEqual(byteLimit, request.StandardOutputByteLimit);
        Assert.AreEqual(byteLimit, request.StandardErrorByteLimit);
    }

    private static string ReadFixture(string name) =>
        File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "LlmFit", name));

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

    private sealed class CancellingRunner(CancellationTokenSource source) : IExternalProcessRunner
    {
        public Task<ExternalProcessResult> RunAsync(
            VerifiedTrustedTool tool,
            ExternalProcessRequest request,
            CancellationToken cancellationToken)
        {
            source.Cancel();
            return Task.FromResult(Result(ExternalProcessTerminationReason.Cancelled));
        }
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
            string toolId = "llmfit",
            string version = "1.1.9",
            TrustedToolCommand[]? commands = null)
        {
            string root = Path.Combine(Path.GetTempPath(), $"hi-provider-{Guid.NewGuid():N}");
            string approvedRoot = Path.Combine(root, "approved");
            string packageRoot = Path.Combine(approvedRoot, "package");
            Directory.CreateDirectory(packageRoot);
            byte[] executable = CreatePeImage();
            File.WriteAllBytes(Path.Combine(packageRoot, "llmfit.exe"), executable);
            File.WriteAllText(Path.Combine(packageRoot, "LICENSE"), "synthetic license fixture");
            File.WriteAllText(Path.Combine(packageRoot, "README.md"), "synthetic readme fixture");
            string hash = Convert.ToHexString(SHA256.HashData(executable)).ToLowerInvariant();
            TrustedToolPackageManifest manifest = new(
                toolId,
                version,
                "llmfit.exe",
                hash,
                ["llmfit.exe", "LICENSE", "README.md"],
                PeMachine.Amd64,
                TrustedToolPackageDisposition.FunctionalPassWithPackagingConcern,
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
