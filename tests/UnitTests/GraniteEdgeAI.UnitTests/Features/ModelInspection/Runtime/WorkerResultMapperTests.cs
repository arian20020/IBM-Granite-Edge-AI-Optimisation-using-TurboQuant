using GraniteEdgeAI.Features.ModelInspection.Contracts;
using GraniteEdgeAI.Features.ModelInspection.Runtime;
using GraniteEdgeAI.ModelInspection.Contracts;
using GraniteEdgeAI.ModelInspection.WorkerClient;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace GraniteEdgeAI.UnitTests.Features.ModelInspection.Runtime;

[TestClass]
public sealed class WorkerResultMapperTests
{
    private readonly WorkerResultMapper _mapper = new();

    [TestMethod]
    public void MapProgress_MapsEveryWorkerStageAndStatus()
    {
        foreach (WorkerStage stage in Enum.GetValues<WorkerStage>())
        {
            foreach (WorkerStageStatus status in
                Enum.GetValues<WorkerStageStatus>())
            {
                int completedStageCount = status is
                    WorkerStageStatus.Completed or WorkerStageStatus.Warning
                        ? (int)stage
                        : (int)stage - 1;
                WorkerProgressMessage source = new()
                {
                    ProtocolVersion = WorkerProtocol.Version,
                    MessageType = WorkerMessageKind.Progress,
                    RequestId = RuntimeTestData.RequestId,
                    Stage = stage,
                    StageStatus = status,
                    CompletedStageCount = completedStageCount,
                    TotalStageCount = 5,
                    StageFraction = 0.25
                };

                ModelInspectionProgress actual =
                    _mapper.MapProgress(source);

                Assert.AreEqual((int)stage, (int)actual.Stage);
                Assert.AreEqual((int)status, (int)actual.StageStatus);
                Assert.AreEqual(completedStageCount, actual.CompletedStageCount);
                Assert.AreEqual(5, actual.TotalStageCount);
                Assert.AreEqual(0.25, actual.StageFraction);
                Assert.IsFalse(string.IsNullOrWhiteSpace(actual.UserMessage));
            }
        }
    }

    [TestMethod]
    public void MapResult_CompletedMapsCompleteApplicationEvidence()
    {
        WorkerClientResult source = RuntimeTestData.CreateClientResult(
            RuntimeTestData.CreateTerminal());

        ModelInspectionProbeResult result = _mapper.MapResult(
            RuntimeTestData.CreateRequest(),
            RuntimeTestData.RequestId,
            source);

        Assert.AreEqual(ModelInspectionProbeStatus.Completed, result.Status);
        Assert.IsNotNull(result.Evidence);
        Assert.IsNull(result.Failure);
        ModelInspectionEvidence evidence = result.Evidence;
        Assert.AreEqual(RuntimeTestData.ModelFileName, evidence.File.FileName);
        Assert.AreEqual(
            RuntimeTestData.CanonicalPathSha256,
            evidence.File.CanonicalPathSha256);
        Assert.AreEqual(100, evidence.File.LengthBytes);
        Assert.AreEqual(RuntimeTestData.FixedUtcTime,
            evidence.File.LastWriteTimeUtc);
        Assert.AreEqual(RuntimeTestData.ModelSha256,
            evidence.File.ModelSha256);
        Assert.IsTrue(evidence.File.IntegrityPreserved);
        Assert.AreEqual("GGUF", evidence.Configuration.Format);
        Assert.AreEqual((uint)3, evidence.Configuration.GgufVersion);
        Assert.AreEqual("Granite 4.1 3B", evidence.Configuration.ModelName);
        Assert.AreEqual("granite", evidence.Configuration.Architecture);
        Assert.AreEqual(15, evidence.Configuration.FileType);
        Assert.AreEqual(2, evidence.Configuration.QuantisationVersion);
        Assert.AreEqual((ulong)4_096,
            evidence.Configuration.DeclaredContextLength);
        Assert.AreEqual((ulong)2_048, evidence.Configuration.EmbeddingSize);
        Assert.AreEqual(32, evidence.Configuration.LayerCount);
        Assert.AreEqual(32, evidence.Configuration.AttentionHeadCount);
        Assert.AreEqual(8, evidence.Configuration.KvHeadCount);
        Assert.AreEqual((ulong)3_000_000_000,
            evidence.Configuration.ParameterCount);
        Assert.AreEqual("llama", evidence.Tokenizer.TokenizerModel);
        Assert.AreEqual(49_152, evidence.Tokenizer.VocabularyCount);
        Assert.AreEqual("BPE", evidence.Tokenizer.VocabularyType);
        Assert.IsTrue(evidence.Tokenizer.TokenizerSmokePassed);
        Assert.AreEqual(3, evidence.Tokenizer.TokenizerSmokeTokenCount);
        Assert.AreEqual(1, evidence.Tokenizer.KnownSpecialTokenIds["bos"]);
        Assert.AreEqual(2, evidence.Tokenizer.KnownSpecialTokenIds["eos"]);
        Assert.IsTrue(evidence.ChatTemplate.Present);
        Assert.AreEqual(42, evidence.ChatTemplate.LengthCharacters);
        Assert.AreEqual(RuntimeTestData.ChatTemplateSha256,
            evidence.ChatTemplate.Sha256);
        Assert.AreEqual(WorkerProtocol.WorkerId,
            evidence.Runtime.WorkerId);
        Assert.AreEqual("1.0.0", evidence.Runtime.WorkerVersion);
        Assert.AreEqual(WorkerProtocol.Version,
            evidence.Runtime.ProtocolVersion);
        Assert.AreEqual(WorkerProtocol.RuntimeProfile,
            evidence.Runtime.RuntimeProfile);
        Assert.AreEqual("0.27.0", evidence.Runtime.LLamaSharpVersion);
        Assert.AreEqual("0.27.0", evidence.Runtime.BackendPackageVersion);
        Assert.AreEqual(RuntimeTestData.MappedLlamaCppCommit,
            evidence.Runtime.MappedLlamaCppCommit);
        Assert.AreEqual("llama.dll", evidence.Runtime.NativeLibraryName);
        Assert.AreEqual("X64", evidence.Runtime.ProcessArchitecture);
        Assert.AreEqual("VocabOnly", evidence.Runtime.InspectionMode);
        Assert.IsFalse(evidence.Runtime.UsesCuda);
        Assert.IsFalse(evidence.Runtime.UsesVulkan);
        Assert.AreEqual(0, evidence.Runtime.GpuLayerCount);
        Assert.HasCount(0, evidence.Observations);
    }

    [TestMethod]
    public void MapResult_UnavailableNativeModelNameRemainsNullAndAccepted()
    {
        WorkerInspectionEvidence evidence =
            RuntimeTestData.CreateWorkerEvidence() with
            {
                Configuration = RuntimeTestData.CreateWorkerEvidence()
                    .Configuration with
                {
                    ModelName = null
                }
            };

        ModelInspectionProbeResult result = _mapper.MapResult(
            RuntimeTestData.CreateRequest(),
            RuntimeTestData.RequestId,
            RuntimeTestData.CreateClientResult(
                RuntimeTestData.CreateTerminal(evidence: evidence)));

        Assert.AreEqual(ModelInspectionProbeStatus.Completed, result.Status);
        Assert.IsNotNull(result.Evidence);
        Assert.IsNull(result.Evidence.Configuration.ModelName);
    }

    [TestMethod]
    public void MapResult_TrustedCancelledMapsCooperativeCancellation()
    {
        WorkerClientResult source = RuntimeTestData.CreateClientResult(
            RuntimeTestData.CreateTerminal(WorkerCompletionStatus.Cancelled));

        ModelInspectionProbeResult result = _mapper.MapResult(
            RuntimeTestData.CreateRequest(),
            RuntimeTestData.RequestId,
            source);

        Assert.AreEqual(ModelInspectionProbeStatus.Cancelled, result.Status);
        Assert.IsNull(result.Evidence);
        Assert.IsNull(result.Failure);
    }

    [TestMethod]
    public void MapResult_WorkerFailureDoesNotExposeWorkerDiagnostics()
    {
        const string secret = @"C:\private\granite.gguf";
        WorkerCompletedMessage terminal = RuntimeTestData.CreateTerminal(
            WorkerCompletionStatus.OperationalFailure,
            operationalFailure: new WorkerOperationalFailure
            {
                Code = $"secret-{secret}",
                Message = $"request {RuntimeTestData.RequestId} {secret}"
            });

        ModelInspectionProbeResult result = _mapper.MapResult(
            RuntimeTestData.CreateRequest(),
            RuntimeTestData.RequestId,
            RuntimeTestData.CreateClientResult(terminal));

        AssertOperationalFailure(result, "MI-OP-WORKER");
        StringAssert.DoesNotContain(
            result.Failure!.TechnicalDetail,
            secret,
            StringComparison.Ordinal);
        StringAssert.DoesNotContain(
            result.Failure.TechnicalDetail,
            RuntimeTestData.RequestId.ToString(),
            StringComparison.Ordinal);
    }

    [TestMethod]
    public void MapResult_ClientFailureDoesNotExposeStderrOrDiagnostics()
    {
        const string secret = @"C:\private\granite.gguf";
        WorkerClientResult source = new(
            TerminalMessage: null,
            Failure: new WorkerClientFailure(
                WorkerClientFailureCodes.WorkerCrashed,
                $"request {RuntimeTestData.RequestId} {secret}"),
            ExitCode: 9,
            ForcedTermination: true,
            StandardErrorTruncated: false,
            RetainedStandardError: $"stderr {secret}",
            SecondaryDiagnostics: [$"diagnostic {secret}"]);

        ModelInspectionProbeResult result = _mapper.MapResult(
            RuntimeTestData.CreateRequest(),
            RuntimeTestData.RequestId,
            source);

        AssertOperationalFailure(result, "MI-OP-WORKER-CLIENT");
        StringAssert.DoesNotContain(
            result.Failure!.TechnicalDetail,
            secret,
            StringComparison.Ordinal);
        StringAssert.DoesNotContain(
            result.Failure.TechnicalDetail,
            RuntimeTestData.RequestId.ToString(),
            StringComparison.Ordinal);
    }

    [TestMethod]
    public void MapResult_WrongTerminalRequestIdFailsClosed()
    {
        WorkerCompletedMessage terminal = RuntimeTestData.CreateTerminal(
            requestId: Guid.Parse("ecde37fb-20a9-46a3-a67a-3b28319ddc25"));

        ModelInspectionProbeResult result = _mapper.MapResult(
            RuntimeTestData.CreateRequest(),
            RuntimeTestData.RequestId,
            RuntimeTestData.CreateClientResult(terminal));

        AssertOperationalFailure(result, "MI-OP-EVIDENCE-INVALID");
    }

    [TestMethod]
    public void MapResult_RejectsContradictoryOrUnapprovedEvidence()
    {
        (string Name, Func<WorkerInspectionEvidence, WorkerInspectionEvidence>
            Mutate)[] cases =
        [
            ("integrity flag", evidence => evidence with
            {
                ModelFile = evidence.ModelFile with
                {
                    IntegrityPreserved = false
                }
            }),
            ("request length", evidence => evidence with
            {
                ModelFile = evidence.ModelFile with { LengthBefore = 99 }
            }),
            ("before/after timestamp", evidence => evidence with
            {
                ModelFile = evidence.ModelFile with
                {
                    LastWriteTimeAfterUtc =
                        RuntimeTestData.FixedUtcTime.AddSeconds(1)
                }
            }),
            ("before/after hash", evidence => evidence with
            {
                ModelFile = evidence.ModelFile with
                {
                    Sha256After =
                        "dddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddd"
                }
            }),
            ("file name", evidence => evidence with
            {
                ModelFile = evidence.ModelFile with
                {
                    FileName = "other.gguf"
                }
            }),
            ("canonical path fingerprint", evidence => evidence with
            {
                ModelFile = evidence.ModelFile with
                {
                    CanonicalPathSha256 =
                        "dddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddd"
                }
            }),
            ("quick-scan architecture", evidence => evidence with
            {
                Configuration = evidence.Configuration with
                {
                    Architecture = "other"
                }
            }),
            ("available model name", evidence => evidence with
            {
                Configuration = evidence.Configuration with
                {
                    ModelName = "Other model"
                }
            }),
            ("quick-scan context", evidence => evidence with
            {
                Configuration = evidence.Configuration with
                {
                    DeclaredContextLength = 8_192
                }
            }),
            ("LLamaSharp version", evidence => evidence with
            {
                Runtime = evidence.Runtime with
                {
                    LLamaSharpVersion = "0.28.0"
                }
            }),
            ("backend version", evidence => evidence with
            {
                Runtime = evidence.Runtime with
                {
                    BackendPackageVersion = "0.28.0"
                }
            }),
            ("mapped llama.cpp commit", evidence => evidence with
            {
                Runtime = evidence.Runtime with
                {
                    MappedLlamaCppCommit =
                        "0000000000000000000000000000000000000000"
                }
            }),
            ("native library identity", evidence => evidence with
            {
                Runtime = evidence.Runtime with
                {
                    NativeLibraryName = "Other"
                }
            }),
            ("inspection mode", evidence => evidence with
            {
                Runtime = evidence.Runtime with
                {
                    InspectionMode = "ContextCreation"
                }
            }),
            ("process architecture", evidence => evidence with
            {
                Runtime = evidence.Runtime with
                {
                    ProcessArchitecture = "Arm64"
                }
            }),
            ("CUDA", evidence => evidence with
            {
                Runtime = evidence.Runtime with { UsesCuda = true }
            }),
            ("Vulkan", evidence => evidence with
            {
                Runtime = evidence.Runtime with { UsesVulkan = true }
            }),
            ("GPU layers", evidence => evidence with
            {
                Runtime = evidence.Runtime with { GpuLayerCount = 1 }
            }),
            ("invalid tokenizer evidence", evidence => evidence with
            {
                Tokenizer = evidence.Tokenizer with
                {
                    TokenizerSmokePassed = true,
                    TokenizerSmokeTokenCount = 0
                }
            }),
            ("failed tokenizer smoke", evidence => evidence with
            {
                Tokenizer = evidence.Tokenizer with
                {
                    TokenizerSmokePassed = false,
                    TokenizerSmokeTokenCount = null
                }
            }),
            ("unavailable tokenizer smoke", evidence => evidence with
            {
                Tokenizer = evidence.Tokenizer with
                {
                    TokenizerSmokePassed = null,
                    TokenizerSmokeTokenCount = null
                }
            }),
            ("invalid configuration evidence", evidence => evidence with
            {
                Configuration = evidence.Configuration with
                {
                    LayerCount = -1
                }
            }),
            ("invalid file digest", evidence => evidence with
            {
                ModelFile = evidence.ModelFile with
                {
                    CanonicalPathSha256 = "not-a-digest"
                }
            }),
            ("invalid chat-template evidence", evidence => evidence with
            {
                ChatTemplate = evidence.ChatTemplate with
                {
                    Present = false,
                    LengthCharacters = 42,
                    Sha256 = RuntimeTestData.ChatTemplateSha256
                }
            })
        ];

        foreach ((string name, Func<WorkerInspectionEvidence,
            WorkerInspectionEvidence> mutate) in cases)
        {
            WorkerInspectionEvidence evidence =
                mutate(RuntimeTestData.CreateWorkerEvidence());
            WorkerClientResult source = RuntimeTestData.CreateClientResult(
                RuntimeTestData.CreateTerminal(evidence: evidence));

            ModelInspectionProbeResult result = _mapper.MapResult(
                RuntimeTestData.CreateRequest(),
                RuntimeTestData.RequestId,
                source);

            AssertOperationalFailure(
                result,
                "MI-OP-EVIDENCE-INVALID",
                name);
        }
    }

    [TestMethod]
    public void MapResult_UnknownWorkerObservationFailsClosed()
    {
        WorkerInspectionEvidence evidence =
            RuntimeTestData.CreateWorkerEvidence() with
            {
                Observations =
                [
                    new WorkerObservation
                    {
                        Code = "MI-OBS-UNKNOWN",
                        TechnicalCategory = "Unknown",
                        TechnicalDetail = "No application taxonomy exists."
                    }
                ]
            };

        ModelInspectionProbeResult result = _mapper.MapResult(
            RuntimeTestData.CreateRequest(),
            RuntimeTestData.RequestId,
            RuntimeTestData.CreateClientResult(
                RuntimeTestData.CreateTerminal(evidence: evidence)));

        AssertOperationalFailure(result, "MI-OP-EVIDENCE-INVALID");
    }

    private static void AssertOperationalFailure(
        ModelInspectionProbeResult result,
        string expectedCode,
        string? message = null)
    {
        Assert.AreEqual(
            ModelInspectionProbeStatus.OperationalFailure,
            result.Status,
            message);
        Assert.IsNull(result.Evidence, message);
        Assert.IsNotNull(result.Failure, message);
        Assert.AreEqual(expectedCode, result.Failure.Code, message);
        Assert.IsFalse(
            string.IsNullOrWhiteSpace(result.Failure.UserMessage),
            message);
        Assert.IsFalse(
            string.IsNullOrWhiteSpace(result.Failure.TechnicalDetail),
            message);
    }
}
