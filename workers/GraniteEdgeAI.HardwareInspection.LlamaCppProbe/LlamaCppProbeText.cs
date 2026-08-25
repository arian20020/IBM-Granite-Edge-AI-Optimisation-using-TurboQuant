using System.Buffers;
using System.Globalization;
using System.Text;

namespace GraniteEdgeAI.HardwareInspection.LlamaCppProbe;

internal static class LlamaCppProbeText
{
    internal static bool IsSafe(string? value, int maximumScalarCount)
    {
        if (string.IsNullOrEmpty(value) || maximumScalarCount < 1)
        {
            return false;
        }

        ReadOnlySpan<char> remaining = value.AsSpan();
        int count = 0;
        Rune first = default;
        Rune last = default;
        while (!remaining.IsEmpty)
        {
            OperationStatus status = Rune.DecodeFromUtf16(remaining, out Rune rune, out int consumed);
            if (status != OperationStatus.Done || consumed == 0 || IsNonPrintable(rune))
            {
                return false;
            }

            if (count == 0)
            {
                first = rune;
            }

            last = rune;
            count++;
            if (count > maximumScalarCount)
            {
                return false;
            }

            remaining = remaining[consumed..];
        }

        return count > 0 && !Rune.IsWhiteSpace(first) && !Rune.IsWhiteSpace(last);
    }

    private static bool IsNonPrintable(Rune rune) => Rune.GetUnicodeCategory(rune) is
        UnicodeCategory.Control or
        UnicodeCategory.Format or
        UnicodeCategory.LineSeparator or
        UnicodeCategory.ParagraphSeparator;
}
