using GraniteEdgeAI.ModelInspection.Contracts;

namespace GraniteEdgeAI.ModelInspection.WorkerClient;

/// <summary>
/// Applies the shared message-sequence contract to one already-handshaking
/// worker request and exposes only validated progress and terminal messages.
/// </summary>
internal sealed class WorkerConversation
{
    private const string SafeFailureMessage =
        "The Model Inspection worker protocol conversation was invalid.";
    private readonly WorkerMessageSequenceValidator _sequence = new();

    internal WorkerConversation(
        WorkerHelloMessage hello,
        Guid expectedRequestId)
    {
        ArgumentNullException.ThrowIfNull(hello);

        try
        {
            _sequence.AcceptHello(hello);
            _sequence.SetExpectedRequest(expectedRequestId);
        }
        catch (WorkerProtocolException)
        {
            throw InvalidConversation();
        }
    }

    internal bool HasStarted => _sequence.HasStarted;

    internal WorkerProgressMessage? LastProgress { get; private set; }

    internal WorkerCompletedMessage? TerminalMessage { get; private set; }

    internal void Accept(object message)
    {
        ArgumentNullException.ThrowIfNull(message);

        try
        {
            switch (message)
            {
                case WorkerStartedMessage started:
                    _sequence.AcceptStarted(started);
                    break;

                case WorkerProgressMessage progress:
                    _sequence.AcceptProgress(progress);
                    LastProgress = progress;
                    break;

                case WorkerCompletedMessage completed:
                    _sequence.AcceptCompleted(completed);
                    TerminalMessage = completed;
                    break;

                default:
                    throw new WorkerProtocolException(
                        "The worker emitted an unsupported message type.");
            }
        }
        catch (WorkerProtocolException)
        {
            throw InvalidConversation();
        }
    }

    /// <summary>
    /// Must be called when stdout reaches EOF. A process exit without one valid
    /// terminal frame is a protocol failure, not a partial result.
    /// </summary>
    internal void CompleteOutput()
    {
        if (!_sequence.IsTerminal || TerminalMessage is null)
        {
            throw InvalidConversation();
        }
    }

    private static WorkerClientPolicyException InvalidConversation() =>
        WorkerClientPolicyException.For(
            WorkerClientFailureCodes.WorkerProtocolInvalid,
            SafeFailureMessage);
}
