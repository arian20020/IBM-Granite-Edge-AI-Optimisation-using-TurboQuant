using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Estimation;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Domain;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Routes.Gguf;

namespace GraniteEdgeAI.ModelHardwareCompatibility.Tests.Invariants;

/// <summary>
/// Sweeps that fail when a new enum member is added without a corresponding
/// rule. Without these, a new weight format or cache format would fall through a
/// switch expression and be sized as free - a silent false-safe result.
/// </summary>
[TestClass]
public sealed class EnumExhaustivenessTests
{
    [TestMethod]
    public void EveryKvCacheFormat_HasARecordedBlockEncoding()
    {
        foreach (GgufKvCacheFormat format in Enum.GetValues<GgufKvCacheFormat>())
        {
            if (format == GgufKvCacheFormat.Unspecified)
            {
                continue;
            }

            Assert.IsTrue(
                GgufKvCacheBlockSpec.TryFor(format, out GgufKvCacheBlockSpec spec),
                $"{format} has no recorded block encoding, so it would be sized as free.");

            Assert.IsTrue(spec.ValuesPerBlock > 0 && spec.BytesPerBlock > 0);
        }
    }

    [TestMethod]
    public void EveryWeightFormat_MapsToAnEncodingWithABitWidth()
    {
        foreach (GgufWeightFormat format in Enum.GetValues<GgufWeightFormat>())
        {
            if (format is GgufWeightFormat.Unspecified or GgufWeightFormat.Imported)
            {
                continue;
            }

            WeightQuantisation quantisation = GgufWeightFormatMap.ToCanonical(format);

            Assert.AreNotEqual(
                WeightQuantisation.Unknown,
                quantisation,
                $"{format} has no canonical encoding.");

            Assert.IsTrue(WeightQuantisationMap.BitsPerWeight(quantisation) > 0m);
        }
    }

    [TestMethod]
    public void EveryCanonicalQuantisation_HasABitWidth()
    {
        foreach (WeightQuantisation quantisation in Enum.GetValues<WeightQuantisation>())
        {
            if (quantisation == WeightQuantisation.Unknown)
            {
                continue;
            }

            Assert.IsTrue(
                WeightQuantisationMap.BitsPerWeight(quantisation) > 0m,
                $"{quantisation} has no recorded bit width.");
        }
    }

    [TestMethod]
    public void EveryLimitation_IsAcceptedByAnEstablishedEstimate()
    {
        foreach (EstimationLimitation limitation in Enum.GetValues<EstimationLimitation>())
        {
            if (limitation == EstimationLimitation.Unspecified)
            {
                continue;
            }

            ResourceEstimate estimate = ResourceEstimate.Established(
                [
                    ResourceComponent.Create(
                        ResourceComponentKind.Weights,
                        ResourceTarget.SystemMemory,
                        ByteCount.FromBytes(1024),
                        new HashSet<LifecyclePhase> { LifecyclePhase.Load })
                ],
                new HashSet<EstimationLimitation> { limitation });

            Assert.IsTrue(estimate.Limitations.Contains(limitation));
        }
    }

    [TestMethod]
    public void EveryUnavailableReason_IsDistinctlyNamed()
    {
        // Stable codes carry the whole explanation to the user, so a duplicated
        // value would collapse two different failures into one message.
        EstimationUnavailableReason[] values = Enum.GetValues<EstimationUnavailableReason>();

        Assert.AreEqual(values.Length, values.Distinct().Count());
    }
}
