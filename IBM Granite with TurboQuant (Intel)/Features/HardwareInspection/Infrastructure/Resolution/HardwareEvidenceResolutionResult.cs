using GraniteEdgeAI.Features.HardwareInspection.Domain;
using System;
using System.Collections.Generic;
using System.Linq;

namespace GraniteEdgeAI.Features.HardwareInspection.Resolution;

internal sealed class HardwareEvidenceResolutionResult
{
    private HardwareEvidenceResolutionResult(
        HardwareSnapshot? snapshot,
        HardwareEvidenceManifest evidence,
        IReadOnlyList<HardwareResolutionDiagnosticCode> diagnostics)
    {
        Snapshot = snapshot;
        Evidence = evidence;
        Diagnostics = diagnostics;
    }

    internal HardwareSnapshot? Snapshot { get; }

    internal HardwareEvidenceManifest Evidence { get; }

    internal IReadOnlyList<HardwareResolutionDiagnosticCode> Diagnostics { get; }

    internal bool IsResolved => Snapshot is not null;

    internal static HardwareEvidenceResolutionResult Success(
        HardwareSnapshot snapshot,
        HardwareEvidenceManifest evidence,
        IEnumerable<HardwareResolutionDiagnosticCode> diagnostics)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentNullException.ThrowIfNull(evidence);
        if (snapshot.Usability == HardwareSnapshotUsability.NotUsable)
        {
            throw new ArgumentException("A resolved result requires displayable facts.", nameof(snapshot));
        }

        if (!ReferenceEquals(snapshot.Evidence, evidence))
        {
            throw new ArgumentException(
                "The result evidence must be the snapshot evidence instance.",
                nameof(evidence));
        }

        return new(snapshot, evidence, CopyDiagnostics(diagnostics, requireNonEmpty: false));
    }

    internal static HardwareEvidenceResolutionResult Failure(
        HardwareEvidenceManifest evidence,
        IEnumerable<HardwareResolutionDiagnosticCode> diagnostics)
    {
        ArgumentNullException.ThrowIfNull(evidence);
        return new(
            snapshot: null,
            evidence,
            CopyDiagnostics(diagnostics, requireNonEmpty: true));
    }

    private static IReadOnlyList<HardwareResolutionDiagnosticCode> CopyDiagnostics(
        IEnumerable<HardwareResolutionDiagnosticCode> diagnostics,
        bool requireNonEmpty)
    {
        ArgumentNullException.ThrowIfNull(diagnostics);
        int maximumInputCount = Enum.GetValues<HardwareResolutionDiagnosticCode>().Length;
        int inputCount = 0;
        HashSet<HardwareResolutionDiagnosticCode> unique = [];
        foreach (HardwareResolutionDiagnosticCode diagnostic in diagnostics)
        {
            if (++inputCount > maximumInputCount)
            {
                throw new ArgumentException(
                    "The diagnostic collection exceeds its closed bound.",
                    nameof(diagnostics));
            }

            if (!Enum.IsDefined(diagnostic))
            {
                throw new ArgumentOutOfRangeException(nameof(diagnostics));
            }

            unique.Add(diagnostic);
        }

        HardwareResolutionDiagnosticCode[] copy = unique.Order().ToArray();
        if (requireNonEmpty && copy.Length == 0)
        {
            throw new ArgumentException(
                "A failed resolution requires at least one diagnostic.",
                nameof(diagnostics));
        }

        return Array.AsReadOnly(copy);
    }
}
