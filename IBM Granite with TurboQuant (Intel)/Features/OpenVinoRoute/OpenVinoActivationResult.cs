using GraniteEdgeAI.Features.Prompting;
using GraniteEdgeAI.OpenVino.Contracts;
using GraniteEdgeAI.OpenVino.WorkerClient;

namespace GraniteEdgeAI.Features.OpenVinoRoute;

public enum OpenVinoActivationDisposition
{
    Succeeded,
    Failed,
    Cancelled
}

public enum OpenVinoActivationFailureCategory
{
    None,
    RouteUnavailable,
    TargetStale,
    InspectionRejected,
    WorkerUnavailable,
    RuntimeRejected,
    Cancelled,
    UnexpectedFailure
}

/// <summary>
/// Bounded activation outcome. It deliberately carries neither exception text
/// nor worker output, because both may contain private local details.
/// </summary>
public sealed record OpenVinoActivationResult
{
    private OpenVinoActivationResult(
        OpenVinoActivationDisposition disposition,
        OpenVinoActivationFailureCategory failureCategory,
        PromptRouteSessionActivation? activation)
    {
        Disposition = disposition;
        FailureCategory = failureCategory;
        Activation = activation;
    }

    public OpenVinoActivationDisposition Disposition { get; }

    public OpenVinoActivationFailureCategory FailureCategory { get; }

    public PromptRouteSessionActivation? Activation { get; }

    public bool IsSuccessful => Disposition == OpenVinoActivationDisposition.Succeeded;

    internal static OpenVinoActivationResult Success(
        PromptRouteSessionActivation activation) => new(
            OpenVinoActivationDisposition.Succeeded,
            OpenVinoActivationFailureCategory.None,
            activation ?? throw new ArgumentNullException(nameof(activation)));

    internal static OpenVinoActivationResult Failure(
        OpenVinoActivationFailureCategory category)
    {
        if (category is OpenVinoActivationFailureCategory.None
            or OpenVinoActivationFailureCategory.Cancelled)
        {
            throw new ArgumentOutOfRangeException(nameof(category));
        }

        return new OpenVinoActivationResult(
            OpenVinoActivationDisposition.Failed,
            category,
            activation: null);
    }

    internal static OpenVinoActivationResult Cancelled() => new(
        OpenVinoActivationDisposition.Cancelled,
        OpenVinoActivationFailureCategory.Cancelled,
        activation: null);
}

public static class OpenVinoActivationRunner
{
    public static async Task<OpenVinoActivationResult> RunAsync(
        Func<CancellationToken, Task<PromptRouteSessionActivation>> activate,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(activate);

        try
        {
            PromptRouteSessionActivation activation = await activate(cancellationToken)
                .ConfigureAwait(false);
            return OpenVinoActivationResult.Success(activation);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return OpenVinoActivationResult.Cancelled();
        }
        catch (OpenVinoWorkerClientException failure)
        {
            OpenVinoActivationFailureCategory category = Classify(
                failure.SupportCode);
            return category == OpenVinoActivationFailureCategory.Cancelled
                ? OpenVinoActivationResult.Cancelled()
                : OpenVinoActivationResult.Failure(category);
        }
        catch (Exception)
        {
            return OpenVinoActivationResult.Failure(
                OpenVinoActivationFailureCategory.UnexpectedFailure);
        }
    }

    internal static OpenVinoActivationFailureCategory Classify(
        OpenVinoSupportCode supportCode) => supportCode switch
        {
            OpenVinoSupportCode.PackageMissingResource
                or OpenVinoSupportCode.PackageInconsistentResource
                or OpenVinoSupportCode.PackageUnsafePath
                or OpenVinoSupportCode.PackageUnreadable
                or OpenVinoSupportCode.ModelArchitectureUnsupported
                or OpenVinoSupportCode.ModelTaskUnsupported
                or OpenVinoSupportCode.TokenizerUnsupported =>
                OpenVinoActivationFailureCategory.InspectionRejected,
            OpenVinoSupportCode.PackageChanged =>
                OpenVinoActivationFailureCategory.TargetStale,
            OpenVinoSupportCode.RuntimeDependencyMissing
                or OpenVinoSupportCode.RuntimeIntegrityFailed =>
                OpenVinoActivationFailureCategory.WorkerUnavailable,
            OpenVinoSupportCode.OperationCancelled =>
                OpenVinoActivationFailureCategory.Cancelled,
            _ => OpenVinoActivationFailureCategory.RuntimeRejected
        };
}
