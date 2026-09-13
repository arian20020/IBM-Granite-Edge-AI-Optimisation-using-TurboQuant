using System;

namespace GraniteEdgeAI.Features.HardwareInspection.Resolution;

internal enum EvidenceFreshness
{
    Accepted,
    Stale,
    Future,
}

internal static class HardwareEvidenceNormalizer
{
    private const decimal BytesPerGiB = 1_073_741_824m;

    internal static bool TryConvertGibToBytes(double value, out ulong bytes)
    {
        bytes = 0;
        if (!double.IsFinite(value) || value < 0)
        {
            return false;
        }

        try
        {
            decimal converted = checked((decimal)value * BytesPerGiB);
            decimal rounded = decimal.Round(converted, 0, MidpointRounding.ToEven);
            bytes = checked((ulong)rounded);
            return true;
        }
        catch (OverflowException)
        {
            return false;
        }
    }

    internal static EvidenceFreshness GetFreshness(
        DateTimeOffset capturedAtUtc,
        DateTimeOffset resolvedAtUtc,
        TimeSpan maximumAge)
    {
        RequireUtc(capturedAtUtc, nameof(capturedAtUtc));
        RequireUtc(resolvedAtUtc, nameof(resolvedAtUtc));
        if (maximumAge < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(maximumAge));
        }

        TimeSpan captureOffset = capturedAtUtc - resolvedAtUtc;
        if (captureOffset > HardwareResolutionPolicy.FutureClockSkew)
        {
            return EvidenceFreshness.Future;
        }

        if (captureOffset >= TimeSpan.Zero)
        {
            return EvidenceFreshness.Accepted;
        }

        return -captureOffset <= maximumAge
            ? EvidenceFreshness.Accepted
            : EvidenceFreshness.Stale;
    }

    internal static bool AreWithinTolerance(ulong first, ulong second, ulong tolerance) =>
        first >= second
            ? first - second <= tolerance
            : second - first <= tolerance;

    internal static ulong GetAvailableMemoryTolerance(ulong osUsableBytes)
    {
        ulong tenPercent = osUsableBytes / 10;
        return Math.Max(HardwareResolutionPolicy.MinimumAvailableMemoryToleranceBytes, tenPercent);
    }

    private static void RequireUtc(DateTimeOffset value, string parameterName)
    {
        if (value.Offset != TimeSpan.Zero)
        {
            throw new ArgumentException("Timestamp must use the UTC offset.", parameterName);
        }
    }
}
