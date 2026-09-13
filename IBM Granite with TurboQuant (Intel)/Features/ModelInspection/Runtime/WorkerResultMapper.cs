using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using GraniteEdgeAI.Features.ModelInspection.Contracts;
using GraniteEdgeAI.ModelInspection.Contracts;
using GraniteEdgeAI.ModelInspection.WorkerClient;

namespace GraniteEdgeAI.Features.ModelInspection.Runtime;

/// <summary>
/// Converts the validated worker boundary into application-owned progress and
/// evidence while rejecting contradictions and unapproved runtime identities.
/// </summary>
internal sealed class WorkerResultMapper
{
    private const string CompletionFailureUserMessage =
        "Model inspection could not be completed.";
    private const string ExpectedManagedVersion = "0.27.0";
    private const string ExpectedBackendVersion = "0.27.0";
    private const string ExpectedLlamaCppCommit =
        "3f7c29d318e317b63f54c558bc69803963d7d88c";

    internal ModelInspectionProgress MapProgress(
        WorkerProgressMessage message)
    {
        ArgumentNullException.ThrowIfNull(message);
        message.Validate();

        ModelInspectionStage stage = message.Stage switch
        {
            WorkerStage.CheckModelPackage =>
                ModelInspectionStage.CheckModelPackage,
            WorkerStage.ReadModelConfiguration =>
                ModelInspectionStage.ReadModelConfiguration,
            WorkerStage.ValidateTokenizerAndChatSetup =>
                ModelInspectionStage.ValidateTokenizerAndChatSetup,
            WorkerStage.ValidateModelStructure =>
                ModelInspectionStage.ValidateModelStructure,
            WorkerStage.ConfirmCoreRuntimeCompatibility =>
                ModelInspectionStage.ConfirmCoreRuntimeCompatibility,
            _ => throw new InvalidDataException(
                "The worker reported an unknown inspection stage.")
        };
        ModelInspectionStageStatus stageStatus = message.StageStatus switch
        {
            WorkerStageStatus.Active => ModelInspectionStageStatus.Active,
            WorkerStageStatus.Completed =>
                ModelInspectionStageStatus.Completed,
            WorkerStageStatus.Warning => ModelInspectionStageStatus.Warning,
            WorkerStageStatus.Failed => ModelInspectionStageStatus.Failed,
            WorkerStageStatus.Cancelled =>
                ModelInspectionStageStatus.Cancelled,
            _ => throw new InvalidDataException(
                "The worker reported an unknown inspection stage status.")
        };

        return new ModelInspectionProgress(
            stage,
            stageStatus,
            message.CompletedStageCount,
            message.TotalStageCount,
            message.StageFraction,
            GetUserMessage(stage, stageStatus));
    }

    internal ModelInspectionProbeResult MapResult(
        ModelInspectionRequest request,
        Guid expectedRequestId,
        WorkerClientResult result)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(result);
        if (expectedRequestId == Guid.Empty)
        {
            throw new ArgumentException(
                "Expected request identity must not be empty.",
                nameof(expectedRequestId));
        }

        if (result.Failure is not null)
        {
            try
            {
                result.Validate();
            }
            catch (Exception error) when (IsMappingFailure(error))
            {
                return ClientFailure();
            }

            return ClientFailure();
        }

        WorkerCompletedMessage? terminal = result.TerminalMessage;
        if (terminal is null)
        {
            return ClientFailure();
        }

        try
        {
            result.Validate();
            if (terminal.RequestId != expectedRequestId)
            {
                return InvalidEvidence();
            }

            return terminal.CompletionStatus switch
            {
                WorkerCompletionStatus.Completed =>
                    ModelInspectionProbeResult.Completed(
                        MapEvidence(request, terminal.Evidence!)),
                WorkerCompletionStatus.Cancelled =>
                    ModelInspectionProbeResult.Cancelled(),
                WorkerCompletionStatus.OperationalFailure => WorkerFailure(),
                _ => InvalidEvidence()
            };
        }
        catch (Exception error) when (IsMappingFailure(error))
        {
            return InvalidEvidence();
        }
    }

    internal ModelInspectionProbeResult MapClientFailure()
    {
        return ClientFailure();
    }

    private static ModelInspectionEvidence MapEvidence(
        ModelInspectionRequest request,
        WorkerInspectionEvidence source)
    {
        source.Validate();
        ValidateFileEnvelope(request, source.ModelFile);
        ValidateConfigurationEnvelope(request, source.Configuration);
        ValidateTokenizerEnvelope(source.Tokenizer);
        ValidateRuntimeEnvelope(source.Runtime);

        if (source.Observations.Count != 0)
        {
            throw new InvalidDataException(
                "The worker returned an observation without an application taxonomy.");
        }

        WorkerModelFileEvidence file = source.ModelFile;
        WorkerModelConfigurationEvidence configuration =
            source.Configuration;
        WorkerTokenizerEvidence tokenizer = source.Tokenizer;
        WorkerChatTemplateEvidence chatTemplate = source.ChatTemplate;
        WorkerRuntimeIdentity runtime = source.Runtime;

        return new ModelInspectionEvidence(
            new ModelInspectionFileEvidence(
                file.FileName,
                file.CanonicalPathSha256,
                file.LengthBefore,
                file.LastWriteTimeBeforeUtc,
                file.Sha256Before,
                file.IntegrityPreserved),
            new ModelInspectionConfigurationEvidence(
                format: "GGUF",
                ggufVersion: request.QuickScan.GgufVersion,
                modelName: configuration.ModelName,
                architecture: configuration.Architecture,
                fileType: configuration.FileType,
                quantisationVersion: configuration.QuantisationVersion,
                declaredContextLength:
                    configuration.DeclaredContextLength,
                embeddingSize: configuration.EmbeddingSize,
                layerCount: configuration.LayerCount,
                attentionHeadCount: configuration.AttentionHeadCount,
                kvHeadCount: configuration.KvHeadCount,
                parameterCount: configuration.ParameterCount),
            new ModelInspectionTokenizerEvidence(
                tokenizerModel: configuration.TokenizerModel,
                vocabularyCount: tokenizer.VocabularyCount,
                vocabularyType: tokenizer.VocabularyType,
                tokenizerSmokePassed: tokenizer.TokenizerSmokePassed,
                tokenizerSmokeTokenCount:
                    tokenizer.TokenizerSmokeTokenCount,
                knownSpecialTokenIds: tokenizer.KnownSpecialTokenIds),
            new ModelInspectionChatTemplateEvidence(
                present: chatTemplate.Present,
                lengthCharacters: chatTemplate.LengthCharacters,
                sha256: chatTemplate.Sha256),
            new ModelInspectionRuntimeIdentity(
                workerId: WorkerProtocol.WorkerId,
                workerVersion: runtime.WorkerVersion,
                protocolVersion: runtime.ProtocolVersion,
                runtimeProfile: runtime.RuntimeProfile,
                llamaSharpVersion: runtime.LLamaSharpVersion,
                backendPackageVersion: runtime.BackendPackageVersion,
                mappedLlamaCppCommit: runtime.MappedLlamaCppCommit,
                nativeLibraryName: "llama.dll",
                processArchitecture: runtime.ProcessArchitecture,
                inspectionMode: runtime.InspectionMode,
                usesCuda: runtime.UsesCuda,
                usesVulkan: runtime.UsesVulkan,
                gpuLayerCount: runtime.GpuLayerCount),
            Array.Empty<ModelInspectionObservation>());
    }

    private static void ValidateFileEnvelope(
        ModelInspectionRequest request,
        WorkerModelFileEvidence file)
    {
        ExpectedModelFileIdentity expected = request.ExpectedFileIdentity;
        bool matches =
            file.IntegrityPreserved &&
            string.Equals(
                file.FileName,
                request.FileName,
                StringComparison.OrdinalIgnoreCase) &&
            file.LengthBefore == expected.LengthBytes &&
            file.LengthAfter == expected.LengthBytes &&
            file.LengthBefore == file.LengthAfter &&
            file.LastWriteTimeBeforeUtc == expected.LastWriteTimeUtc &&
            file.LastWriteTimeAfterUtc == expected.LastWriteTimeUtc &&
            file.LastWriteTimeBeforeUtc == file.LastWriteTimeAfterUtc &&
            string.Equals(
                file.CanonicalPathSha256,
                ComputeCanonicalPathSha256(request.ModelPath),
                StringComparison.OrdinalIgnoreCase) &&
            string.Equals(
                file.Sha256Before,
                file.Sha256After,
                StringComparison.OrdinalIgnoreCase);

        if (!matches)
        {
            throw new InvalidDataException(
                "The worker file evidence does not match the selected model identity.");
        }
    }

    private static string ComputeCanonicalPathSha256(string path)
    {
        string fullPath = Path.GetFullPath(path);
        string canonicalPath = OperatingSystem.IsWindows()
            ? fullPath.ToUpperInvariant()
            : fullPath;
        byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(canonicalPath));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private static void ValidateConfigurationEnvelope(
        ModelInspectionRequest request,
        WorkerModelConfigurationEvidence configuration)
    {
        bool architectureMatches = string.Equals(
            configuration.Architecture,
            request.QuickScan.Architecture,
            StringComparison.OrdinalIgnoreCase);
        bool modelNameMatches =
            configuration.ModelName is null ||
            string.Equals(
                configuration.ModelName,
                request.QuickScan.ModelName,
                StringComparison.Ordinal);
        bool contextMatches =
            !configuration.DeclaredContextLength.HasValue ||
            !request.QuickScan.DeclaredContextLength.HasValue ||
            configuration.DeclaredContextLength ==
                request.QuickScan.DeclaredContextLength;

        if (!architectureMatches || !modelNameMatches || !contextMatches)
        {
            throw new InvalidDataException(
                "The native configuration evidence contradicts the validated quick scan.");
        }
    }

    private static void ValidateRuntimeEnvelope(
        WorkerRuntimeIdentity runtime)
    {
        bool approved =
            runtime.ProtocolVersion == WorkerProtocol.Version &&
            string.Equals(
                runtime.RuntimeProfile,
                WorkerProtocol.RuntimeProfile,
                StringComparison.Ordinal) &&
            string.Equals(
                runtime.LLamaSharpVersion,
                ExpectedManagedVersion,
                StringComparison.Ordinal) &&
            string.Equals(
                runtime.BackendPackageVersion,
                ExpectedBackendVersion,
                StringComparison.Ordinal) &&
            string.Equals(
                runtime.MappedLlamaCppCommit,
                ExpectedLlamaCppCommit,
                StringComparison.OrdinalIgnoreCase) &&
            string.Equals(
                runtime.NativeLibraryName,
                "LLama",
                StringComparison.Ordinal) &&
            string.Equals(
                runtime.ProcessArchitecture,
                "X64",
                StringComparison.Ordinal) &&
            string.Equals(
                runtime.InspectionMode,
                "VocabOnly",
                StringComparison.Ordinal) &&
            !runtime.UsesCuda &&
            !runtime.UsesVulkan &&
            runtime.GpuLayerCount == 0;

        if (!approved)
        {
            throw new InvalidDataException(
                "The worker runtime evidence is outside the approved CPU-only profile.");
        }
    }

    private static void ValidateTokenizerEnvelope(
        WorkerTokenizerEvidence tokenizer)
    {
        if (tokenizer.TokenizerSmokePassed is not true ||
            tokenizer.TokenizerSmokeTokenCount is not > 0)
        {
            throw new InvalidDataException(
                "The worker did not return a successful tokenizer smoke check.");
        }
    }

    private static ModelInspectionProbeResult ClientFailure()
    {
        return ModelInspectionProbeResult.OperationalFailure(
            new ModelInspectionOperationalFailure(
                "MI-OP-WORKER-CLIENT",
                CompletionFailureUserMessage,
                "The protected worker client did not return a trusted terminal result."));
    }

    private static ModelInspectionProbeResult WorkerFailure()
    {
        return ModelInspectionProbeResult.OperationalFailure(
            new ModelInspectionOperationalFailure(
                "MI-OP-WORKER",
                CompletionFailureUserMessage,
                "The protected Model Inspection worker reported an operational failure."));
    }

    private static ModelInspectionProbeResult InvalidEvidence()
    {
        return ModelInspectionProbeResult.OperationalFailure(
            new ModelInspectionOperationalFailure(
                "MI-OP-EVIDENCE-INVALID",
                CompletionFailureUserMessage,
                "The protected Model Inspection worker returned inconsistent evidence."));
    }

    private static bool IsMappingFailure(Exception error)
    {
        return error is ArgumentException or
            InvalidOperationException or
            InvalidDataException or
            OverflowException or
            WorkerProtocolException;
    }

    private static string GetUserMessage(
        ModelInspectionStage stage,
        ModelInspectionStageStatus status)
    {
        string stageName = stage switch
        {
            ModelInspectionStage.CheckModelPackage => "model package",
            ModelInspectionStage.ReadModelConfiguration =>
                "model configuration",
            ModelInspectionStage.ValidateTokenizerAndChatSetup =>
                "tokenizer and chat setup",
            ModelInspectionStage.ValidateModelStructure => "model structure",
            ModelInspectionStage.ConfirmCoreRuntimeCompatibility =>
                "core runtime compatibility",
            _ => "model inspection"
        };

        return status switch
        {
            ModelInspectionStageStatus.Active => $"Checking {stageName}.",
            ModelInspectionStageStatus.Completed =>
                $"Completed {stageName} check.",
            ModelInspectionStageStatus.Warning =>
                $"Completed {stageName} check with a warning.",
            ModelInspectionStageStatus.Failed =>
                $"Could not complete {stageName} check.",
            ModelInspectionStageStatus.Cancelled =>
                $"Cancelled {stageName} check.",
            _ => "Updating model inspection progress."
        };
    }
}
