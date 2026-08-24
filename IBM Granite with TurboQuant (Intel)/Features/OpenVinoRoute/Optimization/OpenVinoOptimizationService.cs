using GraniteEdgeAI.Features.OpenVinoRoute.Conversion;
using GraniteEdgeAI.Features.OpenVinoRoute.Inspection;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Optimization;
using GraniteEdgeAI.OpenVino.Contracts;

namespace GraniteEdgeAI.Features.OpenVinoRoute.Optimization;

public enum OpenVinoOptimizationStatus
{
    Published,
    Failed,
    Cancelled,
    ReplanRequired
}

public enum OpenVinoOptimizationStage
{
    Preflight, Optimizing, ValidatingOutput, SmokeTesting, Publishing, Reinspecting, Completed
}

public sealed record OpenVinoOptimizationRequest(
    string SourceDirectory,
    string DestinationDirectory,
    OptimizationExecutionPlan Plan,
    OptimizationCapabilitySnapshot CurrentCapabilities,
    bool Confirmed);

public sealed record OpenVinoOptimizationLegacyRequestV1(
    string SourceDirectory,
    string DestinationDirectory,
    OpenVinoOptimizationCandidate Candidate,
    bool Confirmed);

public sealed record OpenVinoOptimizationProgress(
    Guid OperationId,
    OpenVinoOptimizationStage Stage);

public sealed record OpenVinoOptimizationResult(
    OpenVinoOptimizationStatus Status,
    Guid OperationId,
    Guid InspectionRunId,
    OpenVinoSupportCode? SupportCode,
    OpenVinoWeightPrecision? ActualWeightPrecision,
    OpenVinoKvCachePrecision? ActualKvCachePrecision,
    string? ActualDevice)
{
    internal string? PublishedDirectory { get; init; }
    public OptimizationSupportCode? ReplanSupportCode { get; init; }
}

internal sealed record OpenVinoOptimizationInvocation(
    Guid OperationId,
    string SourceDirectory,
    string StagingDirectory,
    string SourceManifestSha256,
    OpenVinoOptimizationCandidate Candidate);

internal sealed record OpenVinoOptimizationCompletion(
    OpenVinoWeightPrecision ActualWeightPrecision,
    IReadOnlyDictionary<string, string> Versions)
{
    internal static OpenVinoOptimizationCompletion CreateTestInstance(
        OpenVinoWeightPrecision precision) => new(
            precision,
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["nncf"] = "3.3.0",
                ["openvino"] = "2026.3.0",
                ["openvino-genai"] = "2026.3.0.0",
                ["optimum"] = "2.3.0",
                ["optimum-intel"] = "2.1.0",
                ["transformers"] = "5.5.4"
            });
}

internal sealed class OpenVinoOptimizationValidation : IDisposable
{
    private IDisposable? retainedLease;

    internal OpenVinoOptimizationValidation(Guid validationRunId, IDisposable? retainedLease = null)
    {
        if (validationRunId == Guid.Empty) throw new ArgumentException(
            "A validation identity is required.", nameof(validationRunId));
        ValidationRunId = validationRunId;
        this.retainedLease = retainedLease;
    }

    internal Guid ValidationRunId { get; }
    internal IDisposable ConsumeLease() => Interlocked.Exchange(ref retainedLease, null) ??
        throw new OpenVinoOptimizationException(OpenVinoSupportCode.ConversionOutputInvalid);
    public void Dispose() => Interlocked.Exchange(ref retainedLease, null)?.Dispose();
    internal static OpenVinoOptimizationValidation CreateTestInstance() => new(Guid.NewGuid());
}

internal sealed record OpenVinoRuntimeOptimizationEvidence(
    string ActualDevice,
    OpenVinoKvCachePrecision ActualKvCachePrecision,
    string GenerationDisposition,
    string QualityDisposition);

internal interface IOpenVinoOptimizationPipeline
{
    Task<OpenVinoOptimizationCompletion> OptimizeAsync(
        OpenVinoOptimizationInvocation invocation,
        CancellationToken cancellationToken);
    Task<OpenVinoOptimizationValidation> ValidateAsync(
        string stagingDirectory,
        CancellationToken cancellationToken);
    Task<OpenVinoRuntimeOptimizationEvidence> SmokeAsync(
        OpenVinoOptimizationValidation validation,
        string stagingDirectory,
        OpenVinoRuntimeOptimization runtime,
        CancellationToken cancellationToken);
    Task<Guid> ReinspectPublishedAsync(
        string destinationDirectory,
        CancellationToken cancellationToken);
}

public sealed class OpenVinoOptimizationService
{
    private readonly IOpenVinoOptimizationPipeline pipeline;
    private readonly Func<string, string, bool> hasSufficientSpace;
    private readonly Func<Guid> operationIdFactory;

    internal OpenVinoOptimizationService(
        IOpenVinoOptimizationPipeline pipeline,
        Func<string, bool> hasSufficientSpace,
        Func<Guid>? operationIdFactory = null)
    {
        this.pipeline = pipeline ?? throw new ArgumentNullException(nameof(pipeline));
        ArgumentNullException.ThrowIfNull(hasSufficientSpace);
        this.hasSufficientSpace = (_, destination) => hasSufficientSpace(destination);
        this.operationIdFactory = operationIdFactory ?? Guid.NewGuid;
    }

    internal OpenVinoOptimizationService(IOpenVinoOptimizationPipeline pipeline)
    {
        this.pipeline = pipeline ?? throw new ArgumentNullException(nameof(pipeline));
        hasSufficientSpace = HasSufficientSpace;
        operationIdFactory = Guid.NewGuid;
    }

    public async Task<OpenVinoOptimizationResult> OptimizeAsync(
        OpenVinoOptimizationRequest request,
        IProgress<OpenVinoOptimizationProgress>? progress,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.Plan);
        ArgumentNullException.ThrowIfNull(request.CurrentCapabilities);
        return await OptimizeCoreAsync(
            request.SourceDirectory,
            request.DestinationDirectory,
            request.Confirmed,
            request.Plan,
            request.CurrentCapabilities,
            legacyCandidate: null,
            progress,
            cancellationToken).ConfigureAwait(false);
    }

    public async Task<OpenVinoOptimizationResult> OptimizeLegacyV1Async(
        OpenVinoOptimizationLegacyRequestV1 request,
        IProgress<OpenVinoOptimizationProgress>? progress,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        return await OptimizeCoreAsync(
            request.SourceDirectory,
            request.DestinationDirectory,
            request.Confirmed,
            plan: null,
            currentCapabilities: null,
            request.Candidate,
            progress,
            cancellationToken).ConfigureAwait(false);
    }

    private async Task<OpenVinoOptimizationResult> OptimizeCoreAsync(
        string sourceDirectory,
        string destinationDirectory,
        bool confirmed,
        OptimizationExecutionPlan? plan,
        OptimizationCapabilitySnapshot? currentCapabilities,
        OpenVinoOptimizationCandidate? legacyCandidate,
        IProgress<OpenVinoOptimizationProgress>? progress,
        CancellationToken cancellationToken)
    {
        Guid operationId = operationIdFactory();
        if (!confirmed || plan is null &&
            (legacyCandidate is null ||
             !OpenVinoOptimizationLegacyRegistryV1.IsRegistered(legacyCandidate)))
        {
            return Failed(operationId, OpenVinoSupportCode.OptimizationUnsupported);
        }

        OpenVinoOptimizationCandidate? candidate = legacyCandidate;
        OpenVinoPackageSnapshot? source = null;
        OpenVinoOptimizationValidation? validation = null;
        try
        {
            Report(progress, operationId, OpenVinoOptimizationStage.Preflight);
            cancellationToken.ThrowIfCancellationRequested();
            OpenVinoPackageSnapshotCapture capture =
                new OpenVinoPackageSnapshotter().Capture(sourceDirectory);
            source = capture.Snapshot;
            if (source is null)
            {
                return Failed(operationId, OpenVinoSupportCode.PackageUnreadable);
            }
            OpenVinoStaticPackageInspectionResult inspection =
                new OpenVinoStaticPackageInspector().Inspect(sourceDirectory);
            if (inspection.Status != OpenVinoStaticInspectionStatus.NativeValidationRequired ||
                inspection.Evidence is null || !hasSufficientSpace(
                    sourceDirectory, destinationDirectory))
            {
                return Failed(operationId, inspection.SupportCode ??
                    OpenVinoSupportCode.ConversionPreflightFailed);
            }

            if (plan is not null)
            {
                if (currentCapabilities is null)
                {
                    return Replan(
                        operationId,
                        OptimizationSupportCode.CapabilityDrift);
                }

                OpenVinoOptimizationAdaptation adaptation =
                    OpenVinoOptimizationPlanAdapter.Adapt(
                        plan,
                        currentCapabilities,
                        inspection.Evidence.ModelSha256,
                        checked((ulong)inspection.Evidence.ModelLengthBytes));
                if (adaptation.Status ==
                    OpenVinoOptimizationAdaptationStatus.ReplanRequired)
                {
                    return Replan(operationId, adaptation.SupportCode);
                }

                candidate = adaptation.Candidate;
            }

            if (candidate is null || candidate.PersistentArtifact is null)
            {
                return Failed(operationId, OpenVinoSupportCode.OptimizationUnsupported);
            }

            OpenVinoWeightPrecision sourcePrecision =
                OpenVinoOptimizationProvenance.ReadSourcePrecision(source);
            OpenVinoWeightPrecision targetPrecision =
                candidate.PersistentArtifact.WeightPrecision;
            if (!CanOptimize(sourcePrecision, targetPrecision))
            {
                return Failed(operationId, OpenVinoSupportCode.OptimizationUnsupported);
            }

            using ConversionTransaction transaction = ConversionTransaction.Create(
                sourceDirectory,
                destinationDirectory,
                operationId);
            Report(progress, operationId, OpenVinoOptimizationStage.Optimizing);
            cancellationToken.ThrowIfCancellationRequested();
            OpenVinoOptimizationCompletion completion = await pipeline.OptimizeAsync(
                new OpenVinoOptimizationInvocation(
                    operationId,
                    sourceDirectory,
                    transaction.StagingDirectory,
                    inspection.Evidence.PackageManifestDigest,
                    candidate),
                cancellationToken).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            if (completion.ActualWeightPrecision != targetPrecision ||
                source.ValidateStillCurrent() != OpenVinoSnapshotFailure.None)
            {
                return Failed(operationId, OpenVinoSupportCode.PackageChanged);
            }

            Report(progress, operationId, OpenVinoOptimizationStage.ValidatingOutput);
            cancellationToken.ThrowIfCancellationRequested();
            validation = await pipeline.ValidateAsync(
                transaction.StagingDirectory, cancellationToken).ConfigureAwait(false);
            Report(progress, operationId, OpenVinoOptimizationStage.SmokeTesting);
            cancellationToken.ThrowIfCancellationRequested();
            OpenVinoRuntimeOptimizationEvidence runtime = await pipeline.SmokeAsync(
                validation,
                transaction.StagingDirectory,
                candidate.Runtime,
                cancellationToken).ConfigureAwait(false);
            if (runtime.ActualDevice != candidate.Device ||
                runtime.ActualKvCachePrecision != candidate.Runtime.KvCachePrecision ||
                runtime.GenerationDisposition != "passed" || runtime.QualityDisposition != "passed" ||
                source.ValidateStillCurrent() != OpenVinoSnapshotFailure.None)
            {
                return Failed(operationId, OpenVinoSupportCode.OptimizationUnsupported);
            }

            IReadOnlyList<OpenVinoOutputArtifact> output =
                OpenVinoOptimizationProvenance.CaptureOutput(transaction.StagingDirectory);
            OpenVinoOptimizationProvenance provenance = new(
                OpenVinoOptimizationProvenance.CurrentSchemaVersion,
                operationId,
                validation.ValidationRunId,
                inspection.Evidence.PackageManifestDigest,
                candidate.ConfigurationId,
                sourcePrecision,
                targetPrecision,
                candidate.Runtime.KvCachePrecision,
                runtime.ActualDevice,
                runtime.ActualKvCachePrecision,
                completion.Versions,
                output,
                OpenVinoProvenance.ComputeOutputManifestDigest(output),
                "passed",
                runtime.GenerationDisposition,
                runtime.QualityDisposition);
            provenance.Write(transaction.StagingDirectory);

            Report(progress, operationId, OpenVinoOptimizationStage.Publishing);
            cancellationToken.ThrowIfCancellationRequested();
            transaction.Publish();
            Guid inspectionRunId;
            try
            {
                Report(progress, operationId, OpenVinoOptimizationStage.Reinspecting);
                cancellationToken.ThrowIfCancellationRequested();
                inspectionRunId = await pipeline.ReinspectPublishedAsync(
                    transaction.DestinationDirectory,
                    cancellationToken).ConfigureAwait(false);
            }
            catch
            {
                if (!transaction.TryRollbackPublished())
                {
                    throw new ConversionTransactionException(
                        OpenVinoSupportCode.ConversionPublishFailed);
                }
                throw;
            }
            Report(progress, operationId, OpenVinoOptimizationStage.Completed);
            return new OpenVinoOptimizationResult(
                OpenVinoOptimizationStatus.Published,
                operationId,
                inspectionRunId,
                SupportCode: null,
                completion.ActualWeightPrecision,
                runtime.ActualKvCachePrecision,
                runtime.ActualDevice)
            {
                PublishedDirectory = transaction.DestinationDirectory
            };
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return new OpenVinoOptimizationResult(
                OpenVinoOptimizationStatus.Cancelled,
                operationId,
                Guid.Empty,
                OpenVinoSupportCode.OperationCancelled,
                null,
                null,
                null);
        }
        catch (Exception exception) when (exception is OpenVinoOptimizationException or
                                          ConversionTransactionException or InvalidDataException)
        {
            OpenVinoSupportCode code = exception switch
            {
                OpenVinoOptimizationException optimization => optimization.SupportCode,
                ConversionTransactionException transaction => transaction.SupportCode,
                _ => OpenVinoSupportCode.ConversionOutputInvalid
            };
            return Failed(operationId, code);
        }
        catch (Exception)
        {
            return Failed(operationId, OpenVinoSupportCode.OptimizationUnsupported);
        }
        finally
        {
            validation?.Dispose();
            source?.Dispose();
        }
    }

    private static bool CanOptimize(
        OpenVinoWeightPrecision source,
        OpenVinoWeightPrecision target) =>
        target switch
        {
            OpenVinoWeightPrecision.Fp16 => source == OpenVinoWeightPrecision.Fp16,
            OpenVinoWeightPrecision.EightBit => source is OpenVinoWeightPrecision.Fp16 or
                OpenVinoWeightPrecision.EightBit,
            OpenVinoWeightPrecision.FourBit => true,
            _ => false
        };

    private static void Report(
        IProgress<OpenVinoOptimizationProgress>? progress,
        Guid operationId,
        OpenVinoOptimizationStage stage) =>
        progress?.Report(new OpenVinoOptimizationProgress(operationId, stage));

    private static OpenVinoOptimizationResult Failed(
        Guid operationId,
        OpenVinoSupportCode code) => new(
            OpenVinoOptimizationStatus.Failed,
            operationId,
            Guid.Empty,
            code,
            null,
            null,
            null);

    private static OpenVinoOptimizationResult Replan(
        Guid operationId,
        OptimizationSupportCode supportCode) => new(
            OpenVinoOptimizationStatus.ReplanRequired,
            operationId,
            Guid.Empty,
            SupportCode: null,
            ActualWeightPrecision: null,
            ActualKvCachePrecision: null,
            ActualDevice: null)
        {
            ReplanSupportCode = supportCode
        };

    private static bool HasSufficientSpace(
        string sourceDirectory,
        string destinationDirectory)
    {
        try
        {
            long sourceBytes = 0;
            foreach (string path in Directory.EnumerateFiles(
                         sourceDirectory, "*", SearchOption.AllDirectories))
            {
                sourceBytes = checked(sourceBytes + new FileInfo(path).Length);
            }
            long required = Math.Max(
                1024L * 1024 * 1024,
                checked(sourceBytes * 2 + 512L * 1024 * 1024));
            string? root = Path.GetPathRoot(Path.GetFullPath(destinationDirectory));
            return !string.IsNullOrWhiteSpace(root) &&
                new DriveInfo(root).AvailableFreeSpace >= required;
        }
        catch (Exception exception) when (exception is IOException or
                                          UnauthorizedAccessException or
                                          OverflowException or ArgumentException)
        {
            return false;
        }
    }
}
