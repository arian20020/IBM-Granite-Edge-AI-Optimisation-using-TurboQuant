using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace GraniteEdgeAI.Features.HardwareInspection.Domain;

public sealed record HardwareEvidenceEntry
{
    public HardwareEvidenceEntry(
        string canonicalField,
        EvidenceSourceKind source,
        EvidenceResolutionState resolution,
        DateTimeOffset capturedAtUtc,
        EvidenceConfidence confidence,
        string? safeDiagnosticCode)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(canonicalField);
        if (!IsToken(canonicalField, allowUppercase: true))
        {
            throw new ArgumentException("Canonical field must be a bounded field token.", nameof(canonicalField));
        }

        ContractTime.RequireUtc(capturedAtUtc, nameof(capturedAtUtc));
        if (safeDiagnosticCode is not null && !IsToken(safeDiagnosticCode, allowUppercase: false))
        {
            throw new ArgumentException(
                "Safe diagnostic code must contain only lowercase ASCII letters, digits, dots, and hyphens.",
                nameof(safeDiagnosticCode));
        }

        CanonicalField = canonicalField;
        Source = source;
        Resolution = resolution;
        CapturedAtUtc = capturedAtUtc;
        Confidence = confidence;
        SafeDiagnosticCode = safeDiagnosticCode;
    }

    public string CanonicalField { get; }
    public EvidenceSourceKind Source { get; }
    public EvidenceResolutionState Resolution { get; }
    public DateTimeOffset CapturedAtUtc { get; }
    public EvidenceConfidence Confidence { get; }
    public string? SafeDiagnosticCode { get; }

    public bool IsResolved =>
        Resolution is EvidenceResolutionState.ResolvedPrimary
            or EvidenceResolutionState.ResolvedCorroborated
            or EvidenceResolutionState.ResolvedFallback;

    private static bool IsToken(string value, bool allowUppercase)
    {
        if (value.Length is < 1 or > 96)
        {
            return false;
        }

        return value.All(character =>
            character is >= 'a' and <= 'z'
            || allowUppercase && character is >= 'A' and <= 'Z'
            || character is >= '0' and <= '9'
            || character is '.' or '-');
    }
}

public sealed class HardwareEvidenceManifest
{
    public HardwareEvidenceManifest(IEnumerable<HardwareEvidenceEntry> entries)
    {
        ArgumentNullException.ThrowIfNull(entries);
        HardwareEvidenceEntry[] copy = entries.ToArray();
        if (copy.Any(entry => entry is null))
        {
            throw new ArgumentException("Evidence entries cannot contain null.", nameof(entries));
        }

        if (copy.Select(entry => entry.CanonicalField)
            .Distinct(StringComparer.Ordinal)
            .Count() != copy.Length)
        {
            throw new ArgumentException("Evidence canonical fields must be unique.", nameof(entries));
        }

        Entries = Array.AsReadOnly(copy);
    }

    public IReadOnlyList<HardwareEvidenceEntry> Entries { get; }

    internal bool HasResolved(string canonicalField) =>
        Entries.Any(entry =>
            string.Equals(entry.CanonicalField, canonicalField, StringComparison.Ordinal)
            && entry.IsResolved);
}
