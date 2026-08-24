namespace GraniteEdgeAI.ModelHardwareCompatibility.Core.Domain;

/// <summary>
/// One non-negative quantity of bytes using checked arithmetic so an overflow
/// becomes a loud failure rather than a silently wrapped value that would
/// understate a memory requirement and produce a false-safe fit result.
/// </summary>
internal readonly struct ByteCount : IEquatable<ByteCount>, IComparable<ByteCount>
{
    private ByteCount(ulong bytes) => Bytes = bytes;

    internal static ByteCount Zero { get; } = new(0);

    internal ulong Bytes { get; }

    internal static ByteCount FromBytes(ulong bytes) => new(bytes);

    internal ByteCount Add(ByteCount other) => new(checked(Bytes + other.Bytes));

    internal ByteCount Subtract(ByteCount other) => new(checked(Bytes - other.Bytes));

    /// <summary>
    /// Subtracts without throwing. Used where "the budget is already exhausted"
    /// is an expected answer rather than a programming error.
    /// </summary>
    internal bool TrySubtract(ByteCount other, out ByteCount remainder)
    {
        if (other.Bytes > Bytes)
        {
            remainder = Zero;
            return false;
        }

        remainder = new ByteCount(Bytes - other.Bytes);
        return true;
    }

    internal ByteCount Multiply(ulong factor) => new(checked(Bytes * factor));

    /// <summary>
    /// Divides rounding upward. The usual "add divisor minus one" form would
    /// overflow near <see cref="ulong.MaxValue"/>, so the remainder is tested
    /// instead.
    /// </summary>
    internal ByteCount CeilingDivide(ulong divisor)
    {
        if (divisor == 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(divisor),
                "A byte quantity cannot be divided by zero.");
        }

        ulong quotient = Bytes / divisor;
        return new ByteCount(Bytes % divisor == 0 ? quotient : checked(quotient + 1));
    }

    /// <summary>
    /// Rounds upward to an allocation boundary. Allocation granularity only ever
    /// costs more memory than requested, so this rounds up and never down.
    /// </summary>
    internal ByteCount AlignUpTo(ulong alignment)
    {
        if (alignment == 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(alignment),
                "An allocation boundary must be a positive number of bytes.");
        }

        if ((alignment & (alignment - 1)) != 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(alignment),
                "An allocation boundary must be a power of two; a non-power-of-two "
                + "value is almost always a units mistake.");
        }

        ulong remainder = Bytes % alignment;
        return remainder == 0
            ? this
            : new ByteCount(checked(Bytes + (alignment - remainder)));
    }

    /// <summary>
    /// Scales by a fraction, rounding upward. Overhead terms are always rounded
    /// against the user, because an understated overhead is a false-safe result.
    /// </summary>
    internal ByteCount MultiplyByFraction(decimal fraction)
    {
        if (fraction < 0m)
        {
            throw new ArgumentOutOfRangeException(
                nameof(fraction),
                "A byte quantity cannot be scaled by a negative fraction.");
        }

        return new ByteCount((ulong)Math.Ceiling(Bytes * fraction));
    }

    /// <summary>
    /// Expresses this quantity as a proportion of a denominator. Decimal is
    /// used rather than double so threshold comparisons are exact.
    /// </summary>
    internal decimal RatioAgainst(ByteCount denominator)
    {
        if (denominator.Bytes == 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(denominator),
                "A ratio against a zero budget is undefined.");
        }

        return (decimal)Bytes / denominator.Bytes;
    }

    public bool Equals(ByteCount other) => Bytes == other.Bytes;

    public override bool Equals(object? obj) => obj is ByteCount other && Equals(other);

    public override int GetHashCode() => Bytes.GetHashCode();

    public int CompareTo(ByteCount other) => Bytes.CompareTo(other.Bytes);

    public override string ToString() => $"{Bytes} B";

    public static bool operator ==(ByteCount left, ByteCount right) => left.Equals(right);

    public static bool operator !=(ByteCount left, ByteCount right) => !left.Equals(right);

    public static bool operator <(ByteCount left, ByteCount right) => left.CompareTo(right) < 0;

    public static bool operator >(ByteCount left, ByteCount right) => left.CompareTo(right) > 0;

    public static bool operator <=(ByteCount left, ByteCount right) => left.CompareTo(right) <= 0;

    public static bool operator >=(ByteCount left, ByteCount right) => left.CompareTo(right) >= 0;
}
