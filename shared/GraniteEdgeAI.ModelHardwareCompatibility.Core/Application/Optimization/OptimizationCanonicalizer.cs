using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Optimization;

/// <summary>
/// Turns a configuration into the exact bytes its digest is taken over.
///
/// The rules exist so two processes agree. An executor recomputes this digest
/// on a different machine, in a different culture, possibly in a different
/// build, and compares it ordinally to the one in the plan - so anything that
/// could vary between those runs is excluded or pinned.
///
/// Fields are emitted in a fixed order with invariant formatting. No paths, no
/// free text, no timestamps, no anything a user typed: those would make the
/// digest vary for two configurations that are in fact identical, and drift
/// would be reported where none exists.
/// </summary>
internal static class OptimizationCanonicalizer
{
    /// <summary>
    /// The canonical form of a complete candidate: its route descriptor and its
    /// context length, joined in that order.
    ///
    /// Context is folded in here rather than inside the route descriptor, which
    /// is why the descriptor stays route-shaped and the candidate stays the one
    /// place where configuration and context are combined exactly once.
    /// </summary>
    internal static string Canonicalize(OptimizationCandidate candidate)
    {
        ArgumentNullException.ThrowIfNull(candidate);

        StringBuilder builder = new();

        Append(builder, "v", OptimizationExecutionPlan.CurrentContractVersion);
        Append(builder, "route", (int)candidate.Route);
        Append(builder, "config", candidate.Configuration.CanonicalDescriptor);
        Append(builder, "ctx", candidate.Metrics.ContextTokens);
        Append(builder, "persistent", candidate.Metrics.RequiresPersistentChange ? 1 : 0);

        return builder.ToString();
    }

    /// <summary>
    /// Lowercase hex, matching what every other digest in this contract uses.
    /// A digest that differed only in case would fail an ordinal comparison and
    /// report drift on a plan that had not changed.
    /// </summary>
    internal static string ConfigurationSha256(OptimizationCandidate candidate)
    {
        byte[] hash = SHA256.HashData(
            new UTF8Encoding(encoderShouldEmitUTF8Identifier: false)
                .GetBytes(Canonicalize(candidate)));

        // ToHexStringLower is .NET 9; this targets net8.0.
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private static void Append(StringBuilder builder, string field, int value) =>
        Append(builder, field, value.ToString(CultureInfo.InvariantCulture));

    private static void Append(StringBuilder builder, string field, string value)
    {
        if (builder.Length > 0)
        {
            builder.Append('|');
        }

        builder.Append(field).Append('=').Append(value);
    }
}
