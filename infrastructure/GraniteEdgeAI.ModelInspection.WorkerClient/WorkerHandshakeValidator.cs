using GraniteEdgeAI.ModelInspection.Contracts;

namespace GraniteEdgeAI.ModelInspection.WorkerClient;

/// <summary>
/// Validates the connection-scoped worker identity before any model path is
/// written to stdin. Raw protocol values are deliberately not preserved in the
/// resulting application-side failure.
/// </summary>
internal static class WorkerHandshakeValidator
{
    private const string SafeFailureMessage =
        "The Model Inspection worker identity could not be verified.";

    internal static void Validate(
        WorkerHelloMessage hello,
        int expectedProcessId)
    {
        ArgumentNullException.ThrowIfNull(hello);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(
            expectedProcessId,
            0);

        try
        {
            // The shared record validates protocol version, worker ID, worker
            // version, runtime profile, architecture, and a positive PID.
            hello.Validate();
        }
        catch (WorkerProtocolException)
        {
            throw InvalidHandshake();
        }

        // The worker-reported PID must also equal the process created by the
        // reviewed launcher; a merely positive PID is not sufficient identity.
        if (hello.WorkerProcessId != expectedProcessId)
        {
            throw InvalidHandshake();
        }
    }

    private static WorkerClientPolicyException InvalidHandshake() =>
        WorkerClientPolicyException.For(
            WorkerClientFailureCodes.WorkerHandshakeInvalid,
            SafeFailureMessage);
}
