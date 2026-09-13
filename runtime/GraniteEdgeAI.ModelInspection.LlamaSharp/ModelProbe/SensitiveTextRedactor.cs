namespace GraniteEdgeAI.ModelInspection.LlamaSharp.ModelProbe;

/// <summary>
/// Removes the canonical local model path from technical messages before they
/// are serialized or displayed.
/// </summary>
public static class SensitiveTextRedactor
{
    /// <summary>
    /// Replaces every canonical-path occurrence with a stable placeholder.
    /// </summary>
    public static string? Redact(
        string? text,
        string? canonicalModelPath,
        bool? windowsCaseInsensitive = null)
    {
        if (string.IsNullOrEmpty(text) ||
            string.IsNullOrEmpty(canonicalModelPath))
        {
            return text;
        }

        bool ignoreCase =
            windowsCaseInsensitive ?? OperatingSystem.IsWindows();
        StringComparison comparison = ignoreCase
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;

        return text.Replace(
            canonicalModelPath,
            "<model-path>",
            comparison);
    }

    /// <summary>
    /// Returns a redacted, independent copy of all native log entries.
    /// </summary>
    public static IReadOnlyList<NativeBackendLogEntry> RedactLogs(
        IEnumerable<NativeBackendLogEntry> logs,
        string? canonicalModelPath)
    {
        ArgumentNullException.ThrowIfNull(logs);

        return logs
            .Select(
                entry => new NativeBackendLogEntry(
                    entry.Level,
                    Redact(
                        entry.Message,
                        canonicalModelPath) ?? string.Empty))
            .ToArray();
    }
}
