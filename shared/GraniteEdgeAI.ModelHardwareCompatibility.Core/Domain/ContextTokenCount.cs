namespace GraniteEdgeAI.ModelHardwareCompatibility.Core.Domain;

/// <summary>
/// A strictly positive planning context length in tokens. Zero would make the
/// KV-cache estimate vanish, so it is rejected at construction rather than
/// producing a silently free configuration downstream.
/// </summary>
public readonly record struct ContextTokenCount
{
    private ContextTokenCount(int tokens) => Tokens = tokens;

    public int Tokens { get; }

    public static ContextTokenCount FromTokens(int tokens)
    {
        if (tokens <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(tokens),
                "A planning context must be a positive number of tokens.");
        }

        return new ContextTokenCount(tokens);
    }

    public override string ToString() => $"{Tokens} tokens";
}
