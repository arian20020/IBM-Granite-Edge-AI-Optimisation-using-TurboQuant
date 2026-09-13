namespace GraniteEdgeAI.GgufRuntime.Contracts.Session;

public enum GgufSessionState
{
    Created,
    Starting,
    Loading,
    Ready,
    Generating,
    Stopping,
    Closing,
    Closed,
    Failed,
}
