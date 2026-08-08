using System.Text.Json;
using GraniteEdgeAI.ModelInspection.Contracts;
using GraniteEdgeAI.ModelInspection.LlamaSharp;
using GraniteEdgeAI.ModelInspection.LlamaSharp.ModelProbe;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace GraniteEdgeAI.ModelInspection.Worker.Tests;

[TestClass]
public sealed class LlamaSharpInspectionEngineTests
{
    private const string WorkerVersion = "1.2.3-test";
    private const string Secret = "ENGINE-RAW-SECRET";

    [TestMethod]
    public void ProgramCreatesProductionLlamaSharpInspectionEngine()
    {
        IWorkerInspectionEngine engine =
            Program.CreateInspectionEngine(WorkerVersion);

        Assert.IsInstanceOfType<LlamaSharpInspectionEngine>(engine);
    }

    [TestMethod]
    public async Task InspectAsyncForwardsExactRequestAndReportsOrderedSuccessfulProgress()
    {
        WorkerStartInspectionCommand command = CreateStart();
        VocabOnlyModelProbeResult runtimeResult = CreateValidRuntimeResult(command);
        var probe = new FakeProbe(
            (progress, _) =>
            {
                progress?.Report(new VocabOnlyProbeProgress(true, null));
                progress?.Report(new VocabOnlyProbeProgress(false, 0.25f));
                progress?.Report(new VocabOnlyProbeProgress(false, 0.75f));
                return Task.FromResult(runtimeResult);
            });
        var progress = new CapturingProgress();
        var engine = new LlamaSharpInspectionEngine(probe, WorkerVersion);

        WorkerEngineResult result = await engine.InspectAsync(
            command,
            progress,
            CancellationToken.None);

        Assert.IsNotNull(probe.Request);
        Assert.AreEqual(command.ModelPath, probe.Request.ModelPath);
        Assert.IsTrue(
            probe.Request.ExpectedLengthBytes ==
            command.ExpectedFileIdentity.LengthBytes);
        Assert.IsTrue(
            probe.Request.ExpectedLastWriteTimeUtc ==
            command.ExpectedFileIdentity.LastWriteTimeUtc);
        Assert.AreEqual(
            WorkerCompletionStatus.Completed,
            result.CompletionStatus);
        Assert.IsNull(result.OperationalFailure);
        Assert.IsNotNull(result.Evidence);
        result.Evidence.Validate();

        AssertProgress(
            progress.Values,
            command.RequestId,
            [
                (WorkerStage.CheckModelPackage, WorkerStageStatus.Active, 0, null),
                (WorkerStage.CheckModelPackage, WorkerStageStatus.Completed, 1, null),
                (WorkerStage.ReadModelConfiguration, WorkerStageStatus.Active, 1, null),
                (WorkerStage.ReadModelConfiguration, WorkerStageStatus.Active, 1, 0.25),
                (WorkerStage.ReadModelConfiguration, WorkerStageStatus.Active, 1, 0.75),
                (WorkerStage.ReadModelConfiguration, WorkerStageStatus.Completed, 2, null),
                (WorkerStage.ValidateTokenizerAndChatSetup, WorkerStageStatus.Active, 2, null),
                (WorkerStage.ValidateTokenizerAndChatSetup, WorkerStageStatus.Completed, 3, null),
                (WorkerStage.ValidateModelStructure, WorkerStageStatus.Active, 3, null),
                (WorkerStage.ValidateModelStructure, WorkerStageStatus.Completed, 4, null),
                (WorkerStage.ConfirmCoreRuntimeCompatibility, WorkerStageStatus.Active, 4, null),
                (WorkerStage.ConfirmCoreRuntimeCompatibility, WorkerStageStatus.Completed, 5, null)
            ]);
    }

    [TestMethod]
    public async Task InspectAsyncMapsCompleteEvidenceAndNumericSpecialTokenValues()
    {
        WorkerStartInspectionCommand command = CreateStart();
        VocabOnlyModelProbeResult runtimeResult = CreateValidRuntimeResult(command) with
        {
            Logs = [new NativeBackendLogEntry("Debug", Secret)]
        };
        var probe = new FakeProbe(
            (progress, _) =>
            {
                progress?.Report(new VocabOnlyProbeProgress(true, null));
                return Task.FromResult(runtimeResult);
            });
        var engine = new LlamaSharpInspectionEngine(probe, WorkerVersion);

        WorkerEngineResult result = await engine.InspectAsync(
            command,
            progress: null,
            CancellationToken.None);

        Assert.AreEqual(WorkerCompletionStatus.Completed, result.CompletionStatus);
        Assert.IsNull(result.OperationalFailure);
        Assert.IsNotNull(result.Evidence);
        WorkerInspectionEvidence evidence = result.Evidence;
        evidence.Validate();

        Assert.AreEqual(WorkerVersion, evidence.Runtime.WorkerVersion);
        Assert.AreEqual(WorkerProtocol.Version, evidence.Runtime.ProtocolVersion);
        Assert.AreEqual(WorkerProtocol.RuntimeProfile, evidence.Runtime.RuntimeProfile);
        Assert.AreEqual("0.27.0", evidence.Runtime.LLamaSharpVersion);
        Assert.AreEqual("0.27.0", evidence.Runtime.BackendPackageVersion);
        Assert.AreEqual(
            PinnedApplicationRuntime.ExpectedLlamaCppCommit,
            evidence.Runtime.MappedLlamaCppCommit);
        Assert.AreEqual("LLama", evidence.Runtime.NativeLibraryName);
        Assert.AreEqual("X64", evidence.Runtime.ProcessArchitecture);
        Assert.AreEqual("VocabOnly", evidence.Runtime.InspectionMode);
        Assert.IsFalse(evidence.Runtime.UsesCuda);
        Assert.IsFalse(evidence.Runtime.UsesVulkan);
        Assert.AreEqual(0, evidence.Runtime.GpuLayerCount);

        Assert.AreEqual("granite.gguf", evidence.ModelFile.FileName);
        Assert.AreEqual(new string('C', 64), evidence.ModelFile.CanonicalPathSha256);
        Assert.AreEqual(100, evidence.ModelFile.LengthBefore);
        Assert.AreEqual(100, evidence.ModelFile.LengthAfter);
        Assert.AreEqual(
            command.ExpectedFileIdentity.LastWriteTimeUtc,
            evidence.ModelFile.LastWriteTimeBeforeUtc);
        Assert.AreEqual(
            command.ExpectedFileIdentity.LastWriteTimeUtc,
            evidence.ModelFile.LastWriteTimeAfterUtc);
        Assert.AreEqual(new string('A', 64), evidence.ModelFile.Sha256Before);
        Assert.AreEqual(new string('A', 64), evidence.ModelFile.Sha256After);
        Assert.IsTrue(evidence.ModelFile.IntegrityPreserved);

        Assert.AreEqual("granite", evidence.Configuration.Architecture);
        Assert.AreEqual("Granite 4.1 3B", evidence.Configuration.ModelName);
        Assert.AreEqual(15, evidence.Configuration.FileType);
        Assert.AreEqual(2, evidence.Configuration.QuantisationVersion);
        Assert.AreEqual("gpt2", evidence.Configuration.TokenizerModel);
        Assert.AreEqual((ulong)131_072, evidence.Configuration.DeclaredContextLength);
        Assert.AreEqual((ulong)4_096, evidence.Configuration.EmbeddingSize);
        Assert.AreEqual(32, evidence.Configuration.LayerCount);
        Assert.AreEqual(32, evidence.Configuration.AttentionHeadCount);
        Assert.AreEqual(8, evidence.Configuration.KvHeadCount);
        Assert.AreEqual((ulong)3_000_000_000, evidence.Configuration.ParameterCount);

        Assert.AreEqual(32_000, evidence.Tokenizer.VocabularyCount);
        Assert.AreEqual("BPE", evidence.Tokenizer.VocabularyType);
        Assert.IsTrue(evidence.Tokenizer.TokenizerSmokePassed);
        Assert.AreEqual(4, evidence.Tokenizer.TokenizerSmokeTokenCount);
        Assert.HasCount(6, evidence.Tokenizer.KnownSpecialTokenIds);
        Assert.AreEqual(1, evidence.Tokenizer.KnownSpecialTokenIds["bos"]);
        Assert.AreEqual(2, evidence.Tokenizer.KnownSpecialTokenIds["eos"]);
        Assert.AreEqual(3, evidence.Tokenizer.KnownSpecialTokenIds["newline"]);
        Assert.AreEqual(4, evidence.Tokenizer.KnownSpecialTokenIds["pad"]);
        Assert.AreEqual(5, evidence.Tokenizer.KnownSpecialTokenIds["mask"]);
        Assert.AreEqual(6, evidence.Tokenizer.KnownSpecialTokenIds["separator"]);

        Assert.IsTrue(evidence.ChatTemplate.Present);
        Assert.AreEqual(128, evidence.ChatTemplate.LengthCharacters);
        Assert.AreEqual(new string('D', 64), evidence.ChatTemplate.Sha256);

        Assert.HasCount(0, evidence.Observations);
        Assert.IsFalse(
            JsonSerializer.Serialize(result).Contains(
                Secret,
                StringComparison.Ordinal));
    }

    [TestMethod]
    [DataRow("MI-OP-MODEL-CONTINUITY-MISMATCH", "The selected model changed after it was prepared for inspection.")]
    [DataRow("MI-OP-MODEL-FILE-NOT-FOUND", "The selected model file was not found.")]
    [DataRow("MI-OP-MODEL-FILE-ACCESS-DENIED", "The selected model file could not be read.")]
    [DataRow("MI-OP-RUNTIME-ARCHITECTURE-MISMATCH", "The native model inspection runtime is incompatible with this process.")]
    [DataRow("MI-OP-MODEL-INTEGRITY-CHANGED", "The selected model changed during inspection.")]
    [DataRow("MI-OP-MODEL-INTEGRITY-VERIFICATION-FAILED", "The selected model integrity could not be verified.")]
    [DataRow("MI-OP-RUNTIME-UNAVAILABLE", "The native model inspection runtime is unavailable.")]
    [DataRow("MI-PROBE-MODEL-LOAD-FAILED", "The selected model could not be opened for inspection.")]
    [DataRow("MI-OP-MODEL-FILE-IO", "The selected model file could not be read reliably.")]
    [DataRow("MI-OP-RUNTIME-INSPECTION-FAILED", "The model inspection runtime could not produce reliable evidence.")]
    public async Task InspectAsyncMapsAllowlistedFailureToFixedMessageWithoutDetailLeak(
        string code,
        string expectedMessage)
    {
        WorkerStartInspectionCommand command = CreateStart();
        VocabOnlyModelProbeResult failed = CreateFailedRuntimeResult(command, code);
        var probe = new FakeProbe((_, _) => Task.FromResult(failed));
        var progress = new CapturingProgress();
        var engine = new LlamaSharpInspectionEngine(probe, WorkerVersion);

        WorkerEngineResult result = await engine.InspectAsync(
            command,
            progress,
            CancellationToken.None);

        AssertControlledFailure(result, code, expectedMessage);
        AssertNoSecret(result, command.ModelPath);
        AssertProgress(
            progress.Values,
            command.RequestId,
            [
                (WorkerStage.CheckModelPackage, WorkerStageStatus.Active, 0, null),
                (WorkerStage.CheckModelPackage, WorkerStageStatus.Failed, 0, null)
            ]);
    }

    [TestMethod]
    public async Task InspectAsyncMapsUnknownFailureAndEscapedExceptionToFixedGenericFailure()
    {
        WorkerStartInspectionCommand command = CreateStart();
        VocabOnlyModelProbeResult unknown = CreateFailedRuntimeResult(
            command,
            "MI-UNKNOWN-" + Secret);
        var resultProbe = new FakeProbe((_, _) => Task.FromResult(unknown));
        var exceptionProbe = new FakeProbe(
            (_, _) => Task.FromException<VocabOnlyModelProbeResult>(
                new InvalidOperationException(
                    command.ModelPath + ":" + Secret)));
        var escapedCancellationProbe = new FakeProbe(
            (_, _) => Task.FromException<VocabOnlyModelProbeResult>(
                new OperationCanceledException(
                    command.ModelPath + ":" + Secret)));

        foreach (FakeProbe probe in new[]
                 {
                     resultProbe,
                     exceptionProbe,
                     escapedCancellationProbe
                 })
        {
            var engine = new LlamaSharpInspectionEngine(probe, WorkerVersion);
            WorkerEngineResult result = await engine.InspectAsync(
                command,
                progress: null,
                CancellationToken.None);

            AssertControlledFailure(
                result,
                "MI-OP-RUNTIME-INSPECTION-FAILED",
                "The model inspection runtime could not produce reliable evidence.");
            AssertNoSecret(result, command.ModelPath);
        }
    }

    [TestMethod]
    public async Task InspectAsyncReturnsCancelledAndFinalizesCurrentStage()
    {
        WorkerStartInspectionCommand command = CreateStart();
        VocabOnlyModelProbeResult cancelled = CreateValidRuntimeResult(command) with
        {
            CompletionStatus = VocabOnlyProbeCompletionStatus.Cancelled,
            ModelEvidence = null,
            SelectedBackend = null,
            NativeHandleClosedAfterDispose = null,
            FailureCode = "MI-PROBE-CANCELLED",
            FailureType = typeof(OperationCanceledException).FullName,
            FailureMessage = Secret
        };
        var probe = new FakeProbe(
            (runtimeProgress, _) =>
            {
                runtimeProgress?.Report(new VocabOnlyProbeProgress(true, null));
                runtimeProgress?.Report(new VocabOnlyProbeProgress(false, 0.5f));
                return Task.FromResult(cancelled);
            });
        var progress = new CapturingProgress();
        var engine = new LlamaSharpInspectionEngine(probe, WorkerVersion);

        WorkerEngineResult result = await engine.InspectAsync(
            command,
            progress,
            CancellationToken.None);

        Assert.AreEqual(WorkerCompletionStatus.Cancelled, result.CompletionStatus);
        Assert.IsNull(result.Evidence);
        Assert.IsNull(result.OperationalFailure);
        AssertProgress(
            progress.Values,
            command.RequestId,
            [
                (WorkerStage.CheckModelPackage, WorkerStageStatus.Active, 0, null),
                (WorkerStage.CheckModelPackage, WorkerStageStatus.Completed, 1, null),
                (WorkerStage.ReadModelConfiguration, WorkerStageStatus.Active, 1, null),
                (WorkerStage.ReadModelConfiguration, WorkerStageStatus.Active, 1, 0.5),
                (WorkerStage.ReadModelConfiguration, WorkerStageStatus.Cancelled, 1, null)
            ]);
        AssertNoSecret(result, command.ModelPath);
    }

    [TestMethod]
    public async Task InspectAsyncIntegrityFailureTakesPrecedenceOverCancellation()
    {
        WorkerStartInspectionCommand command = CreateStart();
        VocabOnlyModelProbeResult cancelled = CreateValidRuntimeResult(command) with
        {
            CompletionStatus = VocabOnlyProbeCompletionStatus.Cancelled,
            Integrity = CreatePreservedIntegrity() with { Sha256Unchanged = false },
            ModelEvidence = null,
            NativeHandleClosedAfterDispose = null,
            FailureCode = "MI-PROBE-CANCELLED",
            FailureMessage = Secret
        };
        var probe = new FakeProbe((_, _) => Task.FromResult(cancelled));
        var engine = new LlamaSharpInspectionEngine(probe, WorkerVersion);

        WorkerEngineResult result = await engine.InspectAsync(
            command,
            progress: null,
            CancellationToken.None);

        AssertControlledFailure(
            result,
            "MI-OP-MODEL-INTEGRITY-CHANGED",
            "The selected model changed during inspection.");
    }

    [TestMethod]
    [DataRow(VocabOnlyProbeCompletionStatus.Succeeded, "integrity")]
    [DataRow(VocabOnlyProbeCompletionStatus.Succeeded, "before")]
    [DataRow(VocabOnlyProbeCompletionStatus.Succeeded, "after")]
    [DataRow(VocabOnlyProbeCompletionStatus.Cancelled, "integrity")]
    [DataRow(VocabOnlyProbeCompletionStatus.Cancelled, "before")]
    [DataRow(VocabOnlyProbeCompletionStatus.Cancelled, "after")]
    public async Task InspectAsyncMissingIntegrityEvidenceTakesVerificationFailurePrecedence(
        VocabOnlyProbeCompletionStatus status,
        string missing)
    {
        WorkerStartInspectionCommand command = CreateStart();
        VocabOnlyModelProbeResult runtimeResult = CreateValidRuntimeResult(command) with
        {
            CompletionStatus = status,
            Integrity = missing == "integrity" ? null : CreatePreservedIntegrity(),
            BeforeSnapshot = missing == "before"
                ? null
                : CreateValidRuntimeResult(command).BeforeSnapshot,
            AfterSnapshot = missing == "after"
                ? null
                : CreateValidRuntimeResult(command).AfterSnapshot,
            FailureCode = status == VocabOnlyProbeCompletionStatus.Cancelled
                ? "MI-PROBE-CANCELLED"
                : null
        };
        var probe = new FakeProbe((_, _) => Task.FromResult(runtimeResult));
        var engine = new LlamaSharpInspectionEngine(probe, WorkerVersion);

        WorkerEngineResult result = await engine.InspectAsync(
            command,
            progress: null,
            CancellationToken.None);

        AssertControlledFailure(
            result,
            "MI-OP-MODEL-INTEGRITY-VERIFICATION-FAILED",
            "The selected model integrity could not be verified.");
    }

    [TestMethod]
    public async Task InspectAsyncSerializesConcurrentNativeProgressCallbacks()
    {
        WorkerStartInspectionCommand command = CreateStart();
        VocabOnlyModelProbeResult runtimeResult = CreateValidRuntimeResult(command);
        using var progress = new EventCoordinatedProgress();
        var probe = new FakeProbe(
            (runtimeProgress, _) =>
            {
                runtimeProgress?.Report(new VocabOnlyProbeProgress(true, null));
                progress.StartCompetingCallbacks(runtimeProgress!);
                return Task.FromResult(runtimeResult);
            });
        var engine = new LlamaSharpInspectionEngine(probe, WorkerVersion);

        WorkerEngineResult result = await engine.InspectAsync(
            command,
            progress,
            CancellationToken.None);

        Assert.AreEqual(WorkerCompletionStatus.Completed, result.CompletionStatus);
        Assert.IsTrue(progress.SecondCallbackWasBlockedUntilFirstReleased);
        Assert.AreEqual(2, progress.FractionalProgressReportCount);
        Assert.IsTrue(progress.Values.All(message =>
        {
            message.Validate();
            return true;
        }));
    }

    [TestMethod]
    public async Task InspectAsyncRejectsImpossibleRuntimeProgressFacts()
    {
        WorkerStartInspectionCommand command = CreateStart();
        VocabOnlyProbeProgress[] impossibleFacts =
        [
            new VocabOnlyProbeProgress(true, 0.5f),
            new VocabOnlyProbeProgress(false, null)
        ];

        foreach (VocabOnlyProbeProgress impossible in impossibleFacts)
        {
            var probe = new FakeProbe(
                (runtimeProgress, _) =>
                {
                    runtimeProgress?.Report(impossible);
                    return Task.FromResult(CreateValidRuntimeResult(command));
                });
            var progress = new CapturingProgress();
            var engine = new LlamaSharpInspectionEngine(probe, WorkerVersion);

            WorkerEngineResult result = await engine.InspectAsync(
                command,
                progress,
                CancellationToken.None);

            AssertControlledFailure(
                result,
                "MI-OP-RUNTIME-INSPECTION-FAILED",
                "The model inspection runtime could not produce reliable evidence.");
            AssertProgress(
                progress.Values,
                command.RequestId,
                [
                    (WorkerStage.CheckModelPackage, WorkerStageStatus.Active, 0, null),
                    (WorkerStage.CheckModelPackage, WorkerStageStatus.Failed, 0, null)
                ]);
        }
    }

    [TestMethod]
    [DataRow("missing-model-evidence")]
    [DataRow("missing-vocabulary")]
    [DataRow("missing-tokenizer-smoke")]
    [DataRow("missing-chat-template")]
    [DataRow("wrong-schema-version")]
    [DataRow("wrong-probe-mode")]
    [DataRow("vocab-only-not-requested")]
    [DataRow("nonzero-gpu-layer-count")]
    [DataRow("memory-map-disabled")]
    [DataRow("memory-lock-enabled")]
    [DataRow("wrong-managed-package-name")]
    [DataRow("wrong-managed-pin")]
    [DataRow("wrong-backend-package-name")]
    [DataRow("wrong-backend-package-version")]
    [DataRow("wrong-source-tag")]
    [DataRow("wrong-source-commit")]
    [DataRow("wrong-llama-commit")]
    [DataRow("wrong-runtime-identifier")]
    [DataRow("wrong-process-architecture")]
    [DataRow("missing-selected-backend")]
    [DataRow("wrong-native-library")]
    [DataRow("missing-cuda-identity")]
    [DataRow("wrong-backend")]
    [DataRow("missing-vulkan-identity")]
    [DataRow("vulkan-backend")]
    [DataRow("native-handle-open")]
    [DataRow("missing-native-handle-state")]
    [DataRow("unexpected-failure-code")]
    [DataRow("unexpected-failure-type")]
    [DataRow("unexpected-failure-message")]
    [DataRow("empty-before-file-name")]
    [DataRow("request-file-name-mismatch")]
    [DataRow("snapshot-file-name-mismatch")]
    [DataRow("empty-before-path-hash")]
    [DataRow("snapshot-path-hash-mismatch")]
    [DataRow("request-length-mismatch")]
    [DataRow("snapshot-length-mismatch")]
    [DataRow("request-timestamp-mismatch")]
    [DataRow("snapshot-timestamp-mismatch")]
    [DataRow("empty-before-sha256")]
    [DataRow("snapshot-sha256-mismatch")]
    public async Task InspectAsyncRejectsIncompleteOrUnsafeSuccessfulResult(
        string mutation)
    {
        WorkerStartInspectionCommand command = CreateStart();
        VocabOnlyModelProbeResult runtimeResult = MutateUnsafe(
            CreateValidRuntimeResult(command),
            mutation);
        var probe = new FakeProbe(
            (runtimeProgress, _) =>
            {
                runtimeProgress?.Report(new VocabOnlyProbeProgress(true, null));
                return Task.FromResult(runtimeResult);
            });
        var engine = new LlamaSharpInspectionEngine(probe, WorkerVersion);

        WorkerEngineResult result = await engine.InspectAsync(
            command,
            progress: null,
            CancellationToken.None);

        AssertControlledFailure(
            result,
            "MI-OP-RUNTIME-INSPECTION-FAILED",
            "The model inspection runtime could not produce reliable evidence.");
        AssertNoSecret(result, command.ModelPath);
    }

    [TestMethod]
    public async Task InspectAsyncCompletesWithNullableFindingsAndOmitsUnsafeDetail()
    {
        WorkerStartInspectionCommand command = CreateStart();
        VocabOnlyRuntimeModelEvidence model = CreateValidRuntimeResult(command)
            .ModelEvidence!;
        VocabOnlyModelProbeResult runtimeResult = CreateValidRuntimeResult(command) with
        {
            ModelEvidence = model with
            {
                FileType = "not-an-int",
                QuantizationVersion = "-1",
                Vocabulary = model.Vocabulary with
                {
                    Bos = Token("not-an-int"),
                    Eos = Token("-1")
                },
                TokenizerSmoke = new TokenizerSmokeEvidence
                {
                    Succeeded = false,
                    FailureType = typeof(InvalidOperationException).FullName,
                    FailureMessage = Secret
                },
                ChatTemplate = new ChatTemplateEvidence
                {
                    Present = false,
                    LengthCharacters = null,
                    Sha256 = null
                }
            }
        };
        var probe = new FakeProbe(
            (runtimeProgress, _) =>
            {
                runtimeProgress?.Report(new VocabOnlyProbeProgress(true, null));
                return Task.FromResult(runtimeResult);
            });
        var engine = new LlamaSharpInspectionEngine(probe, WorkerVersion);

        WorkerEngineResult result = await engine.InspectAsync(
            command,
            progress: null,
            CancellationToken.None);

        Assert.AreEqual(WorkerCompletionStatus.Completed, result.CompletionStatus);
        Assert.IsNotNull(result.Evidence);
        Assert.IsNull(result.Evidence.Configuration.FileType);
        Assert.IsNull(result.Evidence.Configuration.QuantisationVersion);
        Assert.IsFalse(result.Evidence.Tokenizer.TokenizerSmokePassed);
        Assert.IsNull(result.Evidence.Tokenizer.TokenizerSmokeTokenCount);
        Assert.IsFalse(
            result.Evidence.Tokenizer.KnownSpecialTokenIds.ContainsKey("bos"));
        Assert.IsFalse(
            result.Evidence.Tokenizer.KnownSpecialTokenIds.ContainsKey("eos"));
        Assert.IsFalse(result.Evidence.ChatTemplate.Present);
        AssertNoSecret(result, command.ModelPath);
    }

    [TestMethod]
    public async Task InspectAsyncDoesNotFillUnavailableRuntimeEvidenceFromQuickScan()
    {
        WorkerStartInspectionCommand command = CreateStart();
        VocabOnlyRuntimeModelEvidence model = CreateValidRuntimeResult(command)
            .ModelEvidence!;
        VocabOnlyModelProbeResult runtimeResult = CreateValidRuntimeResult(command) with
        {
            ModelEvidence = model with
            {
                Architecture = null,
                ModelName = null,
                TokenizerModel = null,
                ContextSize = null,
                ParameterCount = null
            }
        };
        var probe = new FakeProbe(
            (runtimeProgress, _) =>
            {
                runtimeProgress?.Report(new VocabOnlyProbeProgress(true, null));
                return Task.FromResult(runtimeResult);
            });
        var engine = new LlamaSharpInspectionEngine(probe, WorkerVersion);

        WorkerEngineResult result = await engine.InspectAsync(
            command,
            progress: null,
            CancellationToken.None);

        Assert.AreEqual(WorkerCompletionStatus.Completed, result.CompletionStatus);
        Assert.IsNotNull(result.Evidence);
        Assert.IsNull(result.Evidence.Configuration.Architecture);
        Assert.IsNull(result.Evidence.Configuration.ModelName);
        Assert.IsNull(result.Evidence.Configuration.TokenizerModel);
        Assert.IsNull(result.Evidence.Configuration.DeclaredContextLength);
        Assert.IsNull(result.Evidence.Configuration.ParameterCount);
    }

    private static WorkerStartInspectionCommand CreateStart()
    {
        DateTimeOffset timestamp = new(2026, 8, 8, 10, 0, 0, TimeSpan.Zero);
        return new WorkerStartInspectionCommand
        {
            ProtocolVersion = WorkerProtocol.Version,
            CommandType = WorkerCommandKind.StartInspection,
            RequestId = Guid.NewGuid(),
            ParentProcessId = 1234,
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
                ModelName = "Quick scan value must not be used",
                Architecture = "quick-scan-must-not-fill-evidence",
                FileSizeBytes = 100,
                DeclaredContextLength = 99,
                GgufVersion = 3
            }
        };
    }

    private static VocabOnlyModelProbeResult CreateValidRuntimeResult(
        WorkerStartInspectionCommand command)
    {
        DateTimeOffset timestamp = command.ExpectedFileIdentity.LastWriteTimeUtc;
        var snapshot = new ModelFileSnapshot
        {
            FileName = "granite.gguf",
            CanonicalPathSha256 = new string('C', 64),
            LengthBytes = command.ExpectedFileIdentity.LengthBytes,
            LastWriteTimeUtc = timestamp,
            Sha256 = new string('A', 64)
        };
        return new VocabOnlyModelProbeResult
        {
            SchemaVersion = "1.1",
            ProbeMode = "VocabOnly",
            StartedAtUtc = timestamp,
            CompletedAtUtc = timestamp.AddSeconds(1),
            DurationMilliseconds = 1_000,
            CompletionStatus = VocabOnlyProbeCompletionStatus.Succeeded,
            VocabOnlyRequested = true,
            GpuLayerCount = 0,
            UseMemoryMap = true,
            UseMemoryLock = false,
            ManagedPackageName = PinnedApplicationRuntime.ManagedPackageName,
            ManagedPackageVersion = PinnedApplicationRuntime.ManagedPackageVersion,
            BackendPackageName = PinnedApplicationRuntime.BackendPackageName,
            BackendPackageVersion = PinnedApplicationRuntime.BackendPackageVersion,
            LlamaSharpSourceTag = PinnedApplicationRuntime.LlamaSharpSourceTag,
            LlamaSharpReleaseCommit = PinnedApplicationRuntime.LlamaSharpReleaseCommit,
            ExpectedLlamaCppCommit = PinnedApplicationRuntime.ExpectedLlamaCppCommit,
            IntendedProductionRuntimeIdentifier = PinnedApplicationRuntime.IntendedProductionRuntimeIdentifier,
            ProcessArchitecture = "X64",
            OperatingSystem = "Windows",
            FrameworkDescription = ".NET 8",
            SelectedBackend = new SelectedNativeBackend
            {
                ImplementationType = "LLama.Native.NativeLibrary",
                NativeLibraryName = "LLama",
                UsesCuda = false,
                UsesVulkan = false,
                AvxLevel = "Avx512"
            },
            BeforeSnapshot = snapshot,
            AfterSnapshot = snapshot with { },
            Integrity = CreatePreservedIntegrity(),
            ModelEvidence = new VocabOnlyRuntimeModelEvidence
            {
                Architecture = "granite",
                ModelName = "Granite 4.1 3B",
                FileType = "15",
                QuantizationVersion = "2",
                TokenizerModel = "gpt2",
                ContextSize = 131_072,
                ParameterCount = 3_000_000_000,
                EmbeddingSize = 4_096,
                LayerCount = 32,
                HeadCount = 32,
                KvHeadCount = 8,
                Vocabulary = new VocabularyEvidence
                {
                    Count = 32_000,
                    Type = "BPE",
                    Bos = Token("1"),
                    Eos = Token("2"),
                    Newline = Token("3"),
                    Pad = Token("4"),
                    Mask = Token("5"),
                    Separator = Token("6")
                },
                TokenizerSmoke = new TokenizerSmokeEvidence
                {
                    Succeeded = true,
                    TokenCount = 4
                },
                ChatTemplate = new ChatTemplateEvidence
                {
                    Present = true,
                    LengthCharacters = 128,
                    Sha256 = new string('D', 64)
                }
            },
            NativeHandleClosedAfterDispose = true,
            Logs = []
        };
    }

    private static SpecialTokenEvidence Token(string id) => new()
    {
        TokenId = id,
        DecodedText = Secret + id
    };

    private static ModelFileIntegrityComparison CreatePreservedIntegrity() => new()
    {
        PathUnchanged = true,
        LengthUnchanged = true,
        LastWriteTimeUnchanged = true,
        Sha256Unchanged = true
    };

    private static VocabOnlyModelProbeResult CreateFailedRuntimeResult(
        WorkerStartInspectionCommand command,
        string code) => CreateValidRuntimeResult(command) with
    {
        CompletionStatus = VocabOnlyProbeCompletionStatus.Failed,
        SelectedBackend = null,
        BeforeSnapshot = null,
        AfterSnapshot = null,
        Integrity = null,
        ModelEvidence = null,
        NativeHandleClosedAfterDispose = null,
        FailureCode = code,
        FailureType = typeof(InvalidOperationException).FullName,
        FailureMessage = command.ModelPath + ":" + Secret,
        Logs = [new NativeBackendLogEntry("Error", Secret)]
    };

    private static VocabOnlyModelProbeResult MutateUnsafe(
        VocabOnlyModelProbeResult result,
        string mutation) => mutation switch
    {
        "missing-model-evidence" => result with { ModelEvidence = null },
        "missing-vocabulary" => result with
        {
            ModelEvidence = result.ModelEvidence! with { Vocabulary = null! }
        },
        "missing-tokenizer-smoke" => result with
        {
            ModelEvidence = result.ModelEvidence! with { TokenizerSmoke = null! }
        },
        "missing-chat-template" => result with
        {
            ModelEvidence = result.ModelEvidence! with { ChatTemplate = null! }
        },
        "wrong-schema-version" => result with { SchemaVersion = "1.0" },
        "wrong-probe-mode" => result with { ProbeMode = "Full" },
        "vocab-only-not-requested" => result with { VocabOnlyRequested = false },
        "nonzero-gpu-layer-count" => result with { GpuLayerCount = 1 },
        "memory-map-disabled" => result with { UseMemoryMap = false },
        "memory-lock-enabled" => result with { UseMemoryLock = true },
        "wrong-managed-package-name" => result with
        {
            ManagedPackageName = "Wrong.Managed.Package"
        },
        "wrong-managed-pin" => result with { ManagedPackageVersion = "0.28.0" },
        "wrong-backend-package-name" => result with
        {
            BackendPackageName = "Wrong.Backend.Package"
        },
        "wrong-backend-package-version" => result with
        {
            BackendPackageVersion = "0.28.0"
        },
        "wrong-source-tag" => result with { LlamaSharpSourceTag = "v0.28.0" },
        "wrong-source-commit" => result with
        {
            LlamaSharpReleaseCommit = new string('1', 40)
        },
        "wrong-llama-commit" => result with
        {
            ExpectedLlamaCppCommit = new string('2', 40)
        },
        "wrong-runtime-identifier" => result with
        {
            IntendedProductionRuntimeIdentifier = "linux-x64"
        },
        "wrong-process-architecture" => result with
        {
            ProcessArchitecture = "Arm64"
        },
        "missing-selected-backend" => result with { SelectedBackend = null },
        "wrong-native-library" => result with
        {
            SelectedBackend = result.SelectedBackend! with
            {
                NativeLibraryName = "Other"
            }
        },
        "missing-cuda-identity" => result with
        {
            SelectedBackend = result.SelectedBackend! with { UsesCuda = null }
        },
        "wrong-backend" => result with
        {
            SelectedBackend = result.SelectedBackend! with { UsesCuda = true }
        },
        "missing-vulkan-identity" => result with
        {
            SelectedBackend = result.SelectedBackend! with { UsesVulkan = null }
        },
        "vulkan-backend" => result with
        {
            SelectedBackend = result.SelectedBackend! with { UsesVulkan = true }
        },
        "native-handle-open" => result with { NativeHandleClosedAfterDispose = false },
        "missing-native-handle-state" => result with
        {
            NativeHandleClosedAfterDispose = null
        },
        "unexpected-failure-code" => result with { FailureCode = Secret },
        "unexpected-failure-type" => result with { FailureType = Secret },
        "unexpected-failure-message" => result with { FailureMessage = Secret },
        "empty-before-file-name" => result with
        {
            BeforeSnapshot = result.BeforeSnapshot! with { FileName = "" }
        },
        "request-file-name-mismatch" => result with
        {
            BeforeSnapshot = result.BeforeSnapshot! with { FileName = "other.gguf" },
            AfterSnapshot = result.AfterSnapshot! with { FileName = "other.gguf" }
        },
        "snapshot-file-name-mismatch" => result with
        {
            AfterSnapshot = result.AfterSnapshot! with { FileName = "other.gguf" }
        },
        "empty-before-path-hash" => result with
        {
            BeforeSnapshot = result.BeforeSnapshot! with
            {
                CanonicalPathSha256 = ""
            },
            AfterSnapshot = result.AfterSnapshot! with
            {
                CanonicalPathSha256 = ""
            }
        },
        "snapshot-path-hash-mismatch" => result with
        {
            AfterSnapshot = result.AfterSnapshot! with
            {
                CanonicalPathSha256 = new string('E', 64)
            }
        },
        "request-length-mismatch" => result with
        {
            BeforeSnapshot = result.BeforeSnapshot! with { LengthBytes = 101 },
            AfterSnapshot = result.AfterSnapshot! with { LengthBytes = 101 }
        },
        "snapshot-length-mismatch" => result with
        {
            AfterSnapshot = result.AfterSnapshot! with { LengthBytes = 101 }
        },
        "request-timestamp-mismatch" => result with
        {
            BeforeSnapshot = result.BeforeSnapshot! with
            {
                LastWriteTimeUtc = result.BeforeSnapshot.LastWriteTimeUtc.AddSeconds(1)
            },
            AfterSnapshot = result.AfterSnapshot! with
            {
                LastWriteTimeUtc = result.AfterSnapshot.LastWriteTimeUtc.AddSeconds(1)
            }
        },
        "snapshot-timestamp-mismatch" => result with
        {
            AfterSnapshot = result.AfterSnapshot! with
            {
                LastWriteTimeUtc = result.AfterSnapshot.LastWriteTimeUtc.AddSeconds(1)
            }
        },
        "empty-before-sha256" => result with
        {
            BeforeSnapshot = result.BeforeSnapshot! with { Sha256 = "" },
            AfterSnapshot = result.AfterSnapshot! with { Sha256 = "" }
        },
        "snapshot-sha256-mismatch" => result with
        {
            AfterSnapshot = result.AfterSnapshot! with
            {
                Sha256 = new string('E', 64)
            }
        },
        _ => throw new AssertFailedException($"Unknown mutation: {mutation}")
    };

    private static void AssertControlledFailure(
        WorkerEngineResult result,
        string code,
        string message)
    {
        Assert.AreEqual(
            WorkerCompletionStatus.OperationalFailure,
            result.CompletionStatus);
        Assert.IsNull(result.Evidence);
        Assert.IsNotNull(result.OperationalFailure);
        Assert.AreEqual(code, result.OperationalFailure.Code);
        Assert.AreEqual(message, result.OperationalFailure.Message);
        result.OperationalFailure.Validate();
    }

    private static void AssertNoSecret(
        WorkerEngineResult result,
        string modelPath)
    {
        string serialized = JsonSerializer.Serialize(result);
        Assert.IsFalse(serialized.Contains(Secret, StringComparison.Ordinal));
        Assert.IsFalse(serialized.Contains(modelPath, StringComparison.Ordinal));
        Assert.IsFalse(
            serialized.Contains(
                nameof(InvalidOperationException),
                StringComparison.Ordinal));
    }

    private static void AssertProgress(
        List<WorkerProgressMessage> actual,
        Guid requestId,
        IReadOnlyList<(
            WorkerStage Stage,
            WorkerStageStatus Status,
            int Completed,
            double? Fraction)> expected)
    {
        Assert.AreEqual(expected.Count, actual.Count);
        for (int index = 0; index < expected.Count; index++)
        {
            WorkerProgressMessage message = actual[index];
            message.Validate();
            Assert.AreEqual(WorkerProtocol.Version, message.ProtocolVersion);
            Assert.AreEqual(WorkerMessageKind.Progress, message.MessageType);
            Assert.AreEqual(requestId, message.RequestId);
            Assert.AreEqual(expected[index].Stage, message.Stage);
            Assert.AreEqual(expected[index].Status, message.StageStatus);
            Assert.AreEqual(expected[index].Completed, message.CompletedStageCount);
            Assert.AreEqual(5, message.TotalStageCount);
            Assert.AreEqual(expected[index].Fraction, message.StageFraction);
        }
    }

    private sealed class CapturingProgress : IProgress<WorkerProgressMessage>
    {
        internal List<WorkerProgressMessage> Values { get; } = [];

        public void Report(WorkerProgressMessage value) => Values.Add(value);
    }

    private sealed class EventCoordinatedProgress :
        IProgress<WorkerProgressMessage>, IDisposable
    {
        private static readonly TimeSpan EventTimeout = TimeSpan.FromSeconds(5);

        private readonly object _sync = new();
        private readonly ManualResetEvent _firstFractionEntered = new(false);
        private readonly ManualResetEvent _releaseFirstFraction = new(false);
        private readonly ManualResetEvent _secondCallbackAboutToReport = new(false);
        private readonly ManualResetEvent _secondFractionEntered = new(false);
        private readonly ManualResetEvent _secondCallbackReturned = new(false);
        private int _fractionalProgressReportCount;

        internal List<WorkerProgressMessage> Values { get; } = [];

        internal bool SecondCallbackWasBlockedUntilFirstReleased { get; private set; }

        internal int FractionalProgressReportCount =>
            Volatile.Read(ref _fractionalProgressReportCount);

        internal void StartCompetingCallbacks(
            IProgress<VocabOnlyProbeProgress> runtimeProgress)
        {
            Exception? firstException = null;
            Exception? secondException = null;
            var firstThread = new Thread(
                () => CaptureException(
                    () => runtimeProgress.Report(
                        new VocabOnlyProbeProgress(false, 0.25f)),
                    exception => firstException = exception))
            {
                IsBackground = true,
                Name = "model-inspection-first-progress-callback"
            };
            var secondThread = new Thread(
                () =>
                {
                    _secondCallbackAboutToReport.Set();
                    CaptureException(
                        () => runtimeProgress.Report(
                            new VocabOnlyProbeProgress(false, 0.75f)),
                        exception => secondException = exception);
                    _secondCallbackReturned.Set();
                })
            {
                IsBackground = true,
                Name = "model-inspection-second-progress-callback"
            };

            firstThread.Start();
            Assert.IsTrue(
                _firstFractionEntered.WaitOne(EventTimeout),
                "The first progress callback did not enter the consumer.");

            secondThread.Start();
            Assert.IsTrue(
                _secondCallbackAboutToReport.WaitOne(EventTimeout),
                "The second dedicated callback thread did not start.");

            bool enteredBeforeRelease =
                _secondFractionEntered.WaitOne(TimeSpan.FromMilliseconds(250));
            bool returnedBeforeRelease = _secondCallbackReturned.WaitOne(0);
            SecondCallbackWasBlockedUntilFirstReleased =
                !enteredBeforeRelease && !returnedBeforeRelease;

            _releaseFirstFraction.Set();
            Assert.IsTrue(
                firstThread.Join(EventTimeout),
                "The first progress callback did not finish after release.");
            Assert.IsTrue(
                secondThread.Join(EventTimeout),
                "The second progress callback did not finish after release.");
            Assert.IsNull(firstException);
            Assert.IsNull(secondException);
            Assert.IsTrue(
                _secondFractionEntered.WaitOne(0),
                "The second progress callback never reached the consumer.");
        }

        public void Report(WorkerProgressMessage value)
        {
            if (value.Stage == WorkerStage.ReadModelConfiguration &&
                value.StageStatus == WorkerStageStatus.Active &&
                value.StageFraction is not null)
            {
                int callbackNumber =
                    Interlocked.Increment(ref _fractionalProgressReportCount);

                if (callbackNumber == 1)
                {
                    _firstFractionEntered.Set();
                    Assert.IsTrue(
                        _releaseFirstFraction.WaitOne(EventTimeout),
                        "The first progress callback was not released.");
                }
                else if (callbackNumber == 2)
                {
                    _secondFractionEntered.Set();
                }
            }

            lock (_sync)
            {
                Values.Add(value);
            }
        }

        public void Dispose()
        {
            _firstFractionEntered.Dispose();
            _releaseFirstFraction.Dispose();
            _secondCallbackAboutToReport.Dispose();
            _secondFractionEntered.Dispose();
            _secondCallbackReturned.Dispose();
        }

        private static void CaptureException(
            Action action,
            Action<Exception> capture)
        {
            try
            {
                action();
            }
            catch (Exception exception)
            {
                capture(exception);
            }
        }
    }

    private sealed class FakeProbe(
        Func<IProgress<VocabOnlyProbeProgress>?, CancellationToken,
            Task<VocabOnlyModelProbeResult>> run) : IVocabOnlyModelProbe
    {
        internal VocabOnlyProbeRequest? Request { get; private set; }

        public Task<VocabOnlyModelProbeResult> RunAsync(
            VocabOnlyProbeRequest request,
            IProgress<VocabOnlyProbeProgress>? progress,
            CancellationToken cancellationToken)
        {
            Request = request;
            return run(progress, cancellationToken);
        }
    }
}
