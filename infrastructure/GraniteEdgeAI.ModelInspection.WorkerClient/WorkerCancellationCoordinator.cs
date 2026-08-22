using GraniteEdgeAI.ModelInspection.Contracts;
using GraniteEdgeAI.ModelInspection.Transport;

namespace GraniteEdgeAI.ModelInspection.WorkerClient;

/// <summary>
/// Serializes at most one request-scoped cancellation command after the start
/// command has been sent. The caller supplies an independent bounded token;
/// the already-cancelled user token is never reused for this cleanup message.
/// </summary>
internal sealed class WorkerCancellationCoordinator
{
    private readonly BoundedUtf8LineWriter _writer;
    private readonly Guid _requestId;
    private int _sendState;

    internal WorkerCancellationCoordinator(
        BoundedUtf8LineWriter writer,
        Guid requestId)
    {
        ArgumentNullException.ThrowIfNull(writer);
        if (requestId == Guid.Empty)
        {
            throw new ArgumentException(
                "A cancellation coordinator requires a request identifier.",
                nameof(requestId));
        }

        _writer = writer;
        _requestId = requestId;
    }

    internal bool WasRequested => Volatile.Read(ref _sendState) != 0;

    internal async Task<bool> RequestAsync(
        CancellationToken cancellationToken)
    {
        if (Interlocked.Exchange(ref _sendState, 1) != 0)
        {
            return false;
        }

        WorkerCancelInspectionCommand command = new()
        {
            ProtocolVersion = WorkerProtocol.Version,
            CommandType = WorkerCommandKind.CancelInspection,
            RequestId = _requestId
        };
        command.Validate();

        await _writer.WriteLineAsync(
                WorkerProtocolJson.Serialize(command),
                cancellationToken)
            .ConfigureAwait(false);
        return true;
    }
}
