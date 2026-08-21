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

    private static CandidateGenerationResult GenerateAll(
        TrustedSourceAvailability? trustedSource = null)
    {
        SupportMatrix matrix = SupportMatrix.ProvisionalV1();

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
        // quality already discarded.
        decimal sourceBits = WeightQuantisationMap.BitsPerWeight(WeightQuantisation.Q4_K_M);

        foreach (CompatibilityCandidate candidate in GenerateAll().Candidates)
        {
            GgufRouteConfiguration configuration =
                (GgufRouteConfiguration)candidate.Configuration;

            if (configuration.Weights == GgufWeightFormat.Imported)
            {
                continue;
            }

            WeightQuantisation target =
                GgufWeightFormatMap.ToCanonical(configuration.Weights);

            Assert.IsTrue(
                WeightQuantisationMap.BitsPerWeight(target) <= sourceBits,
                $"{candidate.SupportEntryId} converts upward to {target}.");
        }
    }

    [TestMethod]
    public void EveryCandidateIsEstimable()
    {
        // A generated candidate that the estimator cannot size would reach the
        // user as an option with no memory figure beside it.
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
            [.. SupportMatrix.ProvisionalV1().Entries.Select(entry => entry.EntryId)];

        Assert.IsTrue(GenerateAll().Candidates.All(
            candidate => entryIds.Contains(candidate.SupportEntryId)));
    }

    [TestMethod]
    public void EveryConversionCandidateDeclaresWeightConversionRequired()
    {
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
                Assert.AreEqual(
                    CandidatePreparation.WeightConversionRequired,
                    candidate.Preparation,
                    $"{candidate.SupportEntryId} writes a new file without saying so.");
            }
        }
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
    }

    [TestMethod]
    public void ExperimentalEntriesProduceOnlyExperimentalCandidates()
    {
        Dictionary<string, SupportLevel> levels = SupportMatrix.ProvisionalV1().Entries
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
        // The matrix's authoring order must not change which candidates exist,
        // only the order they appear in after the baseline.
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
