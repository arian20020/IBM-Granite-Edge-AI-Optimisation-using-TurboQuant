namespace GraniteEdgeAI.GgufRuntime.Contracts.Session;

public readonly record struct GgufSessionId
{
    public GgufSessionId(Guid value)
    {
        if (value == Guid.Empty)
        {
            throw new ArgumentException("A session identifier cannot be empty.", nameof(value));
        }

        Value = value;
    }

    public Guid Value { get; }
}
