using System.Globalization;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Candidates;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Contracts;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Estimation;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Domain;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Routes.Gguf;

namespace GraniteEdgeAI.ModelHardwareCompatibility.Tests.Invariants;

/// <summary>
/// A forward-looking canary, not a regression guard over existing behaviour. It
/// cannot currently fail: every figure compared here is pure decimal/ulong
/// arithmetic with no formatting in its path, and the fingerprint is built with
/// <c>string.Create(CultureInfo.InvariantCulture, ...)</c> over an
/// interpolated <c>int</c> under the culture-invariant "G" format (no group
/// separator) and <c>Convert.ToHexString</c> (digits 0-9 and letters A-F,
/// neither of which is the dotted/dotless I that trips Turkish casing). Culture
/// independence therefore holds today by construction rather than by this
/// test's enforcement. It is kept and asserted anyway so that if a later
/// change threads a culture-sensitive format (a percentage, a locale-formatted
/// count) into the calculation or fingerprint path, this pins the expectation
/// before that happens rather than after. A reader must not mistake a pass
/// here for evidence that the current code has been exercised against a bug
/// class it structurally cannot exhibit.
/// </summary>
[TestClass]
public sealed class CultureInvarianceTests
{
    private static readonly string[] Cultures = ["", "tr-TR", "de-DE"];

    private static (ulong Weights, ulong Kv, string Fingerprint) MeasureUnder(string culture)
    {
        CultureInfo original = CultureInfo.CurrentCulture;

        try
        {
            CultureInfo.CurrentCulture = culture.Length == 0
                ? CultureInfo.InvariantCulture
                : new CultureInfo(culture);

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
                    GgufWeightFormat.Q4KM,
                    GgufKvCacheFormat.Q8_0,
                    CompatibilityBackend.IntelSycl,
                    DeviceRouteId.IntelDiscreteGpu,
                    GpuOffloadLevel.Full),
                ContextTokenCount.FromTokens(8192),
                CandidatePreparation.RuntimeProfileOnly,
                supportEntryId: "entry-1",
                isExperimental: false,
                isBaseline: false);

            ResourceEstimate estimate = GgufResourceEstimator.Estimate(
                facts, candidate, EstimatorPolicy.ProvisionalV1());

            return (
                estimate.Components
                    .Single(component => component.Kind == ResourceComponentKind.Weights)
                    .Bytes.Bytes,
                estimate.Components
                    .Single(component => component.Kind == ResourceComponentKind.KvCache)
                    .Bytes.Bytes,
                candidate.Fingerprint.Value);
        }
        finally
        {
            CultureInfo.CurrentCulture = original;
        }
    }

    [TestMethod]
    public void EstimatesAndFingerprints_AreIdenticalUnderEveryCulture()
    {
        // Turkish lowercases I differently and German uses a comma as the decimal
        // separator. Either could change a fingerprint or a parsed constant if
        // any formatting slipped into the calculation path.
        (ulong Weights, ulong Kv, string Fingerprint) baseline = MeasureUnder(Cultures[0]);

        foreach (string culture in Cultures)
        {
            Assert.AreEqual(baseline, MeasureUnder(culture), $"Culture {culture} changed a result.");
        }
    }
}
