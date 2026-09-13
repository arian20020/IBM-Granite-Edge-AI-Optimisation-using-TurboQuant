using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace GraniteEdgeAI.Features.ModelInspection.Contracts;

/// <summary>
/// Represents one deterministic, classified model outcome ready for later
/// presentation by the Model Inspection ViewModel.
/// </summary>
internal sealed record ModelInspectionResult
{
    /// <summary>
    /// creates one immutable result and defensively copies all findings
    /// </summary>
    internal ModelInspectionResult(
        ModelInspectionOutcome outcome,
        ModelInspectionEvidence evidence,
        IEnumerable<ModelInspectionFinding> findings,
        string summary,
        string recommendedAction,
        string? verifiedConversionRouteId,
        DateTimeOffset startedAtUtc,
        DateTimeOffset completedAtUtc)
    {
        Outcome = ModelInspectionContractValidation.RequireDefinedEnum(
            outcome,
            nameof(outcome));
        Evidence = evidence ?? throw new ArgumentNullException(nameof(evidence));
        ArgumentNullException.ThrowIfNull(findings);

        // copy the sequence so later caller mutations cannot alter the result
        ModelInspectionFinding[] findingCopy = findings.ToArray();
        if (findingCopy.Any(finding => finding is null))
        {
            throw new ArgumentException(
                "Findings must not contain null entries.",
                nameof(findings));
        }

        Findings = new ReadOnlyCollection<ModelInspectionFinding>(findingCopy);
        Summary = ModelInspectionContractValidation.RequireText(
            summary,
            nameof(summary));
        RecommendedAction = ModelInspectionContractValidation.RequireText(
            recommendedAction,
            nameof(recommendedAction));

        // The route is conditionally required only for ConversionRequired. Use
        // a cross-property argument error rather than treating this otherwise
        // optional parameter as universally non-null
        if (Outcome == ModelInspectionOutcome.ConversionRequired)
        {
            if (string.IsNullOrWhiteSpace(verifiedConversionRouteId))
            {
                throw new ArgumentException(
                    "ConversionRequired must identify a verified conversion route.",
                    nameof(verifiedConversionRouteId));
            }

            VerifiedConversionRouteId = verifiedConversionRouteId;
        }
        else
        {
            if (verifiedConversionRouteId is not null)
            {
                throw new ArgumentException(
                    "Only ConversionRequired may carry a conversion route.",
                    nameof(verifiedConversionRouteId));
            }

            VerifiedConversionRouteId = null;
        }

        StartedAtUtc = ModelInspectionContractValidation.RequireUtc(
            startedAtUtc,
            nameof(startedAtUtc));
        CompletedAtUtc = ModelInspectionContractValidation.RequireUtc(
            completedAtUtc,
            nameof(completedAtUtc));
        if (CompletedAtUtc < StartedAtUtc)
        {
            throw new ArgumentException(
                "Completion time must not precede start time.",
                nameof(completedAtUtc));
        }
    }

    internal ModelInspectionOutcome Outcome { get; }

    internal ModelInspectionEvidence Evidence { get; }

    internal IReadOnlyList<ModelInspectionFinding> Findings { get; }

    internal string Summary { get; }

    internal string RecommendedAction { get; }

    internal string? VerifiedConversionRouteId { get; }

    internal DateTimeOffset StartedAtUtc { get; }

    internal DateTimeOffset CompletedAtUtc { get; }

    /// <summary>
    /// Gets whether this classified model may proceed to Hardware Fit analysis.
    /// </summary>
    internal bool CanContinueToHardwareFit => CanContinue(Outcome);

    /// <summary>
    /// returns true only for outcomes that passed every blocking model check
    /// </summary>
    internal static bool CanContinue(ModelInspectionOutcome outcome)
    {
        ModelInspectionOutcome validatedOutcome =
            ModelInspectionContractValidation.RequireDefinedEnum(
                outcome,
                nameof(outcome));

        return validatedOutcome is
            ModelInspectionOutcome.Ready or
            ModelInspectionOutcome.ReadyWithWarnings;
    }
}
