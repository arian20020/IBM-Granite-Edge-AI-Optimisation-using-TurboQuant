using GraniteEdgeAI.Features.ModelInspection.Contracts;
using GraniteEdgeAI.Features.ModelInspection.Runtime;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace GraniteEdgeAI.UnitTests.Features.ModelInspection.Runtime;

[TestClass]
public sealed class ModelInspectionProbeResultTests
{
    [TestMethod]
    public void Completed_HoldsOnlyReliableEvidence()
    {
        ModelInspectionEvidence evidence = CreateEvidence();

        ModelInspectionProbeResult result =
            ModelInspectionProbeResult.Completed(evidence);

        Assert.AreEqual(ModelInspectionProbeStatus.Completed, result.Status);
        Assert.AreSame(evidence, result.Evidence);
        Assert.IsNull(result.Failure);
    }

    [TestMethod]
    public void Cancelled_HoldsNoPartialEvidenceOrFailure()
    {
        ModelInspectionProbeResult result =
            ModelInspectionProbeResult.Cancelled();

        Assert.AreEqual(ModelInspectionProbeStatus.Cancelled, result.Status);
        Assert.IsNull(result.Evidence);
        Assert.IsNull(result.Failure);
    }

    [TestMethod]
    public void OperationalFailure_HoldsOnlySafeFailure()
    {
        ModelInspectionOperationalFailure failure = new(
            "MI-OP-TEST",
            "Inspection could not be completed.",
            "A controlled test failure occurred.");

        ModelInspectionProbeResult result =
            ModelInspectionProbeResult.OperationalFailure(failure);

        Assert.AreEqual(
            ModelInspectionProbeStatus.OperationalFailure,
            result.Status);
        Assert.IsNull(result.Evidence);
        Assert.AreSame(failure, result.Failure);
    }

    [TestMethod]
    public void Factories_RejectNullPayloads()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() =>
            ModelInspectionProbeResult.Completed(null!));
        Assert.ThrowsExactly<ArgumentNullException>(() =>
            ModelInspectionProbeResult.OperationalFailure(null!));
    }

    private static ModelInspectionEvidence CreateEvidence()
    {
        return new ModelInspectionEvidence(
            new ModelInspectionFileEvidence(
                RuntimeTestData.ModelFileName,
                RuntimeTestData.CanonicalPathSha256,
                100,
                RuntimeTestData.FixedUtcTime,
                RuntimeTestData.ModelSha256,
                integrityPreserved: true),
            new ModelInspectionConfigurationEvidence(
                "GGUF",
                3,
                "Granite 4.1 3B",
                "granite",
                15,
                2,
                4_096,
                2_048,
                32,
                32,
                8,
                3_000_000_000),
            new ModelInspectionTokenizerEvidence(
                "llama",
                49_152,
                "BPE",
                true,
                3,
                new Dictionary<string, int>(StringComparer.Ordinal)),
            new ModelInspectionChatTemplateEvidence(
                true,
                42,
                RuntimeTestData.ChatTemplateSha256),
            new ModelInspectionRuntimeIdentity(
                "GraniteEdgeAI.ModelInspection.Worker",
                "1.0.0",
                1,
                "llamasharp-0.27.0-cpu-win-x64-vocab-only-v1",
                "0.27.0",
                "0.27.0",
                RuntimeTestData.MappedLlamaCppCommit,
                "llama.dll",
                "X64",
                "VocabOnly",
                false,
                false,
                0),
            Array.Empty<ModelInspectionObservation>());
    }
}
