using GraniteEdgeAI.Features.ModelInspection.Contracts;
using System.Windows.Input;

namespace GraniteEdgeAI.UnitTests.Features.ModelInspection.Presentation;

internal static class PresentationTestData
{
    internal const string Sha256 =
        "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
    internal const string LlamaCppCommit =
        "3f7c29d318e317b63f54c558bc69803963d7d88c";
    internal const string PrivateDirectoryMarker = "private-model-directory";
    internal const string SensitiveTechnicalMarker = "SENSITIVE-TECHNICAL-DATA";

    internal static readonly DateTimeOffset FixedUtc =
        new(2026, 8, 9, 8, 0, 0, TimeSpan.Zero);

    internal static ModelInspectionRequest CreateRequest()
    {
        string modelPath = Path.GetFullPath(
            Path.Combine(
                Path.GetPathRoot(Environment.SystemDirectory)!,
                PrivateDirectoryMarker,
                "granite.gguf"));

        return new ModelInspectionRequest(
            modelPath,
            "granite.gguf",
            new ExpectedModelFileIdentity(4_096, FixedUtc),
            ValidatedQuickScanSnapshot.CreateGguf(
                modelName: "Granite 4.1 3B",
                architecture: "granite",
                parameterSizeLabel: "3B",
                quantisation: "Q4_K_M",
                fileSizeBytes: 4_096,
                declaredContextLength: 4_096,
                ggufVersion: 3));
    }

    internal static ModelInspectionResult CreateResult(
        ModelInspectionOutcome outcome,
        string technicalDetail = SensitiveTechnicalMarker)
    {
        ModelInspectionFinding[] findings = outcome switch
        {
            ModelInspectionOutcome.Ready => [],
            ModelInspectionOutcome.ReadyWithWarnings =>
            [
                CreateFinding(
                    ModelInspectionFindingSeverity.Warning,
                    "Review this warning",
                    technicalDetail)
            ],
            _ =>
            [
                CreateFinding(
                    ModelInspectionFindingSeverity.Blocking,
                    "Resolve this issue",
                    technicalDetail)
            ]
        };

        return new ModelInspectionResult(
            outcome,
            CreateEvidence(),
            findings,
            summary: $"Safe {outcome} summary.",
            recommendedAction: "Choose another model if needed.",
            verifiedConversionRouteId:
                outcome == ModelInspectionOutcome.ConversionRequired
                    ? "gguf-conversion-route-v1"
                    : null,
            startedAtUtc: FixedUtc,
            completedAtUtc: FixedUtc.AddSeconds(2));
    }

    internal static ModelInspectionOperationalFailure CreateFailure(
        string technicalDetail = SensitiveTechnicalMarker)
    {
        return new ModelInspectionOperationalFailure(
            "MI-OP-TEST",
            "Inspection could not be completed safely.",
            technicalDetail);
    }

    internal static RecordingCommand CreateCommand(bool canExecute = true)
    {
        return new RecordingCommand(canExecute);
    }

    private static ModelInspectionFinding CreateFinding(
        ModelInspectionFindingSeverity severity,
        string title,
        string technicalDetail)
    {
        return new ModelInspectionFinding(
            "MI-FINDING-TEST",
            severity,
            title,
            "A safe explanation.",
            "A safe recommended action.",
            technicalDetail);
    }

    private static ModelInspectionEvidence CreateEvidence()
    {
        return new ModelInspectionEvidence(
            new ModelInspectionFileEvidence(
                "granite.gguf",
                Sha256,
                4_096,
                FixedUtc,
                Sha256,
                integrityPreserved: true),
            new ModelInspectionConfigurationEvidence(
                "GGUF",
                ggufVersion: 3,
                modelName: "Granite 4.1 3B",
                architecture: "granite",
                fileType: 15,
                quantisationVersion: 2,
                declaredContextLength: 4_096,
                embeddingSize: 2_048,
                layerCount: 24,
                attentionHeadCount: 16,
                kvHeadCount: 8,
                parameterCount: 3_000_000_000),
            new ModelInspectionTokenizerEvidence(
                "gpt2",
                vocabularyCount: 49_152,
                vocabularyType: "BPE",
                tokenizerSmokePassed: true,
                tokenizerSmokeTokenCount: 4,
                knownSpecialTokenIds: new Dictionary<string, int>()),
            new ModelInspectionChatTemplateEvidence(
                present: true,
                lengthCharacters: 128,
                sha256: Sha256),
            new ModelInspectionRuntimeIdentity(
                "GraniteEdgeAI.ModelInspection.Worker",
                "1.0.0",
                protocolVersion: 1,
                "llamasharp-0.27.0-cpu-win-x64-vocab-only-v1",
                "0.27.0",
                "0.27.0",
                LlamaCppCommit,
                "llama.dll",
                "X64",
                "VocabOnly",
                usesCuda: false,
                usesVulkan: false,
                gpuLayerCount: 0),
            Array.Empty<ModelInspectionObservation>());
    }
}

internal sealed class RecordingCommand : ICommand
{
    private readonly bool canExecute;

    internal RecordingCommand(bool canExecute)
    {
        this.canExecute = canExecute;
    }

    public event EventHandler? CanExecuteChanged
    {
        add { }
        remove { }
    }

    internal int ExecutionCount { get; private set; }

    public bool CanExecute(object? parameter)
    {
        return canExecute;
    }

    public void Execute(object? parameter)
    {
        ExecutionCount++;
    }
}
