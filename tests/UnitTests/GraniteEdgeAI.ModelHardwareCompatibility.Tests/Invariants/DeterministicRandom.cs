namespace GraniteEdgeAI.ModelHardwareCompatibility.Tests.Invariants;

/// <summary>
/// A seeded xorshift generator. The framework Random is not contractually stable
/// across runtime versions, and a property failure that cannot be reproduced from
/// its seed is a property test that cannot be debugged.
/// </summary>
internal struct DeterministicRandom
{
    private ulong _state;

    internal DeterministicRandom(ulong seed) =>
        _state = seed == 0 ? 0x9E3779B97F4A7C15UL : seed;

    internal ulong NextUInt64()
    {
        _state ^= _state >> 12;
        _state ^= _state << 25;
        _state ^= _state >> 27;
        return unchecked(_state * 0x2545F4914F6CDD1DUL);
    }

    internal int Next(int minInclusive, int maxInclusive) =>
        minInclusive + (int)(NextUInt64() % (ulong)(maxInclusive - minInclusive + 1));

    internal T Pick<T>(IReadOnlyList<T> options) => options[Next(0, options.Count - 1)];
}
