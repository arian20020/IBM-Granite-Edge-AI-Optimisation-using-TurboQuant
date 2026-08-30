using System.Text.Json;

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
        TryReadNonNegative(totals, "discovered", out int discovered)
        && TryReadNonNegative(totals, "executed", out int executed)
        && TryReadNonNegative(totals, "passed", out int passed)
        && TryReadNonNegative(totals, "failed", out int failed)
        && TryReadNonNegative(totals, "skipped", out int skipped)
        && discovered == executed
        && (long)executed == (long)passed + failed + skipped;

    private static bool TryReadNonNegative(
        JsonElement parent,
        string name,
        out int value)
    {
        value = 0;
        return parent.ValueKind == JsonValueKind.Object
            && parent.TryGetProperty(name, out JsonElement property)
            && property.TryGetInt32(out value)
            && value >= 0;
    }
}
