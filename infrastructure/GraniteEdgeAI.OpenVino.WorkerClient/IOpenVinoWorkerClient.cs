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
