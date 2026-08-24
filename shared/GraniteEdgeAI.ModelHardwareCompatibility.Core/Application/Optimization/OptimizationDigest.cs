namespace GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Optimization;

/// <summary>
/// The one definition of what a digest looks like in this contract.
///
/// Exactly 64 lowercase hex characters. Case matters because every consumer
/// compares ordinally: an executor that recomputed a digest in uppercase would
/// see drift where none existed and return ReplanRequired for a plan that was
/// still perfectly valid. Defining it once means the model hash, the capability
/// hash and the configuration hash cannot disagree about the rule.
/// </summary>
internal static class OptimizationDigest
{
    internal const int Length = 64;

    internal static bool IsCanonical(string? value)
    {
        if (value is null || value.Length != Length)
        {
            return false;
        }

        foreach (char character in value)
        {
            bool digit = character is >= '0' and <= '9';
            bool lowerHex = character is >= 'a' and <= 'f';

            if (!digit && !lowerHex)
            {
                return false;
            }
        }

        return true;
    }
}
