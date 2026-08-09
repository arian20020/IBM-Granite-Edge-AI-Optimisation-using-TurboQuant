using GraniteEdgeAI.Features.ModelInspection.Contracts;
using GraniteEdgeAI.ModelInspection.Contracts;
using GraniteEdgeAI.ModelInspection.WorkerClient;
using System.Security.Cryptography;
using System.Text;

namespace GraniteEdgeAI.UnitTests.Features.ModelInspection.Runtime;

internal static class RuntimeTestData
{
    internal const string ModelFileName = "granite.gguf";
    internal const string ModelSha256 =
        "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb";
    internal const string ChatTemplateSha256 =
        "cccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccc";
    internal const string MappedLlamaCppCommit =
        "3f7c29d318e317b63f54c558bc69803963d7d88c";
    internal static readonly DateTimeOffset FixedUtcTime =
        new(2026, 8, 9, 1, 2, 3, TimeSpan.Zero);
    internal static readonly Guid RequestId =
        Guid.Parse("9a6e6875-fb30-4ecb-83e2-f3e73a6a4e96");
    internal static readonly string ModelPath = Path.GetFullPath(
        Path.Combine(Path.GetPathRoot(Environment.SystemDirectory)!,
            "Models",
            ModelFileName));
    internal static readonly string CanonicalPathSha256 =
        ComputeCanonicalPathSha256(ModelPath);

    internal static ModelInspectionRequest CreateRequest()
    {
        return new ModelInspectionRequest(
            ModelPath,
            ModelFileName,
            new ExpectedModelFileIdentity(100, FixedUtcTime),
            ValidatedQuickScanSnapshot.CreateGguf(
                modelName: "Granite 4.1 3B",
                architecture: "granite",
                parameterSizeLabel: "3B",
                quantisation: "Q4_K_M",
                fileSizeBytes: 100,
                declaredContextLength: 4_096,
                ggufVersion: 3));
    }

    private static string ComputeCanonicalPathSha256(string path)
    {
        string canonicalPath = OperatingSystem.IsWindows()
            ? path.ToUpperInvariant()
            : path;
        byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(canonicalPath));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    internal static WorkerInspectionEvidence CreateWorkerEvidence()
    {
        return new WorkerInspectionEvidence
        {
            Runtime = new WorkerRuntimeIdentity
            {
                WorkerVersion = "1.0.0",
                ProtocolVersion = WorkerProtocol.Version,
                RuntimeProfile = WorkerProtocol.RuntimeProfile,
                LLamaSharpVersion = "0.27.0",
                BackendPackageVersion = "0.27.0",
                MappedLlamaCppCommit = MappedLlamaCppCommit,
                NativeLibraryName = "LLama",
                ProcessArchitecture = "X64",
                InspectionMode = "VocabOnly",
                UsesCuda = false,
                UsesVulkan = false,
                GpuLayerCount = 0
            },
            ModelFile = new WorkerModelFileEvidence
            {
                FileName = ModelFileName,
                CanonicalPathSha256 = CanonicalPathSha256,
                LengthBefore = 100,
                LengthAfter = 100,
                LastWriteTimeBeforeUtc = FixedUtcTime,
                LastWriteTimeAfterUtc = FixedUtcTime,
                Sha256Before = ModelSha256,
                Sha256After = ModelSha256,
                IntegrityPreserved = true
            },
            Configuration = new WorkerModelConfigurationEvidence
            {
                Architecture = "granite",
                ModelName = "Granite 4.1 3B",
                FileType = 15,
                QuantisationVersion = 2,
                TokenizerModel = "llama",
                DeclaredContextLength = 4_096,
                EmbeddingSize = 2_048,
                LayerCount = 32,
                AttentionHeadCount = 32,
                KvHeadCount = 8,
                ParameterCount = 3_000_000_000
            },
            Tokenizer = new WorkerTokenizerEvidence
            {
                VocabularyCount = 49_152,
                VocabularyType = "BPE",
                TokenizerSmokePassed = true,
                TokenizerSmokeTokenCount = 3,
                KnownSpecialTokenIds = new Dictionary<string, int>(
                    StringComparer.Ordinal)
                {
                    ["bos"] = 1,
                    ["eos"] = 2
                }
            },
            ChatTemplate = new WorkerChatTemplateEvidence
            {
                Present = true,
                LengthCharacters = 42,
                Sha256 = ChatTemplateSha256
            },
            Observations = Array.Empty<WorkerObservation>()
        };
    }

    internal static WorkerCompletedMessage CreateTerminal(
        WorkerCompletionStatus status = WorkerCompletionStatus.Completed,
        WorkerInspectionEvidence? evidence = null,
        WorkerOperationalFailure? operationalFailure = null,
        Guid? requestId = null)
    {
        return new WorkerCompletedMessage
        {
            ProtocolVersion = WorkerProtocol.Version,
            MessageType = WorkerMessageKind.Completed,
            RequestId = requestId ?? RequestId,
            CompletionStatus = status,
            Evidence = status == WorkerCompletionStatus.Completed
                ? evidence ?? CreateWorkerEvidence()
                : null,
            OperationalFailure = status ==
                WorkerCompletionStatus.OperationalFailure
                    ? operationalFailure ?? new WorkerOperationalFailure
                    {
                        Code = "MI-WORKER-TEST",
                        Message = "The worker failed safely."
                    }
                    : null
        };
    }

    internal static WorkerClientResult CreateClientResult(
        WorkerCompletedMessage terminal)
    {
        return new WorkerClientResult(
            terminal,
            Failure: null,
            ExitCode: 0,
            ForcedTermination: false,
            StandardErrorTruncated: false,
            RetainedStandardError: string.Empty,
            SecondaryDiagnostics: Array.Empty<string>());
    }
}
