using GraniteEdgeAI.ModelInspection.LlamaSharp.ModelProbe;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace GraniteEdgeAI.Tools.ModelInspection.LlamaSharpSpike.Tests;

/// <summary>
/// Proves that runtime probe progress brackets the operations it describes.
/// </summary>
[TestClass]
[TestCategory("Deterministic")]
public sealed class VocabOnlyProbeStageBoundaryTests
{
    [TestMethod]
    public void Run_ReportsActiveDuringDelegateAndCompletedAfterReturn()
    {
        var progress = new CapturingProgress();
        var sequence = new VocabOnlyProbePhaseSequence(progress);
        bool observedActive = false;

        int result = sequence.Run(
            VocabOnlyProbePhase.CheckModelPackage,
            () =>
            {
                observedActive = ProgressEquals(
                    progress.Values,
                    (VocabOnlyProbePhase.CheckModelPackage,
                        VocabOnlyProbePhaseStatus.Active));
                return 42;
            });

        Assert.AreEqual(42, result);
        Assert.IsTrue(observedActive);
        AssertProgress(
            progress.Values,
            [
                (VocabOnlyProbePhase.CheckModelPackage,
                    VocabOnlyProbePhaseStatus.Active, null),
                (VocabOnlyProbePhase.CheckModelPackage,
                    VocabOnlyProbePhaseStatus.Completed, null)
            ]);
    }

    [TestMethod]
    public async Task RunAsync_ReportsActiveDuringDelegateAndCompletedAfterReturn()
    {
        var progress = new CapturingProgress();
        var sequence = new VocabOnlyProbePhaseSequence(progress);
        bool observedActive = false;

        int result = await sequence.RunAsync(
            VocabOnlyProbePhase.ReadModelConfiguration,
            async () =>
            {
                observedActive = ProgressEquals(
                    progress.Values,
                    (VocabOnlyProbePhase.ReadModelConfiguration,
                        VocabOnlyProbePhaseStatus.Active));
                await Task.Yield();
                Assert.HasCount(1, progress.Values);
                return 84;
            });

        Assert.AreEqual(84, result);
        Assert.IsTrue(observedActive);
        AssertProgress(
            progress.Values,
            [
                (VocabOnlyProbePhase.ReadModelConfiguration,
                    VocabOnlyProbePhaseStatus.Active, null),
                (VocabOnlyProbePhase.ReadModelConfiguration,
                    VocabOnlyProbePhaseStatus.Completed, null)
            ]);
    }

    [TestMethod]
    public void Run_WhenDelegateThrows_DoesNotReportCompleted()
    {
        var progress = new CapturingProgress();
        var sequence = new VocabOnlyProbePhaseSequence(progress);

        Assert.ThrowsExactly<InvalidOperationException>(
            () => sequence.Run<int>(
                VocabOnlyProbePhase.ValidateTokenizerAndChatSetup,
                () => throw new InvalidOperationException("expected")));

        AssertProgress(
            progress.Values,
            [
                (VocabOnlyProbePhase.ValidateTokenizerAndChatSetup,
                    VocabOnlyProbePhaseStatus.Active, null)
            ]);
    }

    [TestMethod]
    public async Task RunAsync_WhenDelegateIsCancelled_DoesNotReportCompleted()
    {
        var progress = new CapturingProgress();
        var sequence = new VocabOnlyProbePhaseSequence(progress);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsExactlyAsync<TaskCanceledException>(
            () => sequence.RunAsync(
                VocabOnlyProbePhase.ValidateModelStructure,
                () => Task.FromCanceled<int>(cancellation.Token)));

        AssertProgress(
            progress.Values,
            [
                (VocabOnlyProbePhase.ValidateModelStructure,
                    VocabOnlyProbePhaseStatus.Active, null)
            ]);
    }

    [TestMethod]
    public void ReportNativeFraction_IsAcceptedOnlyWhileConfigurationIsActive()
    {
        var progress = new CapturingProgress();
        var sequence = new VocabOnlyProbePhaseSequence(progress);

        Assert.ThrowsExactly<InvalidOperationException>(
            () => sequence.ReportNativeFraction(0.25f));

        sequence.Run(
            VocabOnlyProbePhase.ReadModelConfiguration,
            () =>
            {
                sequence.ReportNativeFraction(0.25f);
                return true;
            });

        Assert.ThrowsExactly<InvalidOperationException>(
            () => sequence.ReportNativeFraction(0.75f));

        var otherPhase = new VocabOnlyProbePhaseSequence(progress: null);
        otherPhase.Run(
            VocabOnlyProbePhase.CheckModelPackage,
            () =>
            {
                Assert.ThrowsExactly<InvalidOperationException>(
                    () => otherPhase.ReportNativeFraction(0.5f));
                return true;
            });

        AssertProgress(
            progress.Values,
            [
                (VocabOnlyProbePhase.ReadModelConfiguration,
                    VocabOnlyProbePhaseStatus.Active, null),
                (VocabOnlyProbePhase.ReadModelConfiguration,
                    VocabOnlyProbePhaseStatus.Fraction, 0.25f),
                (VocabOnlyProbePhase.ReadModelConfiguration,
                    VocabOnlyProbePhaseStatus.Completed, null)
            ]);
    }

    [TestMethod]
    public void Progress_RejectsEveryInvalidPhaseStatusAndFractionCombination()
    {
        _ = new VocabOnlyProbeProgress(
            VocabOnlyProbePhase.ReadModelConfiguration,
            VocabOnlyProbePhaseStatus.Fraction,
            0.5f);

        Action[] invalidFactories =
        [
            () => _ = new VocabOnlyProbeProgress(
                VocabOnlyProbePhase.CheckModelPackage,
                VocabOnlyProbePhaseStatus.Active,
                0f),
            () => _ = new VocabOnlyProbeProgress(
                VocabOnlyProbePhase.ReadModelConfiguration,
                VocabOnlyProbePhaseStatus.Completed,
                1f),
            () => _ = new VocabOnlyProbeProgress(
                VocabOnlyProbePhase.ValidateModelStructure,
                VocabOnlyProbePhaseStatus.Fraction,
                0.5f),
            () => _ = new VocabOnlyProbeProgress(
                VocabOnlyProbePhase.CheckModelPackage,
                VocabOnlyProbePhaseStatus.Fraction),
            () => _ = new VocabOnlyProbeProgress(
                VocabOnlyProbePhase.ReadModelConfiguration,
                VocabOnlyProbePhaseStatus.Fraction),
            () => _ = new VocabOnlyProbeProgress(
                VocabOnlyProbePhase.ValidateTokenizerAndChatSetup,
                VocabOnlyProbePhaseStatus.Fraction),
            () => _ = new VocabOnlyProbeProgress(
                VocabOnlyProbePhase.ValidateModelStructure,
                VocabOnlyProbePhaseStatus.Fraction),
            () => _ = new VocabOnlyProbeProgress(
                VocabOnlyProbePhase.ReadModelConfiguration,
                VocabOnlyProbePhaseStatus.Fraction,
                float.NaN),
            () => _ = new VocabOnlyProbeProgress(
                VocabOnlyProbePhase.ReadModelConfiguration,
                VocabOnlyProbePhaseStatus.Fraction,
                float.PositiveInfinity),
            () => _ = new VocabOnlyProbeProgress(
                VocabOnlyProbePhase.ReadModelConfiguration,
                VocabOnlyProbePhaseStatus.Fraction,
                -0.01f),
            () => _ = new VocabOnlyProbeProgress(
                VocabOnlyProbePhase.ReadModelConfiguration,
                VocabOnlyProbePhaseStatus.Fraction,
                1.01f),
            () => _ = new VocabOnlyProbeProgress(
                (VocabOnlyProbePhase)int.MaxValue,
                VocabOnlyProbePhaseStatus.Active),
            () => _ = new VocabOnlyProbeProgress(
                VocabOnlyProbePhase.CheckModelPackage,
                (VocabOnlyProbePhaseStatus)int.MaxValue)
        ];

        foreach (Action invalidFactory in invalidFactories)
        {
            Assert.Throws<ArgumentException>(invalidFactory);
        }
    }

    [TestMethod]
    public void Sequence_ReportsExactFactsWhileEachDescribedOperationRuns()
    {
        var progress = new CapturingProgress();
        var sequence = new VocabOnlyProbePhaseSequence(progress);
        var operations = new List<string>();

        sequence.Run(
            VocabOnlyProbePhase.CheckModelPackage,
            () => ObserveActive("initial snapshot",
                VocabOnlyProbePhase.CheckModelPackage));
        sequence.Run(
            VocabOnlyProbePhase.ReadModelConfiguration,
            () =>
            {
                ObserveActive("load and configuration",
                    VocabOnlyProbePhase.ReadModelConfiguration);
                sequence.ReportNativeFraction(0.5f);
                return true;
            });
        sequence.Run(
            VocabOnlyProbePhase.ValidateTokenizerAndChatSetup,
            () =>
            {
                ObserveActive("tokenizer smoke",
                    VocabOnlyProbePhase.ValidateTokenizerAndChatSetup);
                ObserveActive("chat template",
                    VocabOnlyProbePhase.ValidateTokenizerAndChatSetup);
                return true;
            });
        sequence.Run(
            VocabOnlyProbePhase.ValidateModelStructure,
            () =>
            {
                ObserveActive("structural projection",
                    VocabOnlyProbePhase.ValidateModelStructure);
                ObserveActive("model disposal",
                    VocabOnlyProbePhase.ValidateModelStructure);
                ObserveActive("final snapshot",
                    VocabOnlyProbePhase.ValidateModelStructure);
                ObserveActive("integrity comparison",
                    VocabOnlyProbePhase.ValidateModelStructure);
                return true;
            });

        CollectionAssert.AreEqual(
            new[]
            {
                "initial snapshot",
                "load and configuration",
                "tokenizer smoke",
                "chat template",
                "structural projection",
                "model disposal",
                "final snapshot",
                "integrity comparison"
            },
            operations);
        AssertProgress(
            progress.Values,
            [
                (VocabOnlyProbePhase.CheckModelPackage,
                    VocabOnlyProbePhaseStatus.Active, null),
                (VocabOnlyProbePhase.CheckModelPackage,
                    VocabOnlyProbePhaseStatus.Completed, null),
                (VocabOnlyProbePhase.ReadModelConfiguration,
                    VocabOnlyProbePhaseStatus.Active, null),
                (VocabOnlyProbePhase.ReadModelConfiguration,
                    VocabOnlyProbePhaseStatus.Fraction, 0.5f),
                (VocabOnlyProbePhase.ReadModelConfiguration,
                    VocabOnlyProbePhaseStatus.Completed, null),
                (VocabOnlyProbePhase.ValidateTokenizerAndChatSetup,
                    VocabOnlyProbePhaseStatus.Active, null),
                (VocabOnlyProbePhase.ValidateTokenizerAndChatSetup,
                    VocabOnlyProbePhaseStatus.Completed, null),
                (VocabOnlyProbePhase.ValidateModelStructure,
                    VocabOnlyProbePhaseStatus.Active, null),
                (VocabOnlyProbePhase.ValidateModelStructure,
                    VocabOnlyProbePhaseStatus.Completed, null)
            ]);

        bool ObserveActive(string operation, VocabOnlyProbePhase phase)
        {
            Assert.IsNotEmpty(
                progress.Values,
                $"{operation} ran before its Active callback.");
            VocabOnlyProbeProgress current = progress.Values[^1];
            Assert.AreEqual(phase, current.Phase);
            Assert.AreEqual(VocabOnlyProbePhaseStatus.Active, current.Status);
            operations.Add(operation);
            return true;
        }
    }

    [TestMethod]
    public void Compose_ReproducesEveryRuntimeEvidenceField()
    {
        var configuration = new VocabOnlyConfigurationProjection
        {
            Description = "description",
            MetadataCount = 2,
            MetadataKeys = ["zeta", "alpha"],
            Architecture = "architecture",
            ModelName = "model-name",
            FileType = "15",
            QuantizationVersion = "2",
            TokenizerModel = "tokenizer",
            ContextSize = 131_072
        };
        var tokenizer = new VocabOnlyTokenizerProjection
        {
            Vocabulary = new VocabularyEvidence
            {
                Count = 32_000,
                Type = "BPE",
                Bos = new SpecialTokenEvidence
                {
                    TokenId = "1",
                    DecodedText = "bos"
                },
                Eos = new SpecialTokenEvidence
                {
                    TokenId = "2",
                    DecodedText = "eos"
                },
                Newline = new SpecialTokenEvidence
                {
                    TokenId = "3",
                    DecodedText = "newline"
                },
                Pad = new SpecialTokenEvidence
                {
                    TokenId = "4",
                    DecodedText = "pad"
                },
                Mask = new SpecialTokenEvidence
                {
                    TokenId = "5",
                    DecodedText = "mask"
                },
                Separator = new SpecialTokenEvidence
                {
                    TokenId = "6",
                    DecodedText = "separator"
                }
            },
            TokenizerSmoke = new TokenizerSmokeEvidence
            {
                Succeeded = false,
                TokenCount = null,
                FailureType = "failure-type",
                FailureMessage = "failure-message"
            },
            ChatTemplate = new ChatTemplateEvidence
            {
                Present = true,
                LengthCharacters = 128,
                Sha256 = new string('A', 64)
            }
        };
        var structure = new VocabOnlyStructureProjection
        {
            ParameterCount = 3_000_000_000,
            EmbeddingSize = 4_096,
            LayerCount = 32,
            HeadCount = 32,
            KvHeadCount = 8
        };

        VocabOnlyRuntimeModelEvidence evidence =
            VocabOnlyEvidenceCollector.Compose(
                configuration,
                tokenizer,
                structure);

        Assert.AreEqual(configuration.Description, evidence.Description);
        Assert.AreEqual(configuration.MetadataCount, evidence.MetadataCount);
        CollectionAssert.AreEqual(
            new[] { "alpha", "zeta" },
            evidence.MetadataKeys.ToArray());
        Assert.AreEqual(configuration.Architecture, evidence.Architecture);
        Assert.AreEqual(configuration.ModelName, evidence.ModelName);
        Assert.AreEqual(configuration.FileType, evidence.FileType);
        Assert.AreEqual(
            configuration.QuantizationVersion,
            evidence.QuantizationVersion);
        Assert.AreEqual(
            configuration.TokenizerModel,
            evidence.TokenizerModel);
        Assert.AreEqual(configuration.ContextSize, evidence.ContextSize);
        Assert.IsNull(evidence.RuntimeReportedSizeBytes);
        Assert.AreEqual(structure.ParameterCount, evidence.ParameterCount);
        Assert.AreEqual(structure.EmbeddingSize, evidence.EmbeddingSize);
        Assert.AreEqual(structure.LayerCount, evidence.LayerCount);
        Assert.AreEqual(structure.HeadCount, evidence.HeadCount);
        Assert.AreEqual(structure.KvHeadCount, evidence.KvHeadCount);
        Assert.IsNull(evidence.HasEncoder);
        Assert.IsNull(evidence.HasDecoder);
        Assert.IsNull(evidence.IsRecurrent);
        Assert.IsNull(evidence.IsDiffusion);
        Assert.AreEqual(tokenizer.Vocabulary.Count, evidence.Vocabulary.Count);
        Assert.AreEqual(tokenizer.Vocabulary.Type, evidence.Vocabulary.Type);
        AssertToken(tokenizer.Vocabulary.Bos, evidence.Vocabulary.Bos);
        AssertToken(tokenizer.Vocabulary.Eos, evidence.Vocabulary.Eos);
        AssertToken(tokenizer.Vocabulary.Newline, evidence.Vocabulary.Newline);
        AssertToken(tokenizer.Vocabulary.Pad, evidence.Vocabulary.Pad);
        AssertToken(tokenizer.Vocabulary.Mask, evidence.Vocabulary.Mask);
        AssertToken(
            tokenizer.Vocabulary.Separator,
            evidence.Vocabulary.Separator);
        Assert.AreEqual(
            tokenizer.TokenizerSmoke.Succeeded,
            evidence.TokenizerSmoke.Succeeded);
        Assert.AreEqual(
            tokenizer.TokenizerSmoke.TokenCount,
            evidence.TokenizerSmoke.TokenCount);
        Assert.AreEqual(
            tokenizer.TokenizerSmoke.FailureType,
            evidence.TokenizerSmoke.FailureType);
        Assert.AreEqual(
            tokenizer.TokenizerSmoke.FailureMessage,
            evidence.TokenizerSmoke.FailureMessage);
        Assert.AreEqual(
            tokenizer.ChatTemplate.Present,
            evidence.ChatTemplate.Present);
        Assert.AreEqual(
            tokenizer.ChatTemplate.LengthCharacters,
            evidence.ChatTemplate.LengthCharacters);
        Assert.AreEqual(
            tokenizer.ChatTemplate.Sha256,
            evidence.ChatTemplate.Sha256);
    }

    private static bool ProgressEquals(
        IReadOnlyList<VocabOnlyProbeProgress> values,
        (VocabOnlyProbePhase Phase, VocabOnlyProbePhaseStatus Status) expected)
    {
        return values.Count == 1 &&
            values[0].Phase == expected.Phase &&
            values[0].Status == expected.Status &&
            values[0].NativeFraction is null;
    }

    private static void AssertProgress(
        IReadOnlyList<VocabOnlyProbeProgress> actual,
        IReadOnlyList<(
            VocabOnlyProbePhase Phase,
            VocabOnlyProbePhaseStatus Status,
            float? NativeFraction)> expected)
    {
        Assert.AreEqual(expected.Count, actual.Count);

        for (int index = 0; index < expected.Count; index++)
        {
            Assert.AreEqual(expected[index].Phase, actual[index].Phase);
            Assert.AreEqual(expected[index].Status, actual[index].Status);
            Assert.AreEqual(
                expected[index].NativeFraction,
                actual[index].NativeFraction);
        }
    }

    private static void AssertToken(
        SpecialTokenEvidence expected,
        SpecialTokenEvidence actual)
    {
        Assert.AreEqual(expected.TokenId, actual.TokenId);
        Assert.AreEqual(expected.DecodedText, actual.DecodedText);
    }

    private sealed class CapturingProgress :
        IProgress<VocabOnlyProbeProgress>
    {
        internal List<VocabOnlyProbeProgress> Values { get; } = [];

        public void Report(VocabOnlyProbeProgress value) => Values.Add(value);
    }
}
