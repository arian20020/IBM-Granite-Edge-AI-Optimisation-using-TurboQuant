using System.Collections.ObjectModel;

namespace HardwareInspection.LlmFitSpike.Candidate;

public sealed record LlmFitCandidateVerification(
    bool IntegrityPassed,
    string? ArchiveSha256,
    string? ExecutableSha256,
    string? PeMachine,
    bool AuthenticodePresent,
    string AuthenticodeStatus,
    string? AuthenticodeSubject,
    IReadOnlyList<string> DiagnosticCodes)
{
    private readonly IReadOnlyList<string> _diagnosticCodes = CopyDiagnosticCodes(DiagnosticCodes);

    public IReadOnlyList<string> DiagnosticCodes
    {
        get => _diagnosticCodes;
        init => _diagnosticCodes = CopyDiagnosticCodes(value);
    }

    public bool MayExecuteForGate1 =>
        IntegrityPassed && string.Equals(PeMachine, "AMD64", StringComparison.Ordinal);

    private static ReadOnlyCollection<string> CopyDiagnosticCodes(IReadOnlyList<string> diagnosticCodes)
    {
        ArgumentNullException.ThrowIfNull(diagnosticCodes);
        return new ReadOnlyCollection<string>(diagnosticCodes.ToArray());
    }
}
