using GraniteEdgeAI.OpenVino.Contracts;

namespace GraniteEdgeAI.OpenVino.WorkerClient;

/// <summary>Starts strict OpenVINO inspection and generation conversations.</summary>
public interface IOpenVinoWorkerClient
{
    Task<IOpenVinoEvent> InspectAsync(
        StartInspectionCommand command,
        CancellationToken cancellationToken);

    Task<OpenVinoConversation> StartSessionAsync(
        StartSessionCommand command,
        CancellationToken cancellationToken);
}

/// <summary>
/// Extends the worker boundary with validated native inspection milestones.
/// Older test and alternate clients can retain the terminal-only boundary.
/// </summary>
public interface IOpenVinoInspectionProgressClient : IOpenVinoWorkerClient
{
    Task<IOpenVinoEvent> InspectAsync(
        StartInspectionCommand command,
        IProgress<InspectionProgressEvent> progress,
        CancellationToken cancellationToken);
}

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
