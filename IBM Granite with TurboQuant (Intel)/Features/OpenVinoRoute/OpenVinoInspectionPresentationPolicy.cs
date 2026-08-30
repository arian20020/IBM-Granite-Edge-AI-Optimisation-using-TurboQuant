using System;
using GraniteEdgeAI.OpenVino.Contracts;

namespace GraniteEdgeAI.Features.OpenVinoRoute;

public enum OpenVinoInspectionPresentationKind
{
    ConversionRequired,
    IncompletePackage,
    Unsupported,
    OperationalFailure,
    Invalid,
    Cancelled
}

public sealed record OpenVinoInspectionPresentationDisposition(
    OpenVinoInspectionPresentationKind Kind,
    string Title,
    string Message,
    string RecoveryAction,
    string DiagnosticCode,
    bool IsWarning);

public static class OpenVinoInspectionPresentationPolicy
{
    public static OpenVinoInspectionPresentationDisposition Create(
        OpenVinoRouteInspectionResult result)
    {
        ArgumentNullException.ThrowIfNull(result);
        return result.Outcome switch
        {
            OpenVinoRouteInspectionOutcome.ConversionRequired => new(
                OpenVinoInspectionPresentationKind.ConversionRequired,
                "Conversion required",
                "This supported Granite source must be converted before local prompting.",
                "Choose another model package.",
                "conversion_required",
                IsWarning: true),
            OpenVinoRouteInspectionOutcome.IncompletePackage => new(
                OpenVinoInspectionPresentationKind.IncompletePackage,
                "Incomplete package",
                "The local OpenVINO operation could not continue.",
                "Close the session and retry from model inspection.",
                DiagnosticCode(result, "package_inconsistent_resource"),
                IsWarning: false),
            OpenVinoRouteInspectionOutcome.Unsupported => new(
                OpenVinoInspectionPresentationKind.Unsupported,
                "Unsupported package",
                "The local OpenVINO operation could not continue.",
                "Close the session and retry from model inspection.",
                DiagnosticCode(result, "model_architecture_unsupported"),
                IsWarning: false),
            OpenVinoRouteInspectionOutcome.DependencyUnavailable => new(
                OpenVinoInspectionPresentationKind.OperationalFailure,
                "OpenVINO unavailable",
                "The local OpenVINO operation could not continue.",
                "Close the session and retry from model inspection.",
                DiagnosticCode(result, "runtime_dependency_missing"),
                IsWarning: false),
            OpenVinoRouteInspectionOutcome.Cancelled => new(
                OpenVinoInspectionPresentationKind.Cancelled,
                "Inspection cancelled",
                "The local OpenVINO operation was cancelled.",
                "Start inspection again when ready.",
                "operation_cancelled",
                IsWarning: false),
            OpenVinoRouteInspectionOutcome.TimedOut => new(
                OpenVinoInspectionPresentationKind.OperationalFailure,
                "Inspection timed out",
                "The local OpenVINO operation exceeded its time limit.",
                "Retry the operation.",
                DiagnosticCode(result, "runtime_timed_out"),
                IsWarning: false),
            OpenVinoRouteInspectionOutcome.InvalidEvidence => new(
                OpenVinoInspectionPresentationKind.Invalid,
                "Invalid evidence",
                "The local OpenVINO operation could not continue.",
                "Close the session and retry from model inspection.",
                DiagnosticCode(result, "runtime_protocol_failed"),
                IsWarning: false),
            OpenVinoRouteInspectionOutcome.StaleEvidence => new(
                OpenVinoInspectionPresentationKind.Invalid,
                "Package changed",
                "The local OpenVINO operation could not continue.",
                "Close the session and retry from model inspection.",
                DiagnosticCode(result, "package_changed"),
                IsWarning: false),
            OpenVinoRouteInspectionOutcome.Ready or
            OpenVinoRouteInspectionOutcome.ReadyWithWarnings =>
                throw new InvalidOperationException(
                    "A ready OpenVINO result has no non-ready presentation."),
            _ => throw new ArgumentOutOfRangeException(
                nameof(result),
                result.Outcome,
                "Unknown OpenVINO inspection outcome.")
        };
    }

    private static string DiagnosticCode(
        OpenVinoRouteInspectionResult result,
        string fallback)
    {
        string? candidate = result.Failure?.SupportCode;
        foreach (OpenVinoSupportCode supportCode in
            Enum.GetValues<OpenVinoSupportCode>())
        {
            if (string.Equals(
                    candidate,
                    supportCode.ToProtocolValue(),
                    StringComparison.Ordinal))
            {
                return supportCode.ToProtocolValue();
            }
        }
        return fallback;
    }
}
