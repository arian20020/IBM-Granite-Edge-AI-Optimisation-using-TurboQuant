namespace GraniteEdgeAI.UnitTests.Features.ModelInspection.Visual;

public readonly record struct PixelDifferenceBounds(
    int X,
    int Y,
    int Width,
    int Height);

public sealed class PixelDiff
{
    private PixelDiff(
        int differentPixelCount,
        PixelDifferenceBounds? differenceBounds,
        int maxChannelDelta,
        RenderedFrame differenceFrame)
    {
        DifferentPixelCount = differentPixelCount;
        DifferenceBounds = differenceBounds;
        MaxChannelDelta = maxChannelDelta;
        DifferenceFrame = differenceFrame;
    }

    public int DifferentPixelCount { get; }

    public PixelDifferenceBounds? DifferenceBounds { get; }

    public int MaxChannelDelta { get; }

    public RenderedFrame DifferenceFrame { get; }

    public bool IsMatch => DifferentPixelCount == 0;

    public static PixelDiff Compare(
        RenderedFrame reference,
        RenderedFrame actual)
    {
        ArgumentNullException.ThrowIfNull(reference);
        ArgumentNullException.ThrowIfNull(actual);
        if (reference.Width != actual.Width || reference.Height != actual.Height)
        {
            throw new ArgumentException(
                "Reference and actual frames must have identical dimensions.",
                nameof(actual));
        }

        ReadOnlySpan<byte> expected = reference.Bgra8Pixels.Span;
        ReadOnlySpan<byte> observed = actual.Bgra8Pixels.Span;
        byte[] differencePixels = new byte[expected.Length];
        int differentPixelCount = 0;
        int maxChannelDelta = 0;
        int minimumX = reference.Width;
        int minimumY = reference.Height;
        int maximumX = -1;
        int maximumY = -1;
        for (int offset = 0; offset < expected.Length; offset += 4)
        {
            int pixelMaximumDelta = 0;
            for (int channel = 0; channel < 4; channel++)
            {
                pixelMaximumDelta = Math.Max(
                    pixelMaximumDelta,
                    Math.Abs(expected[offset + channel] -
                        observed[offset + channel]));
            }

            if (pixelMaximumDelta == 0)
            {
                continue;
            }

            differentPixelCount++;
            maxChannelDelta = Math.Max(maxChannelDelta, pixelMaximumDelta);
            int pixelIndex = offset / 4;
            int x = pixelIndex % reference.Width;
            int y = pixelIndex / reference.Width;
            minimumX = Math.Min(minimumX, x);
            minimumY = Math.Min(minimumY, y);
            maximumX = Math.Max(maximumX, x);
            maximumY = Math.Max(maximumY, y);

            // Opaque red makes every differing pixel visible in the attached
            // diagnostic even when the source pixel itself is transparent
            differencePixels[offset + 2] = 0xff;
            differencePixels[offset + 3] = 0xff;
        }

        PixelDifferenceBounds? differenceBounds = differentPixelCount == 0
            ? null
            : new PixelDifferenceBounds(
                minimumX,
                minimumY,
                maximumX - minimumX + 1,
                maximumY - minimumY + 1);
        return new PixelDiff(
            differentPixelCount,
            differenceBounds,
            maxChannelDelta,
            RenderedFrame.FromBgra8(
                reference.Width,
                reference.Height,
                differencePixels));
    }
}
