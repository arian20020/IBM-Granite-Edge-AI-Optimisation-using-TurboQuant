using GraniteEdgeAI.Features.ModelInspection.Classification;
using GraniteEdgeAI.Features.ModelInspection.Contracts;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace GraniteEdgeAI.UnitTests;

[TestClass]
public sealed class ModelInspectionClassifierTests
{
    private const string Sha256 =
        "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
    private const string LlamaCppCommit =
        "3f7c29d318e317b63f54c558bc69803963d7d88c";

    private static readonly DateTimeOffset StartedAtUtc = new(
        2026,
        8,
        9,
        8,
        0,
        0,
        TimeSpan.Zero);

    private static readonly DateTimeOffset CompletedAtUtc =
        StartedAtUtc.AddSeconds(2);

    [TestMethod]
    public void Classify_PassedTokenizerWithEmbeddedTemplate_ReturnsReady()
    {
        ModelInspectionEvidence evidence = CreateEvidence(chatTemplatePresent: true);
        IModelInspectionClassifier classifier = new ModelInspectionClassifier();

        ModelInspectionResult result = classifier.Classify(
            evidence,
            StartedAtUtc,
            CompletedAtUtc);

        Assert.AreEqual(ModelInspectionOutcome.Ready, result.Outcome);
        Assert.HasCount(0, result.Findings);
        Assert.AreSame(evidence, result.Evidence);
        Assert.AreEqual(StartedAtUtc, result.StartedAtUtc);
        Assert.AreEqual(CompletedAtUtc, result.CompletedAtUtc);
        Assert.IsTrue(result.CanContinueToHardwareFit);
        Assert.IsNull(result.VerifiedConversionRouteId);
    }

    [TestMethod]
    public void Classify_PassedTokenizerWithoutEmbeddedTemplate_ReturnsStableWarning()
    {
        ModelInspectionEvidence evidence = CreateEvidence(chatTemplatePresent: false);
        IModelInspectionClassifier classifier = new ModelInspectionClassifier();

        ModelInspectionResult result = classifier.Classify(
            evidence,
            StartedAtUtc,
            CompletedAtUtc);

        Assert.AreEqual(ModelInspectionOutcome.ReadyWithWarnings, result.Outcome);
        Assert.HasCount(1, result.Findings);
        ModelInspectionFinding warning = result.Findings[0];
        Assert.AreEqual("MI-WARN-CHAT-TEMPLATE-MISSING", warning.Code);
        Assert.AreEqual(ModelInspectionFindingSeverity.Warning, warning.Severity);
        Assert.AreEqual("No embedded chat template", warning.Title);
        Assert.AreEqual(
            "The model does not include an embedded chat template.",
            warning.Explanation);
        Assert.AreEqual(
            "Configure and verify a compatible chat template before starting chat.",
            warning.RecommendedAction);
        Assert.AreEqual(
            "Completed GGUF evidence reported chat_template_present=false.",
            warning.TechnicalDetail);
        Assert.IsTrue(result.CanContinueToHardwareFit);
        Assert.IsNull(result.VerifiedConversionRouteId);
    }

    [TestMethod]
    public void Classify_FailedTokenizerSmoke_RejectsUnreliableEvidence()
    {
        ModelInspectionEvidence evidence = CreateEvidence(
            chatTemplatePresent: true,
            tokenizerSmokePassed: false);
        IModelInspectionClassifier classifier = new ModelInspectionClassifier();

        Assert.ThrowsExactly<InvalidOperationException>(() =>
            classifier.Classify(evidence, StartedAtUtc, CompletedAtUtc));
    }

    [TestMethod]
    public void Classify_UnknownTokenizerSmoke_RejectsUnreliableEvidence()
    {
        ModelInspectionEvidence evidence = CreateEvidence(
            chatTemplatePresent: true,
            tokenizerSmokePassed: null);
        IModelInspectionClassifier classifier = new ModelInspectionClassifier();

        Assert.ThrowsExactly<InvalidOperationException>(() =>
            classifier.Classify(evidence, StartedAtUtc, CompletedAtUtc));
    }

    [TestMethod]
    public void Classify_UnknownChatTemplatePresence_RejectsUnreliableEvidence()
    {
        ModelInspectionEvidence evidence = CreateEvidence(
            chatTemplatePresent: null,
            tokenizerSmokePassed: true);
        IModelInspectionClassifier classifier = new ModelInspectionClassifier();

        Assert.ThrowsExactly<InvalidOperationException>(() =>
            classifier.Classify(evidence, StartedAtUtc, CompletedAtUtc));
    }

    private static ModelInspectionEvidence CreateEvidence(
        bool? chatTemplatePresent,
        bool? tokenizerSmokePassed = true)
    {
        return new ModelInspectionEvidence(
            file: new ModelInspectionFileEvidence(
                fileName: "granite.gguf",
                canonicalPathSha256: Sha256,
                lengthBytes: 4_096,
                lastWriteTimeUtc: StartedAtUtc,
                modelSha256: Sha256,
                integrityPreserved: true),
            configuration: new ModelInspectionConfigurationEvidence(
                format: "GGUF",
                ggufVersion: 3,
                modelName: "Granite",
                architecture: "granite",
                fileType: 15,
                quantisationVersion: 2,
                declaredContextLength: 4_096,
                embeddingSize: 2_048,
                layerCount: 24,
                attentionHeadCount: 16,
                kvHeadCount: 8,
                parameterCount: 3_000_000_000),
            tokenizer: new ModelInspectionTokenizerEvidence(
                tokenizerModel: "gpt2",
                vocabularyCount: 49_152,
                vocabularyType: "BPE",
                tokenizerSmokePassed: tokenizerSmokePassed,
                tokenizerSmokeTokenCount:
                    tokenizerSmokePassed is true ? 4 : null,
                knownSpecialTokenIds: new Dictionary<string, int>(
                    StringComparer.Ordinal)),
            chatTemplate: chatTemplatePresent switch
            {
                true => new ModelInspectionChatTemplateEvidence(
                    present: true,
                    lengthCharacters: 128,
                    sha256: Sha256),
                false => new ModelInspectionChatTemplateEvidence(
                    present: false,
                    lengthCharacters: null,
                    sha256: null),
                null => new ModelInspectionChatTemplateEvidence(
                    present: null,
                    lengthCharacters: null,
                    sha256: null)
            },
            runtime: new ModelInspectionRuntimeIdentity(
                workerId: "GraniteEdgeAI.ModelInspection.Worker",
                workerVersion: "1.0.0",
                protocolVersion: 1,
                runtimeProfile: "llamasharp-0.27.0-cpu-win-x64-vocab-only-v1",
                llamaSharpVersion: "0.27.0",
                backendPackageVersion: "0.27.0",
                mappedLlamaCppCommit: LlamaCppCommit,
                nativeLibraryName: "llama.dll",
                processArchitecture: "X64",
                inspectionMode: "VocabOnly",
                usesCuda: false,
                usesVulkan: false,
                gpuLayerCount: 0),
            observations: Array.Empty<ModelInspectionObservation>());
    }
}
