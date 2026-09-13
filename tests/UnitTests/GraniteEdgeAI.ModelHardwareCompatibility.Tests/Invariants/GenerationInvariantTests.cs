using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Candidates;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Capabilities;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Contracts;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Estimation;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Domain;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Routes.Gguf;

namespace GraniteEdgeAI.ModelHardwareCompatibility.Tests.Invariants;

/// <summary>
/// Properties of generation that must hold however the matrix is later edited.
/// A new entry that violates one of these is an offer the design forbids.
/// </summary>
[TestClass]
public sealed class GenerationInvariantTests
{
    private static InspectedModelFacts Facts() =>
        InspectedModelFacts.Create(
            ByteCount.FromBytes(4_000_000_000),
            layerCount: 32,
            embeddingSize: 4096,
            attentionHeadCount: 32,
            keyValueHeadCount: 8,
            declaredContextLimit: 32768,
            fileType: 15,
            quantisationVersion: 2);

    /// <summary>
    /// ProvisionalV1 plus one genuine downward-conversion entry (Q4_K_M -&gt;
    /// Q3_K_M). ProvisionalV1 alone has no entry that survives generation as
    /// anything other than Imported: its lone non-Imported entry
    /// ("gguf-cpu-q4km-f16") names the same encoding the Q4_K_M source file
    /// already has, so TryResolvePreparationKind normalises it straight back
    /// to Imported and it dedupes onto the baseline. Every conversion-focused
    /// invariant below needs a candidate that actually converts, or its loop
    /// body never runs.
    /// </summary>
    private static SupportMatrix MatrixWithADownwardConversionEntry()
    {
        SupportMatrix baseline = SupportMatrix.ProvisionalV1();

        CompatibilitySupportEntry downwardConversion = CompatibilitySupportEntry.Create(
            "gguf-cpu-q3km-f16",
            RuntimeRouteId.LlamaCpp,
            CompatibilityBackend.Cpu,
            DeviceRouteId.Cpu,
            GpuOffloadLevel.None,
            GgufWeightFormat.Q3KM,
            GgufKvCacheFormat.F16,
            minimumContextTokens: 1024,
            maximumContextTokens: 32768,
            SupportLevel.DeclaredSupported,
            requiresEvidence: false);

        return SupportMatrix.FromEntries(
            baseline.MatrixVersion,
            baseline.Provenance,
            [.. baseline.Entries, downwardConversion]);
    }

    private static CandidateGenerationResult GenerateAll(
        TrustedSourceAvailability? trustedSource = null)
    {
        SupportMatrix matrix = MatrixWithADownwardConversionEntry();

        return CandidateGenerator.Generate(CandidateGenerationRequest.Create(
            matrix,
            matrix.Entries.ToDictionary(
                entry => entry.EntryId,
                entry => entry.Level == SupportLevel.Experimental
                    ? InstallationState.VerifiedAndOptedIn
                    : InstallationState.InstalledAndVerified),
            Facts(),
            GgufRouteConfiguration.Create(
                GgufWeightFormat.Imported,
                GgufKvCacheFormat.F16,
                CompatibilityBackend.Cpu,
                DeviceRouteId.Cpu,
                GpuOffloadLevel.None),
            ContextTokenCount.FromTokens(4096),
            ContextTokenCount.FromTokens(8192),
            trustedSource ?? TrustedSourceAvailability.HigherPrecisionAvailable()));
    }

    [TestMethod]
    public void NoGeneratedCandidateEverConvertsUpward()
    {
        // The source is Q4_K_M throughout. Any candidate whose target encoding
        // uses more bits per weight would be an upgrade that cannot restore
        // quality already discarded
        decimal sourceBits = WeightQuantisationMap.BitsPerWeight(WeightQuantisation.Q4_K_M);

        bool sawNonImportedCandidate = false;

        foreach (CompatibilityCandidate candidate in GenerateAll().Candidates)
        {
            GgufRouteConfiguration configuration =
                (GgufRouteConfiguration)candidate.Configuration;

            if (configuration.Weights == GgufWeightFormat.Imported)
            {
                continue;
            }

            sawNonImportedCandidate = true;

            WeightQuantisation target =
                GgufWeightFormatMap.ToCanonical(configuration.Weights);

            Assert.IsTrue(
                WeightQuantisationMap.BitsPerWeight(target) <= sourceBits,
                $"{candidate.SupportEntryId} converts upward to {target}.");
        }

        // Guards against this invariant going vacuous again: without at least
        // one non-Imported candidate, the loop above never runs its body and
        // the test passes no matter what the generator does
        Assert.IsTrue(
            sawNonImportedCandidate,
            "No non-Imported candidate was generated; this invariant checked nothing.");
    }

    [TestMethod]
    public void EveryCandidateIsEstimable()
    {
        // a generated candidate that the estimator cannot size would reach the
        // user as an option with no memory figure beside it
        foreach (CompatibilityCandidate candidate in GenerateAll().Candidates)
        {
            ResourceEstimate estimate = GgufResourceEstimator.Estimate(
                Facts(), candidate, EstimatorPolicy.ProvisionalV1());

            Assert.AreEqual(
                nameof(EstimationStatus.Established),
                estimate.Status.ToString(),
                $"{candidate.SupportEntryId} at {candidate.Context} gave {estimate.Reason}.");
        }
    }

    [TestMethod]
    public void EveryCandidateNamesAnEntryThatExistsInTheMatrix()
    {
        HashSet<string> entryIds =
            [.. MatrixWithADownwardConversionEntry().Entries.Select(entry => entry.EntryId)];

        Assert.IsTrue(GenerateAll().Candidates.All(
            candidate => entryIds.Contains(candidate.SupportEntryId)));
    }

    [TestMethod]
    public void EveryConversionCandidateDeclaresWeightConversionRequired()
    {
        bool sawAConversionCandidate = false;

        foreach (CompatibilityCandidate candidate in GenerateAll().Candidates)
        {
            GgufRouteConfiguration configuration =
                (GgufRouteConfiguration)candidate.Configuration;

            bool changesEncoding =
                configuration.Weights != GgufWeightFormat.Imported
                && GgufWeightFormatMap.ToCanonical(configuration.Weights)
                    != WeightQuantisation.Q4_K_M;

            if (changesEncoding)
            {
                sawAConversionCandidate = true;

                Assert.AreEqual(
                    CandidatePreparation.WeightConversionRequired,
                    candidate.Preparation,
                    $"{candidate.SupportEntryId} writes a new file without saying so.");
            }
        }

        // Guards against this invariant going vacuous again: without at least
        // one encoding-changing candidate, the loop above never runs its
        // Assert.AreEqual and the test passes no matter what the generator does.
        Assert.IsTrue(
            sawAConversionCandidate,
            "No encoding-changing candidate was generated; this invariant checked nothing.");
    }

    [TestMethod]
    public void NoCandidateIsGeneratedWithoutATrustedSourceExceptImportedEncodings()
    {
        foreach (CompatibilityCandidate candidate in
            GenerateAll(TrustedSourceAvailability.None()).Candidates)
        {
            Assert.AreNotEqual(
                CandidatePreparation.WeightConversionRequired,
                candidate.Preparation,
                $"{candidate.SupportEntryId} converts with no trusted source.");
        }

        // Guards against this invariant going vacuous again: it is only
        // meaningful if, with a trusted source available, the same matrix
        // and facts DO produce a WeightConversionRequired candidate. Without
        // that, "no candidate converts without a trusted source" would be
        // true merely because no candidate ever converts at all
        Assert.IsTrue(
            GenerateAll(TrustedSourceAvailability.HigherPrecisionAvailable()).Candidates.Any(
                candidate => candidate.Preparation == CandidatePreparation.WeightConversionRequired),
            "No candidate ever declares WeightConversionRequired even with a trusted "
            + "source available; this invariant checked nothing.");
    }

    [TestMethod]
    public void ExperimentalEntriesProduceOnlyExperimentalCandidates()
    {
        Dictionary<string, SupportLevel> levels = MatrixWithADownwardConversionEntry().Entries
            .ToDictionary(entry => entry.EntryId, entry => entry.Level);

        foreach (CompatibilityCandidate candidate in GenerateAll().Candidates)
        {
            Assert.AreEqual(
                levels[candidate.SupportEntryId] == SupportLevel.Experimental,
                candidate.IsExperimental,
                $"{candidate.SupportEntryId} disagrees with its entry's support level.");
        }
    }

    [TestMethod]
    public void GenerationIsStableUnderEntryReordering()
    {
        // the matrix's authoring order must not change which candidates exist,
        // only the order they appear in after the baseline
        SupportMatrix forward = SupportMatrix.ProvisionalV1();
        SupportMatrix reversed = SupportMatrix.FromEntries(
            forward.MatrixVersion,
            forward.Provenance,
            [.. forward.Entries.Reverse()]);

        Dictionary<string, InstallationState> installation = forward.Entries.ToDictionary(
            entry => entry.EntryId,
            entry => entry.Level == SupportLevel.Experimental
                ? InstallationState.VerifiedAndOptedIn
                : InstallationState.InstalledAndVerified);

        CandidateGenerationRequest Request(SupportMatrix matrix) => CandidateGenerationRequest.Create(
            matrix,
            installation,
            Facts(),
            GgufRouteConfiguration.Create(
                GgufWeightFormat.Imported,
                GgufKvCacheFormat.F16,
                CompatibilityBackend.Cpu,
                DeviceRouteId.Cpu,
                GpuOffloadLevel.None),
            ContextTokenCount.FromTokens(4096),
            ContextTokenCount.FromTokens(8192),
            TrustedSourceAvailability.HigherPrecisionAvailable());

        HashSet<string> forwardPrints =
            [.. CandidateGenerator.Generate(Request(forward)).Candidates
                .Select(candidate => candidate.Fingerprint.Value)];

        HashSet<string> reversedPrints =
            [.. CandidateGenerator.Generate(Request(reversed)).Candidates
                .Select(candidate => candidate.Fingerprint.Value)];

        Assert.IsTrue(forwardPrints.SetEquals(reversedPrints));
    }

    [TestMethod]
    public void TheBaselineIsAlwaysFirstWhenItIsIncluded()
    {
        CandidateGenerationResult result = GenerateAll();

        Assert.IsTrue(result.BaselineIncluded);
        Assert.IsTrue(result.Candidates[0].IsBaseline);
        Assert.IsFalse(result.Candidates.Skip(1).Any(candidate => candidate.IsBaseline));
    }
}
