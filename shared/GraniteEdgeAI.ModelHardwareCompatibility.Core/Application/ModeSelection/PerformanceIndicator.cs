namespace GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.ModeSelection;

/// <summary>
/// Throughput, when it is known.
///
/// Section 11 lists performance as an ordering factor in three of the four
/// modes, but nothing in the system measures it yet. Rather than invent a proxy
/// — which would silently decide real rankings on a fabricated number — this is
/// a typed absence. Comparators treat every candidate as equal on it and the
/// selection records that the factor was not established.
/// </summary>
internal sealed record PerformanceIndicator
{
    private PerformanceIndicator(bool isEstablished, decimal tokensPerSecond)
    {
        IsEstablished = isEstablished;
        TokensPerSecond = tokensPerSecond;
    }

    internal bool IsEstablished { get; }

    /// <summary>Meaningful only when <see cref="IsEstablished"/> is true.</summary>
    internal decimal TokensPerSecond { get; }

    internal static PerformanceIndicator NotEstablished() => new(false, 0m);

    internal static PerformanceIndicator Measured(decimal tokensPerSecond)
    {
        if (tokensPerSecond <= 0m)
        {
            throw new ArgumentOutOfRangeException(
                nameof(tokensPerSecond),
                "A measured throughput must be positive; zero is not a measurement.");
        }

        return new PerformanceIndicator(true, tokensPerSecond);
    }
}
