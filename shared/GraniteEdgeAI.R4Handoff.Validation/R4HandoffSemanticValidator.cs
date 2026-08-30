using System.Text.Json;
using System.Globalization;
using System.Numerics;

namespace GraniteEdgeAI.R4Handoff.Validation;

public static class R4HandoffSemanticValidator
{
    public static bool HasValidReceiptArithmetic(JsonElement receipt) =>
        receipt.ValueKind == JsonValueKind.Object
        && receipt.TryGetProperty("testTotals", out JsonElement totals)
        && HasValidTotals(totals);

    public static bool HasValidEvidenceArithmetic(JsonElement evidence)
    {
        if (evidence.ValueKind != JsonValueKind.Object
            || !evidence.TryGetProperty("commands", out JsonElement commands)
            || commands.ValueKind != JsonValueKind.Array)
        {
            return false;
        }

        foreach (JsonElement command in commands.EnumerateArray())
        {
            if (!HasValidTotals(command))
            {
                return false;
            }
        }

        return true;
    }

    private static bool HasValidTotals(JsonElement totals) =>
        TryReadNonNegative(totals, "discovered", out BigInteger discovered)
        && TryReadNonNegative(totals, "executed", out BigInteger executed)
        && TryReadNonNegative(totals, "passed", out BigInteger passed)
        && TryReadNonNegative(totals, "failed", out BigInteger failed)
        && TryReadNonNegative(totals, "skipped", out BigInteger skipped)
        && discovered == executed
        && executed == passed + failed + skipped;

    private static bool TryReadNonNegative(
        JsonElement parent,
        string name,
        out BigInteger value)
    {
        value = BigInteger.Zero;
        return parent.ValueKind == JsonValueKind.Object
            && parent.TryGetProperty(name, out JsonElement property)
            && property.ValueKind == JsonValueKind.Number
            && BigInteger.TryParse(
                property.GetRawText(),
                NumberStyles.None,
                CultureInfo.InvariantCulture,
                out value)
            && value >= BigInteger.Zero;
    }
}
