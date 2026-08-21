using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Candidates;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Contracts;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Estimation;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.FitAssessment;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.ModeSelection;
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

    // Regression guard: the previous version of this test put a limitation
    // into a HashSet, handed it to ResourceEstimate.Established, and asserted
    // the result contained it - that proves set-copy semantics, nothing about
    // whether any real estimator configuration ever produces the limitation.
    // This version drives GgufResourceEstimator itself and records, for each
    // non-Unspecified limitation, a real configuration that emits it. All four
    // are producible today (each is already exercised individually in
    // GgufResourceEstimatorTests); if a future limitation had no producer,
    // this fails loudly rather than passing on set-copy semantics alone.
    [TestMethod]
    public void EveryLimitation_IsProducedByARealEstimatorConfiguration()
    {
        InspectedModelFacts facts = InspectedModelFacts.Create(
            ByteCount.FromBytes(4_000_000_000),
            layerCount: 32,
            embeddingSize: 4096,
            attentionHeadCount: 32,
            keyValueHeadCount: 8,
            declaredContextLimit: 32768,
            fileType: 15,
            quantisationVersion: 2);

        // Baseline: imported weights, no conversion. Producer of
        // WeightsDerivedFromFileLength (every estimate carries it),
        // SingleSequenceAssumed (every estimate carries it) and
        // UncalibratedEstimatorPolicy (ProvisionalV1 is not Calibrated).
        CompatibilityCandidate baselineCandidate = CompatibilityCandidate.Create(
            GgufRouteConfiguration.Create(
                GgufWeightFormat.Imported,
                GgufKvCacheFormat.F16,
                CompatibilityBackend.Cpu,
                DeviceRouteId.Cpu,
                GpuOffloadLevel.None),
            ContextTokenCount.FromTokens(4096),
            CandidatePreparation.None,
            supportEntryId: "entry-1",
            isExperimental: false,
            isBaseline: true);

        ResourceEstimate baselineEstimate = GgufResourceEstimator.Estimate(
            facts, baselineCandidate, EstimatorPolicy.ProvisionalV1());

        // A weight-format conversion. Producer of
        // WeightsScaledAcrossQuantisation, which only fires when the target
        // format differs from the imported encoding.
        CompatibilityCandidate convertedCandidate = CompatibilityCandidate.Create(
            GgufRouteConfiguration.Create(
                GgufWeightFormat.Q8_0,
                GgufKvCacheFormat.F16,
                CompatibilityBackend.Cpu,
                DeviceRouteId.Cpu,
                GpuOffloadLevel.None),
            ContextTokenCount.FromTokens(4096),
            CandidatePreparation.WeightConversionRequired,
            supportEntryId: "entry-2",
            isExperimental: false,
            isBaseline: false);

        ResourceEstimate convertedEstimate = GgufResourceEstimator.Estimate(
            facts, convertedCandidate, EstimatorPolicy.ProvisionalV1());

        Dictionary<EstimationLimitation, ResourceEstimate> producers = new()
        {
            [EstimationLimitation.WeightsDerivedFromFileLength] = baselineEstimate,
            [EstimationLimitation.SingleSequenceAssumed] = baselineEstimate,
            [EstimationLimitation.UncalibratedEstimatorPolicy] = baselineEstimate,
            [EstimationLimitation.WeightsScaledAcrossQuantisation] = convertedEstimate,
            [EstimationLimitation.ConversionSourceStorageNotCounted] = convertedEstimate
        };

        foreach (EstimationLimitation limitation in Enum.GetValues<EstimationLimitation>())
        {
            if (limitation == EstimationLimitation.Unspecified)
            {
                continue;
            }

            Assert.IsTrue(
                producers.TryGetValue(limitation, out ResourceEstimate? estimate),
                $"{limitation} has no real estimator configuration recorded as its "
                + "producer. Either a configuration that emits it exists and must "
                + "be added here, or none does - which is itself a finding to "
                + "report, not a reason to weaken this test.");

            Assert.AreEqual(
                nameof(EstimationStatus.Established), estimate!.Status.ToString(), limitation.ToString());
            Assert.IsTrue(estimate.Limitations.Contains(limitation), limitation.ToString());
        }
    }

    // Regression guard: GgufResourceEstimator's policy guard must have a
    // defined outcome for every PolicyProvenance member, not just the ones
    // named explicitly in the guard's condition. Driven from Enum.GetValues so
    // a future member is swept automatically rather than silently falling
    // through to whichever branch its ordinal happens to satisfy.
    [TestMethod]
    public void EveryPolicyProvenance_HasADefinedEstimatorOutcome()
    {
        InspectedModelFacts facts = InspectedModelFacts.Create(
            ByteCount.FromBytes(4_000_000_000),
            layerCount: 32,
            embeddingSize: 4096,
            attentionHeadCount: 32,
            keyValueHeadCount: 8,
            declaredContextLimit: 32768,
            fileType: 15,
            quantisationVersion: 2);

        CompatibilityCandidate candidate = CompatibilityCandidate.Create(
            GgufRouteConfiguration.Create(
                GgufWeightFormat.Imported,
                GgufKvCacheFormat.F16,
                CompatibilityBackend.Cpu,
                DeviceRouteId.Cpu,
                GpuOffloadLevel.None),
            ContextTokenCount.FromTokens(4096),
            CandidatePreparation.None,
            supportEntryId: "entry-1",
            isExperimental: false,
            isBaseline: true);

        // EstimatorPolicy exposes only two factories today: ProvisionalV1
        // (Provenance = Provisional) and Absent (Provenance = Absent).
        // PolicyProvenance.Calibrated has no factory by design (see the
        // fix note on SafetyPolicy/EstimatorPolicy: adding one is deferred),
        // and Unspecified exists only as the enum's unset default, never
        // returned by any factory. Both are therefore unreachable through the
        // public API, which is asserted explicitly below instead of being
        // skipped without comment.
        Dictionary<string, EstimatorPolicy> reachable = new()
        {
            [nameof(PolicyProvenance.Provisional)] = EstimatorPolicy.ProvisionalV1(),
            [nameof(PolicyProvenance.Absent)] = EstimatorPolicy.Absent()
        };

        foreach (PolicyProvenance provenance in Enum.GetValues<PolicyProvenance>())
        {
            string name = provenance.ToString();

            if (!reachable.TryGetValue(name, out EstimatorPolicy? policy))
            {
                Assert.IsTrue(
                    provenance is PolicyProvenance.Calibrated or PolicyProvenance.Unspecified,
                    $"{name} is not constructible through EstimatorPolicy's public "
                    + "factories and is not one of the members explicitly recorded as "
                    + "unreachable. Add a factory or account for it above.");
                continue;
            }

            ResourceEstimate estimate = GgufResourceEstimator.Estimate(facts, candidate, policy);

            if (provenance == PolicyProvenance.Provisional)
            {
                Assert.AreEqual(
                    nameof(EstimationStatus.Established), estimate.Status.ToString(), name);
            }
            else
            {
                Assert.AreEqual(
                    nameof(EstimationStatus.NotEstablished), estimate.Status.ToString(), name);
                Assert.AreEqual(
                    nameof(EstimationUnavailableReason.EstimatorPolicyUnavailable),
                    estimate.Reason.ToString(),
                    name);
            }

            // The invariant the guard exists to protect: an Established
            // estimate built on a non-Calibrated policy always records
            // UncalibratedEstimatorPolicy. Nothing here can present an
            // unvalidated policy's numbers as if they were calibrated.
            if (estimate.Status == EstimationStatus.Established
                && provenance != PolicyProvenance.Calibrated)
            {
                Assert.IsTrue(
                    estimate.Limitations.Contains(EstimationLimitation.UncalibratedEstimatorPolicy),
                    $"{name} produced an Established estimate without "
                    + "UncalibratedEstimatorPolicy while not Calibrated.");
            }
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

    // Regression guard: ModeSelector.RefusalPrecedence names a stable,
    // most-fundamental-first order for reporting a refusal reason when
    // candidates fail different admission gates. It is a fixed array, not
    // something driven from Enum.GetValues, so a future ModeAdmissionReason
    // member added without a precedence entry would fall through
    // RefusalPrecedence.FirstOrDefault to ModeAdmissionReason.None - which
    // CompatibilityModeSelection.Unavailable then rejects, turning a
    // reportable refusal into a crash. Driven from Enum.GetValues so a new
    // member is swept automatically rather than silently missed.
    [TestMethod]
    public void EveryModeAdmissionReason_HasAPlaceInTheRefusalPrecedenceOrder()
    {
        foreach (ModeAdmissionReason reason in Enum.GetValues<ModeAdmissionReason>())
        {
            if (reason == ModeAdmissionReason.None)
            {
                continue;
            }

            Assert.IsTrue(
                ModeSelector.RefusalPrecedenceForTests.Contains(reason),
                $"{reason} has no place in ModeSelector's RefusalPrecedence order, so a "
                + "run refused only for this reason would report no reason at all. Add "
                + "it to the array.");
        }
    }
}
