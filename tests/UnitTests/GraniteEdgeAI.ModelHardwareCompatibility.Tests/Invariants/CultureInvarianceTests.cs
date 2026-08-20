using System.Globalization;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Candidates;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Contracts;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Estimation;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Domain;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Routes.Gguf;

namespace GraniteEdgeAI.ModelHardwareCompatibility.Tests.Invariants;

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
