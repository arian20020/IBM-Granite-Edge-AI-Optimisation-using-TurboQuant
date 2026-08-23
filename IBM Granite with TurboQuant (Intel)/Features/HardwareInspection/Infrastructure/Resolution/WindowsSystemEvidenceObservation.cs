using GraniteEdgeAI.HardwareInspection.Foundation.Windows;
using System;

namespace GraniteEdgeAI.Features.HardwareInspection.Resolution;

internal enum WindowsSystemObservationState
{
    Available,
    Unavailable,
}

internal enum WindowsSystemObservationDiagnosticCode
{
    MemoryUnavailable,
    MemoryOverflow,
    MemoryInconsistent,
    OperatingSystemUnavailable,
}

internal sealed class WindowsSystemEvidenceObservation
{
    private WindowsSystemEvidenceObservation(
        WindowsSystemObservationState state,
        WindowsSystemSnapshot? snapshot,
        DateTimeOffset attemptedAtUtc,
        WindowsSystemObservationDiagnosticCode? diagnostic)
    {
        State = state;
        Snapshot = snapshot;
        AttemptedAtUtc = attemptedAtUtc;
        Diagnostic = diagnostic;
    }

    internal WindowsSystemObservationState State { get; }

    internal WindowsSystemSnapshot? Snapshot { get; }

    internal DateTimeOffset AttemptedAtUtc { get; }

    internal WindowsSystemObservationDiagnosticCode? Diagnostic { get; }

    internal static WindowsSystemEvidenceObservation Available(WindowsSystemSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        return new(
            WindowsSystemObservationState.Available,
            snapshot,
            snapshot.CapturedAtUtc,
            diagnostic: null);
    }

    internal static WindowsSystemEvidenceObservation Unavailable(
        DateTimeOffset attemptedAtUtc,
        WindowsSystemObservationDiagnosticCode diagnostic)
    {
        if (attemptedAtUtc.Offset != TimeSpan.Zero)
        {
            throw new ArgumentException("Attempt time must use the UTC offset.", nameof(attemptedAtUtc));
        }

        if (!Enum.IsDefined(diagnostic))
        {
            throw new ArgumentOutOfRangeException(nameof(diagnostic));
        }

        return new(
            WindowsSystemObservationState.Unavailable,
            snapshot: null,
            attemptedAtUtc,
            diagnostic);
    }
}
