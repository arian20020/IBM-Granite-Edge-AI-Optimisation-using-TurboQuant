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
    public IReadOnlyList<string> DiagnosticCodes { get; init; } =
        new ReadOnlyCollection<string>(DiagnosticCodes.ToArray());

    public bool MayExecuteForGate1 =>
        IntegrityPassed && string.Equals(PeMachine, "AMD64", StringComparison.Ordinal);
}
