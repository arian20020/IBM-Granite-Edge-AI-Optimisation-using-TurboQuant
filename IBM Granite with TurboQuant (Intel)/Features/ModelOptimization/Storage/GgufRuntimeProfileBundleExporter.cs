using System;
using System.Buffers;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using GraniteEdgeAI.Features.ModelInspection.SourceCustody;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Optimization;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Optimization.Execution;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Domain;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Routes.Gguf;

namespace GraniteEdgeAI.Features.ModelOptimization.Storage;

/// <summary>
/// Copies an unchanged GGUF together with the exact runtime profile that an
/// issued runtime-only plan declared. This is not runtime validation and does
/// not turn a runtime-only result into a converted model artifact.
/// </summary>
internal sealed class GgufRuntimeProfileBundleExporter(
    OptimizationExecutionPlan plan,
    ModelSourceCustodyRegistry sourceCustody)
{
    private const int BufferSize = 128 * 1024;
    private const int ExecutorContractVersion = 3;
    private const string ModelName = "model.gguf";
    private const string ProfileName = "runtime-profile.json";
    private const string ManifestName = "bundle-manifest.json";
    private static readonly string[] MemberNames =
        [ManifestName, ModelName, ProfileName];

    private readonly OptimizationExecutionPlan _plan = plan
        ?? throw new ArgumentNullException(nameof(plan));
    private readonly ModelSourceCustodyRegistry _sourceCustody = sourceCustody
        ?? throw new ArgumentNullException(nameof(sourceCustody));

    internal async Task<GgufRuntimeProfileBundleExportResult> ExportAsync(
        OptimizationExecutionResult result,
        string destinationDirectory,
        ulong maximumBytes,
        IProgress<GgufRuntimeProfileBundleExportStage> progress,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(result);
        ArgumentNullException.ThrowIfNull(progress);
        cancellationToken.ThrowIfCancellationRequested();

        if (!MatchesDeclaredRuntimeProfile(result, out GgufExecutionPayload? payload))
        {
            return Rejected(
                GgufRuntimeProfileBundleExportDisposition.ResultRejected);
        }
        if (maximumBytes == 0 || _plan.Binding.ModelLengthBytes > maximumBytes)
        {
            return Rejected(GgufRuntimeProfileBundleExportDisposition.Oversized);
        }
        if (!TryResolveDestination(
                destinationDirectory,
                out string? destination,
                out string? parent,
                out GgufRuntimeProfileBundleExportDisposition destinationFailure))
        {
            return Rejected(destinationFailure);
        }

        ModelSourceCustodyKey sourceKey;
        try
        {
            sourceKey = new ModelSourceCustodyKey(
                Guid.ParseExact(_plan.Binding.ModelInspectionHandoffId, "N"),
                _plan.Binding.ModelSha256,
                checked((long)_plan.Binding.ModelLengthBytes),
                OptimizationRoute.Gguf);
        }
        catch (Exception exception) when (exception is
            ArgumentException or FormatException or OverflowException)
        {
            return Rejected(
                GgufRuntimeProfileBundleExportDisposition.ResultRejected);
        }
        if (!_sourceCustody.TryAcquire(sourceKey, out ModelSourceLease? sourceLease))
        {
            return Rejected(
                GgufRuntimeProfileBundleExportDisposition.SourceUnavailable);
        }

        using (sourceLease!)
        {
            string temporary = Path.Combine(
                parent!, $".granite-profile-{Guid.NewGuid():N}");
            bool published = false;
            GgufRuntimeProfileBundleExportResult outcome;
            OperationCanceledException? cancellation = null;
            try
            {
                Directory.CreateDirectory(temporary);
                StoragePathGuard.RequireChild(parent!, temporary, mustExist: true);
                outcome = await WriteBundleAsync(
                    sourceLease!.SourcePath,
                    payload!,
                    result,
                    temporary,
                    destination!,
                    maximumBytes,
                    progress,
                    cancellationToken).ConfigureAwait(false);
                published = outcome.Disposition
                    == GgufRuntimeProfileBundleExportDisposition.Succeeded;
            }
            catch (OperationCanceledException exception)
                when (exception.CancellationToken == cancellationToken
                    || cancellationToken.IsCancellationRequested)
            {
                cancellation = exception;
                outcome = Rejected(
                    GgufRuntimeProfileBundleExportDisposition.Failed);
            }
            catch (Exception exception) when (exception is
                IOException or UnauthorizedAccessException
                    or InvalidDataException or InvalidOperationException
                    or ArgumentException or OverflowException)
            {
                outcome = Rejected(
                    GgufRuntimeProfileBundleExportDisposition.Failed);
            }

            bool cleanupSucceeded = published
                || CleanupTemporary(parent!, temporary);
            if (cancellation is not null)
            {
                if (!cleanupSucceeded)
                {
                    throw new OperationCanceledException(
                        "The bundle export was cancelled and cleanup integrity failed.",
                        new GgufRuntimeProfileBundleCleanupException(),
                        cancellation.CancellationToken);
                }
                throw cancellation;
            }
            if (!cleanupSucceeded)
            {
                return Rejected(
                    GgufRuntimeProfileBundleExportDisposition.CleanupFailed);
            }
            return outcome;
        }
    }

    private async Task<GgufRuntimeProfileBundleExportResult> WriteBundleAsync(
        string sourcePath,
        GgufExecutionPayload payload,
        OptimizationExecutionResult result,
        string temporary,
        string destination,
        ulong maximumBytes,
        IProgress<GgufRuntimeProfileBundleExportStage> progress,
        CancellationToken cancellationToken)
    {
        StoragePathGuard.RequireRegularFile(sourcePath);
        await using FileStream source = new(
            sourcePath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            BufferSize,
            FileOptions.Asynchronous | FileOptions.SequentialScan);
        if (source.Length <= 0
            || checked((ulong)source.Length) != _plan.Binding.ModelLengthBytes)
        {
            return Rejected(
                GgufRuntimeProfileBundleExportDisposition.SourceChanged);
        }

        progress.Report(GgufRuntimeProfileBundleExportStage.CopyingModel);
        cancellationToken.ThrowIfCancellationRequested();
        string modelPath = Path.Combine(temporary, ModelName);
        (string modelSha256, ulong modelLength) = await CopyAndHashAsync(
            source, modelPath, maximumBytes, cancellationToken)
            .ConfigureAwait(false);
        if (modelLength != _plan.Binding.ModelLengthBytes
            || !string.Equals(
                modelSha256, _plan.Binding.ModelSha256, StringComparison.Ordinal))
        {
            return Rejected(
                GgufRuntimeProfileBundleExportDisposition.SourceChanged);
        }

        source.Position = 0;
        (string currentSourceSha256, ulong currentSourceLength) =
            await HashAsync(source, maximumBytes, cancellationToken)
                .ConfigureAwait(false);
        if (currentSourceLength != modelLength
            || !string.Equals(
                currentSourceSha256, modelSha256, StringComparison.Ordinal))
        {
            return Rejected(
                GgufRuntimeProfileBundleExportDisposition.SourceChanged);
        }

        progress.Report(GgufRuntimeProfileBundleExportStage.WritingProfile);
        cancellationToken.ThrowIfCancellationRequested();
        byte[] profileBytes = CreateProfileBytes(payload);
        string profilePath = Path.Combine(temporary, ProfileName);
        await WriteNewAsync(profilePath, profileBytes, cancellationToken)
            .ConfigureAwait(false);
        string profileSha256 = Sha256(profileBytes);
        ulong profileLength = checked((ulong)profileBytes.Length);

        progress.Report(GgufRuntimeProfileBundleExportStage.WritingManifest);
        cancellationToken.ThrowIfCancellationRequested();
        byte[] manifestBytes = CreateManifestBytes(
            result, modelSha256, modelLength, profileSha256, profileLength);
        string manifestPath = Path.Combine(temporary, ManifestName);
        await WriteNewAsync(manifestPath, manifestBytes, cancellationToken)
            .ConfigureAwait(false);
        string manifestSha256 = Sha256(manifestBytes);
        ulong manifestLength = checked((ulong)manifestBytes.Length);
        ulong totalLength = checked(modelLength + profileLength + manifestLength);
        if (totalLength > maximumBytes)
        {
            return Rejected(GgufRuntimeProfileBundleExportDisposition.Oversized);
        }

        progress.Report(GgufRuntimeProfileBundleExportStage.Verifying);
        cancellationToken.ThrowIfCancellationRequested();
        if (!await VerifyBundleAsync(
                temporary,
                modelSha256,
                modelLength,
                profileSha256,
                profileLength,
                manifestSha256,
                manifestLength,
                maximumBytes,
                cancellationToken).ConfigureAwait(false))
        {
            return Rejected(GgufRuntimeProfileBundleExportDisposition.Failed);
        }

        progress.Report(GgufRuntimeProfileBundleExportStage.Publishing);
        cancellationToken.ThrowIfCancellationRequested();
        if (!await VerifyBundleAsync(
                temporary,
                modelSha256,
                modelLength,
                profileSha256,
                profileLength,
                manifestSha256,
                manifestLength,
                maximumBytes,
                cancellationToken).ConfigureAwait(false))
        {
            return Rejected(GgufRuntimeProfileBundleExportDisposition.Failed);
        }
        if (File.Exists(destination) || Directory.Exists(destination))
        {
            return Rejected(
                GgufRuntimeProfileBundleExportDisposition.DestinationExists);
        }
        try
        {
            Directory.Move(temporary, destination);
        }
        catch (IOException) when (File.Exists(destination)
                                  || Directory.Exists(destination))
        {
            return Rejected(
                GgufRuntimeProfileBundleExportDisposition.DestinationExists);
        }
        return GgufRuntimeProfileBundleExportResult.Succeeded(
            manifestSha256, manifestLength, totalLength);
    }

    private bool MatchesDeclaredRuntimeProfile(
        OptimizationExecutionResult result,
        out GgufExecutionPayload? payload)
    {
        payload = _plan.ExecutionPayload.Gguf;
        return _plan.Route == OptimizationRoute.Gguf
            && _plan.IsExecutableBy(ExecutorContractVersion)
            && !_plan.ProducesPersistentArtifact
            && payload is
            {
                PersistentTargetWeightFormat: GgufWeightFormat.Imported,
                Quantiser: null,
                ConversionSource: null,
                RequantisationPolicy: null,
                RequiresPersistentConversion: false,
            }
            && PayloadMatchesCandidate(payload)
            && result.Status == OptimizationExecutionStatus.SucceededRuntimeProfile
            && result.SupportCode == OptimizationSupportCode.None
            && result.SourceUnchanged
            && !result.ProducedPersistentArtifact
            && result.OutputSizeBytes == 0
            && result.ExecutionId != Guid.Empty
            && result.Route == _plan.Route
            && result.OptimizationPlanId == _plan.OptimizationPlanId
            && string.Equals(result.ConfigurationSha256,
                _plan.ConfigurationSha256, StringComparison.Ordinal)
            && string.Equals(result.SourceSha256,
                _plan.Binding.ModelSha256, StringComparison.Ordinal)
            && result.SourceLengthBytes == _plan.Binding.ModelLengthBytes
            && string.Equals(result.ModelInspectionRunId,
                _plan.Binding.ModelInspectionRunId, StringComparison.Ordinal)
            && string.Equals(result.ModelInspectionHandoffId,
                _plan.Binding.ModelInspectionHandoffId, StringComparison.Ordinal)
            && string.Equals(result.ProductHardwareRunId,
                _plan.Binding.ProductHardwareRunId, StringComparison.Ordinal)
            && string.Equals(result.HardwareSnapshotSha256,
                _plan.Binding.HardwareSnapshotSha256, StringComparison.Ordinal)
            && string.Equals(result.OutputIdentity,
                $"gguf-profile-{_plan.OptimizationPlanId:N}",
                StringComparison.Ordinal)
            && string.Equals(result.OutputManifestSha256,
                _plan.ConfigurationSha256, StringComparison.Ordinal);
    }

    private bool PayloadMatchesCandidate(GgufExecutionPayload payload)
    {
        if (_plan.Candidate.Configuration is not GgufRouteConfiguration
            {
                Weights: GgufWeightFormat.Imported,
            } configuration
            || _plan.Candidate.Metrics.RequiresPersistentChange
            || _plan.Candidate.Metrics.ContextTokens != payload.ContextSize
            || payload.KeyCacheType != payload.ValueCacheType)
        {
            return false;
        }
        GgufCacheType expectedCache = configuration.KvCache switch
        {
            GgufKvCacheFormat.F16 => GgufCacheType.F16,
            GgufKvCacheFormat.Q8_0 => GgufCacheType.Q8Zero,
            GgufKvCacheFormat.TurboQuant4Bit => GgufCacheType.Turbo4,
            GgufKvCacheFormat.TurboQuant3Bit => GgufCacheType.Turbo3,
            GgufKvCacheFormat.TurboQuant2Bit => GgufCacheType.Turbo2,
            _ => (GgufCacheType)(-1),
        };
        GgufRuntimeBackend expectedBackend = configuration.Backend switch
        {
            CompatibilityBackend.Cpu =>
                GgufRuntimeBackend.Cpu,
            CompatibilityBackend.IntelVulkan =>
                GgufRuntimeBackend.Vulkan,
            CompatibilityBackend.IntelSycl =>
                GgufRuntimeBackend.Sycl,
            _ => (GgufRuntimeBackend)(-1),
        };
        return payload.KeyCacheType == expectedCache
            && payload.Backend == expectedBackend;
    }

    private static bool TryResolveDestination(
        string destinationDirectory,
        out string? destination,
        out string? parent,
        out GgufRuntimeProfileBundleExportDisposition disposition)
    {
        destination = null;
        parent = null;
        disposition = GgufRuntimeProfileBundleExportDisposition.DestinationRejected;
        try
        {
            if (string.IsNullOrWhiteSpace(destinationDirectory)
                || !Path.IsPathFullyQualified(destinationDirectory))
            {
                return false;
            }
            destination = Path.TrimEndingDirectorySeparator(
                Path.GetFullPath(destinationDirectory));
            parent = Path.GetDirectoryName(destination);
            if (string.IsNullOrWhiteSpace(parent))
            {
                return false;
            }
            parent = StoragePathGuard.RequireRoot(parent, create: false);
            StoragePathGuard.RequireChild(parent, destination, mustExist: false);
            if (File.Exists(destination) || Directory.Exists(destination))
            {
                disposition =
                    GgufRuntimeProfileBundleExportDisposition.DestinationExists;
                return false;
            }
            return true;
        }
        catch (Exception exception) when (exception is
            ArgumentException or IOException or UnauthorizedAccessException
                or InvalidOperationException or DirectoryNotFoundException)
        {
            return false;
        }
    }

    private byte[] CreateProfileBytes(GgufExecutionPayload payload)
    {
        using var buffer = new MemoryStream();
        using (var writer = new Utf8JsonWriter(buffer))
        {
            writer.WriteStartObject();
            writer.WriteString("schema", "granite.gguf-issued-runtime-profile.v1");
            writer.WriteString("validation_claim", "declared-not-runtime-validated");
            writer.WriteString("configuration_sha256", _plan.ConfigurationSha256);
            writer.WriteString("runtime_build_id", payload.RuntimeBuildId);
            writer.WriteString("runtime_source_commit", payload.RuntimeSourceCommit);
            writer.WriteString("backend", BackendToken(payload.Backend));
            writer.WriteString("device_id", payload.DeviceId);
            writer.WriteNumber("context_size", payload.ContextSize);
            writer.WriteString("key_cache_type", CacheToken(payload.KeyCacheType));
            writer.WriteString("value_cache_type", CacheToken(payload.ValueCacheType));
            writer.WriteNumber("gpu_layer_count", payload.GpuLayerCount);
            writer.WriteBoolean("flash_attention", payload.FlashAttention);
            writer.WriteNumber("thread_count", payload.ThreadCount);
            writer.WriteNumber("batch_size", payload.BatchSize);
            writer.WriteString("evidence_grade", payload.EvidenceGrade);
            writer.WriteString("profile_id", payload.ProfileId);
            writer.WriteNumber(
                "maximum_generated_tokens", payload.MaximumGeneratedTokens);
            writer.WriteString("persistent_target_weight_format", "imported");
            writer.WriteBoolean("requires_persistent_conversion", false);
            writer.WriteEndObject();
        }
        return buffer.ToArray();
    }

    private byte[] CreateManifestBytes(
        OptimizationExecutionResult result,
        string modelSha256,
        ulong modelLength,
        string profileSha256,
        ulong profileLength)
    {
        using var buffer = new MemoryStream();
        using (var writer = new Utf8JsonWriter(buffer))
        {
            writer.WriteStartObject();
            writer.WriteString("schema", "granite.gguf-runtime-profile-bundle.v1");
            writer.WriteString("profile_claim", "issued-declared-not-runtime-validated");
            writer.WriteString("route", "gguf");
            writer.WriteString(
                "optimization_plan_id", _plan.OptimizationPlanId.ToString("N"));
            writer.WriteString("execution_id", result.ExecutionId.ToString("N"));
            writer.WriteString("configuration_sha256", _plan.ConfigurationSha256);
            writer.WriteString("source_sha256", _plan.Binding.ModelSha256);
            writer.WriteNumber(
                "source_length_bytes", _plan.Binding.ModelLengthBytes);
            writer.WriteString(
                "model_inspection_run_id", _plan.Binding.ModelInspectionRunId);
            writer.WriteString(
                "model_inspection_handoff_id",
                _plan.Binding.ModelInspectionHandoffId);
            writer.WriteString(
                "product_hardware_run_id", _plan.Binding.ProductHardwareRunId);
            writer.WriteString(
                "hardware_snapshot_sha256",
                _plan.Binding.HardwareSnapshotSha256);
            writer.WriteString("result_output_identity", result.OutputIdentity);
            writer.WriteStartArray("members");
            WriteMember(writer, ModelName, modelSha256, modelLength);
            WriteMember(writer, ProfileName, profileSha256, profileLength);
            writer.WriteEndArray();
            writer.WriteEndObject();
        }
        return buffer.ToArray();
    }

    private static void WriteMember(
        Utf8JsonWriter writer,
        string relativeName,
        string sha256,
        ulong length)
    {
        writer.WriteStartObject();
        writer.WriteString("name", relativeName);
        writer.WriteString("sha256", sha256);
        writer.WriteNumber("length_bytes", length);
        writer.WriteEndObject();
    }

    private static async Task<bool> VerifyBundleAsync(
        string root,
        string modelSha256,
        ulong modelLength,
        string profileSha256,
        ulong profileLength,
        string manifestSha256,
        ulong manifestLength,
        ulong maximumBytes,
        CancellationToken cancellationToken)
    {
        if (Directory.EnumerateDirectories(root, "*", SearchOption.TopDirectoryOnly)
            .Any())
        {
            return false;
        }
        string[] files = Directory.GetFiles(root, "*", SearchOption.TopDirectoryOnly);
        if (files.Length != MemberNames.Length
            || !files.Select(Path.GetFileName).Order(StringComparer.Ordinal)
                .SequenceEqual(MemberNames, StringComparer.Ordinal))
        {
            return false;
        }
        foreach (string file in files)
        {
            StoragePathGuard.RequireRegularFile(file);
        }

        (string ModelSha, ulong ModelLength) model = await BoundedFileTransfer.HashAsync(
            Path.Combine(root, ModelName), maximumBytes, cancellationToken)
            .ConfigureAwait(false);
        (string ProfileSha, ulong ProfileLength) profile =
            await BoundedFileTransfer.HashAsync(
                Path.Combine(root, ProfileName), maximumBytes, cancellationToken)
                .ConfigureAwait(false);
        (string ManifestSha, ulong ManifestLength) manifest =
            await BoundedFileTransfer.HashAsync(
                Path.Combine(root, ManifestName), maximumBytes, cancellationToken)
                .ConfigureAwait(false);
        return model == (modelSha256, modelLength)
            && profile == (profileSha256, profileLength)
            && manifest == (manifestSha256, manifestLength);
    }

    private static async Task<(string Sha256, ulong Length)> CopyAndHashAsync(
        FileStream source,
        string destination,
        ulong maximumBytes,
        CancellationToken cancellationToken)
    {
        source.Position = 0;
        byte[] buffer = ArrayPool<byte>.Shared.Rent(BufferSize);
        try
        {
            await using FileStream output = new(
                destination,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                BufferSize,
                FileOptions.Asynchronous | FileOptions.WriteThrough);
            using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
            ulong total = 0;
            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();
                int read = await source.ReadAsync(
                    buffer.AsMemory(0, BufferSize), cancellationToken)
                    .ConfigureAwait(false);
                if (read == 0)
                {
                    break;
                }
                total = checked(total + (ulong)read);
                if (total > maximumBytes)
                {
                    throw new InvalidDataException(
                        "The source exceeds the bounded bundle size.");
                }
                hash.AppendData(buffer, 0, read);
                await output.WriteAsync(
                    buffer.AsMemory(0, read), cancellationToken)
                    .ConfigureAwait(false);
            }
            await output.FlushAsync(cancellationToken).ConfigureAwait(false);
            output.Flush(flushToDisk: true);
            return (
                Convert.ToHexString(hash.GetHashAndReset()).ToLowerInvariant(),
                total);
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer, clearArray: true);
        }
    }

    private static async Task<(string Sha256, ulong Length)> HashAsync(
        FileStream source,
        ulong maximumBytes,
        CancellationToken cancellationToken)
    {
        source.Position = 0;
        byte[] buffer = ArrayPool<byte>.Shared.Rent(BufferSize);
        try
        {
            using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
            ulong total = 0;
            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();
                int read = await source.ReadAsync(
                    buffer.AsMemory(0, BufferSize), cancellationToken)
                    .ConfigureAwait(false);
                if (read == 0)
                {
                    break;
                }
                total = checked(total + (ulong)read);
                if (total > maximumBytes)
                {
                    throw new InvalidDataException(
                        "The source exceeds the bounded bundle size.");
                }
                hash.AppendData(buffer, 0, read);
            }
            return (
                Convert.ToHexString(hash.GetHashAndReset()).ToLowerInvariant(),
                total);
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer, clearArray: true);
        }
    }

    private static async Task WriteNewAsync(
        string path,
        byte[] bytes,
        CancellationToken cancellationToken)
    {
        await using FileStream output = new(
            path,
            FileMode.CreateNew,
            FileAccess.Write,
            FileShare.None,
            BufferSize,
            FileOptions.Asynchronous | FileOptions.WriteThrough);
        await output.WriteAsync(bytes, cancellationToken).ConfigureAwait(false);
        await output.FlushAsync(cancellationToken).ConfigureAwait(false);
        output.Flush(flushToDisk: true);
    }

    private static bool CleanupTemporary(string parent, string temporary)
    {
        if (!Directory.Exists(temporary))
        {
            return true;
        }
        try
        {
            string owned = StoragePathGuard.RequireChild(
                parent, temporary, mustExist: true);
            if (Directory.EnumerateDirectories(
                    owned, "*", SearchOption.TopDirectoryOnly).Any())
            {
                return Quarantine(parent, owned);
            }
            string[] files = Directory.GetFiles(
                owned, "*", SearchOption.TopDirectoryOnly);
            if (files.Any(path => !MemberNames.Contains(
                    Path.GetFileName(path), StringComparer.Ordinal)))
            {
                return Quarantine(parent, owned);
            }
            foreach (string file in files)
            {
                StoragePathGuard.RequireRegularFile(file);
                File.SetAttributes(file, FileAttributes.Normal);
                File.Delete(file);
            }
            Directory.Delete(owned, recursive: false);
            return true;
        }
        catch (Exception exception) when (exception is
            IOException or UnauthorizedAccessException
                or InvalidOperationException or ArgumentException)
        {
            return false;
        }
    }

    private static bool Quarantine(string parent, string temporary)
    {
        try
        {
            string quarantine = StoragePathGuard.RequireChild(
                parent,
                Path.Combine(
                    parent, $".granite-profile-rejected-{Guid.NewGuid():N}"),
                mustExist: false);
            Directory.Move(temporary, quarantine);
        }
        catch (Exception exception) when (exception is
            IOException or UnauthorizedAccessException
                or InvalidOperationException or ArgumentException)
        {
            // Unknown content remains untouched in the exact private staging
            // directory rather than broadening cleanup to data we did not create
        }
        return false;
    }

    private static string BackendToken(GgufRuntimeBackend backend) => backend switch
    {
        GgufRuntimeBackend.Cpu => "cpu",
        GgufRuntimeBackend.Vulkan => "vulkan",
        GgufRuntimeBackend.Sycl => "sycl",
        _ => throw new ArgumentOutOfRangeException(nameof(backend)),
    };

    private static string CacheToken(GgufCacheType cache) => cache switch
    {
        GgufCacheType.F16 => "f16",
        GgufCacheType.Q8Zero => "q8_0",
        GgufCacheType.Q4Zero => "q4_0",
        GgufCacheType.Turbo3 => "turbo3",
        GgufCacheType.Turbo4 => "turbo4",
        GgufCacheType.Turbo2 => "turbo2",
        _ => throw new ArgumentOutOfRangeException(nameof(cache)),
    };

    private static string Sha256(byte[] bytes) =>
        Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();

    private static GgufRuntimeProfileBundleExportResult Rejected(
        GgufRuntimeProfileBundleExportDisposition disposition) =>
        GgufRuntimeProfileBundleExportResult.For(disposition);
}
