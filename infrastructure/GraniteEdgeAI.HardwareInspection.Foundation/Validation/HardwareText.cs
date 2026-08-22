using System.Buffers;
using System.Globalization;
using System.Text;

namespace GraniteEdgeAI.HardwareInspection.Foundation.Validation;

internal static class HardwareText
{
    internal static bool IsSafe(string? value, int maximumScalarCount)
    {
        if (string.IsNullOrEmpty(value) || maximumScalarCount < 1)
        {
            return false;
        }

        ReadOnlySpan<char> remaining = value.AsSpan();
        int scalarCount = 0;
        Rune first = default;
        Rune last = default;

        while (!remaining.IsEmpty)
        {
            OperationStatus status = Rune.DecodeFromUtf16(remaining, out Rune rune, out int consumed);
            if (status != OperationStatus.Done || consumed == 0 || IsNonPrintable(rune))
            {
                return false;
            }

            if (scalarCount == 0)
            {
                first = rune;
            }

            last = rune;
            scalarCount++;
            if (scalarCount > maximumScalarCount)
            {
                return false;
            }

            remaining = remaining[consumed..];
        }

        return scalarCount > 0 && !Rune.IsWhiteSpace(first) && !Rune.IsWhiteSpace(last);
    }

    internal static void Validate(string? value, int maximumScalarCount, string parameterName)
    {
        ArgumentNullException.ThrowIfNull(value, parameterName);
        if (!IsSafe(value, maximumScalarCount))
        {
            throw new ArgumentException("The value is not safe bounded hardware text.", parameterName);
        }
    }

    private static bool IsNonPrintable(Rune rune) => Rune.GetUnicodeCategory(rune) is
        UnicodeCategory.Control or
        UnicodeCategory.Format or
        UnicodeCategory.LineSeparator or
        UnicodeCategory.ParagraphSeparator;
}
