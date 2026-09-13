using System.Globalization;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Candidates;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Domain;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Routes.Gguf;

namespace GraniteEdgeAI.ModelHardwareCompatibility.Tests.Application;

[TestClass]
public sealed class CompatibilityCandidateTests
{
    private static GgufRouteConfiguration Config(
        GgufKvCacheFormat kv = GgufKvCacheFormat.Q8_0) =>
        GgufRouteConfiguration.Create(
            GgufWeightFormat.Q4KM, kv,
            CompatibilityBackend.IntelSycl, DeviceRouteId.IntelIntegratedGpu,
            GpuOffloadLevel.Full);

    private static CompatibilityCandidate Candidate(
        GgufKvCacheFormat kv = GgufKvCacheFormat.Q8_0,
        int tokens = 8192,
        CandidatePreparation preparation = CandidatePreparation.RuntimeProfileOnly) =>
        CompatibilityCandidate.Create(
            Config(kv),
            ContextTokenCount.FromTokens(tokens),
            preparation,
            supportEntryId: "gguf-granite-sycl-v1",
            isExperimental: false,
            isBaseline: false);

    [TestMethod]
    public void Fingerprint_IsSixtyFourLowercaseHexCharacters()
    {
        string value = Candidate().Fingerprint.Value;

        Assert.AreEqual(64, value.Length);
        Assert.IsTrue(value.All(c => (c >= '0' && c <= '9') || (c >= 'a' && c <= 'f')));
    }

    [TestMethod]
    public void Fingerprint_IsIdenticalForIdenticalConfiguration()
    {
        Assert.AreEqual(Candidate().Fingerprint, Candidate().Fingerprint);
    }

    [TestMethod]
    public void Fingerprint_DiffersWhenTheKvCacheFormatDiffers()
    {
        Assert.AreNotEqual(
            Candidate(kv: GgufKvCacheFormat.Q8_0).Fingerprint,
            Candidate(kv: GgufKvCacheFormat.F16).Fingerprint);
    }

    [TestMethod]
    public void Fingerprint_DiffersWhenTheContextDiffers()
    {
        Assert.AreNotEqual(
            Candidate(tokens: 8192).Fingerprint,
            Candidate(tokens: 16384).Fingerprint);
    }

    [TestMethod]
    public void Fingerprint_DiffersWhenThePreparationDiffers()
    {
        Assert.AreNotEqual(
            Candidate(preparation: CandidatePreparation.RuntimeProfileOnly).Fingerprint,
            Candidate(preparation: CandidatePreparation.WeightConversionRequired).Fingerprint);
    }

    [TestMethod]
    public void Fingerprint_IsCultureInvariant()
    {
        CultureInfo original = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = new CultureInfo("tr-TR");
            CandidateFingerprint turkish = Candidate().Fingerprint;

            CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
            CandidateFingerprint invariant = Candidate().Fingerprint;

            Assert.AreEqual(invariant, turkish);
        }
        finally
        {
            CultureInfo.CurrentCulture = original;
        }
    }

    [TestMethod]
    public void Fingerprint_DoesNotDependOnBaselineOrExperimentalFlags()
    {
        // those are labels about the candidate, not memory-relevant settings,
        // so two otherwise identical candidates must deduplicate together
        CompatibilityCandidate plain = CompatibilityCandidate.Create(
            Config(), ContextTokenCount.FromTokens(4096), CandidatePreparation.None,
            "entry", isExperimental: false, isBaseline: false);
        CompatibilityCandidate flagged = CompatibilityCandidate.Create(
            Config(), ContextTokenCount.FromTokens(4096), CandidatePreparation.None,
            "entry", isExperimental: true, isBaseline: true);

        Assert.AreEqual(plain.Fingerprint, flagged.Fingerprint);
    }

    [TestMethod]
    public void Create_RejectsAMissingSupportEntryId()
    {
        Assert.ThrowsExactly<ArgumentException>(() => CompatibilityCandidate.Create(
            Config(), ContextTokenCount.FromTokens(4096),
            CandidatePreparation.None, supportEntryId: "  ",
            isExperimental: false, isBaseline: false));
    }

    [TestMethod]
    public void Create_RejectsUnspecifiedPreparation()
    {
        Assert.ThrowsExactly<ArgumentException>(() => CompatibilityCandidate.Create(
            Config(), ContextTokenCount.FromTokens(4096),
            CandidatePreparation.Unspecified, supportEntryId: "entry",
            isExperimental: false, isBaseline: false));
    }

    [TestMethod]
    public void Create_RejectsANullConfiguration()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() => CompatibilityCandidate.Create(
            null!, ContextTokenCount.FromTokens(4096),
            CandidatePreparation.None, supportEntryId: "entry",
            isExperimental: false, isBaseline: false));
    }

    [TestMethod]
    public void Candidate_ExposesItsRouteFromTheConfiguration()
    {
        Assert.AreEqual(RuntimeRouteId.LlamaCpp, Candidate().RouteId);
    }

    [TestMethod]
    public void Candidate_PreservesEveryDeclaredFact()
    {
        CompatibilityCandidate candidate = CompatibilityCandidate.Create(
            Config(), ContextTokenCount.FromTokens(16384),
            CandidatePreparation.WeightConversionRequired,
            supportEntryId: "entry-x",
            isExperimental: true,
            isBaseline: true);

        Assert.AreEqual(16384, candidate.Context.Tokens);
        Assert.AreEqual(CandidatePreparation.WeightConversionRequired, candidate.Preparation);
        Assert.AreEqual("entry-x", candidate.SupportEntryId);
        Assert.IsTrue(candidate.IsExperimental);
        Assert.IsTrue(candidate.IsBaseline);
    }
}
