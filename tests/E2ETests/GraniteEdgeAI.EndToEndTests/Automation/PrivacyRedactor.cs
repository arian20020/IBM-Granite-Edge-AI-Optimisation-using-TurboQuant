using System.Text.RegularExpressions;

namespace GraniteEdgeAI.EndToEndTests.Automation;

internal static partial class PrivacyRedactor
{
    [GeneratedRegex("(?i)(?:[a-z]:\\\\|\\\\\\\\)[^\\r\\n\\t\\\"<>|]+")]
    private static partial Regex WindowsPath();

    internal static string Redact(string? value) => string.IsNullOrEmpty(value) ? string.Empty : WindowsPath().Replace(value, "<redacted-path>");
}
