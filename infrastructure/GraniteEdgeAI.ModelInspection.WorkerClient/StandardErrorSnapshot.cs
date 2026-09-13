namespace GraniteEdgeAI.ModelInspection.WorkerClient;

/// <summary>
/// Contains the bounded, redacted stderr evidence retained after the raw stream
/// has been drained fully to EOF.
/// </summary>
public sealed record StandardErrorSnapshot
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

    public string RetainedText { get; }

    public bool IsTruncated { get; }

    public bool InvalidUtf8Detected { get; }
}
