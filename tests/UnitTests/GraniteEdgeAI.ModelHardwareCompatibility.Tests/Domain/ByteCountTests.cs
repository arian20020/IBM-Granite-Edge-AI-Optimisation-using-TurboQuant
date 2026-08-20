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
}
