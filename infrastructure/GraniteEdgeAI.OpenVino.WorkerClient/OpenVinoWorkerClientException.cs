using GraniteEdgeAI.OpenVino.Contracts;

namespace GraniteEdgeAI.OpenVino.WorkerClient;

/// <summary>A privacy-safe failure reported by the managed worker boundary.</summary>
public sealed class OpenVinoWorkerClientException : Exception
{
    internal OpenVinoWorkerClientException(
        OpenVinoSupportCode supportCode,
        string message,
        string retainedStandardError = "",
        bool standardErrorTruncated = false)
        : base(message)
    {
        supportCode.Validate();
        SupportCode = supportCode;
        RetainedStandardError = retainedStandardError;
        StandardErrorTruncated = standardErrorTruncated;
    }

    public OpenVinoSupportCode SupportCode { get; }

    public string RetainedStandardError { get; }

    public bool StandardErrorTruncated { get; }
}
