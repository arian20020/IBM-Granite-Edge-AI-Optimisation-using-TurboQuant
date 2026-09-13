using System;
using System.Buffers;
using System.Globalization;
using System.Text;

namespace GraniteEdgeAI.Features.ModelInspection.Presentation;

internal static class ModelInspectionDisplayTextPolicy
{
    private const int ModelNameLimit = 160;
    private const int OptionalLabelLimit = 96;
    private const int RequiredDetailLimit = 512;
    private const string GgufSuffix = ".gguf";
    private const string NotReported = "Not reported";
    private const string FixedRequiredDetailFallback = "Details are unavailable.";

    internal static string ProjectModelName(
        string? completedName,
        string? quickScanName,
        string fileName)
    {
        if (TryProject(completedName, ModelNameLimit, out string projected) ||
            TryProject(quickScanName, ModelNameLimit, out projected) ||
            TryProjectFileName(fileName, out projected))
        {
            return projected;
        }

        return NotReported;
    }

    internal static string ProjectOptionalLabel(string? value) =>
        TryProject(value, OptionalLabelLimit, out string projected)
            ? projected
            : NotReported;

    internal static string ProjectRequiredDetail(
        string? value,
        string genericFallback)
    {
        if (TryProject(value, RequiredDetailLimit, out string projected) ||
            TryProject(genericFallback, RequiredDetailLimit, out projected))
        {
            return projected;
        }

        return FixedRequiredDetailFallback;
    }

    private static bool TryProjectFileName(
        string? fileName,
        out string projected)
    {
        projected = string.Empty;
        if (string.IsNullOrEmpty(fileName) ||
            fileName.Length > ModelNameLimit + GgufSuffix.Length ||
            !HasSafeScalars(fileName) ||
            IsPathOrAbsoluteUrl(fileName))
        {
            return false;
        }

        string candidate = fileName.EndsWith(
            GgufSuffix,
            StringComparison.OrdinalIgnoreCase)
            ? fileName[..^GgufSuffix.Length]
            : fileName;

        return TryProject(candidate, ModelNameLimit, out projected);
    }

    private static bool TryProject(
        string? value,
        int maximumCodeUnits,
        out string projected)
    {
        projected = string.Empty;
        if (string.IsNullOrEmpty(value) ||
            value.Length > maximumCodeUnits ||
            !IsSafe(value))
        {
            return false;
        }

        string normalized;
        try
        {
            normalized = value.Normalize(NormalizationForm.FormC);
        }
        catch (ArgumentException)
        {
            return false;
        }

        if (normalized.Length > maximumCodeUnits || !IsSafe(normalized))
        {
            return false;
        }

        projected = CollapseOrdinarySpaces(normalized.Trim());
        if (string.IsNullOrWhiteSpace(projected) ||
            projected.Length > maximumCodeUnits ||
            !IsSafe(projected))
        {
            projected = string.Empty;
            return false;
        }

        return true;
    }

    private static bool IsSafe(string value) =>
        HasSafeScalars(value) && !IsPathOrAbsoluteUrl(value);

    private static bool HasSafeScalars(string value)
    {
        ReadOnlySpan<char> remaining = value.AsSpan();
        while (!remaining.IsEmpty)
        {
            OperationStatus status = Rune.DecodeFromUtf16(
                remaining,
                out Rune scalar,
                out int consumed);
            if (status != OperationStatus.Done ||
                IsDisallowedCategory(Rune.GetUnicodeCategory(scalar)))
            {
                return false;
            }

            remaining = remaining[consumed..];
        }

        return true;
    }

    private static bool IsDisallowedCategory(UnicodeCategory category) =>
        category is UnicodeCategory.Control or
            UnicodeCategory.Format or
            UnicodeCategory.LineSeparator or
            UnicodeCategory.ParagraphSeparator or
            UnicodeCategory.PrivateUse or
            UnicodeCategory.OtherNotAssigned;

    private static bool IsPathOrAbsoluteUrl(string value)
    {
        if (value.Contains('/') || value.Contains('\\'))
        {
            return true;
        }

        string trimmed = value.Trim();
        if (trimmed.Length >= 2 &&
            IsAsciiLetter(trimmed[0]) &&
            trimmed[1] == ':')
        {
            return true;
        }

        return Uri.TryCreate(trimmed, UriKind.Absolute, out _);
    }

    private static bool IsAsciiLetter(char value) =>
        value is >= 'A' and <= 'Z' or >= 'a' and <= 'z';

    private static string CollapseOrdinarySpaces(string value)
    {
        StringBuilder builder = new(value.Length);
        bool previousWasOrdinarySpace = false;
        foreach (char codeUnit in value)
        {
            bool isOrdinarySpace = codeUnit == ' ';
            if (!isOrdinarySpace || !previousWasOrdinarySpace)
            {
                builder.Append(codeUnit);
            }

            previousWasOrdinarySpace = isOrdinarySpace;
        }

        return builder.ToString();
    }
}
