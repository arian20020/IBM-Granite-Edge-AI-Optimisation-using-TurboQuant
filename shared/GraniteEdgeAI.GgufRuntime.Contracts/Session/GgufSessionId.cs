using System.Text.Json.Serialization;

namespace GraniteEdgeAI.GgufRuntime.Contracts.Session;

public readonly record struct GgufSessionId
{
    [JsonConstructor]
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
