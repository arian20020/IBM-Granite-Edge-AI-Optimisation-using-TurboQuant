using GraniteEdgeAI.HardwareInspection.Foundation.Processes;
using GraniteEdgeAI.HardwareInspection.Foundation.TrustedTools;

namespace GraniteEdgeAI.HardwareInspection.Foundation.LlmFit;

public sealed class LlmFitHardwareEvidenceProvider : ILlmFitHardwareEvidenceProvider
{
    private static readonly TimeSpan VersionTimeout = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan SystemTimeout = TimeSpan.FromSeconds(15);
    private const int VersionOutputByteLimit = 4 * 1024;
    private const int SystemOutputByteLimit = 256 * 1024;

    private readonly IExternalProcessRunner _processRunner;
    private readonly TimeProvider _timeProvider;

    public LlmFitHardwareEvidenceProvider(
        IExternalProcessRunner processRunner,
        TimeProvider? timeProvider = null)
    {
        _processRunner = processRunner ?? throw new ArgumentNullException(nameof(processRunner));
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    public async Task<LlmFitHardwareEvidence> CaptureAsync(
        VerifiedTrustedTool tool,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(tool);
        cancellationToken.ThrowIfCancellationRequested();

        if (!string.Equals(tool.ToolId, LlmFitCommandContract.ToolId, StringComparison.Ordinal) ||
            !string.Equals(tool.Version, LlmFitCommandContract.Version, StringComparison.Ordinal))
        {
            return Unavailable(LlmFitDiagnosticCode.ToolIdentityMismatch);
        }

        if (!HasExactCommandContract(tool))
        {
            return Unavailable(LlmFitDiagnosticCode.CommandContractMismatch);
        }

        ExternalProcessResult versionResult = await _processRunner.RunAsync(
            tool,
            new ExternalProcessRequest(
                LlmFitCommandContract.VersionCommandIdentity,
                VersionTimeout,
                VersionOutputByteLimit,
                VersionOutputByteLimit),
            cancellationToken).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();

        LlmFitDiagnosticCode? versionFailure = MapVersionFailure(versionResult);
        if (versionFailure.HasValue)
        {
            return Unavailable(versionFailure.Value);
        }

        if (!IsExactVersionOutput(versionResult.StandardOutput))
        {
            return Unavailable(LlmFitDiagnosticCode.VersionOutputMismatch);
        }

        ExternalProcessResult systemResult = await _processRunner.RunAsync(
            tool,
            new ExternalProcessRequest(
                LlmFitCommandContract.SystemCommandIdentity,
                SystemTimeout,
                SystemOutputByteLimit,
                SystemOutputByteLimit),
            cancellationToken).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();

        LlmFitDiagnosticCode? systemFailure = MapSystemFailure(systemResult);
        if (systemFailure.HasValue)
        {
            return Unavailable(systemFailure.Value);
        }

        LlmFitSystemParseResult parsed = LlmFitSystemJsonParser.Parse(systemResult.StandardOutput);
        DateTimeOffset capturedAtUtc = _timeProvider.GetUtcNow().ToUniversalTime();
        if (parsed.State == LlmFitEvidenceState.Available)
        {
            return LlmFitHardwareEvidence.Available(
                LlmFitCommandContract.ToolId,
                LlmFitCommandContract.Version,
                capturedAtUtc,
                parsed.CpuName!,
                parsed.CpuLogicalProcessorCount!.Value,
                parsed.TotalRamGiB!.Value,
                parsed.AvailableRamGiB!.Value,
                parsed.GpuState,
                parsed.Gpus,
                parsed.RawOutputSha256);
        }

        return LlmFitHardwareEvidence.Invalid(
            LlmFitCommandContract.ToolId,
            LlmFitCommandContract.Version,
            capturedAtUtc,
            parsed.CpuName,
            parsed.CpuLogicalProcessorCount,
            parsed.TotalRamGiB,
            parsed.AvailableRamGiB,
            parsed.GpuState,
            parsed.Gpus,
            parsed.RawOutputSha256,
            parsed.Diagnostics);
    }

    private static bool HasExactCommandContract(VerifiedTrustedTool tool)
    {
        if (tool.Commands.Count != 2 ||
            !tool.Commands.TryGetValue(
                LlmFitCommandContract.VersionCommandIdentity,
                out TrustedToolCommand? version) ||
            !tool.Commands.TryGetValue(
                LlmFitCommandContract.SystemCommandIdentity,
                out TrustedToolCommand? system))
        {
            return false;
        }

        return CommandMatches(version, LlmFitCommandContract.CreateVersionCommand()) &&
            CommandMatches(system, LlmFitCommandContract.CreateSystemCommand());
    }

    private static bool CommandMatches(TrustedToolCommand actual, TrustedToolCommand expected) =>
        string.Equals(actual.Identity, expected.Identity, StringComparison.Ordinal) &&
        actual.Arguments.SequenceEqual(expected.Arguments, StringComparer.Ordinal);

    private static bool IsExactVersionOutput(string output) =>
        string.Equals(output, LlmFitCommandContract.ExpectedVersionOutput, StringComparison.Ordinal) ||
        string.Equals(output, $"{LlmFitCommandContract.ExpectedVersionOutput}\n", StringComparison.Ordinal) ||
        string.Equals(output, $"{LlmFitCommandContract.ExpectedVersionOutput}\r\n", StringComparison.Ordinal);

    private static LlmFitDiagnosticCode? MapVersionFailure(ExternalProcessResult result) =>
        result.TerminationReason switch
        {
            ExternalProcessTerminationReason.StartFailed => LlmFitDiagnosticCode.VersionStartFailed,
            ExternalProcessTerminationReason.TimedOut => LlmFitDiagnosticCode.VersionTimedOut,
            ExternalProcessTerminationReason.OutputLimitExceeded => LlmFitDiagnosticCode.VersionOutputLimitExceeded,
            ExternalProcessTerminationReason.CleanupFailed => LlmFitDiagnosticCode.VersionCleanupFailed,
            ExternalProcessTerminationReason.Cancelled => LlmFitDiagnosticCode.VersionCancelledUnexpectedly,
            ExternalProcessTerminationReason.Exited when result.ExitCode != 0 => LlmFitDiagnosticCode.VersionNonZeroExit,
            ExternalProcessTerminationReason.Exited => null,
            _ => LlmFitDiagnosticCode.VersionCleanupFailed,
        };

    private static LlmFitDiagnosticCode? MapSystemFailure(ExternalProcessResult result) =>
        result.TerminationReason switch
        {
            ExternalProcessTerminationReason.StartFailed => LlmFitDiagnosticCode.SystemStartFailed,
            ExternalProcessTerminationReason.TimedOut => LlmFitDiagnosticCode.SystemTimedOut,
            ExternalProcessTerminationReason.OutputLimitExceeded => LlmFitDiagnosticCode.SystemOutputLimitExceeded,
            ExternalProcessTerminationReason.CleanupFailed => LlmFitDiagnosticCode.SystemCleanupFailed,
            ExternalProcessTerminationReason.Cancelled => LlmFitDiagnosticCode.SystemCancelledUnexpectedly,
            ExternalProcessTerminationReason.Exited when result.ExitCode != 0 => LlmFitDiagnosticCode.SystemNonZeroExit,
            ExternalProcessTerminationReason.Exited => null,
            _ => LlmFitDiagnosticCode.SystemCleanupFailed,
        };

    private LlmFitHardwareEvidence Unavailable(LlmFitDiagnosticCode diagnostic) =>
        LlmFitHardwareEvidence.Unavailable(
            LlmFitCommandContract.ToolId,
            LlmFitCommandContract.Version,
            _timeProvider.GetUtcNow().ToUniversalTime(),
            diagnostic);
}
