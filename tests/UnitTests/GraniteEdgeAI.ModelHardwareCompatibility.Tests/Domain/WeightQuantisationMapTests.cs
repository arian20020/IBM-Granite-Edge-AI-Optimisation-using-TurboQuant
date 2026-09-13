using GraniteEdgeAI.ModelHardwareCompatibility.Core.Domain;

namespace GraniteEdgeAI.ModelHardwareCompatibility.Tests.Domain;

[TestClass]
public sealed class WeightQuantisationMapTests
{
    [TestMethod]
    [DataRow(0, nameof(WeightQuantisation.F32))]
    [DataRow(1, nameof(WeightQuantisation.F16))]
    [DataRow(7, nameof(WeightQuantisation.Q8_0))]
    [DataRow(12, nameof(WeightQuantisation.Q3_K_M))]
    [DataRow(15, nameof(WeightQuantisation.Q4_K_M))]
    [DataRow(17, nameof(WeightQuantisation.Q5_K_M))]
    [DataRow(18, nameof(WeightQuantisation.Q6_K))]
    [DataRow(32, nameof(WeightQuantisation.BF16))]
    public void FromGgufFileType_MapsKnownFileTypes(int fileType, string expected)
    {
        Assert.AreEqual(
            expected,
            WeightQuantisationMap.FromGgufFileType(fileType, quantisationVersion: 2).ToString());
    }

    [TestMethod]
    public void FromGgufFileType_UnknownFileTypeIsUnknownNotAGuess()
    {
        Assert.AreEqual(
            nameof(WeightQuantisation.Unknown),
            WeightQuantisationMap.FromGgufFileType(9999, quantisationVersion: 2).ToString());
    }

    [TestMethod]
    public void FromGgufFileType_MissingFileTypeIsUnknown()
    {
        Assert.AreEqual(
            nameof(WeightQuantisation.Unknown),
            WeightQuantisationMap.FromGgufFileType(fileType: null, quantisationVersion: 2)
                .ToString());
    }

    [TestMethod]
    public void FromGgufFileType_MissingQuantisationVersionIsUnknown()
    {
        // the version pins how the file type is to be read. a file type without
        // one is an unpinned number rather than an established fact
        Assert.AreEqual(
            nameof(WeightQuantisation.Unknown),
            WeightQuantisationMap.FromGgufFileType(fileType: 15, quantisationVersion: null)
                .ToString());
    }

    [TestMethod]
    [DataRow(1)]
    [DataRow(3)]
    [DataRow(99)]
    public void FromGgufFileType_UnrecognisedQuantisationVersionIsUnknown(int version)
    {
        // This table is built against version 2 only. Reading a file type from
        // any other version with this table would silently misstate the source
        // encoding, so every other version collapses to Unknown rather than
        // being decoded as if it were version 2.
        Assert.AreEqual(
            nameof(WeightQuantisation.Unknown),
            WeightQuantisationMap.FromGgufFileType(fileType: 15, quantisationVersion: version)
                .ToString());
    }

    [TestMethod]
    public void BitsPerWeight_IsMonotonicAcrossTheQualityLadder()
    {
        // the ladder is declared highest to lowest quality, so bits per weight
        // must never rise as it descends. a transposed table entry fails here
        WeightQuantisation[] ladder =
        [
            WeightQuantisation.F32,
            WeightQuantisation.F16,
            WeightQuantisation.Q8_0,
            WeightQuantisation.Q6_K,
            WeightQuantisation.Q5_K_M,
            WeightQuantisation.TQ4_1S,
            WeightQuantisation.Q4_K_M,
            WeightQuantisation.TQ3_1S,
            WeightQuantisation.Q3_K_M,
            WeightQuantisation.Q2_K
        ];

        for (int index = 1; index < ladder.Length; index++)
        {
            Assert.IsTrue(
                WeightQuantisationMap.BitsPerWeight(ladder[index])
                    < WeightQuantisationMap.BitsPerWeight(ladder[index - 1]),
                $"{ladder[index]} must use fewer bits per weight than {ladder[index - 1]}.");
        }
    }

    [TestMethod]
    public void BitsPerWeight_RejectsUnknown()
    {
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(
            () => WeightQuantisationMap.BitsPerWeight(WeightQuantisation.Unknown));
    }

}
