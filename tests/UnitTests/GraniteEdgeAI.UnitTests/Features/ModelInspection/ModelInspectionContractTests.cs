using GraniteEdgeAI.Features.ModelInspection.Contracts;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Reflection;

namespace GraniteEdgeAI.UnitTests;

/// <summary>
/// Defines the application-owned Model Inspection request, progress, evidence,
/// result, finding, and execution-state contracts.
/// </summary>
[TestClass]
public sealed class ModelInspectionContractTests
{
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

        Assert.AreEqual("GGUF", snapshot.Format);
        Assert.AreEqual("Granite 4.1 3B", snapshot.ModelName);
        Assert.AreEqual("granite", snapshot.Architecture);
        Assert.AreEqual(100L, snapshot.FileSizeBytes);
        Assert.AreEqual(3U, snapshot.GgufVersion);
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
                "granite.gguf",
                CreateFileIdentity(),
                CreateQuickScan()));
    }

    [TestMethod]
    public void Request_RejectsMismatchedFileName()
    {
        Assert.ThrowsExactly<ArgumentException>(() =>
            new ModelInspectionRequest(
                @"C:\Models\granite.gguf",
                "different.gguf",
                CreateFileIdentity(),
                CreateQuickScan()));
    }

    [TestMethod]
    public void Request_RejectsFileNameContainingDirectory()
    {
        Assert.ThrowsExactly<ArgumentException>(() =>
            new ModelInspectionRequest(
                @"C:\Models\granite.gguf",
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
                @"C:\Models\granite.gguf",
                "granite.gguf",
                identity,
                CreateQuickScan()));
    }

    [TestMethod]
    public void Progress_FractionOutsideRange_Throws()
    {
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() =>
            new ModelInspectionProgress(
                ModelInspectionStage.ReadModelConfiguration,
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
            completedStageCount: 1,
            totalStageCount: 5,
            stageFraction: null,
            userMessage: "Reading model configuration.");

        Assert.IsNull(progress.StageFraction);
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

        Assert.AreEqual(ModelInspectionExecutionStatus.Completed, execution.Status);
        Assert.AreSame(expected, execution.Result);
        Assert.IsNull(execution.OperationalFailure);
        Assert.IsNull(execution.CancellationWasCooperative);
    }

    [TestMethod]
    public void CancelledExecution_ContainsNoResultOrFailure()
    {
        ModelInspectionExecutionResult execution =
            ModelInspectionExecutionResult.Cancelled(cooperative: true);

        Assert.AreEqual(ModelInspectionExecutionStatus.Cancelled, execution.Status);
        Assert.IsNull(execution.Result);
        Assert.IsNull(execution.OperationalFailure);
        Assert.AreEqual(true, execution.CancellationWasCooperative);
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

        Assert.AreEqual(
            ModelInspectionExecutionStatus.OperationalFailure,
            execution.Status);
        Assert.IsNull(execution.Result);
        Assert.AreSame(expected, execution.OperationalFailure);
        Assert.IsNull(execution.CancellationWasCooperative);
    }

    [TestMethod]
    public void Evidence_AllowsUnavailableNativeValues()
    {
        ModelInspectionEvidence evidence = CreateEvidence();

        Assert.IsNull(evidence.ParameterCount);
        Assert.IsNull(evidence.DeclaredContextLength);
        Assert.IsNull(evidence.LayerCount);
        Assert.IsNull(evidence.AttentionHeadCount);
        Assert.IsNull(evidence.KvHeadCount);
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

    private static ModelInspectionEvidence CreateEvidence()
    {
        return new ModelInspectionEvidence(
            modelName: "Granite 4.1 3B",
            architecture: "granite",
            fileType: null,
            quantisationVersion: null,
            tokenizerModel: null,
            declaredContextLength: null,
            embeddingSize: null,
            layerCount: null,
            attentionHeadCount: null,
            kvHeadCount: null,
            parameterCount: null,
            vocabularyCount: null,
            tokenizerSmokePassed: null,
            chatTemplatePresent: null,
            modelIntegrityPreserved: true,
            modelSha256: new string('a', 64),
            runtimeIdentity: new ModelInspectionRuntimeIdentity(
                workerVersion: "1.0.0",
                llamaSharpVersion: "0.27.0",
                backendPackageVersion: "0.27.0",
                mappedLlamaCppCommit:
                    "3f7c29d318e317b63f54c558bc69803963d7d88c",
                processArchitecture: "X64",
                inspectionMode: "VocabOnly"));
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
