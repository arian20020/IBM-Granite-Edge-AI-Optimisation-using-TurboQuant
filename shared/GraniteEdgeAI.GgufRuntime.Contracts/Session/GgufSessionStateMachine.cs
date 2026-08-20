namespace GraniteEdgeAI.GgufRuntime.Contracts.Session;

public sealed class GgufSessionStateMachine
{
    public GgufSessionStateMachine(GgufSessionState initialState)
    {
        if (!Enum.IsDefined(initialState))
        {
            throw new ArgumentOutOfRangeException(nameof(initialState));
        }

        Current = initialState;
    }

    public GgufSessionState Current { get; private set; }

    public void AdvanceTo(GgufSessionState next)
    {
        if (!IsAllowed(Current, next))
        {
            throw new InvalidOperationException(
                $"The session cannot transition from {Current} to {next}.");
        }

        Current = next;
    }

    private static bool IsAllowed(GgufSessionState current, GgufSessionState next)
    {
        if (!Enum.IsDefined(next) || current == GgufSessionState.Closed)
        {
            return false;
        }

        if (next == GgufSessionState.Closing)
        {
            return current is not GgufSessionState.Closing;
        }

        if (next == GgufSessionState.Failed)
        {
            return current is not GgufSessionState.Failed and not GgufSessionState.Closing;
        }

        return (current, next) switch
        {
            (GgufSessionState.Created, GgufSessionState.Starting) => true,
            (GgufSessionState.Starting, GgufSessionState.Loading) => true,
            (GgufSessionState.Loading, GgufSessionState.Ready) => true,
            (GgufSessionState.Ready, GgufSessionState.Generating) => true,
            (GgufSessionState.Generating, GgufSessionState.Ready) => true,
            (GgufSessionState.Generating, GgufSessionState.Stopping) => true,
            (GgufSessionState.Stopping, GgufSessionState.Ready) => true,
            (GgufSessionState.Failed, GgufSessionState.Closing) => true,
            (GgufSessionState.Closing, GgufSessionState.Closed) => true,
            _ => false,
        };
    }
}
