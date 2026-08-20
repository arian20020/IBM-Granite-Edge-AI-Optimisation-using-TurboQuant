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

    // Regression guard D: the pool router's expected outcome for every
    // DeviceRouteId x GpuOffloadLevel pairing, pinned so a newly added enum
    // member fails loudly instead of silently falling through a switch arm.
    // Driven from Enum.GetValues over both enums rather than a fixed list, so
    // a new device or offload level is swept automatically.
    //
    // The switch's default arm mirrors GgufPoolRouter's own default arm, so on
    // its own a newly added enum member would silently match "refuse,
    // UnsupportedDeviceRoute" on both sides and pass without anyone updating
    // this table. The pairing count below closes that hole: it is fixed at 20
    // for the current 5 DeviceRouteId x 4 GpuOffloadLevel members, so a new
    // member changes the total and fails this assertion, forcing the table to
    // be reviewed by hand rather than silently accepted by a matching default.
    [TestMethod]
    public void EveryDeviceAndOffloadPairing_RoutesToItsExpectedOutcome()
    {
        int pairingsVisited = 0;

        foreach (DeviceRouteId device in Enum.GetValues<DeviceRouteId>())
        {
            foreach (GpuOffloadLevel offload in Enum.GetValues<GpuOffloadLevel>())
            {
                pairingsVisited++;

                bool resolved = GgufPoolRouter.TryResolve(
                    device,
                    offload,
                    out GgufPoolRouting routing,
                    out EstimationUnavailableReason reason);

                string context = $"device={device}, offload={offload}";

                // The Partial check runs before the device switch, so every
                // device refuses with UnknownOffloadSplit under Partial offload
                // regardless of whether that device is otherwise supported.
                if (offload == GpuOffloadLevel.Partial)
                {
                    Assert.IsFalse(resolved, context);
                    Assert.AreEqual(
                        nameof(EstimationUnavailableReason.UnknownOffloadSplit),
                        reason.ToString(),
                        context);
                    continue;
                }

                switch (device, offload)
                {
                    case (DeviceRouteId.Cpu, GpuOffloadLevel.None):
                    case (DeviceRouteId.IntelIntegratedGpu, GpuOffloadLevel.None):
                    case (DeviceRouteId.IntelDiscreteGpu, GpuOffloadLevel.None):
                        Assert.IsTrue(resolved, context);
                        Assert.AreEqual(ResourceTarget.SystemMemory, routing.ModelTarget, context);
                        Assert.IsFalse(routing.RequiresHostStaging, context);
                        break;

                    case (DeviceRouteId.IntelIntegratedGpu, GpuOffloadLevel.Full):
                        Assert.IsTrue(resolved, context);
                        Assert.AreEqual(
                            ResourceTarget.SharedDeviceMemory, routing.ModelTarget, context);
                        Assert.IsFalse(routing.RequiresHostStaging, context);
                        break;

                    case (DeviceRouteId.IntelDiscreteGpu, GpuOffloadLevel.Full):
                        Assert.IsTrue(resolved, context);
                        Assert.AreEqual(
                            ResourceTarget.DedicatedDeviceMemory, routing.ModelTarget, context);
                        Assert.IsTrue(routing.RequiresHostStaging, context);
                        break;

                    default:
                        Assert.IsFalse(resolved, context);
                        Assert.AreEqual(
                            nameof(EstimationUnavailableReason.UnsupportedDeviceRoute),
                            reason.ToString(),
                            context);
                        break;
                }
            }
        }

        Assert.AreEqual(
            20,
            pairingsVisited,
            "The DeviceRouteId x GpuOffloadLevel pairing count changed, which means an "
            + "enum member was added or removed. Update the expected-outcome table above "
            + "for the new member, then update this expected total.");
    }
}
