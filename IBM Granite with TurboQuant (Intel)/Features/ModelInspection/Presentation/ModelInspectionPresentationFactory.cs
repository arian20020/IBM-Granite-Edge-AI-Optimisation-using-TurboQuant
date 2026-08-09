using GraniteEdgeAI.Features.ModelInspection.Contracts;
using GraniteEdgeAI.Features.ModelInspection.Models;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows.Input;

namespace GraniteEdgeAI.Features.ModelInspection.Presentation;

/// <summary>
/// Converts application-owned Model Inspection state into privacy-safe WinUI
/// presentation snapshots. It does not launch downstream workflows.
/// </summary>
internal static class ModelInspectionPresentationFactory
{
    /// <summary>
    /// Creates the inspecting state shown before the first live progress event.
    /// </summary>
    internal static ModelInspectionPagePresentation CreateInitial(
        ModelInspectionRequest request,
        ICommand cancelCommand)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(cancelCommand);

        return new ModelInspectionPagePresentation(
            CreateModelCard(
                request,
                InspectionModelBadgeState.ModelSelected,
                "Awaiting inspection"),
            InitialInspectionProgressPresentationFactory.Create(),
            InspectionOutcomePresentation.Hidden,
            CreateInspectingActions(cancelCommand));
    }

    /// <summary>
    /// Creates a complete inspecting snapshot from the latest service progress.
    /// </summary>
    internal static ModelInspectionPagePresentation CreateProgress(
        ModelInspectionRequest request,
        ModelInspectionProgress progress,
        ICommand cancelCommand)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(progress);
        ArgumentNullException.ThrowIfNull(cancelCommand);

        return new ModelInspectionPagePresentation(
            CreateModelCard(
                request,
                InspectionModelBadgeState.ModelSelected,
                "Inspection in progress"),
            InspectionProgressPresentationFactory.Create(progress),
            InspectionOutcomePresentation.Hidden,
            CreateInspectingActions(cancelCommand));
    }

    /// <summary>
    /// Creates one of the mutually exclusive completed, cancelled, or
    /// operational-failure presentations.
    /// </summary>
    internal static ModelInspectionPagePresentation CreateTerminal(
        ModelInspectionRequest request,
        ModelInspectionExecutionResult execution,
        ICommand retryCommand,
        ICommand chooseAnotherCommand)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(execution);
        ArgumentNullException.ThrowIfNull(retryCommand);
        ArgumentNullException.ThrowIfNull(chooseAnotherCommand);

        return execution.Status switch
        {
            ModelInspectionExecutionStatus.Completed => CreateCompleted(
                request,
                execution.Result ?? throw new InvalidOperationException(
                    "Completed inspection is missing its model result."),
                chooseAnotherCommand),
            ModelInspectionExecutionStatus.Cancelled => CreateCancelled(
                request,
                retryCommand,
                chooseAnotherCommand),
            ModelInspectionExecutionStatus.OperationalFailure => CreateFailure(
                request,
                execution.Failure ?? throw new InvalidOperationException(
                    "Operational failure is missing its safe failure record."),
                retryCommand,
                chooseAnotherCommand),
            _ => throw new ArgumentOutOfRangeException(
                nameof(execution),
                execution.Status,
                "Unknown Model Inspection execution status.")
        };
    }

    private static ModelInspectionPagePresentation CreateCompleted(
        ModelInspectionRequest request,
        ModelInspectionResult result,
        ICommand chooseAnotherCommand)
    {
        OutcomeDefinition definition = GetDefinition(result.Outcome);

        return new ModelInspectionPagePresentation(
            CreateModelCard(
                request,
                definition.Badge,
                definition.ModelStatusSummary),
            CreateCompletedContent(result, definition),
            new InspectionOutcomePresentation
            {
                Kind = definition.Kind,
                Tone = definition.Tone,
                IconSymbol = definition.Symbol,
                Title = definition.Title,
                Message = result.Summary,
                AutomationName = $"{definition.Title}. {result.Summary}"
            },
            new InspectionActionCardPresentation
            {
                Mode = InspectionActionCardMode.Result,
                Title = "Model inspection finished",
                Message =
                    "You can return to model selection when you are ready.",
                AutomationName = "Actions after model inspection",
                PrimaryAction = CreateAction(
                    "Choose another model",
                    "Choose another model",
                    chooseAnotherCommand)
            });
    }

    private static ModelInspectionPagePresentation CreateCancelled(
        ModelInspectionRequest request,
        ICommand retryCommand,
        ICommand chooseAnotherCommand)
    {
        const string userMessage =
            "No model result was produced because inspection was cancelled.";

        return new ModelInspectionPagePresentation(
            CreateModelCard(
                request,
                InspectionModelBadgeState.NotInspected,
                "Inspection cancelled"),
            new InspectionContentCardPresentation
            {
                Mode = InspectionContentCardMode.Cancelled,
                SectionTitle = "Inspection cancelled",
                Items =
                [
                    CreateContentItem(
                        "Inspection stopped",
                        userMessage,
                        InspectionContentStatus.Information,
                        "Cancelled")
                ],
                SupportingText =
                    "Retry inspection or choose another model.",
                SupportingTextVisibility = Visibility.Visible
            },
            new InspectionOutcomePresentation
            {
                Kind = InspectionOutcomePresentationKind.Cancelled,
                Tone = InspectionOutcomeTone.Neutral,
                IconSymbol = Symbol.Cancel,
                Title = "Inspection cancelled",
                Message = userMessage,
                AutomationName = $"Inspection cancelled. {userMessage}"
            },
            CreateRecoveryActions(
                "Inspection was cancelled",
                "Retry when you are ready, or choose another model.",
                retryCommand,
                chooseAnotherCommand));
    }

    private static ModelInspectionPagePresentation CreateFailure(
        ModelInspectionRequest request,
        ModelInspectionOperationalFailure failure,
        ICommand retryCommand,
        ICommand chooseAnotherCommand)
    {
        return new ModelInspectionPagePresentation(
            CreateModelCard(
                request,
                InspectionModelBadgeState.ResultUnknown,
                "Inspection result unavailable"),
            new InspectionContentCardPresentation
            {
                Mode = InspectionContentCardMode.OperationalFailure,
                SectionTitle = "Inspection did not complete",
                Items =
                [
                    CreateContentItem(
                        "Model result unavailable",
                        failure.UserMessage,
                        InspectionContentStatus.Error,
                        "Not completed")
                ],
                SupportingText =
                    "Retry inspection or choose another model.",
                SupportingTextVisibility = Visibility.Visible,
                DiagnosticCode = failure.Code,
                DiagnosticStatus = InspectionContentStatus.Error,
                DiagnosticCodeVisibility = Visibility.Visible
            },
            new InspectionOutcomePresentation
            {
                Kind = InspectionOutcomePresentationKind.OperationalFailure,
                Tone = InspectionOutcomeTone.Error,
                IconSymbol = Symbol.Cancel,
                Title = "Inspection could not be completed",
                Message = failure.UserMessage,
                AutomationName =
                    $"Inspection could not be completed. {failure.UserMessage}"
            },
            CreateRecoveryActions(
                "Inspection could not be completed",
                "Retry the inspection, or choose another model.",
                retryCommand,
                chooseAnotherCommand));
    }

    private static InspectionModelCardPresentation CreateModelCard(
        ModelInspectionRequest request,
        InspectionModelBadgeState badge,
        string modelStatusSummary)
    {
        ValidatedQuickScanSnapshot quickScan = request.QuickScan;
        string quantisation = quickScan.Quantisation ?? "Not reported";
        string parameterCount = quickScan.ParameterSizeLabel ?? "Not reported";
        string declaredContext = quickScan.DeclaredContextLength is ulong context
            ? $"{context.ToString("N0", CultureInfo.InvariantCulture)} tokens"
            : "Not reported";

        return new InspectionModelCardPresentation
        {
            DisplayMode = InspectionModelCardMode.Compact,
            BadgeState = badge,
            ModelName = request.FileName,
            CompactSummary = $"{quickScan.Format} | {modelStatusSummary}",
            FormatShortName = quickScan.Format,
            OverviewFormatBadgeText = $"{quickScan.Format} MODEL",
            FormatName = quickScan.Format,
            Quantisation = quantisation,
            ParameterCount = parameterCount,
            DeclaredContext = declaredContext,
            FileSize =
                $"{quickScan.FileSizeBytes.ToString("N0", CultureInfo.InvariantCulture)} bytes",
            InspectionChecksSummary = modelStatusSummary
        };
    }

    private static InspectionContentCardPresentation CreateCompletedContent(
        ModelInspectionResult result,
        OutcomeDefinition definition)
    {
        if (definition.ContentMode == InspectionContentCardMode.Hidden)
        {
            return InspectionContentCardPresentation.Hidden;
        }

        IReadOnlyList<InspectionContentItemPresentation> items =
            result.Findings.Count == 0
                ?
                [
                    CreateContentItem(
                        definition.Title,
                        result.Summary,
                        definition.DefaultContentStatus,
                        definition.DefaultStatusText)
                ]
                : result.Findings.Select(CreateFindingItem).ToArray();

        ModelInspectionFinding? firstFinding = result.Findings.FirstOrDefault();

        return new InspectionContentCardPresentation
        {
            Mode = definition.ContentMode,
            SectionTitle = definition.SectionTitle,
            Items = items,
            SupportingText = result.RecommendedAction,
            SupportingTextVisibility = Visibility.Visible,
            DiagnosticCode = firstFinding?.Code ?? string.Empty,
            DiagnosticStatus = firstFinding is null
                ? InspectionContentStatus.Information
                : ToContentStatus(firstFinding.Severity),
            DiagnosticCodeVisibility = firstFinding is null
                ? Visibility.Collapsed
                : Visibility.Visible
        };
    }

    private static InspectionContentItemPresentation CreateFindingItem(
        ModelInspectionFinding finding)
    {
        InspectionContentStatus status = ToContentStatus(finding.Severity);
        string statusText = finding.Severity switch
        {
            ModelInspectionFindingSeverity.Information => "Information",
            ModelInspectionFindingSeverity.Warning => "Warning",
            ModelInspectionFindingSeverity.Blocking => "Action required",
            _ => throw new ArgumentOutOfRangeException(
                nameof(finding),
                finding.Severity,
                "Unknown finding severity.")
        };
        string detail =
            $"{finding.Explanation} {finding.RecommendedAction}";

        return CreateContentItem(
            finding.Title,
            detail,
            status,
            statusText);
    }

    private static InspectionContentItemPresentation CreateContentItem(
        string title,
        string detail,
        InspectionContentStatus status,
        string statusText)
    {
        return new InspectionContentItemPresentation
        {
            Title = title,
            Detail = detail,
            DetailVisibility = Visibility.Visible,
            Status = status,
            StatusText = statusText,
            AutomationName = $"{title}. {statusText}. {detail}"
        };
    }

    private static InspectionContentStatus ToContentStatus(
        ModelInspectionFindingSeverity severity)
    {
        return severity switch
        {
            ModelInspectionFindingSeverity.Information =>
                InspectionContentStatus.Information,
            ModelInspectionFindingSeverity.Warning =>
                InspectionContentStatus.Warning,
            ModelInspectionFindingSeverity.Blocking =>
                InspectionContentStatus.Error,
            _ => throw new ArgumentOutOfRangeException(
                nameof(severity),
                severity,
                "Unknown finding severity.")
        };
    }

    private static InspectionActionCardPresentation CreateInspectingActions(
        ICommand cancelCommand)
    {
        return new InspectionActionCardPresentation
        {
            Mode = InspectionActionCardMode.Inspecting,
            Message =
                "You can safely return to model selection at any time.",
            AutomationName = "Actions available while inspecting the model",
            CancelAction = CreateAction(
                "Cancel inspection",
                "Cancel model inspection",
                cancelCommand,
                minimumWidth: 184d)
        };
    }

    private static InspectionActionCardPresentation CreateRecoveryActions(
        string title,
        string message,
        ICommand retryCommand,
        ICommand chooseAnotherCommand)
    {
        return new InspectionActionCardPresentation
        {
            Mode = InspectionActionCardMode.Result,
            Title = title,
            Message = message,
            AutomationName = "Model inspection recovery actions",
            SecondaryActionOne = CreateAction(
                "Choose another model",
                "Choose another model",
                chooseAnotherCommand),
            PrimaryAction = CreateAction(
                "Retry inspection",
                "Retry model inspection",
                retryCommand)
        };
    }

    private static InspectionActionPresentation CreateAction(
        string text,
        string automationName,
        ICommand command,
        double minimumWidth = 174d)
    {
        return new InspectionActionPresentation
        {
            Text = text,
            Command = command,
            IsEnabled = command.CanExecute(parameter: null),
            Visibility = Visibility.Visible,
            AutomationName = automationName,
            MinimumWidth = minimumWidth
        };
    }

    private static OutcomeDefinition GetDefinition(
        ModelInspectionOutcome outcome)
    {
        return outcome switch
        {
            ModelInspectionOutcome.Ready => new OutcomeDefinition(
                InspectionOutcomePresentationKind.Ready,
                InspectionOutcomeTone.Success,
                Symbol.Accept,
                "Model inspection complete",
                InspectionContentCardMode.Hidden,
                InspectionModelBadgeState.Inspected,
                "Inspection complete",
                "Inspection complete",
                InspectionContentStatus.Passed,
                "Passed"),
            ModelInspectionOutcome.ReadyWithWarnings => new OutcomeDefinition(
                InspectionOutcomePresentationKind.ReadyWithWarnings,
                InspectionOutcomeTone.Warning,
                Symbol.Important,
                "Model inspected with warnings",
                InspectionContentCardMode.Warnings,
                InspectionModelBadgeState.Inspected,
                "Inspected with warnings",
                "Inspection warnings",
                InspectionContentStatus.Warning,
                "Warning"),
            ModelInspectionOutcome.ConversionRequired => new OutcomeDefinition(
                InspectionOutcomePresentationKind.ConversionRequired,
                InspectionOutcomeTone.Information,
                Symbol.Switch,
                "Conversion required",
                InspectionContentCardMode.ConversionRequired,
                InspectionModelBadgeState.SourceModel,
                "Conversion required",
                "Conversion required",
                InspectionContentStatus.Information,
                "Information"),
            ModelInspectionOutcome.IncompletePackage => new OutcomeDefinition(
                InspectionOutcomePresentationKind.IncompletePackage,
                InspectionOutcomeTone.Warning,
                Symbol.Important,
                "Model package is incomplete",
                InspectionContentCardMode.IncompletePackage,
                InspectionModelBadgeState.Incomplete,
                "Incomplete package",
                "Incomplete model package",
                InspectionContentStatus.Warning,
                "Incomplete"),
            ModelInspectionOutcome.Unsupported => new OutcomeDefinition(
                InspectionOutcomePresentationKind.Unsupported,
                InspectionOutcomeTone.Error,
                Symbol.Cancel,
                "Model is not supported",
                InspectionContentCardMode.Unsupported,
                InspectionModelBadgeState.Unsupported,
                "Unsupported model",
                "Unsupported model",
                InspectionContentStatus.Error,
                "Unsupported"),
            ModelInspectionOutcome.Invalid => new OutcomeDefinition(
                InspectionOutcomePresentationKind.Invalid,
                InspectionOutcomeTone.Error,
                Symbol.Cancel,
                "Model is invalid",
                InspectionContentCardMode.Invalid,
                InspectionModelBadgeState.Invalid,
                "Invalid model",
                "Invalid model",
                InspectionContentStatus.Error,
                "Invalid"),
            _ => throw new ArgumentOutOfRangeException(
                nameof(outcome),
                outcome,
                "Unknown Model Inspection outcome.")
        };
    }

    private sealed record OutcomeDefinition(
        InspectionOutcomePresentationKind Kind,
        InspectionOutcomeTone Tone,
        Symbol Symbol,
        string Title,
        InspectionContentCardMode ContentMode,
        InspectionModelBadgeState Badge,
        string ModelStatusSummary,
        string SectionTitle,
        InspectionContentStatus DefaultContentStatus,
        string DefaultStatusText);
}
