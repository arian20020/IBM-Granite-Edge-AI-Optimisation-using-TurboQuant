using GraniteEdgeAI.ModelHardwareCompatibility.Core.Domain;

namespace GraniteEdgeAI.ModelHardwareCompatibility.Tests.Domain;

[TestClass]
public sealed class ByteCountTests
{
    [TestMethod]
    public void Add_Overflow_Throws()
    {
        ByteCount max = ByteCount.FromBytes(ulong.MaxValue);
        Assert.ThrowsExactly<OverflowException>(
            () => _ = max.Add(ByteCount.FromBytes(1)));
    }

    [TestMethod]
    public void Subtract_BelowZero_Throws()
    {
        ByteCount small = ByteCount.FromBytes(4);
        Assert.ThrowsExactly<OverflowException>(
            () => _ = small.Subtract(ByteCount.FromBytes(5)));
    }

    [TestMethod]
    public void TrySubtract_BelowZero_ReturnsFalseAndDoesNotThrow()
    {
        bool ok = ByteCount.FromBytes(4)
            .TrySubtract(ByteCount.FromBytes(5), out ByteCount remainder);

        Assert.IsFalse(ok);
        Assert.AreEqual(ByteCount.Zero, remainder);
    }

    [TestMethod]
    public void CeilingDivide_RoundsUpwardOnly()
    {
        Assert.AreEqual(4UL, ByteCount.FromBytes(13).CeilingDivide(4).Bytes);
        Assert.AreEqual(3UL, ByteCount.FromBytes(12).CeilingDivide(4).Bytes);
    }

    [TestMethod]
    public void CeilingDivide_NearMaxValue_DoesNotOverflow()
    {
        ByteCount result = ByteCount.FromBytes(ulong.MaxValue).CeilingDivide(2);
        Assert.AreEqual((ulong.MaxValue / 2) + 1, result.Bytes);
    }

    [TestMethod]
    public void CeilingDivide_ByZero_Throws()
    {
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(
            () => _ = ByteCount.FromBytes(8).CeilingDivide(0));
    }

    [TestMethod]
    public void RatioAgainst_Zero_Throws()
    {
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(
            () => _ = ByteCount.FromBytes(8).RatioAgainst(ByteCount.Zero));
    }

    [TestMethod]
    public void RatioAgainst_ComputesDecimalRatio()
    {
        decimal ratio = ByteCount.FromBytes(75).RatioAgainst(ByteCount.FromBytes(100));
        Assert.AreEqual(0.75m, ratio);
    }

    [TestMethod]
    public void Equality_IsByValue()
    {
        Assert.AreEqual(ByteCount.FromBytes(42), ByteCount.FromBytes(42));
        Assert.IsTrue(ByteCount.FromBytes(1) < ByteCount.FromBytes(2));
    }

    [TestMethod]
    [DataRow(0UL, 4096UL, 0UL)]
    [DataRow(1UL, 4096UL, 4096UL)]
    [DataRow(4095UL, 4096UL, 4096UL)]
    [DataRow(4096UL, 4096UL, 4096UL)]
    [DataRow(4097UL, 4096UL, 8192UL)]
    public void AlignUpTo_RoundsUpwardOnly(ulong bytes, ulong alignment, ulong expected)
    {
        Assert.AreEqual(
            expected,
            ByteCount.FromBytes(bytes).AlignUpTo(alignment).Bytes);
    }

    [TestMethod]
    public void AlignUpTo_RejectsZeroAlignment()
    {
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(
            () => ByteCount.FromBytes(1).AlignUpTo(0));
    }

    [TestMethod]
    public void AlignUpTo_RejectsNonPowerOfTwoAlignment()
    {
        // A non-power-of-two "alignment" is almost always a units mistake, and
        // silently accepting it would produce a plausible but wrong number
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(
            () => ByteCount.FromBytes(1).AlignUpTo(3000));
    }

    [TestMethod]
    public void AlignUpTo_OverflowIsLoudRatherThanWrapped()
    {
        Assert.ThrowsExactly<OverflowException>(
            () => ByteCount.FromBytes(ulong.MaxValue).AlignUpTo(4096));
    }

    [TestMethod]
    [DataRow(1000UL, 0.10, 100UL)]
    [DataRow(1001UL, 0.10, 101UL)]
    [DataRow(0UL, 0.10, 0UL)]
    [DataRow(1000UL, 0.0, 0UL)]
    public void MultiplyByFraction_RoundsUpward(ulong bytes, double fraction, ulong expected)
    {
        // Rounding up keeps every overhead term on the conservative side of the
        // one-directional bias: an understated overhead is a false-safe result.
        Assert.AreEqual(
            expected,
            ByteCount.FromBytes(bytes).MultiplyByFraction((decimal)fraction).Bytes);
    }

    [TestMethod]
    public void MultiplyByFraction_RejectsNegativeFraction()
    {
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(
            () => ByteCount.FromBytes(1000).MultiplyByFraction(-0.01m));
    }
}
