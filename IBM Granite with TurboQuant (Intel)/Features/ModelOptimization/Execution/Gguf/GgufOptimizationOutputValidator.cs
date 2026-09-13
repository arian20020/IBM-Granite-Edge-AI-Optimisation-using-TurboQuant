using System;
using System.IO;
using System.Runtime.ExceptionServices;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using GraniteEdgeAI.Features.GgufRuntime;
using GraniteEdgeAI.Features.ModelImport.QuickScan;
using GraniteEdgeAI.GgufRuntime.Contracts.Commands;
using GraniteEdgeAI.GgufRuntime.WorkerClient;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Optimization;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Optimization.Execution;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Domain;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Routes.Gguf;

namespace GraniteEdgeAI.Features.ModelOptimization.Execution.Gguf;

internal sealed class GgufOptimizationOutputValidator
    : IGgufOptimizationOutputValidator
{
    private readonly string _runtimePackageRoot;
    private readonly byte[] _trustedManifest;
    private readonly Func<string, CancellationToken, Task<ModelQuickScanResult>>
        _scanAsync;
    private readonly Func<OptimizationExecutionPlan, string, string,
        CancellationToken, Task> _smokeAsync;
    private readonly TimeSpan _smokeTimeout;

    internal GgufOptimizationOutputValidator(
        string runtimePackageRoot,
        ReadOnlyMemory<byte> trustedManifest,
        Func<string, CancellationToken, Task<ModelQuickScanResult>>? scanAsync = null,
        Func<OptimizationExecutionPlan, string, string, CancellationToken, Task>?
            smokeAsync = null,
        TimeSpan? smokeTimeout = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(runtimePackageRoot);
        if (trustedManifest.IsEmpty)
        {
            throw new ArgumentException(
                "A trusted GGUF runtime manifest is required.",
                nameof(trustedManifest));
        }

        _runtimePackageRoot = runtimePackageRoot;
        _trustedManifest = trustedManifest.ToArray();
        _scanAsync = scanAsync ?? new GgufQuickScanner().ScanAsync;
        _smokeAsync = smokeAsync ?? SmokeWithTrustedRuntimeAsync;
        _smokeTimeout = smokeTimeout ?? TimeSpan.FromMinutes(2);
        if (_smokeTimeout <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(smokeTimeout));
        }
    }

    public async Task<GgufOptimizationOutputValidationResult> ValidateAsync(
        OptimizationExecutionPlan plan,
        string outputPath,
        IProgress<GgufOptimizationOutputValidationPhase> progress,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentException.ThrowIfNullOrWhiteSpace(outputPath);
        ArgumentNullException.ThrowIfNull(progress);
        cancellationToken.ThrowIfCancellationRequested();

        if (plan.Route != OptimizationRoute.Gguf
            || plan.ExecutionPayload.Gguf is not { } payload
            || !payload.RequiresPersistentConversion)
        {
            return Failure(OptimizationSupportCode.ValidationFailed);
        }

        progress.Report(GgufOptimizationOutputValidationPhase.Validate);
        FileIdentity? initial;
        try
        {
            initial = await InspectAsync(
                outputPath,
                payload.PersistentTargetWeightFormat,
                cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception) when (IsBoundedValidationFailure(exception))
        {
            return Failure(OptimizationSupportCode.ValidationFailed);
        }
        if (initial is null)
        {
            return Failure(OptimizationSupportCode.ValidationFailed);
        }

        progress.Report(GgufOptimizationOutputValidationPhase.SmokeTest);
        using var smokeDeadline = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken);
        smokeDeadline.CancelAfter(_smokeTimeout);
        try
        {
            await _smokeAsync(
                plan,
                outputPath,
                initial.Sha256,
                smokeDeadline.Token)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException)
            when (!cancellationToken.IsCancellationRequested
                && smokeDeadline.IsCancellationRequested)
        {
            return Failure(OptimizationSupportCode.SmokeTestFailed);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (TimeoutException)
        {
            return Failure(OptimizationSupportCode.SmokeTestFailed);
        }
        catch (Exception exception) when (IsBoundedValidationFailure(exception))
        {
            return Failure(OptimizationSupportCode.SmokeTestFailed);
        }

        progress.Report(GgufOptimizationOutputValidationPhase.Reinspect);
        FileIdentity? finalIdentity;
        try
        {
            finalIdentity = await InspectAsync(
                outputPath,
                payload.PersistentTargetWeightFormat,
                cancellationToken).ConfigureAwait(false);
            if (finalIdentity is null
                || finalIdentity.Length != initial.Length
                || !string.Equals(
                    finalIdentity.Sha256,
                    initial.Sha256,
                    StringComparison.Ordinal))
            {
                return Failure(OptimizationSupportCode.ReinspectionFailed);
            }
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception) when (IsBoundedValidationFailure(exception))
        {
            return Failure(OptimizationSupportCode.ReinspectionFailed);
        }

        return GgufOptimizationOutputValidationResult.Success(
            payload.PersistentTargetWeightFormat,
            finalIdentity.Sha256,
            checked((ulong)finalIdentity.Length));
    }

    public async Task<OptimizationSupportCode> ValidateRuntimeProfileAsync(
        OptimizationExecutionPlan plan,
        string sourcePath,
        IProgress<GgufOptimizationOutputValidationPhase> progress,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentException.ThrowIfNullOrWhiteSpace(sourcePath);
        ArgumentNullException.ThrowIfNull(progress);
        cancellationToken.ThrowIfCancellationRequested();

        if (plan.Route != OptimizationRoute.Gguf
            || plan.ExecutionPayload.Gguf is not
            {
                RequiresPersistentConversion: false,
                PersistentTargetWeightFormat: GgufWeightFormat.Imported,
                Quantiser: null,
                ConversionSource: null,
                RequantisationPolicy: null,
            } payload
            || !RuntimePayloadMatchesCandidate(plan, payload))
        {
            return OptimizationSupportCode.ValidationFailed;
        }

        progress.Report(GgufOptimizationOutputValidationPhase.Validate);
        FileIdentity? initial;
        try
        {
            initial = await ComputeIdentityAsync(sourcePath, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception) when (IsBoundedValidationFailure(exception))
        {
            return OptimizationSupportCode.SourceIdentityMismatch;
        }
        if (!MatchesSource(plan, initial))
        {
            return OptimizationSupportCode.SourceIdentityMismatch;
        }

        progress.Report(GgufOptimizationOutputValidationPhase.SmokeTest);
        using var smokeDeadline = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken);
        smokeDeadline.CancelAfter(_smokeTimeout);
        try
        {
            await _smokeAsync(
                plan,
                sourcePath,
                initial!.Sha256,
                smokeDeadline.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
            when (!cancellationToken.IsCancellationRequested
                && smokeDeadline.IsCancellationRequested)
        {
            return OptimizationSupportCode.SmokeTestFailed;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (TimeoutException)
        {
            return OptimizationSupportCode.SmokeTestFailed;
        }
        catch (Exception exception) when (IsBoundedValidationFailure(exception))
        {
            return OptimizationSupportCode.SmokeTestFailed;
        }

        progress.Report(GgufOptimizationOutputValidationPhase.Reinspect);
        try
        {
            FileIdentity? finalIdentity = await ComputeIdentityAsync(
                sourcePath,
                cancellationToken).ConfigureAwait(false);
            return MatchesSource(plan, finalIdentity)
                && finalIdentity!.Length == initial.Length
                && string.Equals(
                    finalIdentity.Sha256,
                    initial.Sha256,
                    StringComparison.Ordinal)
                    ? OptimizationSupportCode.None
                    : OptimizationSupportCode.SourceIdentityMismatch;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception) when (IsBoundedValidationFailure(exception))
        {
            return OptimizationSupportCode.SourceIdentityMismatch;
        }
    }

    private async Task<FileIdentity?> InspectAsync(
        string outputPath,
        GgufWeightFormat target,
        CancellationToken cancellationToken)
    {
        FileIdentity? identity = await ComputeIdentityAsync(
            outputPath,
            cancellationToken).ConfigureAwait(false);
        if (identity is null)
        {
            return null;
        }

        ModelQuickScanResult scan = await _scanAsync(
            outputPath,
            cancellationToken).ConfigureAwait(false);
        return scan.Outcome == ModelQuickScanOutcome.Success
            && scan.FileSizeBytes == identity.Length
            && string.Equals(
                scan.Quantization,
                TargetLabel(target),
                StringComparison.Ordinal)
            ? identity
            : null;
    }

    private async Task SmokeWithTrustedRuntimeAsync(
        OptimizationExecutionPlan plan,
        string outputPath,
        string outputSha256,
        CancellationToken cancellationToken)
    {
        GgufExecutionPayload payload = plan.ExecutionPayload.Gguf
            ?? throw new ArgumentException(
                "The validation plan has no GGUF execution payload.",
                nameof(plan));
        GgufRuntimeClient client = GgufRuntimeClient.CreateFromPackage(
            _runtimePackageRoot,
            _trustedManifest,
            outputPath);
        var configuration = GgufCurrentModelChatRouteLauncher.CreateConfiguration(
            $"optimized-{outputSha256[..12]}",
            outputSha256,
            payload);
        GgufRuntimeSession? session = null;
        Exception? primaryFailure = null;
        try
        {
            session = await client.StartAsync(
                configuration,
                Array.Empty<GgufConversationTurn>(),
                cancellationToken).ConfigureAwait(false);
            await session.CloseAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            primaryFailure = exception;
        }
        finally
        {
            if (session is not null)
            {
                try
                {
                    await session.DisposeAsync().ConfigureAwait(false);
                }
                catch when (primaryFailure is not null)
                {
                    // preserve the first validation failure. the runtime client
                    // already performs its own bounded cleanup verification
                }
            }
        }

        if (primaryFailure is not null)
        {
            ExceptionDispatchInfo.Capture(primaryFailure).Throw();
        }
    }

    private static async Task<FileIdentity?> ComputeIdentityAsync(
        string outputPath,
        CancellationToken cancellationToken)
    {
        await using var stream = new FileStream(
            outputPath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            1024 * 1024,
            FileOptions.Asynchronous | FileOptions.SequentialScan);
        if (stream.Length <= 0)
        {
            return null;
        }

        byte[] digest = await SHA256.HashDataAsync(stream, cancellationToken)
            .ConfigureAwait(false);
        return new FileIdentity(
            Convert.ToHexString(digest).ToLowerInvariant(),
            stream.Length);
    }

    private static string TargetLabel(GgufWeightFormat target) => target switch
    {
        GgufWeightFormat.F16 => "F16",
        GgufWeightFormat.BF16 => "BF16",
        GgufWeightFormat.Q8_0 => "Q8_0",
        GgufWeightFormat.Q6K => "Q6_K",
        GgufWeightFormat.Q5KM => "Q5_K_M",
        GgufWeightFormat.Q4KM => "Q4_K_M",
        GgufWeightFormat.Q3KM => "Q3_K_M",
        GgufWeightFormat.Q2K => "Q2_K",
        _ => string.Empty,
    };

    private static bool IsBoundedValidationFailure(Exception exception) =>
        exception is ArgumentException
            or IOException
            or UnauthorizedAccessException
            or CryptographicException
            or InvalidDataException
            or InvalidOperationException;

    private static bool MatchesSource(
        OptimizationExecutionPlan plan,
        FileIdentity? identity) => identity is not null
        && checked((ulong)identity.Length) == plan.Binding.ModelLengthBytes
        && string.Equals(
            identity.Sha256,
            plan.Binding.ModelSha256,
            StringComparison.Ordinal);

    private static bool RuntimePayloadMatchesCandidate(
        OptimizationExecutionPlan plan,
        GgufExecutionPayload payload)
    {
        if (plan.Candidate.Configuration is not GgufRouteConfiguration
            {
                Weights: GgufWeightFormat.Imported,
            } configuration
            || plan.Candidate.Metrics.RequiresPersistentChange
            || plan.Candidate.Metrics.ContextTokens != payload.ContextSize
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
            CompatibilityBackend.Cpu => GgufRuntimeBackend.Cpu,
            CompatibilityBackend.IntelVulkan => GgufRuntimeBackend.Vulkan,
            CompatibilityBackend.IntelSycl => GgufRuntimeBackend.Sycl,
            _ => (GgufRuntimeBackend)(-1),
        };
        string expectedDevice = configuration.Device switch
        {
            DeviceRouteId.Cpu => "CPU",
            DeviceRouteId.IntelIntegratedGpu => "GPU.0",
            DeviceRouteId.IntelDiscreteGpu => "GPU.1",
            _ => string.Empty,
        };
        return payload.KeyCacheType == expectedCache
            && payload.Backend == expectedBackend
            && string.Equals(
                payload.DeviceId,
                expectedDevice,
                StringComparison.Ordinal)
            && (configuration.Offload != GpuOffloadLevel.None
                || payload.GpuLayerCount == 0);
    }

    private static GgufOptimizationOutputValidationResult Failure(
        OptimizationSupportCode code) =>
        GgufOptimizationOutputValidationResult.Failure(code);

    private sealed record FileIdentity(string Sha256, long Length);
}
