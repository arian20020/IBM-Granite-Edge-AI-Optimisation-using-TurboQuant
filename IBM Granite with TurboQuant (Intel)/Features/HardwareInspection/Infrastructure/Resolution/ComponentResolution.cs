using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using GraniteEdgeAI.Features.HardwareInspection.Domain;

namespace GraniteEdgeAI.Features.HardwareInspection.Resolution;

internal sealed class ComponentResolution<T>
    where T : class
{
    private const int MaximumManifestEntries = 19;

    internal ComponentResolution(
        T? value,
        IEnumerable<HardwareEvidenceEntry> entries,
        IEnumerable<HardwareResolutionDiagnosticCode> diagnostics,
        bool hasCriticalFailure)
    {
        ArgumentNullException.ThrowIfNull(entries);
        ArgumentNullException.ThrowIfNull(diagnostics);

        HardwareEvidenceEntry[] entryCopy = entries.Take(MaximumManifestEntries + 1).ToArray();
        if (entryCopy.Length is 0 or > MaximumManifestEntries ||
            entryCopy.Any(static entry => entry is null))
        {
            throw new ArgumentException(
                "Component evidence must contain a bounded non-empty collection.",
                nameof(entries));
        }

        if (entryCopy.Select(static entry => entry.CanonicalField)
            .Distinct(StringComparer.Ordinal)
            .Count() != entryCopy.Length)
        {
            throw new ArgumentException(
                "Component evidence fields must be unique.",
                nameof(entries));
        }

        int diagnosticBound = Enum.GetValues<HardwareResolutionDiagnosticCode>().Length;
        HardwareResolutionDiagnosticCode[] diagnosticInput = diagnostics
            .Take(diagnosticBound + 1)
            .ToArray();
        if (diagnosticInput.Length > diagnosticBound ||
            diagnosticInput.Any(static diagnostic => !Enum.IsDefined(diagnostic)))
        {
            throw new ArgumentException(
                "Component diagnostics exceed the closed diagnostic set.",
                nameof(diagnostics));
        }

        HardwareResolutionDiagnosticCode[] diagnosticCopy = diagnosticInput
            .Distinct()
            .Order()
            .ToArray();

        if (hasCriticalFailure != (value is null))
        {
            throw new ArgumentException(
                "A component value and critical-failure state cannot coexist.",
                nameof(hasCriticalFailure));
        }

        Value = value;
        Entries = new ReadOnlyCollection<HardwareEvidenceEntry>(entryCopy);
        Diagnostics = new ReadOnlyCollection<HardwareResolutionDiagnosticCode>(diagnosticCopy);
        HasCriticalFailure = hasCriticalFailure;
    }

    internal T? Value { get; }

    internal IReadOnlyList<HardwareEvidenceEntry> Entries { get; }

    internal IReadOnlyList<HardwareResolutionDiagnosticCode> Diagnostics { get; }

    internal bool HasCriticalFailure { get; }
}
