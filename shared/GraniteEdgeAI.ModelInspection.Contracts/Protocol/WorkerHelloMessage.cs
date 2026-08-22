namespace GraniteEdgeAI.ModelInspection.Contracts;

/// <summary>
/// Announces the worker identity before the application sends a model path.
/// </summary>
public sealed record WorkerHelloMessage
{
    public int ProtocolVersion { get; init; }

    public WorkerMessageKind MessageType { get; init; }

    public string WorkerId { get; init; } = string.Empty;

    public string WorkerVersion { get; init; } = string.Empty;

    public int WorkerProcessId { get; init; }

    public string RuntimeProfile { get; init; } = string.Empty;

    public string ProcessArchitecture { get; init; } = string.Empty;

    /// <summary>
    /// Verifies the exact connection-scoped handshake expected by version 1.
    /// </summary>
    public void Validate()
    {
        WorkerProtocolValidation.RequireProtocolVersion(ProtocolVersion);
        WorkerProtocolValidation.Require(
            MessageType == WorkerMessageKind.Hello,
            nameof(MessageType),
            "must equal Hello");
        WorkerProtocolValidation.Require(
            string.Equals(WorkerId, WorkerProtocol.WorkerId, StringComparison.Ordinal),
            nameof(WorkerId),
            "must match the approved worker identity");
        WorkerProtocolValidation.Require(
            !string.IsNullOrWhiteSpace(WorkerVersion),
            nameof(WorkerVersion),
            "must not be empty");
        WorkerProtocolValidation.Require(
            WorkerProcessId > 0,
            nameof(WorkerProcessId),
            "must be positive");
        WorkerProtocolValidation.Require(
            string.Equals(
                RuntimeProfile,
                WorkerProtocol.RuntimeProfile,
                StringComparison.Ordinal),
            nameof(RuntimeProfile),
            "must match the approved runtime profile");
        WorkerProtocolValidation.Require(
            string.Equals(ProcessArchitecture, "X64", StringComparison.Ordinal),
            nameof(ProcessArchitecture),
            "must equal X64");
    }
}
