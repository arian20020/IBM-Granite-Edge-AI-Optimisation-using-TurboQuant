namespace GraniteEdgeAI.ModelInspection.WorkerClient;

/// <summary>
/// Contains the bounded, redacted stderr evidence retained after the raw stream
/// has been drained fully to EOF.
/// </summary>
internal sealed record StandardErrorSnapshot
{
    internal StandardErrorSnapshot(
        string retainedText,
        bool isTruncated,
        bool invalidUtf8Detected)
    {
        ArgumentNullException.ThrowIfNull(retainedText);
        RetainedText = retainedText;
        IsTruncated = isTruncated;
        InvalidUtf8Detected = invalidUtf8Detected;
    }

    internal string RetainedText { get; }

    internal bool IsTruncated { get; }

    internal bool InvalidUtf8Detected { get; }
}
