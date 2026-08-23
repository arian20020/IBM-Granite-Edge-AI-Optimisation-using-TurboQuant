using GraniteEdgeAI.HardwareInspection.Foundation.Processes;
using GraniteEdgeAI.HardwareInspection.Foundation.TrustedTools;

namespace GraniteEdgeAI.HardwareInspection.Foundation.LlamaCpp;

public sealed class LlamaCppCapabilityEvidenceProvider : ILlamaCppCapabilityEvidenceProvider
{
    private static readonly TimeSpan IdentityTimeout = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan CapabilitiesTimeout = TimeSpan.FromSeconds(10);
    private const int IdentityOutputByteLimit = 4 * 1024;
    private const int CapabilitiesOutputByteLimit = 64 * 1024;

    private readonly IExternalProcessRunner _processRunner;
    private readonly TimeProvider _timeProvider;

    public LlamaCppCapabilityEvidenceProvider(
        IExternalProcessRunner processRunner,
        TimeProvider? timeProvider = null)
    {
        _processRunner = processRunner ?? throw new ArgumentNullException(nameof(processRunner));
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    public async Task<LlamaCppCapabilityEvidence> CaptureAsync(
        VerifiedTrustedTool tool,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(tool);
        cancellationToken.ThrowIfCancellationRequested();

        if (!string.Equals(tool.ToolId, LlamaCppCapabilityCommandContract.ToolId, StringComparison.Ordinal) ||
            !string.Equals(tool.Version, LlamaCppCapabilityCommandContract.Version, StringComparison.Ordinal))
        {
            return Unavailable(LlamaCppCapabilityDiagnosticCode.ToolIdentityMismatch);
        }

        if (!HasExactCommandContract(tool))
        {
            return Unavailable(LlamaCppCapabilityDiagnosticCode.CommandContractMismatch);
        }

        ExternalProcessResult identityResult = await _processRunner.RunAsync(
            tool,
            new ExternalProcessRequest(
                LlamaCppCapabilityCommandContract.IdentityCommandIdentity,
                IdentityTimeout,
                IdentityOutputByteLimit,
                IdentityOutputByteLimit),
            cancellationToken).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();

        LlamaCppCapabilityDiagnosticCode? identityFailure = MapIdentityFailure(identityResult);
        if (identityFailure.HasValue)
        {
            return Unavailable(identityFailure.Value);
        }

        LlamaCppIdentityParseResult identity =
            LlamaCppCapabilityJsonParser.ParseIdentity(identityResult.StandardOutput);
        if (!identity.IsValid)
        {
            return Unavailable(identity.IsMismatch
                ? LlamaCppCapabilityDiagnosticCode.IdentityMismatch
                : LlamaCppCapabilityDiagnosticCode.IdentityOutputInvalid);
        }

        ExternalProcessResult capabilitiesResult = await _processRunner.RunAsync(
            tool,
            new ExternalProcessRequest(
                LlamaCppCapabilityCommandContract.CapabilitiesCommandIdentity,
                CapabilitiesTimeout,
                CapabilitiesOutputByteLimit,
                CapabilitiesOutputByteLimit),
            cancellationToken).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();

        LlamaCppCapabilityDiagnosticCode? capabilitiesFailure =
            MapCapabilitiesFailure(capabilitiesResult);
        if (capabilitiesFailure.HasValue)
        {
            return Unavailable(capabilitiesFailure.Value);
        }

        LlamaCppCapabilitiesParseResult capabilities =
            LlamaCppCapabilityJsonParser.ParseCapabilities(capabilitiesResult.StandardOutput);
        if (!capabilities.IsValid)
        {
            return Unavailable(LlamaCppCapabilityDiagnosticCode.CapabilityOutputInvalid);
        }

        return LlamaCppCapabilityEvidence.Available(
            identity.RuntimeIdentity!,
            CaptureTimeUtc(),
            capabilities.Backends,
            capabilities.Devices);
    }

    private static bool HasExactCommandContract(VerifiedTrustedTool tool)
    {
        if (tool.Commands.Count != 2 ||
            !tool.Commands.TryGetValue(
                LlamaCppCapabilityCommandContract.IdentityCommandIdentity,
                out TrustedToolCommand? identity) ||
            !tool.Commands.TryGetValue(
                LlamaCppCapabilityCommandContract.CapabilitiesCommandIdentity,
                out TrustedToolCommand? capabilities))
        {
            return false;
        }

        return CommandMatches(identity, LlamaCppCapabilityCommandContract.CreateIdentityCommand()) &&
            CommandMatches(capabilities, LlamaCppCapabilityCommandContract.CreateCapabilitiesCommand());
    }

    private static bool CommandMatches(TrustedToolCommand actual, TrustedToolCommand expected) =>
        string.Equals(actual.Identity, expected.Identity, StringComparison.Ordinal) &&
        actual.Arguments.SequenceEqual(expected.Arguments, StringComparer.Ordinal);

    private static LlamaCppCapabilityDiagnosticCode? MapIdentityFailure(
        ExternalProcessResult result) => result.TerminationReason switch
        {
            ExternalProcessTerminationReason.StartFailed => LlamaCppCapabilityDiagnosticCode.IdentityStartFailed,
            ExternalProcessTerminationReason.TimedOut => LlamaCppCapabilityDiagnosticCode.IdentityTimedOut,
            ExternalProcessTerminationReason.OutputLimitExceeded => LlamaCppCapabilityDiagnosticCode.IdentityOutputLimitExceeded,
            ExternalProcessTerminationReason.CleanupFailed => LlamaCppCapabilityDiagnosticCode.IdentityCleanupFailed,
            ExternalProcessTerminationReason.Cancelled => LlamaCppCapabilityDiagnosticCode.IdentityCancelledUnexpectedly,
            ExternalProcessTerminationReason.Exited when result.ExitCode != 0 => LlamaCppCapabilityDiagnosticCode.IdentityNonZeroExit,
            ExternalProcessTerminationReason.Exited => null,
            _ => LlamaCppCapabilityDiagnosticCode.IdentityCleanupFailed,
        };

    private static LlamaCppCapabilityDiagnosticCode? MapCapabilitiesFailure(
        ExternalProcessResult result) => result.TerminationReason switch
        {
            ExternalProcessTerminationReason.StartFailed => LlamaCppCapabilityDiagnosticCode.CapabilityStartFailed,
            ExternalProcessTerminationReason.TimedOut => LlamaCppCapabilityDiagnosticCode.CapabilityTimedOut,
            ExternalProcessTerminationReason.OutputLimitExceeded => LlamaCppCapabilityDiagnosticCode.CapabilityOutputLimitExceeded,
            ExternalProcessTerminationReason.CleanupFailed => LlamaCppCapabilityDiagnosticCode.CapabilityCleanupFailed,
            ExternalProcessTerminationReason.Cancelled => LlamaCppCapabilityDiagnosticCode.CapabilityCancelledUnexpectedly,
            ExternalProcessTerminationReason.Exited
                when result.ExitCode == LlamaCppCapabilityCommandContract.NativeUnavailableExitCode =>
                    LlamaCppCapabilityDiagnosticCode.NativeCapabilityUnavailable,
            ExternalProcessTerminationReason.Exited when result.ExitCode != 0 =>
                LlamaCppCapabilityDiagnosticCode.CapabilityProcessFailed,
            ExternalProcessTerminationReason.Exited => null,
            _ => LlamaCppCapabilityDiagnosticCode.CapabilityCleanupFailed,
        };

    private LlamaCppCapabilityEvidence Unavailable(LlamaCppCapabilityDiagnosticCode diagnostic) =>
        LlamaCppCapabilityEvidence.Unavailable(CaptureTimeUtc(), diagnostic);

    private DateTimeOffset CaptureTimeUtc() => _timeProvider.GetUtcNow().ToUniversalTime();
}
