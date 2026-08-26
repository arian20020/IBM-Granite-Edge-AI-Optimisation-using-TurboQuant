using System;
using System.Text.RegularExpressions;

namespace GraniteEdgeAI.Features.GgufRuntime.History;

internal static partial class ChatTitlePolicy
{
    internal const int MaximumTitleLength = 60;

    internal static string FromPrompt(string prompt)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(prompt);
        string normalized = Whitespace().Replace(prompt.Trim(), " ");
        return normalized.Length <= MaximumTitleLength
            ? normalized
            : normalized[..(MaximumTitleLength - 1)].TrimEnd() + "…";
    }

    [GeneratedRegex(@"\s+", RegexOptions.CultureInvariant)]
    private static partial Regex Whitespace();
}
