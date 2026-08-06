using GraniteEdgeAI.ModelInspection.Contracts;

namespace GraniteEdgeAI.ModelInspection.WorkerClient.Tests;

/// <summary>
/// Creates valid protocol messages so individual tests can alter only the field
/// whose policy they are specifying.
/// </summary>
internal static class WorkerClientTestData
{
    internal static WorkerHelloMessage Hello(int processId = 1234) => new()
    {
        ProtocolVersion = WorkerProtocol.Version,
        MessageType = WorkerMessageKind.Hello,
        WorkerId = WorkerProtocol.WorkerId,
        WorkerVersion = "0.2.0-gate2",
        WorkerProcessId = processId,
        RuntimeProfile = WorkerProtocol.RuntimeProfile,
        ProcessArchitecture = "X64"
    };

    internal static WorkerStartedMessage Started(Guid requestId) => new()
    {
        ProtocolVersion = WorkerProtocol.Version,
        MessageType = WorkerMessageKind.Started,
        RequestId = requestId
    };

    internal static WorkerProgressMessage Progress(
        Guid requestId,
        WorkerStage stage = WorkerStage.CheckModelPackage,
        int completedStageCount = 0) => new()
    {
        ProtocolVersion = WorkerProtocol.Version,
        MessageType = WorkerMessageKind.Progress,
        RequestId = requestId,
        Stage = stage,
        StageStatus = WorkerStageStatus.Active,
        CompletedStageCount = completedStageCount,
        TotalStageCount = 5,
        StageFraction = 0.25
    };

    internal static WorkerCompletedMessage OperationalFailure(Guid requestId) => new()
    {
        ProtocolVersion = WorkerProtocol.Version,
        MessageType = WorkerMessageKind.Completed,
        RequestId = requestId,
        CompletionStatus = WorkerCompletionStatus.OperationalFailure,
        Evidence = null,
        OperationalFailure = new WorkerOperationalFailure
        {
            Code = "MI-OP-ENGINE-NOT-CONFIGURED",
            Message = "The model inspection runtime is not configured in this worker build."
        }
    };

    internal static WorkerCompletedMessage Cancelled(Guid requestId) => new()
    {
        ProtocolVersion = WorkerProtocol.Version,
        MessageType = WorkerMessageKind.Completed,
        RequestId = requestId,
        CompletionStatus = WorkerCompletionStatus.Cancelled,
        Evidence = null,
        OperationalFailure = null
    };
}
