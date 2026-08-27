using System.Text;
using LLama.Abstractions;

namespace GraniteEdgeAI.GgufRuntime.NativeAdapter;

internal sealed class GraniteTurnBoundaryTextTransform : ITextStreamTransform
{
    private const int BoundaryLookbehindLength = 32;
    private readonly int visibleTokenLimit;
    private readonly GraniteGenerationBoundaryObserver observer;

    internal GraniteTurnBoundaryTextTransform()
        : this(int.MaxValue, new GraniteGenerationBoundaryObserver())
    {
    }

    internal GraniteTurnBoundaryTextTransform(
        int visibleTokenLimit,
        GraniteGenerationBoundaryObserver observer)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(visibleTokenLimit);
        this.visibleTokenLimit = visibleTokenLimit;
        this.observer = observer ?? throw new ArgumentNullException(nameof(observer));
    }

    public async IAsyncEnumerable<string> TransformAsync(
        IAsyncEnumerable<string> tokens)
    {
        ArgumentNullException.ThrowIfNull(tokens);
        var text = new StringBuilder();
        int emitted = 0;
        int sourceTokenCount = 0;
        bool prefixResolved = false;
        bool lengthReached = false;

        await foreach (string token in tokens.ConfigureAwait(false))
        {
            sourceTokenCount++;
            if (sourceTokenCount > visibleTokenLimit)
            {
                if (!lengthReached)
                {
                    observer.Complete(GgufAdapterCompletionReason.Length);
                    prefixResolved = ResolveLeadingMe(text, prefixResolved, isFinal: true);
                    if (prefixResolved)
                    {
                        BoundaryAnalysis probeAnalysis = Analyze(text);
                        int probeEnd = probeAnalysis.BoundaryOffset >= 0
                            ? probeAnalysis.BoundaryOffset
                            : probeAnalysis.PendingEmptyFenceOffset >= 0
                                ? probeAnalysis.PendingEmptyFenceOffset
                                : text.Length;
                        if (probeEnd > emitted)
                        {
                            yield return text.ToString(emitted, probeEnd - emitted);
                            emitted = probeEnd;
                        }
                    }

                    lengthReached = true;
                }

                continue;
            }

            text.Append(token);
            prefixResolved = ResolveLeadingMe(
                text,
                prefixResolved,
                isFinal: false);
            if (!prefixResolved)
            {
                continue;
            }

            BoundaryAnalysis analysis = Analyze(text);
            int safeEnd = analysis.BoundaryOffset >= 0
                ? analysis.BoundaryOffset
                : FindSafeStreamingEnd(text, emitted, analysis.PendingEmptyFenceOffset);
            if (safeEnd > emitted)
            {
                yield return text.ToString(emitted, safeEnd - emitted);
                emitted = safeEnd;
            }

            if (analysis.BoundaryOffset >= 0)
            {
                observer.Complete(GgufAdapterCompletionReason.Stop);
                yield break;
            }
        }

        if (lengthReached)
        {
            yield break;
        }

        observer.Complete(GgufAdapterCompletionReason.Stop);
        prefixResolved = ResolveLeadingMe(text, prefixResolved, isFinal: true);
        if (!prefixResolved)
        {
            yield break;
        }

        BoundaryAnalysis finalAnalysis = Analyze(text);
        int finalEnd = finalAnalysis.BoundaryOffset >= 0
            ? finalAnalysis.BoundaryOffset
            : text.Length;
        if (finalEnd > emitted)
        {
            yield return text.ToString(emitted, finalEnd - emitted);
        }
    }

    public ITextStreamTransform Clone() =>
        new GraniteTurnBoundaryTextTransform(visibleTokenLimit, observer);

    private static bool ResolveLeadingMe(
        StringBuilder text,
        bool alreadyResolved,
        bool isFinal)
    {
        if (alreadyResolved)
        {
            return true;
        }

        int index = 0;
        while (index < text.Length && char.IsWhiteSpace(text[index]))
        {
            index++;
        }

        if (index == text.Length)
        {
            return isFinal;
        }

        if (!MatchesCharacter(text[index], 'm'))
        {
            return true;
        }

        index++;
        if (index == text.Length)
        {
            return isFinal;
        }

        if (!MatchesCharacter(text[index], 'e'))
        {
            return true;
        }

        index++;
        while (index < text.Length && char.IsWhiteSpace(text[index]))
        {
            index++;
        }

        if (index == text.Length)
        {
            return isFinal;
        }

        if (text[index] != ':')
        {
            return true;
        }

        int removeLength = index + 1;
        if (removeLength < text.Length && text[removeLength] == ' ')
        {
            removeLength++;
        }

        text.Remove(0, removeLength);
        return true;
    }

    private static bool MatchesCharacter(char actual, char expected) =>
        char.ToLowerInvariant(actual) == expected;

    private static int FindSafeStreamingEnd(
        StringBuilder text,
        int emitted,
        int pendingEmptyFenceOffset)
    {
        if (pendingEmptyFenceOffset >= 0)
        {
            return Math.Max(emitted, pendingEmptyFenceOffset);
        }

        return Math.Max(emitted, text.Length - BoundaryLookbehindLength);
    }

    private static BoundaryAnalysis Analyze(StringBuilder text)
    {
        bool insideFence = false;
        int pendingEmptyFence = -1;
        int lineStart = 0;

        while (lineStart < text.Length)
        {
            int lineEnd = IndexOf(text, '\n', lineStart);
            int contentEnd = lineEnd >= 0 ? lineEnd : text.Length;
            if (contentEnd > lineStart && text[contentEnd - 1] == '\r')
            {
                contentEnd--;
            }

            ReadOnlySpan<char> line = text.ToString(lineStart, contentEnd - lineStart)
                .AsSpan();
            ReadOnlySpan<char> trimmed = line.Trim();
            bool emptyFence = trimmed.SequenceEqual("```".AsSpan());

            if (insideFence)
            {
                if (pendingEmptyFence >= 0)
                {
                    if (emptyFence)
                    {
                        return new BoundaryAnalysis(
                            pendingEmptyFence,
                            pendingEmptyFence);
                    }

                    if (!trimmed.IsEmpty)
                    {
                        pendingEmptyFence = -1;
                    }
                }
                else if (emptyFence)
                {
                    insideFence = false;
                }
            }
            else if (emptyFence)
            {
                insideFence = true;
                pendingEmptyFence = lineStart;
            }
            else if (StartsFence(trimmed))
            {
                insideFence = true;
            }
            else if (lineStart > 0 && IsRoleBoundary(trimmed))
            {
                return new BoundaryAnalysis(lineStart, pendingEmptyFence);
            }

            if (lineEnd < 0)
            {
                break;
            }

            lineStart = lineEnd + 1;
        }

        return new BoundaryAnalysis(-1, pendingEmptyFence);
    }

    private static bool StartsFence(ReadOnlySpan<char> line) =>
        line.StartsWith("```".AsSpan(), StringComparison.Ordinal);

    private static bool IsRoleBoundary(ReadOnlySpan<char> line)
    {
        int colon = line.IndexOf(':');
        if (colon < 0)
        {
            return false;
        }

        ReadOnlySpan<char> label = line[..colon].Trim();
        return label.Equals("me".AsSpan(), StringComparison.OrdinalIgnoreCase) ||
            label.Equals("user".AsSpan(), StringComparison.OrdinalIgnoreCase) ||
            label.Equals("assistant".AsSpan(), StringComparison.OrdinalIgnoreCase);
    }

    private static int IndexOf(StringBuilder text, char value, int startIndex)
    {
        for (int index = startIndex; index < text.Length; index++)
        {
            if (text[index] == value)
            {
                return index;
            }
        }

        return -1;
    }

    private readonly record struct BoundaryAnalysis(
        int BoundaryOffset,
        int PendingEmptyFenceOffset);
}
