using System.Text;

namespace GraniteEdgeAI.ModelInspection.Contracts.Tests;

/// <summary>
/// Creates deterministic protocol values used by strict JSON contract tests.
/// </summary>
internal static class TestJson
{
    internal static byte[] Utf8(string json)
    {
        return Encoding.UTF8.GetBytes(json);
    }

    internal static WorkerHelloMessage CreateValidHello()
    {
        return new WorkerHelloMessage
        {
            ProtocolVersion = WorkerProtocol.Version,
            MessageType = WorkerMessageKind.Hello,
            WorkerId = WorkerProtocol.WorkerId,
            WorkerVersion = "1.0.0",
            WorkerProcessId = 123,
            RuntimeProfile = WorkerProtocol.RuntimeProfile,
            ProcessArchitecture = "X64"
        };
    }

    internal static WorkerCancelInspectionCommand CreateValidCancel()
    {
        return new WorkerCancelInspectionCommand
        {
            ProtocolVersion = WorkerProtocol.Version,
            CommandType = WorkerCommandKind.CancelInspection,
            RequestId = Guid.Parse("11111111-1111-1111-1111-111111111111")
        };
    }

    internal static WorkerStartInspectionCommand CreateValidStart()
    {
        DateTimeOffset timestamp = new(
            2026,
            8,
            5,
            12,
            0,
            0,
            TimeSpan.Zero);

        return new WorkerStartInspectionCommand
        {
            ProtocolVersion = WorkerProtocol.Version,
            CommandType = WorkerCommandKind.StartInspection,
            RequestId = Guid.Parse("11111111-1111-1111-1111-111111111111"),
            ParentProcessId = 4321,
            ParentProcessStartTimeUtc = timestamp,
            ModelPath = @"C:\Models\granite.gguf",
            ExpectedFileIdentity = new WorkerExpectedFileIdentity
            {
                LengthBytes = 100,
                LastWriteTimeUtc = timestamp
            },
            QuickScan = new WorkerQuickScanSnapshot
            {
                Format = "GGUF",
                ModelName = "Granite 4.1 3B",
                Architecture = "granite",
                ParameterSizeLabel = "3B",
                Quantisation = "Q4_K_M",
                FileSizeBytes = 100,
                DeclaredContextLength = 131_072,
                GgufVersion = 3
            }
        };
    }

    internal static WorkerInspectionEvidence CreateValidEvidence()
    {
        DateTimeOffset timestamp = new(
            2026,
            8,
            5,
            12,
            0,
            0,
            TimeSpan.Zero);

        return new WorkerInspectionEvidence
        {
            Runtime = new WorkerRuntimeIdentity
            {
                WorkerVersion = "1.0.0",
                ProtocolVersion = WorkerProtocol.Version,
                RuntimeProfile = WorkerProtocol.RuntimeProfile,
                LLamaSharpVersion = "0.27.0",
                BackendPackageVersion = "0.27.0",
                MappedLlamaCppCommit = "mapped-commit",
                NativeLibraryName = "llama.dll",
                ProcessArchitecture = "X64",
                InspectionMode = "VocabOnly",
                UsesCuda = false,
                UsesVulkan = false,
                GpuLayerCount = 0
            },
            ModelFile = new WorkerModelFileEvidence
            {
                FileName = "granite.gguf",
                CanonicalPathSha256 = new string('C', 64),
                LengthBefore = 100,
                LengthAfter = 100,
                LastWriteTimeBeforeUtc = timestamp,
                LastWriteTimeAfterUtc = timestamp,
                Sha256Before = new string('A', 64),
                Sha256After = new string('A', 64),
                IntegrityPreserved = true
            },
            Configuration = new WorkerModelConfigurationEvidence
            {
                Architecture = "granite",
                ModelName = "Granite 4.1 3B",
                FileType = 15,
                QuantisationVersion = 2,
                TokenizerModel = "BPE",
                DeclaredContextLength = 131_072,
                EmbeddingSize = 4096,
                LayerCount = 32,
                AttentionHeadCount = 32,
                KvHeadCount = 8,
                ParameterCount = 3_000_000_000
            },
            Tokenizer = new WorkerTokenizerEvidence
            {
                VocabularyCount = 32_000,
                VocabularyType = "BPE",
                TokenizerSmokePassed = true,
                TokenizerSmokeTokenCount = 4,
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
                LengthCharacters = 128,
                Sha256 = new string('D', 64)
            },
            Observations =
            [
                new WorkerObservation
                {
                    Code = "MI-EVIDENCE-VALID",
                    TechnicalCategory = "ModelInspection",
                    TechnicalDetail = "Inspection evidence is complete."
                }
            ]
        };
    }
}
