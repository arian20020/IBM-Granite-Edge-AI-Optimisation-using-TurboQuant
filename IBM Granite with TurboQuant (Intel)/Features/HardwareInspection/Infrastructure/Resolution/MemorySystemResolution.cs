using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using GraniteEdgeAI.Features.HardwareInspection.Domain;

namespace GraniteEdgeAI.Features.HardwareInspection.Resolution;

internal sealed class MemorySystemResolution
{
    private const int ExpectedEntryCount = 6;

    internal MemorySystemResolution(
        MemoryFacts? memory,
        OperatingSystemFacts? operatingSystem,
        IEnumerable<HardwareEvidenceEntry> entries,
        IEnumerable<HardwareResolutionDiagnosticCode> diagnostics,
        bool hasCriticalFailure)
    {
        ArgumentNullException.ThrowIfNull(entries);
        ArgumentNullException.ThrowIfNull(diagnostics);

        HardwareEvidenceEntry[] entryCopy = entries.Take(ExpectedEntryCount + 1).ToArray();
        if (entryCopy.Length != ExpectedEntryCount ||
            entryCopy.Any(static entry => entry is null) ||
            entryCopy.Select(static entry => entry.CanonicalField)
                .Distinct(StringComparer.Ordinal)
                .Count() != ExpectedEntryCount)
        {
            throw new ArgumentException(
                "Memory and operating-system resolution requires six unique evidence entries.",
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
                "Memory diagnostics exceed the closed diagnostic set.",
                nameof(diagnostics));
        }

        bool hasBothValues = memory is not null && operatingSystem is not null;
        if (hasCriticalFailure == hasBothValues ||
            (memory is null) != (operatingSystem is null))
        {
            throw new ArgumentException(
                "Memory and operating-system facts must succeed or fail together.",
                nameof(hasCriticalFailure));
        }

        Memory = memory;
        OperatingSystem = operatingSystem;
        Entries = new ReadOnlyCollection<HardwareEvidenceEntry>(entryCopy);
        Diagnostics = new ReadOnlyCollection<HardwareResolutionDiagnosticCode>(
            diagnosticInput.Distinct().Order().ToArray());
        HasCriticalFailure = hasCriticalFailure;
    }

    internal MemoryFacts? Memory { get; }

    internal OperatingSystemFacts? OperatingSystem { get; }

    internal IReadOnlyList<HardwareEvidenceEntry> Entries { get; }

    internal IReadOnlyList<HardwareResolutionDiagnosticCode> Diagnostics { get; }

    internal bool HasCriticalFailure { get; }
}
