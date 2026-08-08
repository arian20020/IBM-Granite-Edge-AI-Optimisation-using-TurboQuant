using System.Globalization;
using GraniteEdgeAI.ModelInspection.Contracts;
using GraniteEdgeAI.ModelInspection.LlamaSharp;
using GraniteEdgeAI.ModelInspection.LlamaSharp.ModelProbe;

namespace GraniteEdgeAI.ModelInspection.Worker;

internal static class LlamaSharpInspectionEvidenceMapper
{
    internal static WorkerInspectionEvidence Map(
        VocabOnlyModelProbeResult result,
        WorkerStartInspectionCommand command,
        string workerVersion)
    {
        ValidateRuntimeEnvelope(result, command);

        ModelFileSnapshot before = result.BeforeSnapshot!;
        ModelFileSnapshot after = result.AfterSnapshot!;
        VocabOnlyRuntimeModelEvidence model = result.ModelEvidence!;
        VocabularyEvidence vocabulary = model.Vocabulary;
        TokenizerSmokeEvidence tokenizerSmoke = model.TokenizerSmoke;
        ChatTemplateEvidence chatTemplate = model.ChatTemplate;
        SelectedNativeBackend backend = result.SelectedBackend!;

        var evidence = new WorkerInspectionEvidence
        {
            Runtime = new WorkerRuntimeIdentity
            {
                WorkerVersion = workerVersion,
                ProtocolVersion = WorkerProtocol.Version,
                RuntimeProfile = WorkerProtocol.RuntimeProfile,
                LLamaSharpVersion = result.ManagedPackageVersion,
                BackendPackageVersion = result.BackendPackageVersion,
                MappedLlamaCppCommit = result.ExpectedLlamaCppCommit,
                NativeLibraryName = backend.NativeLibraryName!,
                ProcessArchitecture = result.ProcessArchitecture,
                InspectionMode = result.ProbeMode,
                UsesCuda = backend.UsesCuda!.Value,
                UsesVulkan = backend.UsesVulkan!.Value,
                GpuLayerCount = result.GpuLayerCount
            },
            ModelFile = new WorkerModelFileEvidence
            {
                FileName = before.FileName,
                CanonicalPathSha256 = before.CanonicalPathSha256,
                LengthBefore = before.LengthBytes,
                LengthAfter = after.LengthBytes,
                LastWriteTimeBeforeUtc = before.LastWriteTimeUtc,
                LastWriteTimeAfterUtc = after.LastWriteTimeUtc,
                Sha256Before = before.Sha256,
                Sha256After = after.Sha256,
                IntegrityPreserved = result.Integrity!.IsPreserved
            },
            Configuration = new WorkerModelConfigurationEvidence
            {
                Architecture = model.Architecture,
                ModelName = model.ModelName,
                FileType = TryParseNonNegativeInt32(model.FileType),
                QuantisationVersion =
                    TryParseNonNegativeInt32(model.QuantizationVersion),
                TokenizerModel = model.TokenizerModel,
                DeclaredContextLength = ToNullableUInt64(model.ContextSize),
                EmbeddingSize = ToNullableUInt64(model.EmbeddingSize),
                LayerCount = ToNonNegative(model.LayerCount),
                AttentionHeadCount = ToNonNegative(model.HeadCount),
                KvHeadCount = ToNonNegative(model.KvHeadCount),
                ParameterCount = model.ParameterCount
            },
            Tokenizer = new WorkerTokenizerEvidence
            {
                VocabularyCount = vocabulary.Count,
                VocabularyType = vocabulary.Type,
                TokenizerSmokePassed = tokenizerSmoke.Succeeded,
                TokenizerSmokeTokenCount = tokenizerSmoke.TokenCount,
                KnownSpecialTokenIds = CreateSpecialTokenIds(vocabulary)
            },
            ChatTemplate = new WorkerChatTemplateEvidence
            {
                Present = chatTemplate.Present,
                LengthCharacters = chatTemplate.LengthCharacters,
                Sha256 = chatTemplate.Sha256
            },
            Observations = []
        };

        evidence.Validate();
        return evidence;
    }

    private static void ValidateRuntimeEnvelope(
        VocabOnlyModelProbeResult result,
        WorkerStartInspectionCommand command)
    {
        ArgumentNullException.ThrowIfNull(result);
        ArgumentNullException.ThrowIfNull(command);

        bool pinsMatch =
            string.Equals(result.SchemaVersion, "1.1", StringComparison.Ordinal) &&
            string.Equals(result.ProbeMode, "VocabOnly", StringComparison.Ordinal) &&
            result.CompletionStatus == VocabOnlyProbeCompletionStatus.Succeeded &&
            result.VocabOnlyRequested &&
            result.GpuLayerCount == 0 &&
            result.UseMemoryMap &&
            !result.UseMemoryLock &&
            string.Equals(
                result.ManagedPackageName,
                PinnedApplicationRuntime.ManagedPackageName,
                StringComparison.Ordinal) &&
            string.Equals(
                result.ManagedPackageVersion,
                PinnedApplicationRuntime.ManagedPackageVersion,
                StringComparison.Ordinal) &&
            string.Equals(
                result.BackendPackageName,
                PinnedApplicationRuntime.BackendPackageName,
                StringComparison.Ordinal) &&
            string.Equals(
                result.BackendPackageVersion,
                PinnedApplicationRuntime.BackendPackageVersion,
                StringComparison.Ordinal) &&
            string.Equals(
                result.LlamaSharpSourceTag,
                PinnedApplicationRuntime.LlamaSharpSourceTag,
                StringComparison.Ordinal) &&
            string.Equals(
                result.LlamaSharpReleaseCommit,
                PinnedApplicationRuntime.LlamaSharpReleaseCommit,
                StringComparison.Ordinal) &&
            string.Equals(
                result.ExpectedLlamaCppCommit,
                PinnedApplicationRuntime.ExpectedLlamaCppCommit,
                StringComparison.Ordinal) &&
            string.Equals(
                result.IntendedProductionRuntimeIdentifier,
                PinnedApplicationRuntime.IntendedProductionRuntimeIdentifier,
                StringComparison.Ordinal) &&
            string.Equals(result.ProcessArchitecture, "X64", StringComparison.Ordinal) &&
            result.SelectedBackend is
            {
                NativeLibraryName: "LLama",
                UsesCuda: false,
                UsesVulkan: false
            } &&
            result.NativeHandleClosedAfterDispose is true &&
            result.FailureCode is null &&
            result.FailureType is null &&
            result.FailureMessage is null;

        if (!pinsMatch)
        {
            throw new InvalidDataException(
                "The runtime identity or disposal evidence is unsafe.");
        }

        if (result.BeforeSnapshot is null ||
            result.AfterSnapshot is null ||
            result.Integrity is null ||
            !result.Integrity.IsPreserved ||
            result.ModelEvidence is null ||
            result.ModelEvidence.Vocabulary is null ||
            result.ModelEvidence.TokenizerSmoke is null ||
            result.ModelEvidence.ChatTemplate is null)
        {
            throw new InvalidDataException(
                "The runtime evidence is incomplete.");
        }

        ModelFileSnapshot before = result.BeforeSnapshot;
        ModelFileSnapshot after = result.AfterSnapshot;
        string expectedFileName = Path.GetFileName(command.ModelPath);
        bool snapshotsMatch =
            !string.IsNullOrWhiteSpace(before.FileName) &&
            string.Equals(before.FileName, expectedFileName, StringComparison.Ordinal) &&
            string.Equals(before.FileName, after.FileName, StringComparison.Ordinal) &&
            !string.IsNullOrWhiteSpace(before.CanonicalPathSha256) &&
            string.Equals(
                before.CanonicalPathSha256,
                after.CanonicalPathSha256,
                StringComparison.Ordinal) &&
            before.LengthBytes == command.ExpectedFileIdentity.LengthBytes &&
            after.LengthBytes == before.LengthBytes &&
            before.LastWriteTimeUtc ==
                command.ExpectedFileIdentity.LastWriteTimeUtc &&
            after.LastWriteTimeUtc == before.LastWriteTimeUtc &&
            !string.IsNullOrWhiteSpace(before.Sha256) &&
            string.Equals(
                before.Sha256,
                after.Sha256,
                StringComparison.OrdinalIgnoreCase);

        if (!snapshotsMatch)
        {
            throw new InvalidDataException(
                "The runtime file evidence does not match the request.");
        }
    }

    private static int? TryParseNonNegativeInt32(string? value) =>
        int.TryParse(
            value,
            NumberStyles.None,
            CultureInfo.InvariantCulture,
            out int parsed) &&
        parsed >= 0
            ? parsed
            : null;

    private static int? ToNonNegative(int? value) =>
        value is >= 0 ? value : null;

    private static ulong? ToNullableUInt64(int? value) =>
        value is >= 0 ? (ulong)value.Value : null;

    private static Dictionary<string, int> CreateSpecialTokenIds(
        VocabularyEvidence vocabulary)
    {
        var values = new Dictionary<string, int>(StringComparer.Ordinal);
        AddSpecialToken(values, "bos", vocabulary.Bos);
        AddSpecialToken(values, "eos", vocabulary.Eos);
        AddSpecialToken(values, "newline", vocabulary.Newline);
        AddSpecialToken(values, "pad", vocabulary.Pad);
        AddSpecialToken(values, "mask", vocabulary.Mask);
        AddSpecialToken(values, "separator", vocabulary.Separator);
        return values;
    }

    private static void AddSpecialToken(
        Dictionary<string, int> values,
        string name,
        SpecialTokenEvidence? token)
    {
        if (token is not null &&
            int.TryParse(
                token.TokenId,
                NumberStyles.None,
                CultureInfo.InvariantCulture,
                out int parsed) &&
            parsed >= 0)
        {
            values.Add(name, parsed);
        }
    }
}
