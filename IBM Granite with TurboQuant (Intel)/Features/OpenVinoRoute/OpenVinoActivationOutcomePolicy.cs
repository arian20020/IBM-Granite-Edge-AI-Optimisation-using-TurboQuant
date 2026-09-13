using GraniteEdgeAI.OpenVino.Contracts;

namespace GraniteEdgeAI.Features.OpenVinoRoute;

internal sealed record OpenVinoChatActivationResult(
    OpenVinoRouteInspectionOutcome Outcome,
    OpenVinoSupportCode? SupportCode)
{
    internal bool IsActivated => Outcome is OpenVinoRouteInspectionOutcome.Ready or
        OpenVinoRouteInspectionOutcome.ReadyWithWarnings;
}

internal static class OpenVinoActivationOutcomePolicy
{
    internal static OpenVinoChatActivationResult Activated(
        OpenVinoRouteInspectionOutcome outcome)
    {
        if (outcome is not (OpenVinoRouteInspectionOutcome.Ready or
            OpenVinoRouteInspectionOutcome.ReadyWithWarnings))
        {
            throw new ArgumentOutOfRangeException(
                nameof(outcome),
                outcome,
                "Only a ready inspection can become an active chat session.");
        }
        return new OpenVinoChatActivationResult(outcome, SupportCode: null);
    }

    internal static OpenVinoChatActivationResult FromInspection(
        OpenVinoRouteInspectionResult result)
    {
        ArgumentNullException.ThrowIfNull(result);
        if (result.Outcome is OpenVinoRouteInspectionOutcome.Ready or
            OpenVinoRouteInspectionOutcome.ReadyWithWarnings)
        {
            throw new InvalidOperationException(
                "A ready inspection is not an activated chat session.");
        }

        OpenVinoSupportCode? supportCode = TryParseSupportCode(
            result.Failure?.SupportCode,
            out OpenVinoSupportCode parsed) && OutcomeFor(parsed) == result.Outcome
                ? parsed
                : FallbackFor(result.Outcome);
        return new OpenVinoChatActivationResult(result.Outcome, supportCode);
    }

    internal static OpenVinoChatActivationResult FromSupportCode(
        OpenVinoSupportCode supportCode) => new(
        OutcomeFor(supportCode),
        supportCode);

    internal static OpenVinoSupportCode GetConversionFailureCode(
        OpenVinoRouteInspectionResult result) =>
        FromInspection(result).SupportCode ??
        OpenVinoSupportCode.ConversionOutputInvalid;

    private static bool TryParseSupportCode(
        string? value,
        out OpenVinoSupportCode supportCode)
    {
        foreach (OpenVinoSupportCode candidate in Enum.GetValues<OpenVinoSupportCode>())
        {
            if (string.Equals(
                    value,
                    candidate.ToProtocolValue(),
                    StringComparison.Ordinal))
            {
                supportCode = candidate;
                return true;
            }
        }
        supportCode = default;
        return false;
    }

    private static OpenVinoSupportCode? FallbackFor(
        OpenVinoRouteInspectionOutcome outcome) => outcome switch
    {
        OpenVinoRouteInspectionOutcome.ConversionRequired => null,
        OpenVinoRouteInspectionOutcome.IncompletePackage =>
            OpenVinoSupportCode.PackageInconsistentResource,
        OpenVinoRouteInspectionOutcome.Unsupported =>
            OpenVinoSupportCode.ModelArchitectureUnsupported,
        OpenVinoRouteInspectionOutcome.DependencyUnavailable =>
            OpenVinoSupportCode.RuntimeDependencyMissing,
        OpenVinoRouteInspectionOutcome.Cancelled =>
            OpenVinoSupportCode.OperationCancelled,
        OpenVinoRouteInspectionOutcome.TimedOut =>
            OpenVinoSupportCode.RuntimeTimedOut,
        OpenVinoRouteInspectionOutcome.InvalidEvidence =>
            OpenVinoSupportCode.RuntimeProtocolFailed,
        OpenVinoRouteInspectionOutcome.StaleEvidence =>
            OpenVinoSupportCode.PackageChanged,
        OpenVinoRouteInspectionOutcome.Ready or
        OpenVinoRouteInspectionOutcome.ReadyWithWarnings =>
            throw new InvalidOperationException(
                "A ready inspection has no activation failure code."),
        _ => throw new ArgumentOutOfRangeException(
            nameof(outcome),
            outcome,
            "Unknown OpenVINO inspection outcome.")
    };

    private static OpenVinoRouteInspectionOutcome OutcomeFor(
        OpenVinoSupportCode supportCode) => supportCode switch
    {
        OpenVinoSupportCode.PackageMissingResource or
        OpenVinoSupportCode.PackageInconsistentResource =>
            OpenVinoRouteInspectionOutcome.IncompletePackage,
        OpenVinoSupportCode.ModelArchitectureUnsupported or
        OpenVinoSupportCode.ModelTaskUnsupported or
        OpenVinoSupportCode.TokenizerUnsupported =>
            OpenVinoRouteInspectionOutcome.Unsupported,
        OpenVinoSupportCode.RuntimeDependencyMissing or
        OpenVinoSupportCode.RuntimeDeviceUnavailable =>
            OpenVinoRouteInspectionOutcome.DependencyUnavailable,
        OpenVinoSupportCode.OperationCancelled =>
            OpenVinoRouteInspectionOutcome.Cancelled,
        OpenVinoSupportCode.RuntimeTimedOut =>
            OpenVinoRouteInspectionOutcome.TimedOut,
        OpenVinoSupportCode.PackageChanged =>
            OpenVinoRouteInspectionOutcome.StaleEvidence,
        OpenVinoSupportCode.PackageUnsafePath or
        OpenVinoSupportCode.PackageUnreadable or
        OpenVinoSupportCode.RuntimeIntegrityFailed or
        OpenVinoSupportCode.RuntimeLoadFailed or
        OpenVinoSupportCode.RuntimeDeviceMismatch or
        OpenVinoSupportCode.RuntimeContextExceeded or
        OpenVinoSupportCode.RuntimeProtocolFailed or
        OpenVinoSupportCode.ConversionPreflightFailed or
        OpenVinoSupportCode.ConversionFailed or
        OpenVinoSupportCode.ConversionOutputInvalid or
        OpenVinoSupportCode.ConversionPublishFailed or
        OpenVinoSupportCode.OptimizationUnsupported or
        OpenVinoSupportCode.TurboQuantUnavailable or
        OpenVinoSupportCode.TurboQuantActivationUnverified =>
            OpenVinoRouteInspectionOutcome.InvalidEvidence,
        _ => throw new ArgumentOutOfRangeException(
            nameof(supportCode),
            supportCode,
            "Unknown OpenVINO support code.")
    };
}
