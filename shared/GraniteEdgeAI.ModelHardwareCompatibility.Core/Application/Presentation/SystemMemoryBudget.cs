namespace GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Presentation;

/// <summary>A physically installed system-memory capacity.</summary>
public readonly record struct TotalPhysicalMemory
{
    private TotalPhysicalMemory(ulong bytes) => Bytes = bytes;
    public ulong Bytes { get; }

    public static TotalPhysicalMemory FromBytes(ulong bytes) => bytes == 0
        ? throw new ArgumentOutOfRangeException(nameof(bytes))
        : new(bytes);
}

/// <summary>A point-in-time observation of memory currently available to execute work.</summary>
public readonly record struct CurrentlyAvailableMemory
{
    private CurrentlyAvailableMemory(ulong bytes) => Bytes = bytes;
    public ulong Bytes { get; }
    public static CurrentlyAvailableMemory FromBytes(ulong bytes) => new(bytes);
}

/// <summary>Memory deliberately withheld by the proportional safety policy.</summary>
public readonly record struct ProportionalSafetyReserve
{
    internal ProportionalSafetyReserve(ulong bytes) => Bytes = bytes;
    public ulong Bytes { get; }
}

/// <summary>Memory remaining for executable model work after the safety reserve.</summary>
public readonly record struct ExecutableModelBudget
{
    internal ExecutableModelBudget(ulong bytes) => Bytes = bytes;
    public ulong Bytes { get; }
}

/// <summary>The three coherent results derived from one current-memory observation.</summary>
public sealed record AvailableMemorySafetyBudget
{
    internal AvailableMemorySafetyBudget(
        CurrentlyAvailableMemory available,
        ProportionalSafetyReserve reserve,
        ExecutableModelBudget executable)
    {
        Available = available;
        Reserve = reserve;
        Executable = executable;
    }

    public CurrentlyAvailableMemory Available { get; }
    public ProportionalSafetyReserve Reserve { get; }
    public ExecutableModelBudget Executable { get; }
}

/// <summary>
/// Deterministic integer-only memory-budget calculation. The proportional term
/// rounds upward without multiplication, and the result is always bounded by
/// the observed available-memory pool.
/// </summary>
public static class SystemMemoryBudgetCalculator
{
    private const ulong DefaultFloorBytes = 512UL * 1024 * 1024;

    public static AvailableMemorySafetyBudget Calculate(
        CurrentlyAvailableMemory available) =>
        Calculate(available, proportionalNumerator: 1, proportionalDenominator: 10,
            reserveFloorBytes: DefaultFloorBytes);

    internal static AvailableMemorySafetyBudget Calculate(
        CurrentlyAvailableMemory available,
        ulong proportionalNumerator,
        ulong proportionalDenominator,
        ulong reserveFloorBytes)
    {
        if (proportionalDenominator == 0
            || proportionalNumerator > proportionalDenominator)
        {
            throw new ArgumentOutOfRangeException(nameof(proportionalDenominator));
        }

        UInt128 product = (UInt128)available.Bytes * proportionalNumerator;
        UInt128 quotient = product / proportionalDenominator;
        UInt128 proportionalWide = product % proportionalDenominator == 0
            ? quotient
            : quotient + 1;
        ulong proportional = checked((ulong)proportionalWide);

        ulong requested = Math.Max(proportional, reserveFloorBytes);
        ulong reserve = Math.Min(available.Bytes, requested);
        return new AvailableMemorySafetyBudget(
            available,
            new ProportionalSafetyReserve(reserve),
            new ExecutableModelBudget(available.Bytes - reserve));
    }
}
