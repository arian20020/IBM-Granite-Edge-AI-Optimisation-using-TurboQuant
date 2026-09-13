using GraniteEdgeAI.Features.HardwareInspection.Application;
using GraniteEdgeAI.Features.HardwareInspection.Presentation.State;
using System;
using System.Collections.Generic;

namespace GraniteEdgeAI.Features.HardwareInspection.Presentation.Factories;

public sealed class HardwareInspectionPresentationFactory
{
    public HardwareInspectionPresentationState CreateInvalidHandoff() =>
        State(
            HardwareInspectionPresentationKind.InvalidHandoff,
            "Hardware inspection could not open safely.",
            "Navigation problem",
            "Hardware inspection could not open",
            "The information needed to begin this page was missing or could not be read safely. No inspection was started. Return to model inspection and try the journey again.",
            "Hardware inspection could not open. No inspection was started. Back to model inspection is available.",
            "BackToModelInspection",
            [],
            [Action(HardwareInspectionActionKind.BackToModelInspection)]);

    public HardwareInspectionPresentationState CreateActive(HardwareInspectionStage activeStage) => CreateActive(activeStage, null, null);

    internal HardwareInspectionPresentationState CreateActive(HardwareInspectionStage activeStage,
        IReadOnlyDictionary<HardwareInspectionStage, (int Completed, int Total)>? groups, object? owner)
    {
        int activeIndex = (int)activeStage;
        HardwareInspectionStageCopy activeCopy = HardwareInspectionCopyCatalog.Stage(activeStage);
        List<HardwareInspectionStageRow> rows = new(7);
        foreach (HardwareInspectionStage stage in Enum.GetValues<HardwareInspectionStage>())
        {
            HardwareInspectionStageCopy copy = HardwareInspectionCopyCatalog.Stage(stage);
            HardwareInspectionStageRowState rowState = (int)stage < activeIndex
                ? HardwareInspectionStageRowState.Complete
                : stage == activeStage
                    ? HardwareInspectionStageRowState.Active
                    : HardwareInspectionStageRowState.Waiting;
            if (activeStage >= HardwareInspectionStage.ReadingProcessorInformation && activeStage <= HardwareInspectionStage.CheckingLocalInferenceRuntimes &&
                stage >= HardwareInspectionStage.ReadingProcessorInformation && stage <= activeStage)
            {
                rowState = groups is not null && groups.TryGetValue(stage, out var measured) && measured.Completed == measured.Total
                    ? HardwareInspectionStageRowState.Complete : HardwareInspectionStageRowState.Active;
            }
            string sentence = rowState switch
            {
                HardwareInspectionStageRowState.Complete => copy.CompletedSentence,
                HardwareInspectionStageRowState.Active => copy.ActiveExplanation,
                HardwareInspectionStageRowState.Waiting => copy.WaitingSentence,
                _ => throw new ArgumentOutOfRangeException(nameof(rowState)),
            };
            string stateText = rowState == HardwareInspectionStageRowState.Complete
                ? "Complete"
                : rowState.ToString();
            rows.Add(new HardwareInspectionStageRow(
                stage,
                rowState,
                copy.Title,
                sentence,
                $"{stateText} — {copy.Title}. {sentence} State: {stateText}."));
        }

        var result = State(
            HardwareInspectionPresentationKind.Active,
            HardwareInspectionCopyCatalog.ActiveSubtitle,
            "Inspection in progress",
            activeCopy.Title,
            activeCopy.ActiveExplanation,
            $"{activeCopy.Title}. {activeCopy.ActiveExplanation} State: Active.",
            activeIndex == 0 ? "PageHeading" : "PreserveFocus",
            rows,
            [Action(HardwareInspectionActionKind.CancelInspection)],
            completedStageCount: rows.FindAll(row => row.State == HardwareInspectionStageRowState.Complete).Count);
        return groups is null ? result : result.WithGroupChecks(groups, owner!);
    }

    public HardwareInspectionPresentationState CreateStopping() =>
        State(
            HardwareInspectionPresentationKind.Stopping,
            "Your cancellation request was received.",
            "Stopping inspection",
            "Closing the inspection safely...",
            "No hardware report will be created from this run.",
            "Stopping hardware inspection. No hardware report will be created from this run.",
            "StoppingTitle",
            [],
            [Action(HardwareInspectionActionKind.Stopping, enabled: false)]);

    public HardwareInspectionPresentationState CreateTerminal(
        HardwareInspectionOutcome outcome,
        HardwareInspectionFailureClass? failureClass = null,
        bool criticalFailureRetryable = false,
        bool hasUsableHandoff = false,
        bool block3RouteRegistered = false,
        bool optionalCapabilityProbeUnavailable = false)
    {
        if (outcome == HardwareInspectionOutcome.Failed)
        {
            if (failureClass is null)
            {
                throw new ArgumentException("Failed outcome requires a failure class.", nameof(failureClass));
            }

            return CreateFailure(failureClass.Value, criticalFailureRetryable);
        }

        if (failureClass is not null || criticalFailureRetryable
            || (optionalCapabilityProbeUnavailable && outcome != HardwareInspectionOutcome.CompletedWithWarnings))
        {
            throw new ArgumentException("Failure inputs are valid only for Failed outcomes.", nameof(failureClass));
        }

        return outcome switch
        {
            HardwareInspectionOutcome.Completed => CreateCompleted(
                withWarnings: false,
                hasUsableHandoff,
                block3RouteRegistered),
            HardwareInspectionOutcome.CompletedWithWarnings => CreateCompleted(
                withWarnings: true,
                hasUsableHandoff,
                block3RouteRegistered,
                optionalCapabilityProbeUnavailable),
            HardwareInspectionOutcome.Cancelled => CreateCancelled(),
            _ => throw new ArgumentOutOfRangeException(nameof(outcome)),
        };
    }

    private static HardwareInspectionPresentationState CreateCompleted(
        bool withWarnings,
        bool hasUsableHandoff,
        bool block3RouteRegistered,
        bool optionalCapabilityProbeUnavailable = false)
    {
        bool canContinue = hasUsableHandoff && block3RouteRegistered;
        List<HardwareInspectionAction> actions =
        [
            Action(HardwareInspectionActionKind.RunInspectionAgain),
            Action(
                HardwareInspectionActionKind.ContinueToCompatibility,
                enabled: canContinue,
                accessibleHelp: canContinue ? null : HardwareInspectionCopyCatalog.ContinueUnavailableHelp),
        ];

        if (withWarnings)
        {
            string body = optionalCapabilityProbeUnavailable
                ? "Available core hardware facts were collected, but the optional hardware capability probe was unavailable. Compatibility and model actions remain disabled until capability evidence is complete."
                : "The neural processor check could not be confirmed. A small memory-source difference was resolved safely. The hardware report was still created.";
            string announcement = optionalCapabilityProbeUnavailable
                ? "Hardware inspection completed with verified core facts. Optional capability evidence is unavailable, so compatibility remains unavailable."
                : "Hardware inspection completed with one detail to review. A hardware report was created.";
            return State(
                HardwareInspectionPresentationKind.CompletedWithWarnings,
                "Your computer's hardware information is ready to review.",
                "Inspection complete · Review recommended",
                "Your hardware information is ready",
                body,
                announcement,
                "OutcomeTitle",
                [],
                actions,
                completedStageCount: 7,
                detailsAvailable: true,
                reportCreated: true,
                unresolvedReviewCount: 1,
                resolvedInformationCount: optionalCapabilityProbeUnavailable ? 4 : 1);
        }

        return State(
            HardwareInspectionPresentationKind.Completed,
            "This computer’s hardware information is ready.",
            "Inspection complete",
            "Your hardware information is ready",
            "The hardware report was created from the reliable information collected on this device.",
            "Hardware inspection completed. A hardware report was created.",
            "OutcomeTitle",
            [],
            actions,
            completedStageCount: 7,
            detailsAvailable: true,
            reportCreated: true);
    }

    private static HardwareInspectionPresentationState CreateFailure(
        HardwareInspectionFailureClass failureClass,
        bool criticalFailureRetryable) => failureClass switch
        {
            HardwareInspectionFailureClass.CriticalEvidence => State(
                HardwareInspectionPresentationKind.FailedCriticalEvidence,
                "The inspection finished, but the report is incomplete.",
                "Inspection incomplete - Cannot continue",
                "Essential hardware information is missing",
                "Installed memory could not be confirmed reliably. This does not mean the computer is unsuitable.",
                "Hardware inspection failed because essential hardware information could not be confirmed. No hardware conclusion was made.",
                "OutcomeTitle",
                [],
                criticalFailureRetryable
                    ? [Action(HardwareInspectionActionKind.Back), Action(HardwareInspectionActionKind.RunInspectionAgain)]
                    : [Action(HardwareInspectionActionKind.Back)],
                detailsAvailable: true),
            HardwareInspectionFailureClass.TransientOperation => State(
                HardwareInspectionPresentationKind.FailedTransientOperation,
                "The inspection could not collect hardware information this time.",
                "Inspection problem - No hardware conclusion",
                "The inspection could not start",
                "The app could not open the tool it needs to check this computer. This might be temporary.",
                "Hardware inspection could not start. Try again is available.",
                "TryAgain",
                [],
                [Action(HardwareInspectionActionKind.Back), Action(HardwareInspectionActionKind.TryAgain)],
                detailsAvailable: true),
            HardwareInspectionFailureClass.ApplicationRepairRequired => State(
                HardwareInspectionPresentationKind.FailedApplicationRepairRequired,
                "The application needs attention before it can check this computer.",
                "Application problem - Cannot continue",
                "The inspection tool could not be verified",
                "The app cannot safely use a required local component. No conclusion has been made about this computer.",
                "The inspection tool could not be verified. Back to model inspection is available.",
                "BackToModelInspection",
                [],
                [Action(HardwareInspectionActionKind.BackToModelInspection)],
                detailsAvailable: true),
            _ => throw new ArgumentOutOfRangeException(nameof(failureClass)),
        };

    private static HardwareInspectionPresentationState CreateCancelled() =>
        State(
            HardwareInspectionPresentationKind.Cancelled,
            "The inspection has stopped.",
            "Inspection cancelled",
            "No hardware report was created",
            "You stopped the inspection. This does not indicate a problem with the computer.",
            "Hardware inspection cancelled. No hardware report was created.",
            "OutcomeTitle",
            [],
            [Action(HardwareInspectionActionKind.Back), Action(HardwareInspectionActionKind.RunInspectionAgain)],
            detailsAvailable: true);

    private static HardwareInspectionAction Action(
        HardwareInspectionActionKind kind,
        bool enabled = true,
        string? accessibleHelp = null) =>
        new(kind, HardwareInspectionCopyCatalog.ActionLabel(kind), true, enabled, accessibleHelp);

    private static HardwareInspectionPresentationState State(
        HardwareInspectionPresentationKind kind,
        string subtitle,
        string kicker,
        string title,
        string body,
        string announcement,
        string focus,
        IEnumerable<HardwareInspectionStageRow> rows,
        IEnumerable<HardwareInspectionAction> actions,
        int completedStageCount = 0,
        bool detailsAvailable = false,
        bool reportCreated = false,
        int unresolvedReviewCount = 0,
        int resolvedInformationCount = 0) =>
        new(
            kind,
            subtitle,
            kicker,
            title,
            body,
            announcement,
            focus,
            rows,
            actions,
            completedStageCount,
            detailsAvailable,
            reportCreated,
            unresolvedReviewCount,
            resolvedInformationCount);
}
