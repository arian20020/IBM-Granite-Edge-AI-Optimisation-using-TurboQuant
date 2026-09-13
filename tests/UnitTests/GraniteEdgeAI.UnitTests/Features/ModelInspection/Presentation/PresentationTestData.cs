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

    internal static ModelInspectionRequest CreateRequest(
        string modelName = "Granite 4.1 3B",
        string architecture = "granite",
        string? parameterSizeLabel = "3B",
        string? quantisation = "Q4_K_M")
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
                modelName,
                architecture,
                parameterSizeLabel,
                quantisation,
                fileSizeBytes: 4_096,
                declaredContextLength: 4_096,
                ggufVersion: 3));
    }

    internal static ModelInspectionResult CreateResult(
        ModelInspectionOutcome outcome,
        string technicalDetail = SensitiveTechnicalMarker,
        string warningCode = "MI-WARN-CHAT-TEMPLATE-MISSING",
        int warningCount = 1,
        ModelInspectionEvidence? evidence = null,
        TimeSpan? duration = null,
        string findingTitle = "Review this warning",
        string findingExplanation = "A safe explanation.",
        string findingRecommendedAction = "A safe recommended action.")
    {
        if (warningCount < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(warningCount));
        }

        ModelInspectionFinding[] findings = outcome switch
        {
            ModelInspectionOutcome.Ready => [],
            ModelInspectionOutcome.ReadyWithWarnings => Enumerable
                .Range(0, warningCount)
                .Select(_ => CreateFinding(
                    warningCode,
                    ModelInspectionFindingSeverity.Warning,
                    findingTitle,
                    findingExplanation,
                    findingRecommendedAction,
                    technicalDetail))
                .ToArray(),
            _ => []
        };

        return new ModelInspectionResult(
            outcome,
            evidence ?? CreateEvidence(
                chatTemplatePresent:
                    outcome != ModelInspectionOutcome.ReadyWithWarnings),
            findings,
            summary: $"Safe {outcome} summary.",
            recommendedAction: "Choose another model if needed.",
            verifiedConversionRouteId:
                outcome == ModelInspectionOutcome.ConversionRequired
                    ? "gguf-conversion-route-v1"
                    : null,
            startedAtUtc: FixedUtc,
            completedAtUtc: FixedUtc + (duration ?? TimeSpan.FromSeconds(2)));
    }

    internal static ModelInspectionOperationalFailure CreateFailure(
        string technicalDetail = SensitiveTechnicalMarker,
        string code = "MI-OP-TEST",
        string userMessage = "Inspection could not be completed safely.")
    {
        return new ModelInspectionOperationalFailure(
            code,
            userMessage,
            technicalDetail);
    }

    internal static RecordingCommand CreateCommand(bool canExecute = true)
    {
        return new RecordingCommand(canExecute);
    }

    private static ModelInspectionFinding CreateFinding(
        string code,
        ModelInspectionFindingSeverity severity,
        string title,
        string explanation,
        string recommendedAction,
        string technicalDetail)
    {
        return new ModelInspectionFinding(
            code,
            severity,
            title,
            explanation,
            recommendedAction,
            technicalDetail);
    }

    internal static ModelInspectionEvidence CreateEvidence(
        string fileName = "granite.gguf",
        string? configurationModelName = "Granite 4.1 3B",
        string? configurationArchitecture = "granite",
        string workerId = "GraniteEdgeAI.ModelInspection.Worker",
        string workerVersion = "1.0.0",
        int protocolVersion = 1,
        string runtimeProfile =
            "llamasharp-0.27.0-cpu-win-x64-vocab-only-v1",
        string llamaSharpVersion = "0.27.0",
        string backendPackageVersion = "0.27.0",
        string mappedLlamaCppCommit = LlamaCppCommit,
        string nativeLibraryName = "llama.dll",
        string processArchitecture = "X64",
        string inspectionMode = "VocabOnly",
        bool usesCuda = false,
        bool usesVulkan = false,
        int gpuLayerCount = 0,
        bool? tokenizerSmokePassed = true,
        int? tokenizerSmokeTokenCount = 4,
        bool? chatTemplatePresent = true,
        string? tokenizerModel = "gpt2",
        string? vocabularyType = "BPE",
        int? layerCount = 24,
        IReadOnlyDictionary<string, int>? knownSpecialTokenIds = null,
        IEnumerable<ModelInspectionObservation>? observations = null)
    {
        return new ModelInspectionEvidence(
            new ModelInspectionFileEvidence(
                fileName,
                Sha256,
                4_096,
                FixedUtc,
                Sha256,
                integrityPreserved: true),
            new ModelInspectionConfigurationEvidence(
                "GGUF",
                ggufVersion: 3,
                modelName: configurationModelName,
                architecture: configurationArchitecture,
                fileType: 15,
                quantisationVersion: 2,
                declaredContextLength: 4_096,
                embeddingSize: 2_048,
                layerCount: layerCount,
                attentionHeadCount: 16,
                kvHeadCount: 8,
                parameterCount: 3_000_000_000),
            new ModelInspectionTokenizerEvidence(
                tokenizerModel,
                vocabularyCount: 49_152,
                vocabularyType,
                tokenizerSmokePassed: tokenizerSmokePassed,
                tokenizerSmokeTokenCount: tokenizerSmokeTokenCount,
                knownSpecialTokenIds:
                    knownSpecialTokenIds ?? new Dictionary<string, int>()),
            chatTemplatePresent switch
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
            new ModelInspectionRuntimeIdentity(
                workerId,
                workerVersion,
                protocolVersion,
                runtimeProfile,
                llamaSharpVersion,
                backendPackageVersion,
                mappedLlamaCppCommit,
                nativeLibraryName,
                processArchitecture,
                inspectionMode,
                usesCuda,
                usesVulkan,
                gpuLayerCount),
            observations ?? Array.Empty<ModelInspectionObservation>());
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
