using GraniteEdgeAI.OpenVino.Contracts;

namespace GraniteEdgeAI.Features.OpenVinoRoute.Conversion;

public enum OpenVinoConversionStatus
{
    Published,
    Failed,
    Cancelled
}

public enum OpenVinoConversionStage
{
    Preflight,
    Converting,
    ValidatingOutput,
    SmokeTesting,
    Publishing,
    Reinspecting,
    Completed
}

public sealed record OpenVinoConversionRequest(
    string SourceDirectory,
    string DestinationDirectory,
    bool Confirmed);

public sealed record OpenVinoConversionProgress(
    Guid OperationId,
    OpenVinoConversionStage Stage);

public sealed record OpenVinoConversionResult(
    OpenVinoConversionStatus Status,
    Guid OperationId,
    Guid InspectionRunId,
    OpenVinoSupportCode? SupportCode)
{
    internal string? PublishedDirectory { get; init; }
}

internal sealed record OpenVinoConverterInvocation(
    Guid OperationId,
    string SourceDirectory,
    string StagingDirectory,
    string SourceManifestSha256);

internal sealed record OpenVinoConverterCompletion(
    string PythonRuntimeSha256,
    string WheelLockSha256,
    string ConverterManifestSha256,
    IReadOnlyDictionary<string, string> Versions)
{
    internal static OpenVinoConverterCompletion CreateTestInstance() => new(
        new string('a', 64),
        new string('b', 64),
        new string('c', 64),
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

internal sealed class OpenVinoConversionValidation : IDisposable
{
    private OpenVinoRouteHandoffLease? lease;

    internal OpenVinoConversionValidation(
        Guid validationRunId,
        OpenVinoRouteHandoffLease? lease = null)
    {
        if (validationRunId == Guid.Empty)
        {
            throw new ArgumentException("A validation identity is required.", nameof(validationRunId));
        }
        ValidationRunId = validationRunId;
        this.lease = lease;
    }

    internal Guid ValidationRunId { get; }

    internal OpenVinoRouteHandoffLease ConsumeLease() =>
        Interlocked.Exchange(ref lease, null) ??
        throw new OpenVinoConversionException(OpenVinoSupportCode.ConversionOutputInvalid);

    public void Dispose() => Interlocked.Exchange(ref lease, null)?.Dispose();

    internal static OpenVinoConversionValidation CreateTestInstance() => new(Guid.NewGuid());
}

internal interface IOpenVinoConversionPipeline
{
    Task<OpenVinoConverterCompletion> ConvertAsync(
        OpenVinoConverterInvocation invocation,
        CancellationToken cancellationToken);

    Task<OpenVinoConversionValidation> ValidateAsync(
        string stagingDirectory,
        CancellationToken cancellationToken);

    Task SmokeAsync(
        OpenVinoConversionValidation validation,
        string stagingDirectory,
        CancellationToken cancellationToken);

    void BeforePublish();

    Task<Guid> ReinspectPublishedAsync(
        string destinationDirectory,
        CancellationToken cancellationToken);
}

internal sealed class OpenVinoConversionException : Exception
{
    public OpenVinoConversionException(OpenVinoSupportCode supportCode)
        : base(supportCode.ToProtocolValue()) => SupportCode = supportCode;

    public OpenVinoSupportCode SupportCode { get; }
}

public sealed class OpenVinoConversionService
{
    private static readonly IReadOnlyDictionary<string, object> FixedOptions =
        new Dictionary<string, object>(StringComparer.Ordinal)
        {
            ["library"] = "transformers",
            ["localFilesOnly"] = true,
            ["task"] = "text-generation-with-past",
            ["trustRemoteCode"] = false,
            ["weightFormat"] = "fp16"
        };

    private readonly IOpenVinoConversionPipeline pipeline;
    private readonly Func<string, string, bool> hasSufficientSpace;
    private readonly Func<Guid> operationIdFactory;

    internal OpenVinoConversionService(
        IOpenVinoConversionPipeline pipeline,
        Func<string, bool> hasSufficientSpace,
        Func<Guid>? operationIdFactory = null)
    {
        this.pipeline = pipeline ?? throw new ArgumentNullException(nameof(pipeline));
        ArgumentNullException.ThrowIfNull(hasSufficientSpace);
        this.hasSufficientSpace = (_, destination) => hasSufficientSpace(destination);
        this.operationIdFactory = operationIdFactory ?? Guid.NewGuid;
    }

    internal OpenVinoConversionService(IOpenVinoConversionPipeline pipeline)
    {
        this.pipeline = pipeline ?? throw new ArgumentNullException(nameof(pipeline));
        hasSufficientSpace = HasSufficientSpace;
        operationIdFactory = Guid.NewGuid;
    }

    internal async Task<OpenVinoConversionResult> ConvertAsync(
        OpenVinoConversionOffer offer,
        bool confirmed,
        IProgress<OpenVinoConversionProgress>? progress,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(offer);
        (SourceModelInspectionResult retained, string sourceDirectory) = offer.Consume();
        try
        {
            string parent = Directory.GetParent(sourceDirectory)?.FullName ??
                throw new OpenVinoConversionException(OpenVinoSupportCode.ConversionPreflightFailed);
            string destination = Path.Combine(
                parent,
                Path.GetFileName(sourceDirectory) + "-openvino");
            return await ConvertCoreAsync(
                new OpenVinoConversionRequest(sourceDirectory, destination, confirmed),
                retained.Evidence?.SourceManifestDigest,
                progress,
                cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            retained.Dispose();
        }
    }

    public Task<OpenVinoConversionResult> ConvertAsync(
        OpenVinoConversionRequest request,
        IProgress<OpenVinoConversionProgress>? progress,
        CancellationToken cancellationToken) =>
        ConvertCoreAsync(request, expectedSourceManifestSha256: null, progress, cancellationToken);

    private async Task<OpenVinoConversionResult> ConvertCoreAsync(
        OpenVinoConversionRequest request,
        string? expectedSourceManifestSha256,
        IProgress<OpenVinoConversionProgress>? progress,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        Guid operationId = operationIdFactory();
        DateTimeOffset started = DateTimeOffset.UtcNow;
        if (!request.Confirmed)
        {
            return Failed(operationId, OpenVinoSupportCode.ConversionPreflightFailed);
        }

        SourceModelInspectionResult? source = null;
        OpenVinoConversionValidation? validation = null;
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            Report(progress, operationId, OpenVinoConversionStage.Preflight);
            cancellationToken.ThrowIfCancellationRequested();
            source = new SourceModelInspector().Inspect(request.SourceDirectory);
            if (source.Status != SourceModelInspectionStatus.ConversionRequired ||
                source.Evidence is null ||
                (expectedSourceManifestSha256 is not null &&
                 source.Evidence.SourceManifestDigest != expectedSourceManifestSha256))
            {
                return Failed(operationId, expectedSourceManifestSha256 is not null
                    ? OpenVinoSupportCode.PackageChanged
                    : source.SupportCode ?? OpenVinoSupportCode.ConversionPreflightFailed);
            }
            if (!hasSufficientSpace(request.SourceDirectory, request.DestinationDirectory))
            {
                return Failed(operationId, OpenVinoSupportCode.ConversionPreflightFailed);
            }

            using ConversionTransaction transaction = ConversionTransaction.Create(
                request.SourceDirectory,
                request.DestinationDirectory,
                operationId);
            Report(progress, operationId, OpenVinoConversionStage.Converting);
            cancellationToken.ThrowIfCancellationRequested();
            OpenVinoConverterCompletion completion = await pipeline.ConvertAsync(
                new OpenVinoConverterInvocation(
                    operationId,
                    request.SourceDirectory,
                    transaction.StagingDirectory,
                    source.Evidence.SourceManifestDigest),
                cancellationToken).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            if (!source.VerifyStillCurrent())
            {
                return Failed(operationId, OpenVinoSupportCode.PackageChanged);
            }

            Report(progress, operationId, OpenVinoConversionStage.ValidatingOutput);
            cancellationToken.ThrowIfCancellationRequested();
            validation = await pipeline.ValidateAsync(
                transaction.StagingDirectory,
                cancellationToken).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            Report(progress, operationId, OpenVinoConversionStage.SmokeTesting);
            cancellationToken.ThrowIfCancellationRequested();
            await pipeline.SmokeAsync(
                validation,
                transaction.StagingDirectory,
                cancellationToken).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            if (!source.VerifyStillCurrent())
            {
                return Failed(operationId, OpenVinoSupportCode.PackageChanged);
            }

            IReadOnlyList<OpenVinoOutputArtifact> output =
                OpenVinoProvenance.CaptureOutput(
                    transaction.StagingDirectory,
                    cancellationToken);
            OpenVinoProvenance provenance = new(
                OpenVinoProvenance.CurrentSchemaVersion,
                operationId,
                validation.ValidationRunId,
                started,
                DateTimeOffset.UtcNow,
                source.Evidence.SourceManifestDigest,
                OpenVinoProvenance.DenseGraniteAllowlist,
                completion.PythonRuntimeSha256,
                completion.WheelLockSha256,
                completion.ConverterManifestSha256,
                completion.Versions,
                FixedOptions,
                output,
                OpenVinoProvenance.ComputeOutputManifestDigest(output),
                "passed",
                "passed");
            provenance.Write(transaction.StagingDirectory);

            pipeline.BeforePublish();
            cancellationToken.ThrowIfCancellationRequested();
            Report(progress, operationId, OpenVinoConversionStage.Publishing);
            cancellationToken.ThrowIfCancellationRequested();
            transaction.Publish();
            Guid inspectionRunId;
            try
            {
                Report(progress, operationId, OpenVinoConversionStage.Reinspecting);
                cancellationToken.ThrowIfCancellationRequested();
                inspectionRunId = await pipeline.ReinspectPublishedAsync(
                    transaction.DestinationDirectory,
                    cancellationToken).ConfigureAwait(false);
                cancellationToken.ThrowIfCancellationRequested();
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
            Report(progress, operationId, OpenVinoConversionStage.Completed);
            return new OpenVinoConversionResult(
                OpenVinoConversionStatus.Published,
                operationId,
                inspectionRunId,
                SupportCode: null)
            {
                PublishedDirectory = transaction.DestinationDirectory
            };
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return new OpenVinoConversionResult(
                OpenVinoConversionStatus.Cancelled,
                operationId,
                Guid.Empty,
                OpenVinoSupportCode.OperationCancelled);
        }
        catch (ConversionTransactionException exception)
        {
            return Failed(operationId, exception.SupportCode);
        }
        catch (OpenVinoConversionException exception)
        {
            if (exception.SupportCode == OpenVinoSupportCode.OperationCancelled)
            {
                return new OpenVinoConversionResult(
                    OpenVinoConversionStatus.Cancelled,
                    operationId,
                    Guid.Empty,
                    OpenVinoSupportCode.OperationCancelled);
            }
            return Failed(operationId, exception.SupportCode);
        }
        catch (InvalidDataException)
        {
            return Failed(operationId, OpenVinoSupportCode.ConversionOutputInvalid);
        }
        catch (Exception)
        {
            return Failed(operationId, OpenVinoSupportCode.ConversionFailed);
        }
        finally
        {
            validation?.Dispose();
            source?.Dispose();
        }
    }

    private static void Report(
        IProgress<OpenVinoConversionProgress>? progress,
        Guid operationId,
        OpenVinoConversionStage stage) =>
        progress?.Report(new OpenVinoConversionProgress(operationId, stage));

    private static OpenVinoConversionResult Failed(
        Guid operationId,
        OpenVinoSupportCode supportCode) => new(
            OpenVinoConversionStatus.Failed,
            operationId,
            Guid.Empty,
            supportCode);

    private static bool HasSufficientSpace(string sourceDirectory, string destinationDirectory)
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
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or
                                          OverflowException or ArgumentException)
        {
            return false;
        }
    }
}
