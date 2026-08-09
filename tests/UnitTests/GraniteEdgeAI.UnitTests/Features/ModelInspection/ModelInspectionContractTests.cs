using GraniteEdgeAI.Features.ModelInspection.Contracts;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Reflection;

namespace GraniteEdgeAI.UnitTests;

/// <summary>
/// Defines the immutable application language used by Model Import, Model
/// Inspection, classification, the service layer, and the future ViewModel.
/// </summary>
[TestClass]
public sealed class ModelInspectionContractTests
{
    private const string ModelPath = @"C:\Models\granite.gguf";
    private const string ModelFileName = "granite.gguf";
    private const string ModelSha256 =
        "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
    private const string CanonicalPathSha256 =
        "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb";
    private const string LlamaCppCommit =
        "3f7c29d318e317b63f54c558bc69803963d7d88c";

    private static readonly DateTimeOffset FixedUtcTime = new(
        2026,
        8,
        5,
        12,
        0,
        0,
        TimeSpan.Zero);

    [TestMethod]
    public void FileIdentity_NonPositiveLength_Throws()
    {
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() =>
            new ExpectedModelFileIdentity(0, FixedUtcTime));
    }

    [TestMethod]
    public void FileIdentity_NonUtcTimestamp_Throws()
    {
        DateTimeOffset nonUtc = FixedUtcTime.ToOffset(TimeSpan.FromHours(1));

        Assert.ThrowsExactly<ArgumentException>(() =>
            new ExpectedModelFileIdentity(100, nonUtc));
    }

    [TestMethod]
    public void QuickScan_CreateGguf_PreservesValidatedFacts()
    {
        ValidatedQuickScanSnapshot snapshot = CreateQuickScan();

        Assert.IsTrue(string.Equals(
            "GGUF",
            snapshot.Format,
            StringComparison.Ordinal));
        Assert.IsTrue(string.Equals(
            "Granite 4.1 3B",
            snapshot.ModelName,
            StringComparison.Ordinal));
        Assert.IsTrue(string.Equals(
            "granite",
            snapshot.Architecture,
            StringComparison.Ordinal));
        Assert.IsTrue(snapshot.FileSizeBytes == 100);
        Assert.IsTrue(snapshot.GgufVersion == 3);
    }

    [TestMethod]
    public void QuickScan_EmptyArchitecture_Throws()
    {
        Assert.ThrowsExactly<ArgumentException>(() =>
            ValidatedQuickScanSnapshot.CreateGguf(
                modelName: "Granite 4.1 3B",
                architecture: string.Empty,
                parameterSizeLabel: "3B",
                quantisation: "Q4_K_M",
                fileSizeBytes: 100,
                declaredContextLength: 131_072,
                ggufVersion: 3));
    }

    [TestMethod]
    public void Request_RelativePath_Throws()
    {
        Assert.ThrowsExactly<ArgumentException>(() =>
            new ModelInspectionRequest(
                "models/granite.gguf",
                ModelFileName,
                CreateFileIdentity(),
                CreateQuickScan()));
    }

    [TestMethod]
    public void Request_RejectsMismatchedFileName()
    {
        Assert.ThrowsExactly<ArgumentException>(() =>
            new ModelInspectionRequest(
                ModelPath,
                "different.gguf",
                CreateFileIdentity(),
                CreateQuickScan()));
    }

    [TestMethod]
    public void Request_RejectsFileNameContainingDirectory()
    {
        Assert.ThrowsExactly<ArgumentException>(() =>
            new ModelInspectionRequest(
                ModelPath,
                @"Models\granite.gguf",
                CreateFileIdentity(),
                CreateQuickScan()));
    }

    [TestMethod]
    public void Request_RejectsQuickScanLengthMismatch()
    {
        ExpectedModelFileIdentity identity =
            new(lengthBytes: 101, lastWriteTimeUtc: FixedUtcTime);

        Assert.ThrowsExactly<ArgumentException>(() =>
            new ModelInspectionRequest(
                ModelPath,
                ModelFileName,
                identity,
                CreateQuickScan()));
    }

    [TestMethod]
    public void Progress_FractionOutsideRange_Throws()
    {
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() =>
            new ModelInspectionProgress(
                ModelInspectionStage.ReadModelConfiguration,
                ModelInspectionStageStatus.Active,
                completedStageCount: 1,
                totalStageCount: 5,
                stageFraction: 1.01,
                userMessage: "Reading model configuration."));
    }

    [TestMethod]
    public void Progress_AllowsUnknownGenuineFraction()
    {
        ModelInspectionProgress progress = new(
            ModelInspectionStage.ReadModelConfiguration,
            ModelInspectionStageStatus.Active,
            completedStageCount: 1,
            totalStageCount: 5,
            stageFraction: null,
            userMessage: "Reading model configuration.");

        Assert.AreEqual(ModelInspectionStageStatus.Active, progress.StageStatus);
        Assert.IsNull(progress.StageFraction);
    }

    [TestMethod]
    public void Progress_RejectsUndefinedStageStatus()
    {
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() =>
            new ModelInspectionProgress(
                ModelInspectionStage.ReadModelConfiguration,
                (ModelInspectionStageStatus)999,
                completedStageCount: 1,
                totalStageCount: 5,
                stageFraction: null,
                userMessage: "Reading model configuration."));
    }

    [TestMethod]
    [DataRow(1, (int)ModelInspectionStageStatus.Active, 1)]
    [DataRow(5, (int)ModelInspectionStageStatus.Completed, 0)]
    [DataRow(3, (int)ModelInspectionStageStatus.Warning, 2)]
    [DataRow(4, (int)ModelInspectionStageStatus.Failed, 4)]
    [DataRow(2, (int)ModelInspectionStageStatus.Cancelled, 2)]
    public void Progress_RejectsContradictoryStageStatusAndCount(
        int stageValue,
        int statusValue,
        int completedStageCount)
    {
        Assert.ThrowsExactly<ArgumentException>(() =>
            new ModelInspectionProgress(
                (ModelInspectionStage)stageValue,
                (ModelInspectionStageStatus)statusValue,
                completedStageCount,
                totalStageCount: 5,
                stageFraction: null,
                userMessage: "Controlled progress update."));
    }

    [TestMethod]
    public void FileEvidence_RejectsDirectoryInFileName()
    {
        Assert.ThrowsExactly<ArgumentException>(() =>
            new ModelInspectionFileEvidence(
                fileName: @"Models\granite.gguf",
                canonicalPathSha256: CanonicalPathSha256,
                lengthBytes: 100,
                lastWriteTimeUtc: FixedUtcTime,
                modelSha256: ModelSha256,
                integrityPreserved: true));
    }

    [TestMethod]
    public void FileEvidence_RequiresPreservedIntegrity()
    {
        Assert.ThrowsExactly<ArgumentException>(() =>
            new ModelInspectionFileEvidence(
                fileName: ModelFileName,
                canonicalPathSha256: CanonicalPathSha256,
                lengthBytes: 100,
                lastWriteTimeUtc: FixedUtcTime,
                modelSha256: ModelSha256,
                integrityPreserved: false));
    }

    [TestMethod]
    public void ConfigurationEvidence_AllowsUnavailableNativeValues()
    {
        ModelInspectionConfigurationEvidence configuration =
            CreateConfigurationEvidence();

        Assert.IsNull(configuration.GgufVersion);
        Assert.IsNull(configuration.ParameterCount);
        Assert.IsNull(configuration.DeclaredContextLength);
        Assert.IsNull(configuration.LayerCount);
        Assert.IsNull(configuration.AttentionHeadCount);
        Assert.IsNull(configuration.KvHeadCount);
    }

    [TestMethod]
    public void TokenizerEvidence_DefensivelyCopiesSpecialTokenIds()
    {
        Dictionary<string, int> source = new(StringComparer.Ordinal)
        {
            ["bos"] = 1
        };

        ModelInspectionTokenizerEvidence tokenizer = new(
            tokenizerModel: "gpt2",
            vocabularyCount: 49_152,
            vocabularyType: "BPE",
            tokenizerSmokePassed: true,
            tokenizerSmokeTokenCount: 3,
            knownSpecialTokenIds: source);

        source["eos"] = 2;

        Assert.HasCount(1, tokenizer.KnownSpecialTokenIds);
        Assert.IsTrue(tokenizer.KnownSpecialTokenIds.ContainsKey("bos"));
    }

    [TestMethod]
    public void ChatTemplate_AbsentRequiresNoLengthOrDigest()
    {
        Assert.ThrowsExactly<ArgumentException>(() =>
            new ModelInspectionChatTemplateEvidence(
                present: false,
                lengthCharacters: 100,
                sha256: ModelSha256));
    }

    [TestMethod]
    public void RuntimeIdentity_PreservesCompleteApprovedClosure()
    {
        ModelInspectionRuntimeIdentity runtime = CreateRuntimeIdentity();

        Assert.IsTrue(string.Equals(
            "GraniteEdgeAI.ModelInspection.Worker",
            runtime.WorkerId,
            StringComparison.Ordinal));
        Assert.IsTrue(runtime.ProtocolVersion == 1);
        Assert.IsTrue(string.Equals(
            "llamasharp-0.27.0-cpu-win-x64-vocab-only-v1",
            runtime.RuntimeProfile,
            StringComparison.Ordinal));
        Assert.IsTrue(string.Equals(
            "llama.dll",
            runtime.NativeLibraryName,
            StringComparison.Ordinal));
        Assert.IsFalse(runtime.UsesCuda);
        Assert.IsFalse(runtime.UsesVulkan);
        Assert.IsTrue(runtime.GpuLayerCount == 0);
    }

    [TestMethod]
    public void Evidence_DefensivelyCopiesTechnicalObservations()
    {
        List<ModelInspectionObservation> source =
        [
            CreateObservation()
        ];

        ModelInspectionEvidence evidence = CreateEvidence(source);
        source.Add(CreateObservation());

        Assert.HasCount(1, evidence.Observations);
    }

    [TestMethod]
    public void Finding_EmptyDiagnosticCode_Throws()
    {
        Assert.ThrowsExactly<ArgumentException>(() =>
            new ModelInspectionFinding(
                code: string.Empty,
                severity: ModelInspectionFindingSeverity.Warning,
                title: "Template missing",
                explanation: "No embedded chat template was found.",
                recommendedAction: "Review the configured fallback template.",
                technicalDetail: "The runtime returned no embedded template."));
    }

    [TestMethod]
    public void OnlyReadyOutcomesCanContinueToHardwareFit()
    {
        Assert.IsTrue(ModelInspectionResult.CanContinue(
            ModelInspectionOutcome.Ready));
        Assert.IsTrue(ModelInspectionResult.CanContinue(
            ModelInspectionOutcome.ReadyWithWarnings));
        Assert.IsFalse(ModelInspectionResult.CanContinue(
            ModelInspectionOutcome.ConversionRequired));
        Assert.IsFalse(ModelInspectionResult.CanContinue(
            ModelInspectionOutcome.IncompletePackage));
        Assert.IsFalse(ModelInspectionResult.CanContinue(
            ModelInspectionOutcome.Unsupported));
        Assert.IsFalse(ModelInspectionResult.CanContinue(
            ModelInspectionOutcome.Invalid));
    }

    [TestMethod]
    public void ConversionRequired_WithoutVerifiedRoute_Throws()
    {
        Assert.ThrowsExactly<ArgumentException>(() =>
            CreateResult(
                ModelInspectionOutcome.ConversionRequired,
                verifiedConversionRouteId: null));
    }

    [TestMethod]
    public void Result_DefensivelyCopiesFindings()
    {
        List<ModelInspectionFinding> source =
        [
            CreateFinding(ModelInspectionFindingSeverity.Warning)
        ];

        ModelInspectionResult result = CreateResult(
            ModelInspectionOutcome.ReadyWithWarnings,
            findings: source);

        source.Add(CreateFinding(ModelInspectionFindingSeverity.Information));

        Assert.HasCount(1, result.Findings);
    }

    [TestMethod]
    public void CompletedExecution_RequiresClassifiedResult()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() =>
            ModelInspectionExecutionResult.Completed(null!));
    }

    [TestMethod]
    public void CompletedExecution_ContainsOnlyClassifiedResult()
    {
        ModelInspectionResult expected =
            CreateResult(ModelInspectionOutcome.Ready);

        ModelInspectionExecutionResult execution =
            ModelInspectionExecutionResult.Completed(expected);

        Assert.IsTrue(
            execution.Status == ModelInspectionExecutionStatus.Completed);
        Assert.AreSame(expected, execution.Result);
        Assert.IsNull(execution.Failure);
        Assert.IsNull(execution.CancellationWasCooperative);
    }

    [TestMethod]
    public void CancelledExecution_ContainsNoResultOrFailure()
    {
        ModelInspectionExecutionResult execution =
            ModelInspectionExecutionResult.Cancelled(cooperative: true);

        Assert.IsTrue(
            execution.Status == ModelInspectionExecutionStatus.Cancelled);
        Assert.IsNull(execution.Result);
        Assert.IsNull(execution.Failure);
        Assert.IsTrue(execution.CancellationWasCooperative is true);
    }

    [TestMethod]
    public void OperationalFailureExecution_RequiresFailure()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() =>
            ModelInspectionExecutionResult.OperationalFailure(null!));
    }

    [TestMethod]
    public void OperationalFailureExecution_ContainsNoModelResult()
    {
        ModelInspectionOperationalFailure expected = new(
            code: "MI-OP-WORKER-START",
            userMessage: "Model inspection could not be started.",
            technicalDetail: "The protected worker process did not start.");

        ModelInspectionExecutionResult execution =
            ModelInspectionExecutionResult.OperationalFailure(expected);

        Assert.IsTrue(
            execution.Status ==
            ModelInspectionExecutionStatus.OperationalFailure);
        Assert.IsNull(execution.Result);
        Assert.AreSame(expected, execution.Failure);
        Assert.IsNull(execution.CancellationWasCooperative);
    }

    [TestMethod]
    public void ApplicationContractGraph_ContainsNoTransportNativeOrUiTypes()
    {
        Type[] contractTypes = typeof(ModelInspectionRequest)
            .Assembly
            .GetTypes()
            .Where(type => string.Equals(
                type.Namespace,
                "GraniteEdgeAI.Features.ModelInspection.Contracts",
                StringComparison.Ordinal))
            .ToArray();

        Assert.IsNotEmpty(contractTypes);

        foreach (Type contractType in contractTypes)
        {
            AssertContractTypeIsApplicationOwned(contractType);

            foreach (PropertyInfo property in contractType.GetProperties(
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
            {
                AssertContractTypeIsApplicationOwned(property.PropertyType);
                StringAssert.DoesNotContain(
                    property.Name,
                    "NativeHandle",
                    StringComparison.OrdinalIgnoreCase);
                StringAssert.DoesNotContain(
                    property.Name,
                    "Pointer",
                    StringComparison.OrdinalIgnoreCase);
                StringAssert.DoesNotContain(
                    property.Name,
                    "FullTemplate",
                    StringComparison.OrdinalIgnoreCase);
            }
        }
    }

    private static void AssertContractTypeIsApplicationOwned(Type type)
    {
        string identity = type.AssemblyQualifiedName ?? type.FullName ?? type.Name;

        StringAssert.DoesNotContain(
            identity,
            "GraniteEdgeAI.ModelInspection.Contracts",
            StringComparison.Ordinal);
        StringAssert.DoesNotContain(identity, "LLamaSharp", StringComparison.Ordinal);
        StringAssert.DoesNotContain(identity, "Microsoft.UI.Xaml", StringComparison.Ordinal);
        StringAssert.DoesNotContain(identity, "SafeHandle", StringComparison.Ordinal);
        StringAssert.DoesNotContain(identity, "IntPtr", StringComparison.Ordinal);
    }

    private static ExpectedModelFileIdentity CreateFileIdentity()
    {
        return new ExpectedModelFileIdentity(
            lengthBytes: 100,
            lastWriteTimeUtc: FixedUtcTime);
    }

    private static ValidatedQuickScanSnapshot CreateQuickScan()
    {
        return ValidatedQuickScanSnapshot.CreateGguf(
            modelName: "Granite 4.1 3B",
            architecture: "granite",
            parameterSizeLabel: "3B",
            quantisation: "Q4_K_M",
            fileSizeBytes: 100,
            declaredContextLength: 131_072,
            ggufVersion: 3);
    }

    private static ModelInspectionFileEvidence CreateFileEvidence()
    {
        return new ModelInspectionFileEvidence(
            fileName: ModelFileName,
            canonicalPathSha256: CanonicalPathSha256,
            lengthBytes: 100,
            lastWriteTimeUtc: FixedUtcTime,
            modelSha256: ModelSha256,
            integrityPreserved: true);
    }

    private static ModelInspectionConfigurationEvidence
        CreateConfigurationEvidence()
    {
        return new ModelInspectionConfigurationEvidence(
            format: "GGUF",
            ggufVersion: null,
            modelName: "Granite 4.1 3B",
            architecture: "granite",
            fileType: null,
            quantisationVersion: null,
            declaredContextLength: null,
            embeddingSize: null,
            layerCount: null,
            attentionHeadCount: null,
            kvHeadCount: null,
            parameterCount: null);
    }

    private static ModelInspectionTokenizerEvidence CreateTokenizerEvidence()
    {
        return new ModelInspectionTokenizerEvidence(
            tokenizerModel: null,
            vocabularyCount: null,
            vocabularyType: null,
            tokenizerSmokePassed: null,
            tokenizerSmokeTokenCount: null,
            knownSpecialTokenIds: new Dictionary<string, int>(
                StringComparer.Ordinal));
    }

    private static ModelInspectionChatTemplateEvidence
        CreateChatTemplateEvidence()
    {
        return new ModelInspectionChatTemplateEvidence(
            present: null,
            lengthCharacters: null,
            sha256: null);
    }

    private static ModelInspectionRuntimeIdentity CreateRuntimeIdentity()
    {
        return new ModelInspectionRuntimeIdentity(
            workerId: "GraniteEdgeAI.ModelInspection.Worker",
            workerVersion: "1.0.0",
            protocolVersion: 1,
            runtimeProfile:
                "llamasharp-0.27.0-cpu-win-x64-vocab-only-v1",
            llamaSharpVersion: "0.27.0",
            backendPackageVersion: "0.27.0",
            mappedLlamaCppCommit: LlamaCppCommit,
            nativeLibraryName: "llama.dll",
            processArchitecture: "X64",
            inspectionMode: "VocabOnly",
            usesCuda: false,
            usesVulkan: false,
            gpuLayerCount: 0);
    }

    private static ModelInspectionObservation CreateObservation()
    {
        return new ModelInspectionObservation(
            code: "MI-OBS-CHAT-TEMPLATE-MISSING",
            domain: "ChatTemplate",
            impact: "NonBlocking",
            technicalDetail: "No embedded chat template was reported.");
    }

    private static ModelInspectionFinding CreateFinding(
        ModelInspectionFindingSeverity severity)
    {
        return new ModelInspectionFinding(
            code: "MI-WARN-CHAT-TEMPLATE-MISSING",
            severity: severity,
            title: "Template missing",
            explanation: "No embedded chat template was found.",
            recommendedAction: "Review the configured fallback template.",
            technicalDetail: "The runtime returned no embedded template.");
    }

    private static ModelInspectionEvidence CreateEvidence(
        IEnumerable<ModelInspectionObservation>? observations = null)
    {
        return new ModelInspectionEvidence(
            file: CreateFileEvidence(),
            configuration: CreateConfigurationEvidence(),
            tokenizer: CreateTokenizerEvidence(),
            chatTemplate: CreateChatTemplateEvidence(),
            runtime: CreateRuntimeIdentity(),
            observations: observations ?? Array.Empty<ModelInspectionObservation>());
    }

    private static ModelInspectionResult CreateResult(
        ModelInspectionOutcome outcome,
        string? verifiedConversionRouteId = null,
        IEnumerable<ModelInspectionFinding>? findings = null)
    {
        return new ModelInspectionResult(
            outcome: outcome,
            evidence: CreateEvidence(),
            findings: findings ?? Array.Empty<ModelInspectionFinding>(),
            summary: "Inspection completed.",
            recommendedAction: "Review the inspection result.",
            verifiedConversionRouteId: verifiedConversionRouteId,
            startedAtUtc: FixedUtcTime,
            completedAtUtc: FixedUtcTime.AddSeconds(2));
    }
}
