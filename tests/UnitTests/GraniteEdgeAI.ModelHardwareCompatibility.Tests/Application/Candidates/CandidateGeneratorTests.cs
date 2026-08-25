using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Candidates;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Capabilities;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Contracts;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.FitAssessment;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Domain;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Routes.Gguf;

namespace GraniteEdgeAI.ModelHardwareCompatibility.Tests.Application.Candidates;

[TestClass]
public sealed class CandidateGeneratorTests
{
    private static InspectedModelFacts Facts(int? fileType = 15) =>
        InspectedModelFacts.Create(
            ByteCount.FromBytes(4_000_000_000),
            layerCount: 32,
            embeddingSize: 4096,
            attentionHeadCount: 32,
            keyValueHeadCount: 8,
            declaredContextLimit: 8192,
            fileType,
            quantisationVersion: 2);

    private static GgufRouteConfiguration Baseline() =>
        GgufRouteConfiguration.Create(
            GgufWeightFormat.Imported,
            GgufKvCacheFormat.F16,
            CompatibilityBackend.Cpu,
            DeviceRouteId.Cpu,
            GpuOffloadLevel.None);

    private static Dictionary<string, InstallationState> AllInstalled(SupportMatrix matrix) =>
        matrix.Entries.ToDictionary(
            entry => entry.EntryId,
            entry => entry.Level == SupportLevel.Experimental
                ? InstallationState.VerifiedAndOptedIn
                : InstallationState.InstalledAndVerified);

    private static CandidateGenerationResult Generate(
        SupportMatrix? matrix = null,
        IReadOnlyDictionary<string, InstallationState>? installation = null,
        InspectedModelFacts? facts = null,
        TrustedSourceAvailability? trustedSource = null,
        int preservationTokens = 4096)
    {
        SupportMatrix resolved = matrix ?? SupportMatrix.ProvisionalV1();

        return CandidateGenerator.Generate(CandidateGenerationRequest.Create(
            resolved,
            installation ?? AllInstalled(resolved),
            facts ?? Facts(),
            Baseline(),
            ContextTokenCount.FromTokens(4096),
            ContextTokenCount.FromTokens(preservationTokens),
            trustedSource ?? TrustedSourceAvailability.None()));
    }

    [TestMethod]
    public void Generate_PutsTheBaselineFirst()
    {
        CandidateGenerationResult result = Generate();

        Assert.IsTrue(result.Candidates.Count > 0);
        Assert.IsTrue(result.Candidates[0].IsBaseline);
        Assert.IsTrue(result.BaselineIncluded);
        Assert.AreEqual(
            nameof(BaselineExclusionReason.None), result.BaselineExclusionReason.ToString());
    }

    [TestMethod]
    public void Generate_MarksExactlyOneCandidateAsTheBaseline()
    {
        Assert.AreEqual(1, Generate().Candidates.Count(candidate => candidate.IsBaseline));
    }

    [TestMethod]
    public void Generate_ProducesNoDuplicateFingerprints()
    {
        IReadOnlyList<CompatibilityCandidate> candidates = Generate().Candidates;

        Assert.AreEqual(
            candidates.Count,
            candidates.Select(candidate => candidate.Fingerprint.Value).Distinct().Count());
    }

    [TestMethod]
    public void Generate_AdmitsNothingFromAnAbsentMatrix()
    {
        // An absent matrix means we do not know what this machine supports.
        // Offering anything would be inventing a capability.
        CandidateGenerationResult result = Generate(
            matrix: SupportMatrix.Absent(),
            installation: new Dictionary<string, InstallationState>());

        Assert.AreEqual(0, result.Candidates.Count);
        Assert.IsFalse(result.BaselineIncluded);
        Assert.AreEqual(
            nameof(BaselineExclusionReason.SupportMatrixUnavailable),
            result.BaselineExclusionReason.ToString());
    }

    [TestMethod]
    public void Generate_SkipsEntriesWhoseInstallationIsUnknown()
    {
        // An entry with no reported installation state resolves to Unsupported,
        // so nothing from it may be offered. Unknown is not the same claim as
        // "declared supported but genuinely not installed" (which resolves to
        // Unavailable, not Unsupported), so this must not be reported as
        // BaselineEntryNotInstalled: nothing here establishes that the backend
        // is absent, only that its state was never reported.
        //
        // Nor is it "nothing matches". An entry does describe what the user
        // has - we simply could not read its state - and telling them no
        // supported setup matches theirs would send them to change something
        // that was never the problem.
        SupportMatrix matrix = SupportMatrix.ProvisionalV1();

        CandidateGenerationResult result = Generate(
            matrix: matrix,
            installation: new Dictionary<string, InstallationState>());

        Assert.AreEqual(0, result.Candidates.Count);
        Assert.AreEqual(
            nameof(BaselineExclusionReason.BaselineEntrySupportStateUnknown),
            result.BaselineExclusionReason.ToString());
    }

    [TestMethod]
    public void Generate_ReportsNotInstalledForAnExperimentalEntryThatIsAbsent()
    {
        // The old rule keyed on support level alone, so any experimental entry
        // that failed to admit was reported as needing an opt-in. For one that
        // is not installed at all that is advice the user cannot act on:
        // opting in to a backend that is not there changes nothing. Installing
        // it is the fix, and it is the same fix as for a declared-supported
        // entry in the same state.
        SupportMatrix matrix = SupportMatrix.FromEntries(
            "v-experimental-absent",
            PolicyProvenance.Provisional,
            [
                CompatibilitySupportEntry.Create(
                    "experimental-absent",
                    RuntimeRouteId.LlamaCpp,
                    CompatibilityBackend.Cpu,
                    DeviceRouteId.Cpu,
                    GpuOffloadLevel.None,
                    GgufWeightFormat.Imported,
                    GgufKvCacheFormat.F16,
                    1024,
                    32768,
                    SupportLevel.Experimental,
                    requiresEvidence: false)
            ]);

        CandidateGenerationResult result = Generate(
            matrix: matrix,
            installation: new Dictionary<string, InstallationState>
            {
                ["experimental-absent"] = InstallationState.NotInstalled
            });

        Assert.IsFalse(result.BaselineIncluded);
        Assert.AreEqual(
            nameof(BaselineExclusionReason.BaselineEntryNotInstalled),
            result.BaselineExclusionReason.ToString());
    }

    [TestMethod]
    public void Generate_ReportsBaselineEntryNotInstalledWhenDeclaredSupportedButNotInstalled()
    {
        // The baseline-matching entry is DeclaredSupported and its installation
        // state is explicitly NotInstalled, which SupportMatrixResolver
        // resolves to Unavailable: the backend is genuinely absent, and the
        // fix is installing it.
        SupportMatrix matrix = SupportMatrix.FromEntries(
            "v-baseline-not-installed",
            PolicyProvenance.Provisional,
            [
                CompatibilitySupportEntry.Create(
                    "baseline-not-installed",
                    RuntimeRouteId.LlamaCpp,
                    CompatibilityBackend.Cpu,
                    DeviceRouteId.Cpu,
                    GpuOffloadLevel.None,
                    GgufWeightFormat.Imported,
                    GgufKvCacheFormat.F16,
                    1024,
                    32768,
                    SupportLevel.DeclaredSupported,
                    requiresEvidence: false)
            ]);

        CandidateGenerationResult result = Generate(
            matrix: matrix,
            installation: new Dictionary<string, InstallationState>
            {
                ["baseline-not-installed"] = InstallationState.NotInstalled
            });

        Assert.AreEqual(0, result.Candidates.Count);
        Assert.IsFalse(result.BaselineIncluded);
        Assert.AreEqual(
            nameof(BaselineExclusionReason.BaselineEntryNotInstalled),
            result.BaselineExclusionReason.ToString());
    }

    [TestMethod]
    public void Generate_ReportsBaselineEntryRequiresExperimentalOptInWhenInstalledButNotOptedIn()
    {
        // The baseline-matching entry is Experimental and is installed and
        // verified, but the user has not opted in: SupportMatrixResolver
        // resolves this to Unsupported, not ExperimentalAvailable, because an
        // experimental route needs explicit opt-in, not merely installation.
        // The actionable fix here is opting in, not installing anything, so
        // this must be distinguished from BaselineEntryNotInstalled.
        SupportMatrix matrix = SupportMatrix.FromEntries(
            "v-baseline-needs-optin",
            PolicyProvenance.Provisional,
            [
                CompatibilitySupportEntry.Create(
                    "baseline-needs-optin",
                    RuntimeRouteId.LlamaCpp,
                    CompatibilityBackend.Cpu,
                    DeviceRouteId.Cpu,
                    GpuOffloadLevel.None,
                    GgufWeightFormat.Imported,
                    GgufKvCacheFormat.F16,
                    1024,
                    32768,
                    SupportLevel.Experimental,
                    requiresEvidence: true)
            ]);

        CandidateGenerationResult result = Generate(
            matrix: matrix,
            installation: new Dictionary<string, InstallationState>
            {
                ["baseline-needs-optin"] = InstallationState.InstalledAndVerified
            });

        Assert.AreEqual(0, result.Candidates.Count);
        Assert.IsFalse(result.BaselineIncluded);
        Assert.AreEqual(
            nameof(BaselineExclusionReason.BaselineEntryRequiresExperimentalOptIn),
            result.BaselineExclusionReason.ToString());
    }

    [TestMethod]
    public void Generate_ReportsNoAdmittedEntryMatchesTheBaselineWhenNoEntryConfigurationMatchesIt()
    {
        // Every entry in this matrix is on a different device to the baseline
        // (which is Cpu), so no entry's configuration ever equals the
        // baseline's - not "matched but excluded", but no match at all.
        SupportMatrix matrix = SupportMatrix.FromEntries(
            "v-no-matching-configuration",
            PolicyProvenance.Provisional,
            [
                CompatibilitySupportEntry.Create(
                    "igpu-only",
                    RuntimeRouteId.LlamaCpp,
                    CompatibilityBackend.IntelSycl,
                    DeviceRouteId.IntelIntegratedGpu,
                    GpuOffloadLevel.Full,
                    GgufWeightFormat.Imported,
                    GgufKvCacheFormat.F16,
                    1024,
                    32768,
                    SupportLevel.DeclaredSupported,
                    requiresEvidence: false)
            ]);

        CandidateGenerationResult result = Generate(
            matrix: matrix, installation: AllInstalled(matrix));

        // The igpu entry still yields candidates of its own; what matters is
        // that none of them is the baseline, and that the reason names a
        // configuration mismatch rather than an installation problem.
        Assert.IsFalse(result.Candidates.Any(candidate => candidate.IsBaseline));
        Assert.IsFalse(result.BaselineIncluded);
        Assert.AreEqual(
            nameof(BaselineExclusionReason.NoAdmittedEntryMatchesTheBaseline),
            result.BaselineExclusionReason.ToString());
    }

    [TestMethod]
    public void Generate_RefusesWithModelContextLimitNotEstablishedWhenTheLimitIsUnknown()
    {
        // A model whose trained context limit is unknown gives no safe basis
        // for a default: this is the refusal branch, and it must actually
        // refuse rather than silently substituting a limit.
        CandidateGenerationResult result = Generate(
            facts: InspectedModelFacts.Create(
                ByteCount.FromBytes(4_000_000_000),
                layerCount: 32,
                embeddingSize: 4096,
                attentionHeadCount: 32,
                keyValueHeadCount: 8,
                declaredContextLimit: null,
                fileType: 15,
                quantisationVersion: 2));

        Assert.AreEqual(0, result.Candidates.Count);
        Assert.IsFalse(result.BaselineIncluded);
        Assert.AreEqual(
            nameof(BaselineExclusionReason.ModelContextLimitNotEstablished),
            result.BaselineExclusionReason.ToString());
    }

    [TestMethod]
    public void Generate_NeverOffersAnUpwardConversion()
    {
        // The source is Q4_K_M. An entry asking for Q8_0 would be an upgrade that
        // cannot restore quality already discarded, so it is never generated.
        SupportMatrix matrix = SupportMatrix.FromEntries(
            "v-upward",
            PolicyProvenance.Provisional,
            [
                CompatibilitySupportEntry.Create(
                    "upward-q8",
                    RuntimeRouteId.LlamaCpp,
                    CompatibilityBackend.Cpu,
                    DeviceRouteId.Cpu,
                    GpuOffloadLevel.None,
                    GgufWeightFormat.Q8_0,
                    GgufKvCacheFormat.F16,
                    1024,
                    32768,
                    SupportLevel.DeclaredSupported,
                    requiresEvidence: false)
            ]);

        CandidateGenerationResult result = Generate(
            matrix: matrix,
            installation: AllInstalled(matrix),
            trustedSource: TrustedSourceAvailability.HigherPrecisionAvailable());

        Assert.AreEqual(0, result.Candidates.Count);
    }

    [TestMethod]
    public void Generate_NeverOffersTheSameConfigurationTwice()
    {
        // With the default ProvisionalV1 matrix and a Q4_K_M source,
        // gguf-cpu-q4km-f16 names the encoding the file already has: it must
        // normalise back to the same runtime configuration as
        // gguf-cpu-imported-f16 rather than being offered as a second,
        // falsely-labelled "conversion" to a configuration that is really
        // identical.
        IReadOnlyList<CompatibilityCandidate> candidates = Generate().Candidates;

        var shapes = candidates
            .Select(candidate => (candidate.Configuration, candidate.Context))
            .ToList();

        Assert.AreEqual(shapes.Count, shapes.Distinct().Count());
    }

    [TestMethod]
    public void Generate_ReportsBaselineContextOutsideEntryBoundsWhenTheEntryMatchesButNoAdmittedContextDoes()
    {
        // The entry's configuration is exactly the baseline's, so the
        // baseline shape is admitted in principle. But the entry's minimum
        // context (8192) sits above the baseline context (4096), so no
        // admitted context for this entry ever equals the baseline context.
        // The caller must be told the entry matched and the context did not,
        // not that nothing matched the baseline at all.
        SupportMatrix matrix = SupportMatrix.FromEntries(
            "v-baseline-context-oob",
            PolicyProvenance.Provisional,
            [
                CompatibilitySupportEntry.Create(
                    "baseline-high-minimum",
                    RuntimeRouteId.LlamaCpp,
                    CompatibilityBackend.Cpu,
                    DeviceRouteId.Cpu,
                    GpuOffloadLevel.None,
                    GgufWeightFormat.Imported,
                    GgufKvCacheFormat.F16,
                    minimumContextTokens: 8192,
                    maximumContextTokens: 32768,
                    SupportLevel.DeclaredSupported,
                    requiresEvidence: false)
            ]);

        CandidateGenerationResult result =
            Generate(matrix: matrix, installation: AllInstalled(matrix));

        Assert.IsFalse(result.BaselineIncluded);
        Assert.AreEqual(
            nameof(BaselineExclusionReason.BaselineContextOutsideEntryBounds),
            result.BaselineExclusionReason.ToString());
    }

    [TestMethod]
    public void Generate_RefusesAQuantisedEntryWhenTheSourceEncodingIsUnknown()
    {
        // The source encoding could not be established (no file type). Without
        // it there is no way to tell whether Q4_K_M would be an upgrade, a
        // no-op, or a genuine downward conversion, so nothing may be offered
        // from an entry asking for it. This guard is also what stops
        // WeightQuantisationMap.BitsPerWeight being asked for the bit width of
        // Unknown.
        SupportMatrix matrix = SupportMatrix.FromEntries(
            "v-unknown-source-quantised",
            PolicyProvenance.Provisional,
            [
                CompatibilitySupportEntry.Create(
                    "quantised-q4km",
                    RuntimeRouteId.LlamaCpp,
                    CompatibilityBackend.Cpu,
                    DeviceRouteId.Cpu,
                    GpuOffloadLevel.None,
                    GgufWeightFormat.Q4KM,
                    GgufKvCacheFormat.F16,
                    1024,
                    32768,
                    SupportLevel.DeclaredSupported,
                    requiresEvidence: false)
            ]);

        CandidateGenerationResult result = Generate(
            matrix: matrix,
            installation: AllInstalled(matrix),
            facts: Facts(fileType: null),
            trustedSource: TrustedSourceAvailability.HigherPrecisionAvailable());

        Assert.AreEqual(0, result.Candidates.Count);
    }

    [TestMethod]
    public void Generate_StillAdmitsTheImportedEntryWhenTheSourceEncodingIsUnknown()
    {
        // A file whose type could not be read must not lose every candidate:
        // the Imported entry describes the file as it already is, so it does
        // not depend on knowing the source encoding at all. A future refactor
        // that moved the Imported early-return below the Unknown check would
        // otherwise silently kill every candidate for such a file.
        CandidateGenerationResult result = Generate(facts: Facts(fileType: null));

        Assert.IsTrue(result.Candidates.Count > 0);
        Assert.IsTrue(result.BaselineIncluded);
    }

    [TestMethod]
    public void Generate_DoesNotRequantiseWithoutATrustedHigherPrecisionSource()
    {
        // Source Q4_K_M, entry asks for Q3_K_M. That is a real downward
        // conversion, but requantising an already-quantised file compounds loss,
        // so it needs a trusted higher-precision source to convert from.
        SupportMatrix matrix = SupportMatrix.FromEntries(
            "v-downward",
            PolicyProvenance.Provisional,
            [
                CompatibilitySupportEntry.Create(
                    "downward-q3",
                    RuntimeRouteId.LlamaCpp,
                    CompatibilityBackend.Cpu,
                    DeviceRouteId.Cpu,
                    GpuOffloadLevel.None,
                    GgufWeightFormat.Q3KM,
                    GgufKvCacheFormat.F16,
                    1024,
                    32768,
                    SupportLevel.DeclaredSupported,
                    requiresEvidence: false)
            ]);

        Assert.AreEqual(
            0,
            Generate(
                matrix: matrix,
                installation: AllInstalled(matrix),
                trustedSource: TrustedSourceAvailability.None()).Candidates.Count);

        IReadOnlyList<CompatibilityCandidate> withSource = Generate(
            matrix: matrix,
            installation: AllInstalled(matrix),
            trustedSource: TrustedSourceAvailability.HigherPrecisionAvailable()).Candidates;

        Assert.IsTrue(withSource.Count > 0);
        Assert.IsTrue(withSource.All(candidate =>
            candidate.Preparation == CandidatePreparation.WeightConversionRequired));
    }

    [TestMethod]
    public void Generate_Q2KUsesTheCanonicalLowestSupportedWeightWidth()
    {
        SupportMatrix matrix = SupportMatrix.FromEntries(
            "v-q2k",
            PolicyProvenance.Provisional,
            [
                CompatibilitySupportEntry.Create(
                    "downward-q2k",
                    RuntimeRouteId.LlamaCpp,
                    CompatibilityBackend.Cpu,
                    DeviceRouteId.Cpu,
                    GpuOffloadLevel.None,
                    GgufWeightFormat.Q2K,
                    GgufKvCacheFormat.F16,
                    1024,
                    32768,
                    SupportLevel.DeclaredSupported,
                    requiresEvidence: false)
            ]);

        CompatibilityCandidate candidate = Generate(
            matrix: matrix,
            installation: AllInstalled(matrix),
            trustedSource: TrustedSourceAvailability.HigherPrecisionAvailable())
            .Candidates.First();

        Assert.AreEqual(
            GgufWeightFormat.Q2K,
            ((GgufRouteConfiguration)candidate.Configuration).Weights);
        Assert.AreEqual(
            WeightQuantisation.Q2_K,
            GgufWeightFormatMap.ToCanonical(GgufWeightFormat.Q2K));
        Assert.AreEqual(
            2.625m,
            WeightQuantisationMap.BitsPerWeight(
                GgufWeightFormatMap.ToCanonical(GgufWeightFormat.Q2K)));
    }

    [TestMethod]
    public void Generate_LabelsASettingsOnlyChangeAsRuntimeProfileOnly()
    {
        // Same weights, different KV format: no new file is written, so the user
        // must not be warned about a conversion that is not happening.
        CompatibilityCandidate candidate = Generate().Candidates.First(candidate =>
            candidate.Configuration is GgufRouteConfiguration configuration
            && configuration.KvCache == GgufKvCacheFormat.Q8_0
            && configuration.Device == DeviceRouteId.Cpu);

        Assert.AreEqual(
            nameof(CandidatePreparation.RuntimeProfileOnly),
            candidate.Preparation.ToString());
    }

    [TestMethod]
    public void Generate_LabelsTheBaselineAsNeedingNoPreparation()
    {
        Assert.AreEqual(
            nameof(CandidatePreparation.None),
            Generate().Candidates[0].Preparation.ToString());
    }

    [TestMethod]
    public void Generate_NeverExceedsTheModelsDeclaredContextLimit()
    {
        // The model declares 8192. A 32768 rung would be an automatic extension
        // beyond the trained limit, which is excluded.
        Assert.IsTrue(
            Generate(preservationTokens: 32768).Candidates.All(
                candidate => candidate.Context.Tokens <= 8192));
    }

    [TestMethod]
    public void Generate_NeverFallsBelowAnEntrysMinimumContext()
    {
        // The preservation target (2048) sits below the entry's minimum
        // (4096). ContextLadderPolicy adds the preservation target to the
        // ladder unconditionally, regardless of the entry minimum, so it is
        // AdmittedContexts' own filter that must exclude it here - unlike a
        // preservation target at or above the minimum, which every standard
        // rung already respects on its own and would leave this assertion
        // green even without the filter.
        SupportMatrix matrix = SupportMatrix.FromEntries(
            "v-min",
            PolicyProvenance.Provisional,
            [
                CompatibilitySupportEntry.Create(
                    "high-minimum",
                    RuntimeRouteId.LlamaCpp,
                    CompatibilityBackend.Cpu,
                    DeviceRouteId.Cpu,
                    GpuOffloadLevel.None,
                    GgufWeightFormat.Imported,
                    GgufKvCacheFormat.F16,
                    minimumContextTokens: 4096,
                    maximumContextTokens: 32768,
                    SupportLevel.DeclaredSupported,
                    requiresEvidence: false)
            ]);

        Assert.IsTrue(
            Generate(matrix: matrix, installation: AllInstalled(matrix), preservationTokens: 2048)
                .Candidates.All(candidate => candidate.Context.Tokens >= 4096));
    }

    [TestMethod]
    public void Generate_NeverExceedsAnEntrysMaximumContext()
    {
        SupportMatrix matrix = SupportMatrix.FromEntries(
            "v-max",
            PolicyProvenance.Provisional,
            [
                CompatibilitySupportEntry.Create(
                    "low-maximum",
                    RuntimeRouteId.LlamaCpp,
                    CompatibilityBackend.Cpu,
                    DeviceRouteId.Cpu,
                    GpuOffloadLevel.None,
                    GgufWeightFormat.Imported,
                    GgufKvCacheFormat.F16,
                    minimumContextTokens: 1024,
                    maximumContextTokens: 2048,
                    SupportLevel.DeclaredSupported,
                    requiresEvidence: false)
            ]);

        Assert.IsTrue(
            Generate(matrix: matrix, installation: AllInstalled(matrix))
                .Candidates.All(candidate => candidate.Context.Tokens <= 2048));
    }

    [TestMethod]
    public void Generate_FlagsExperimentalCandidatesAsExperimental()
    {
        IReadOnlyList<CompatibilityCandidate> experimental =
            [.. Generate().Candidates.Where(candidate => candidate.IsExperimental)];

        Assert.IsTrue(experimental.Count > 0);
        Assert.IsTrue(experimental.All(candidate =>
            candidate.SupportEntryId == "gguf-dgpu-sycl-imported-tq3"));
    }

    [TestMethod]
    public void Generate_IsDeterministicAcrossRuns()
    {
        string[] first = [.. Generate().Candidates.Select(c => c.Fingerprint.Value)];
        string[] second = [.. Generate().Candidates.Select(c => c.Fingerprint.Value)];

        CollectionAssert.AreEqual(first, second);
    }

    [TestMethod]
    public void Generate_CarriesTheEntryIdOnEveryCandidate()
    {
        Assert.IsTrue(Generate().Candidates.All(
            candidate => !string.IsNullOrWhiteSpace(candidate.SupportEntryId)));
    }
}
