using GraniteEdgeAI.Features.ModelInspection.Contracts;
using GraniteEdgeAI.Features.ModelInspection.Models;
using GraniteEdgeAI.Features.ModelInspection.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Windows.Input;
using WorkerProtocol = GraniteEdgeAI.ModelInspection.Contracts.WorkerProtocol;

namespace GraniteEdgeAI.Features.ModelInspection.Presentation;

/// <summary>
/// Maps one atomic application snapshot into the approved, privacy-safe Figma
/// presentation state. The factory is deterministic and performs no I/O.
/// </summary>
internal static class ModelInspectionPresentationFactory
{
    private const string NotReported = "Not reported";
    private const string ComingLater = "Coming later";
    private const string SupportedWarningCode =
        "MI-WARN-CHAT-TEMPLATE-MISSING";
    private const string UnsupportedEvidenceCode =
        "MI-OP-PRESENTATION-UNSUPPORTED-EVIDENCE";
    private const string ExpectedLlamaSharpVersion = "0.27.0";
    private const string ExpectedBackendPackageVersion = "0.27.0";
    private const string ExpectedMappedLlamaCppCommit =
        "3f7c29d318e317b63f54c558bc69803963d7d88c";
    private const string ExpectedNativeLibraryName = "llama.dll";
    private const string ExpectedProcessArchitecture = "X64";
    private const string ExpectedInspectionMode = "VocabOnly";

    internal static ModelInspectionPagePresentation Create(
        ModelInspectionRequest request,
        ModelInspectionViewSnapshot snapshot,
        ModelInspectionPresentationCommands commands,
        bool isDisclosureExpanded,
        InspectionProgressRows progressRows)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentNullException.ThrowIfNull(commands);
        ArgumentNullException.ThrowIfNull(progressRows);

        if (snapshot.TerminalResult is null)
        {
            if (isDisclosureExpanded)
            {
                throw new ArgumentException(
                    "Inspection progress has no disclosure.",
                    nameof(isDisclosureExpanded));
            }

            return CreateProgressState(request, snapshot, commands, progressRows);
        }

        return CreateTerminalState(
            request,
            snapshot,
            commands,
            isDisclosureExpanded);
    }

    private static ModelInspectionPagePresentation CreateProgressState(
        ModelInspectionRequest request,
        ModelInspectionViewSnapshot snapshot,
        ModelInspectionPresentationCommands commands,
        InspectionProgressRows progressRows)
    {
        ModelInspectionProgress? progress = snapshot.Progress;
        bool isStarting = snapshot.IsRunActive && progress is null;
        bool cancelEnabled = snapshot.IsRunActive &&
            !snapshot.IsCancellationRequested &&
            commands.Cancel.CanExecute(parameter: null);
        InspectionProgressRowsUpdate progressRowsUpdate = progress is null
            ? new InspectionProgressRowsUpdate(
                new ModelInspectionProgressRegionKey(
                    stage: null,
                    stageStatus: null,
                    completedStageCount: 0,
                    stageCount: 0,
                    stageFraction: null,
                    detail: string.Empty),
                snapshot.RenderKey,
                "0 of 5 checks complete")
            : InspectionProgressPresentationFactory.Create(
                progress,
                snapshot.RenderKey);
        string progressAnnouncement = isStarting &&
            snapshot.RenderKey.PresentationRevision == 0
                ? "Model inspection is starting."
                : progressRowsUpdate.Key.Detail;
        string statusSummary = isStarting
            ? "Starting secure inspection…"
            : progress is null
                ? "Awaiting inspection"
                : "Inspection in progress";
        InspectionStartupPresentation startup = isStarting
            ? new InspectionStartupPresentation
            {
                Visibility = Visibility.Visible,
                Summary = "Starting secure inspection…",
                AutomationName = "Model inspection is starting."
            }
            : InspectionStartupPresentation.Hidden;
        InspectionModelCardPresentation modelCard = CreateModelCard(
            request,
            result: null,
            InspectionModelBadgeState.ModelSelected,
            statusSummary,
            detailed: false,
            expanded: false);
        InspectionContentCardPresentation contentCard =
            InitialInspectionProgressPresentationFactory.Create(
                progressRows,
                startup);
        InspectionActionCardPresentation actionCard = new()
        {
            Mode = InspectionActionCardMode.Inspecting,
            Message = "You can safely return to model selection at any time.",
            AutomationName = "Actions available while inspecting the model",
            CancelAction = CreateActiveAction(
                "cancel",
                "Cancel inspection",
                "Cancel model inspection",
                commands.Cancel,
                cancelEnabled,
                minimumWidth: 184d)
        };
        return CreatePage(
            snapshot.RenderKey,
            ModelInspectionFigmaState.InspectionProgress,
            modelCard,
            contentCard,
            InspectionOutcomePresentation.Hidden,
            actionCard,
            InspectionFooterStatus.InProgress,
            progressRowsUpdate.Key,
            progressAnnouncement,
            outcomeAnnouncement: string.Empty,
            progressRowsUpdate);
    }

    private static ModelInspectionPagePresentation CreateTerminalState(
        ModelInspectionRequest request,
        ModelInspectionViewSnapshot snapshot,
        ModelInspectionPresentationCommands commands,
        bool isDisclosureExpanded)
    {
        ModelInspectionExecutionResult execution = snapshot.TerminalResult!;
        return execution.Status switch
        {
            ModelInspectionExecutionStatus.Completed => CreateCompletedState(
                request,
                snapshot.RenderKey,
                execution.Result ?? throw new InvalidOperationException(
                    "Completed inspection is missing its model result."),
                commands,
                isDisclosureExpanded),
            ModelInspectionExecutionStatus.Cancelled => CreateCancelledState(
                request,
                snapshot.RenderKey,
                commands,
                isDisclosureExpanded),
            ModelInspectionExecutionStatus.OperationalFailure =>
                CreateOperationalFailureState(
                    request,
                    snapshot.RenderKey,
                    execution.Failure ?? throw new InvalidOperationException(
                        "Operational failure is missing its safe failure record."),
                    commands,
                    isDisclosureExpanded),
            _ => throw new ArgumentOutOfRangeException(
                nameof(execution),
                execution.Status,
                "Unknown Model Inspection execution status.")
        };
    }

    private static ModelInspectionPagePresentation CreateCompletedState(
        ModelInspectionRequest request,
        ModelInspectionRenderKey renderKey,
        ModelInspectionResult result,
        ModelInspectionPresentationCommands commands,
        bool expanded)
    {
        if (!HasSupportedEvidence(request, result))
        {
            return CreateFailClosedState(
                request,
                renderKey,
                commands,
                expanded);
        }

        OutcomeDefinition definition = GetDefinition(result.Outcome);
        if (expanded && !definition.HasDisclosure)
        {
            throw new ArgumentException(
                "This completed outcome has no approved disclosure pair.",
                nameof(expanded));
        }

        ModelInspectionFigmaState state = GetCompletedState(
            result.Outcome,
            expanded);
        InspectionModelCardPresentation modelCard = CreateModelCard(
            request,
            result,
            definition.Badge,
            definition.ModelStatusSummary,
            detailed: result.Outcome == ModelInspectionOutcome.Ready,
            expanded: result.Outcome == ModelInspectionOutcome.Ready && expanded);
        InspectionContentCardPresentation contentCard =
            CreateCompletedContent(result, definition, expanded);
        InspectionOutcomePresentation outcomeCard = new()
        {
            Kind = definition.Kind,
            Tone = definition.Tone,
            IconSymbol = definition.Symbol,
            Title = definition.Title,
            Message = definition.FixedMessage,
            AutomationName = definition.OutcomeAnnouncement
        };
        InspectionActionCardPresentation actionCard =
            CreateCompletedActions(result.Outcome, commands);

        return CreatePage(
            renderKey,
            state,
            modelCard,
            contentCard,
            outcomeCard,
            actionCard,
            definition.FooterStatus,
            EmptyProgressKey(),
            progressAnnouncement: string.Empty,
            outcomeAnnouncement: definition.OutcomeAnnouncement,
            progressRowsUpdate: CreateEmptyProgressRowsUpdate(renderKey));
    }

    private static ModelInspectionPagePresentation CreateCancelledState(
        ModelInspectionRequest request,
        ModelInspectionRenderKey renderKey,
        ModelInspectionPresentationCommands commands,
        bool expanded)
    {
        if (expanded)
        {
            throw new ArgumentException(
                "Cancelled inspection has no disclosure.",
                nameof(expanded));
        }

        const string Message =
            "No model result was produced because inspection was cancelled.";
        InspectionContentCardPresentation contentCard = new()
        {
            Mode = InspectionContentCardMode.Cancelled,
            SectionTitle = "Inspection cancelled",
            Items =
            [
                CreateContentItem(
                    "Inspection stopped",
                    Message,
                    InspectionContentStatus.Information,
                    "Cancelled")
            ],
            SupportingText = "Restart inspection or choose another model.",
            SupportingTextVisibility = Visibility.Visible
        };
        InspectionOutcomePresentation outcomeCard = new()
        {
            Kind = InspectionOutcomePresentationKind.Cancelled,
            Tone = InspectionOutcomeTone.Neutral,
            IconSymbol = Symbol.Cancel,
            Title = "Inspection cancelled",
            Message = Message,
            AutomationName = "Model inspection was cancelled."
        };
        InspectionActionCardPresentation actions = new()
        {
            Mode = InspectionActionCardMode.Result,
            Title = "Inspection was cancelled",
            Message = "Restart when you are ready, or choose another model.",
            AutomationName = "Model inspection recovery actions",
            SecondaryActionOne = CreateActiveAction(
                "choose-another",
                "Choose another model",
                "Choose another model",
                commands.ChooseAnother),
            PrimaryAction = CreateActiveAction(
                "restart",
                "Restart inspection",
                "Restart model inspection",
                commands.Retry)
        };

        return CreatePage(
            renderKey,
            ModelInspectionFigmaState.Cancelled,
            CreateModelCard(
                request,
                result: null,
                InspectionModelBadgeState.NotInspected,
                "Inspection cancelled",
                detailed: false,
                expanded: false),
            contentCard,
            outcomeCard,
            actions,
            InspectionFooterStatus.NotComplete,
            EmptyProgressKey(),
            progressAnnouncement: string.Empty,
            outcomeAnnouncement: "Model inspection was cancelled.",
            progressRowsUpdate: CreateEmptyProgressRowsUpdate(renderKey));
    }

    private static ModelInspectionPagePresentation CreateOperationalFailureState(
        ModelInspectionRequest request,
        ModelInspectionRenderKey renderKey,
        ModelInspectionOperationalFailure failure,
        ModelInspectionPresentationCommands commands,
        bool expanded)
    {
        if (expanded)
        {
            throw new ArgumentException(
                "Operational failure has no disclosure.",
                nameof(expanded));
        }

        string code = ModelInspectionDisplayTextPolicy.ProjectOptionalLabel(
            failure.Code);
        if (code == NotReported)
        {
            code = UnsupportedEvidenceCode;
        }

        string message = ModelInspectionDisplayTextPolicy.ProjectRequiredDetail(
            failure.UserMessage,
            "Inspection could not be completed safely.");
        return CreateFailurePage(
            request,
            renderKey,
            commands,
            code,
            message);
    }

    private static ModelInspectionPagePresentation CreateFailClosedState(
        ModelInspectionRequest request,
        ModelInspectionRenderKey renderKey,
        ModelInspectionPresentationCommands commands,
        bool expanded)
    {
        if (expanded)
        {
            throw new ArgumentException(
                "Unsupported completed evidence has no disclosure.",
                nameof(expanded));
        }

        return CreateFailurePage(
            request,
            renderKey,
            commands,
            UnsupportedEvidenceCode,
            "Inspection evidence could not be presented safely.");
    }

    private static ModelInspectionPagePresentation CreateFailurePage(
        ModelInspectionRequest request,
        ModelInspectionRenderKey renderKey,
        ModelInspectionPresentationCommands commands,
        string diagnosticCode,
        string message)
    {
        InspectionContentCardPresentation contentCard = new()
        {
            Mode = InspectionContentCardMode.OperationalFailure,
            SectionTitle = "Inspection did not complete",
            Items =
            [
                CreateContentItem(
                    "Model result unavailable",
                    message,
                    InspectionContentStatus.Error,
                    "Not completed")
            ],
            SupportingText = "Retry inspection or choose another model.",
            SupportingTextVisibility = Visibility.Visible,
            DiagnosticCode = diagnosticCode,
            DiagnosticStatus = InspectionContentStatus.Error,
            DiagnosticCodeVisibility = Visibility.Visible
        };
        InspectionOutcomePresentation outcomeCard = new()
        {
            Kind = InspectionOutcomePresentationKind.OperationalFailure,
            Tone = InspectionOutcomeTone.Error,
            IconSymbol = Symbol.Cancel,
            Title = "Inspection could not be completed",
            Message = message,
            AutomationName = "Model inspection could not be completed."
        };
        InspectionActionCardPresentation actions = new()
        {
            Mode = InspectionActionCardMode.Result,
            Title = "Inspection could not be completed",
            Message = "Retry the inspection, or choose another model.",
            AutomationName = "Model inspection recovery actions",
            SecondaryActionOne = CreateActiveAction(
                "choose-another",
                "Choose another model",
                "Choose another model",
                commands.ChooseAnother),
            SecondaryActionTwo = CreateFutureAction(
                "technical-report",
                "View technical report",
                "View technical inspection report"),
            PrimaryAction = CreateActiveAction(
                "retry",
                "Retry inspection",
                "Retry model inspection",
                commands.Retry)
        };

        return CreatePage(
            renderKey,
            ModelInspectionFigmaState.OperationalFailure,
            CreateModelCard(
                request,
                result: null,
                InspectionModelBadgeState.ResultUnknown,
                "Inspection result unavailable",
                detailed: false,
                expanded: false),
            contentCard,
            outcomeCard,
            actions,
            InspectionFooterStatus.Interrupted,
            EmptyProgressKey(),
            progressAnnouncement: string.Empty,
            outcomeAnnouncement: "Model inspection could not be completed.",
            progressRowsUpdate: CreateEmptyProgressRowsUpdate(renderKey));
    }

    private static ModelInspectionPagePresentation CreatePage(
        ModelInspectionRenderKey renderKey,
        ModelInspectionFigmaState state,
        InspectionModelCardPresentation modelCard,
        InspectionContentCardPresentation contentCard,
        InspectionOutcomePresentation outcomeCard,
        InspectionActionCardPresentation actionCard,
        InspectionFooterStatus footerStatus,
        ModelInspectionProgressRegionKey progressKey,
        string progressAnnouncement,
        string outcomeAnnouncement,
        InspectionProgressRowsUpdate progressRowsUpdate)
    {
        string outcomeIdentity = GetOutcomeIdentity(state);
        ModelInspectionRegionKeys keys = new(
            CreateOutcomeRegionKey(outcomeIdentity, outcomeCard),
            CreateModelRegionKey(outcomeIdentity, modelCard),
            CreateContentRegionKey(outcomeIdentity, contentCard),
            CreateActionsRegionKey(outcomeIdentity, actionCard),
            new ModelInspectionRegionKey($"footer:{footerStatus}"),
            progressKey,
            CreateAnnouncementsRegionKey(
                renderKey,
                state,
                outcomeIdentity,
                progressKey));

        return new ModelInspectionPagePresentation(
            renderKey,
            state,
            modelCard,
            contentCard,
            outcomeCard,
            actionCard,
            footerStatus,
            keys,
            progressAnnouncement,
            outcomeAnnouncement,
            progressRowsUpdate);
    }

    private static InspectionModelCardPresentation CreateModelCard(
        ModelInspectionRequest request,
        ModelInspectionResult? result,
        InspectionModelBadgeState badge,
        string statusSummary,
        bool detailed,
        bool expanded)
    {
        ModelInspectionConfigurationEvidence? configuration =
            result?.Evidence.Configuration;
        string modelName = ModelInspectionDisplayTextPolicy.ProjectModelName(
            configuration?.ModelName,
            request.QuickScan.ModelName,
            result?.Evidence.File.FileName ?? request.FileName);
        string format = ModelInspectionDisplayTextPolicy.ProjectOptionalLabel(
            configuration?.Format ?? request.QuickScan.Format);
        string quantisation = ModelInspectionDisplayTextPolicy.ProjectOptionalLabel(
            request.QuickScan.Quantisation);
        string parameters = ProjectParameters(
            request.QuickScan.ParameterSizeLabel,
            configuration?.ParameterCount);
        ulong? context = configuration?.DeclaredContextLength ??
            request.QuickScan.DeclaredContextLength;
        long fileLength = result?.Evidence.File.LengthBytes ??
            request.ExpectedFileIdentity.LengthBytes;
        string fileSize = FormatFileSize(fileLength);

        return new InspectionModelCardPresentation
        {
            DisplayMode = detailed
                ? InspectionModelCardMode.Detailed
                : InspectionModelCardMode.Compact,
            BadgeState = badge,
            ModelName = modelName,
            CompactSummary = $"{format} | {quantisation} | {fileSize}",
            FormatShortName = format,
            OverviewFormatBadgeText = $"{format} MODEL",
            Publisher = NotReported,
            FormatName = format,
            Quantisation = quantisation,
            ParameterCount = parameters,
            ModelType = NotReported,
            DeclaredContext = context.HasValue
                ? $"{context.Value.ToString("N0", CultureInfo.CurrentCulture)} tokens"
                : NotReported,
            FileSize = fileSize,
            InspectionChecksSummary = detailed
                ? "All 5 inspection checks passed"
                : statusSummary,
            InspectionChecks = detailed && result is not null
                ? CreateReadyChecks(result, quantisation, parameters)
                : Array.Empty<InspectionCheckPresentation>(),
            InspectionDetailsVisibility = detailed
                ? Visibility.Visible
                : Visibility.Collapsed,
            IsInspectionDetailsExpanded = expanded
        };
    }

    private static IReadOnlyList<InspectionCheckPresentation> CreateReadyChecks(
        ModelInspectionResult result,
        string quantisation,
        string parameters)
    {
        ModelInspectionEvidence evidence = result.Evidence;
        string architecture = ModelInspectionDisplayTextPolicy.ProjectOptionalLabel(
            evidence.Configuration.Architecture);
        string tokenizer = ModelInspectionDisplayTextPolicy.ProjectOptionalLabel(
            evidence.Tokenizer.TokenizerModel);
        string runtimeArchitecture =
            ModelInspectionDisplayTextPolicy.ProjectOptionalLabel(
                evidence.Runtime.ProcessArchitecture);
        string inspectionMode = ModelInspectionDisplayTextPolicy.ProjectOptionalLabel(
            evidence.Runtime.InspectionMode);
        string library = ModelInspectionDisplayTextPolicy.ProjectOptionalLabel(
            evidence.Runtime.NativeLibraryName);

        return
        [
            CreateCheck(
                "Model package",
                $"GGUF version {FormatNullable(evidence.Configuration.GgufVersion)}; " +
                "package boundaries validated and source integrity preserved."),
            CreateCheck(
                "Model configuration",
                $"Architecture {architecture}; " +
                $"{FormatNullable(evidence.Configuration.LayerCount)} layers; " +
                $"quantisation {quantisation}; {WithParameterNoun(parameters)}."),
            CreateCheck(
                "Tokenizer and chat setup",
                $"Tokenizer {tokenizer}; smoke check " +
                $"{FormatBoolean(evidence.Tokenizer.TokenizerSmokePassed)} with " +
                $"{FormatNullable(evidence.Tokenizer.TokenizerSmokeTokenCount)} tokens; " +
                $"chat template {FormatPresence(evidence.ChatTemplate.Present)}."),
            CreateCheck(
                "Model structure",
                "Model structure validation completed using reported configuration evidence."),
            CreateCheck(
                "Core runtime support",
                $"CPU {runtimeArchitecture} {inspectionMode} inspection completed " +
                $"with {library} using the approved profile.")
        ];
    }

    private static InspectionCheckPresentation CreateCheck(
        string title,
        string detail)
    {
        string safeDetail = ModelInspectionDisplayTextPolicy.ProjectRequiredDetail(
            detail,
            "The inspection check completed successfully.");
        return new InspectionCheckPresentation
        {
            Title = title,
            Detail = safeDetail,
            Status = InspectionCheckStatus.Passed,
            StatusText = "Passed",
            AutomationName = $"{title}. Passed. {safeDetail}"
        };
    }

    private static InspectionContentCardPresentation CreateCompletedContent(
        ModelInspectionResult result,
        OutcomeDefinition definition,
        bool expanded)
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
                        definition.GenericRowTitle,
                        definition.GenericRowDetail,
                        definition.DefaultContentStatus,
                        definition.DefaultStatusText)
                ]
                :
                [
                    CreateContentItem(
                        "Chat template not reported",
                        "The model does not report a chat template. Chat formatting may require manual configuration.",
                        InspectionContentStatus.Warning,
                        "Warning")
                ];

        InspectionContentCardPresentation content = new()
        {
            Mode = definition.ContentMode,
            SectionTitle = definition.SectionTitle,
            Items = items,
            SupportingText = definition.SupportingText,
            SupportingTextVisibility = Visibility.Visible,
            DiagnosticCode = result.Findings.Count == 0
                ? NotReported
                : SupportedWarningCode,
            DiagnosticStatus = definition.DefaultContentStatus,
            DiagnosticCodeVisibility = Visibility.Visible,
            DisclosureStatus = definition.DefaultContentStatus,
            DisclosureSummary = definition.DisclosureSummary,
            CollapsedDisclosureText = definition.CollapsedDisclosureText,
            ExpandedDisclosureText = definition.ExpandedDisclosureText,
            DisclosureAutomationName = definition.DisclosureAutomationName,
            DisclosureVisibility = definition.HasContentDisclosure
                ? Visibility.Visible
                : Visibility.Collapsed,
            IsExpanded = definition.HasContentDisclosure && expanded,
            ExpandedItems = definition.HasContentDisclosure
                ? items
                : Array.Empty<InspectionContentItemPresentation>(),
            TechnicalDetailsVisibility = definition.ShowTechnicalDetailsAction
                ? Visibility.Visible
                : Visibility.Collapsed,
            TechnicalDetailsActionText = "View technical details",
            TechnicalDetailsAutomationName = "View technical details",
            IsTechnicalDetailsEnabled = false,
            TechnicalDetailsAutomationHelpText =
                definition.ShowTechnicalDetailsAction
                    ? ComingLater
                    : string.Empty
        };
        return content;
    }

    private static InspectionActionCardPresentation CreateCompletedActions(
        ModelInspectionOutcome outcome,
        ModelInspectionPresentationCommands commands)
    {
        InspectionActionPresentation choose = CreateActiveAction(
            "choose-another",
            "Choose another model",
            "Choose another model",
            commands.ChooseAnother);
        InspectionActionPresentation report = CreateFutureAction(
            "technical-report",
            "View technical report",
            "View technical inspection report");

        return outcome switch
        {
            ModelInspectionOutcome.Ready => new()
            {
                Mode = InspectionActionCardMode.Result,
                Title = "Model is ready",
                Message = "Choose another model or review future next steps.",
                AutomationName = "Actions after model inspection",
                SecondaryActionOne = choose,
                SecondaryActionTwo = report,
                PrimaryAction = CreateFutureAction(
                    "hardware-fit",
                    "Check hardware fit",
                    "Check model hardware fit")
            },
            ModelInspectionOutcome.ReadyWithWarnings => new()
            {
                Mode = InspectionActionCardMode.Result,
                Title = "Model is ready with warnings",
                Message = "Choose another model or review future next steps.",
                AutomationName = "Actions after model inspection",
                SecondaryActionOne = choose,
                SecondaryActionTwo = report,
                PrimaryAction = CreateFutureAction(
                    "continue-hardware",
                    "Continue to hardware check",
                    "Continue to model hardware check")
            },
            ModelInspectionOutcome.ConversionRequired => new()
            {
                Mode = InspectionActionCardMode.Result,
                Title = "Prepare this model",
                Message = "Conversion actions are not available yet.",
                AutomationName = "Model conversion actions",
                SecondaryActionOne = choose,
                SecondaryActionTwo = report,
                PrimaryAction = CreateFutureAction(
                    "conversion-format",
                    "Choose conversion format",
                    "Choose model conversion format")
            },
            ModelInspectionOutcome.IncompletePackage => new()
            {
                Mode = InspectionActionCardMode.Result,
                Title = "Model package is incomplete",
                Message = "Choose another model to continue.",
                AutomationName = "Model inspection result actions",
                SecondaryActionOne = choose,
                SecondaryActionTwo = report,
                PrimaryAction = CreateActiveAction(
                    "locate-missing",
                    "Locate missing file",
                    "Locate missing model file",
                    commands.ChooseAnother)
            },
            ModelInspectionOutcome.Unsupported => new()
            {
                Mode = InspectionActionCardMode.Result,
                Title = "Model is not supported",
                Message = "Choose another model to continue.",
                AutomationName = "Model inspection result actions",
                SecondaryActionOne = report,
                PrimaryAction = choose
            },
            ModelInspectionOutcome.Invalid => new()
            {
                Mode = InspectionActionCardMode.Result,
                Title = "Model is invalid",
                Message = "Choose another model to continue.",
                AutomationName = "Invalid model actions",
                SecondaryActionOne = report,
                PrimaryAction = choose
            },
            _ => throw new ArgumentOutOfRangeException(nameof(outcome), outcome, null)
        };
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

    private static InspectionActionPresentation CreateActiveAction(
        string actionId,
        string text,
        string automationName,
        ICommand command,
        bool? enabled = null,
        double minimumWidth = 174d)
    {
        bool canExecute = enabled ?? command.CanExecute(parameter: null);
        return new InspectionActionPresentation
        {
            Text = text,
            Command = command,
            IsEnabled = canExecute,
            Visibility = Visibility.Visible,
            AutomationName = automationName,
            ActionId = actionId,
            MinimumWidth = minimumWidth
        };
    }

    private static InspectionActionPresentation CreateFutureAction(
        string actionId,
        string text,
        string automationName)
    {
        return new InspectionActionPresentation
        {
            Text = text,
            Command = null,
            IsEnabled = false,
            Visibility = Visibility.Visible,
            AutomationName = automationName,
            ActionId = actionId,
            AutomationHelpText = ComingLater
        };
    }

    private static ModelInspectionRegionKey CreateActionsRegionKey(
        string outcomeIdentity,
        InspectionActionCardPresentation actions)
    {
        List<string> values =
        [
            outcomeIdentity,
            EnumIdentity(actions.Mode),
            actions.Title,
            actions.Message,
            actions.AutomationName
        ];
        AddActionValues(values, actions.CancelAction);
        AddActionValues(values, actions.SecondaryActionOne);
        AddActionValues(values, actions.SecondaryActionTwo);
        AddActionValues(values, actions.PrimaryAction);
        return CreateSemanticRegionKey("actions", values);
    }

    private static ModelInspectionRegionKey CreateOutcomeRegionKey(
        string outcomeIdentity,
        InspectionOutcomePresentation outcome)
    {
        return CreateSemanticRegionKey(
            "outcome",
            [
                outcomeIdentity,
                EnumIdentity(outcome.Kind),
                EnumIdentity(outcome.Tone),
                EnumIdentity(outcome.IconSymbol),
                outcome.Title,
                outcome.Message,
                outcome.AutomationName
            ]);
    }

    private static ModelInspectionRegionKey CreateModelRegionKey(
        string outcomeIdentity,
        InspectionModelCardPresentation model)
    {
        List<string> values =
        [
            outcomeIdentity,
            EnumIdentity(model.DisplayMode),
            EnumIdentity(model.BadgeState),
            model.ModelName,
            model.CompactSummary,
            model.FormatShortName,
            model.OverviewFormatBadgeText,
            model.Publisher,
            model.FormatName,
            model.Quantisation,
            model.ParameterCount,
            model.ModelType,
            model.DeclaredContext,
            model.FileSize,
            EnumIdentity(model.InspectionDetailsVisibility),
            BooleanIdentity(model.IsInspectionDetailsExpanded)
        ];
        if (model.DisplayMode == InspectionModelCardMode.Detailed)
        {
            values.Add(model.InspectionChecksSummary);
            values.Add(model.InspectionChecks.Count.ToString(
                CultureInfo.InvariantCulture));
            foreach (InspectionCheckPresentation check in model.InspectionChecks)
            {
                values.Add(check.Title);
                values.Add(check.Detail);
                values.Add(EnumIdentity(check.Status));
                values.Add(check.StatusText);
                values.Add(check.AutomationName);
            }
        }

        return CreateSemanticRegionKey("model", values);
    }

    private static ModelInspectionRegionKey CreateContentRegionKey(
        string outcomeIdentity,
        InspectionContentCardPresentation content)
    {
        List<string> values =
        [
            outcomeIdentity,
            EnumIdentity(content.Mode),
            content.SectionTitle,
            EnumIdentity(content.Startup.Visibility),
            content.Startup.Summary,
            content.Startup.AutomationName
        ];
        if (content.Mode != InspectionContentCardMode.Progress)
        {
            values.Add(content.ProgressSummary);
            AddContentItems(values, content.Items);
        }

        values.Add(content.SupportingText);
        values.Add(EnumIdentity(content.SupportingTextVisibility));
        values.Add(content.TertiaryText);
        values.Add(EnumIdentity(content.TertiaryStatus));
        values.Add(EnumIdentity(content.TertiaryTextVisibility));
        values.Add(content.DiagnosticCode);
        values.Add(EnumIdentity(content.DiagnosticStatus));
        values.Add(EnumIdentity(content.DiagnosticCodeVisibility));
        values.Add(EnumIdentity(content.DisclosureStatus));
        values.Add(content.DisclosureSummary);
        values.Add(content.CollapsedDisclosureText);
        values.Add(content.ExpandedDisclosureText);
        values.Add(content.DisclosureAutomationName);
        values.Add(BooleanIdentity(content.IsExpanded));
        values.Add(EnumIdentity(content.DisclosureVisibility));
        AddContentItems(values, content.ExpandedItems);
        values.Add(content.TechnicalDetailsActionText);
        values.Add(content.TechnicalDetailsAutomationName);
        values.Add(BooleanIdentity(content.IsTechnicalDetailsEnabled));
        values.Add(content.TechnicalDetailsAutomationHelpText);
        values.Add(EnumIdentity(content.TechnicalDetailsVisibility));
        return CreateSemanticRegionKey("content", values);
    }

    private static void AddContentItems(
        List<string> values,
        IReadOnlyList<InspectionContentItemPresentation> items)
    {
        values.Add(items.Count.ToString(CultureInfo.InvariantCulture));
        foreach (InspectionContentItemPresentation item in items)
        {
            values.Add(item.StageNumber);
            values.Add(item.Title);
            values.Add(item.Detail);
            values.Add(EnumIdentity(item.DetailVisibility));
            values.Add(EnumIdentity(item.Status));
            values.Add(item.StatusText);
            values.Add(BooleanIdentity(item.IsActive));
            values.Add(DoubleIdentity(item.StageFraction));
            values.Add(BooleanIdentity(item.ShowConnector));
            values.Add(item.AutomationName);
        }
    }

    private static void AddActionValues(
        List<string> values,
        InspectionActionPresentation action)
    {
        values.Add(action.ActionId);
        values.Add(action.Text);
        values.Add(BooleanIdentity(action.Command is not null));
        values.Add(BooleanIdentity(
            action.Command?.CanExecute(parameter: null) is true));
        values.Add(BooleanIdentity(action.IsEnabled));
        values.Add(EnumIdentity(action.Visibility));
        values.Add(action.AutomationName);
        values.Add(action.AutomationHelpText);
        values.Add(action.MinimumWidth.ToString(
            "R",
            CultureInfo.InvariantCulture));
    }

    private static ModelInspectionRegionKey CreateSemanticRegionKey(
        string region,
        IEnumerable<string?> values)
    {
        StringBuilder identity = new();
        foreach (string? value in values)
        {
            string safeValue = value ?? "<null>";
            identity.Append(safeValue.Length.ToString(CultureInfo.InvariantCulture));
            identity.Append(':');
            identity.Append(safeValue);
        }

        string digest = Convert.ToHexString(
            SHA256.HashData(Encoding.UTF8.GetBytes(identity.ToString())));
        return new ModelInspectionRegionKey($"{region}:{digest}");
    }

    private static string EnumIdentity<TEnum>(TEnum value)
        where TEnum : struct, Enum => Convert.ToInt64(
            value,
            CultureInfo.InvariantCulture).ToString(CultureInfo.InvariantCulture);

    private static string BooleanIdentity(bool value) => value ? "1" : "0";

    private static string DoubleIdentity(double? value) => value?.ToString(
        "R",
        CultureInfo.InvariantCulture) ?? "none";

    private static ModelInspectionRegionKey CreateAnnouncementsRegionKey(
        ModelInspectionRenderKey renderKey,
        ModelInspectionFigmaState state,
        string outcomeIdentity,
        ModelInspectionProgressRegionKey progress)
    {
        if (state != ModelInspectionFigmaState.InspectionProgress)
        {
            return new ModelInspectionRegionKey(
                $"announcements:terminal:{renderKey.AttemptGeneration}:" +
                outcomeIdentity);
        }

        string detailIdentity = Convert.ToHexString(
            SHA256.HashData(Encoding.UTF8.GetBytes(progress.Detail)));
        return new ModelInspectionRegionKey(
            $"announcements:progress:{renderKey.AttemptGeneration}:" +
            $"{(int?)progress.Stage ?? 0}:{(int?)progress.StageStatus ?? -1}:" +
            $"{progress.CompletedStageCount}:{progress.StageCount}:" +
            detailIdentity);
    }

    private static bool HasSupportedFindings(ModelInspectionResult result)
    {
        if (result.Outcome == ModelInspectionOutcome.ReadyWithWarnings)
        {
            return result.Findings.Count == 1 &&
                string.Equals(
                    result.Findings[0].Code,
                    SupportedWarningCode,
                    StringComparison.Ordinal) &&
                result.Findings[0].Severity ==
                    ModelInspectionFindingSeverity.Warning;
        }

        return result.Findings.Count == 0;
    }

    private static bool HasSupportedEvidence(
        ModelInspectionRequest request,
        ModelInspectionResult result)
    {
        ModelInspectionRuntimeIdentity runtime = result.Evidence.Runtime;
        bool approvedRuntime = string.Equals(
                runtime.WorkerId,
                WorkerProtocol.WorkerId,
                StringComparison.Ordinal) &&
            runtime.ProtocolVersion == WorkerProtocol.Version &&
            string.Equals(
                runtime.RuntimeProfile,
                WorkerProtocol.RuntimeProfile,
                StringComparison.Ordinal) &&
            string.Equals(
                runtime.LLamaSharpVersion,
                ExpectedLlamaSharpVersion,
                StringComparison.Ordinal) &&
            string.Equals(
                runtime.BackendPackageVersion,
                ExpectedBackendPackageVersion,
                StringComparison.Ordinal) &&
            string.Equals(
                runtime.MappedLlamaCppCommit,
                ExpectedMappedLlamaCppCommit,
                StringComparison.OrdinalIgnoreCase) &&
            string.Equals(
                runtime.NativeLibraryName,
                ExpectedNativeLibraryName,
                StringComparison.Ordinal) &&
            string.Equals(
                runtime.ProcessArchitecture,
                ExpectedProcessArchitecture,
                StringComparison.Ordinal) &&
            string.Equals(
                runtime.InspectionMode,
                ExpectedInspectionMode,
                StringComparison.Ordinal) &&
            !runtime.UsesCuda &&
            !runtime.UsesVulkan &&
            runtime.GpuLayerCount == 0;
        bool trustedTokenizer =
            result.Evidence.Tokenizer.TokenizerSmokePassed is true &&
            result.Evidence.Tokenizer.TokenizerSmokeTokenCount is > 0;
        bool outcomeMatchesChatTemplate = result.Outcome switch
        {
            ModelInspectionOutcome.Ready =>
                result.Evidence.ChatTemplate.Present is true,
            ModelInspectionOutcome.ReadyWithWarnings =>
                result.Evidence.ChatTemplate.Present is false,
            _ => true
        };

        return string.Equals(
                result.Evidence.Configuration.Format,
                request.QuickScan.Format,
                StringComparison.Ordinal) &&
            approvedRuntime &&
            trustedTokenizer &&
            outcomeMatchesChatTemplate &&
            HasSupportedFindings(result);
    }

    private static ModelInspectionFigmaState GetCompletedState(
        ModelInspectionOutcome outcome,
        bool expanded)
    {
        return outcome switch
        {
            ModelInspectionOutcome.Ready => expanded
                ? ModelInspectionFigmaState.ReadyExpanded
                : ModelInspectionFigmaState.ReadyCollapsed,
            ModelInspectionOutcome.ReadyWithWarnings => expanded
                ? ModelInspectionFigmaState.ReadyWithWarningsExpanded
                : ModelInspectionFigmaState.ReadyWithWarningsCollapsed,
            ModelInspectionOutcome.ConversionRequired => expanded
                ? ModelInspectionFigmaState.ConversionRequiredExpanded
                : ModelInspectionFigmaState.ConversionRequiredCollapsed,
            ModelInspectionOutcome.IncompletePackage =>
                ModelInspectionFigmaState.IncompletePackage,
            ModelInspectionOutcome.Unsupported =>
                ModelInspectionFigmaState.Unsupported,
            ModelInspectionOutcome.Invalid => expanded
                ? ModelInspectionFigmaState.InvalidExpanded
                : ModelInspectionFigmaState.InvalidCollapsed,
            _ => throw new ArgumentOutOfRangeException(nameof(outcome), outcome, null)
        };
    }

    private static string GetOutcomeIdentity(ModelInspectionFigmaState state)
    {
        return state switch
        {
            ModelInspectionFigmaState.InspectionProgress => "progress",
            ModelInspectionFigmaState.ReadyCollapsed or
            ModelInspectionFigmaState.ReadyExpanded => "ready",
            ModelInspectionFigmaState.ReadyWithWarningsCollapsed or
            ModelInspectionFigmaState.ReadyWithWarningsExpanded => "warnings",
            ModelInspectionFigmaState.ConversionRequiredCollapsed or
            ModelInspectionFigmaState.ConversionRequiredExpanded => "conversion",
            ModelInspectionFigmaState.IncompletePackage => "incomplete",
            ModelInspectionFigmaState.Unsupported => "unsupported",
            ModelInspectionFigmaState.InvalidCollapsed or
            ModelInspectionFigmaState.InvalidExpanded => "invalid",
            ModelInspectionFigmaState.Cancelled => "cancelled",
            ModelInspectionFigmaState.OperationalFailure => "failure",
            _ => throw new ArgumentOutOfRangeException(nameof(state), state, null)
        };
    }

    private static ModelInspectionProgressRegionKey EmptyProgressKey() => new(
        stage: null,
        stageStatus: null,
        completedStageCount: 0,
        stageCount: 0,
        stageFraction: null,
        detail: string.Empty);

    private static InspectionProgressRowsUpdate CreateEmptyProgressRowsUpdate(
        ModelInspectionRenderKey renderKey) => new(
            EmptyProgressKey(),
            renderKey,
            "0 of 5 checks complete");

    private static string ProjectParameters(
        string? quickScanLabel,
        ulong? parameterCount)
    {
        string projected = ModelInspectionDisplayTextPolicy.ProjectOptionalLabel(
            quickScanLabel);
        if (projected != NotReported)
        {
            return projected;
        }

        return parameterCount.HasValue
            ? $"{parameterCount.Value.ToString("N0", CultureInfo.CurrentCulture)} parameters"
            : NotReported;
    }

    private static string FormatFileSize(long bytes)
    {
        string[] units = ["B", "KB", "MB", "GB"];
        decimal value = bytes;
        int unit = 0;
        while (value >= 1024 && unit < units.Length - 1)
        {
            value /= 1024;
            unit++;
        }

        string format = decimal.Truncate(value) == value ? "N0" : "N2";
        return $"{value.ToString(format, CultureInfo.CurrentCulture)} {units[unit]}";
    }

    private static string WithParameterNoun(string parameters) =>
        parameters.EndsWith("parameters", StringComparison.OrdinalIgnoreCase)
            ? parameters
            : $"{parameters} parameters";

    private static string FormatNullable<T>(T? value)
        where T : struct, IFormattable => value.HasValue
            ? value.Value.ToString(null, CultureInfo.CurrentCulture)
            : NotReported;

    private static string FormatBoolean(bool? value) => value switch
    {
        true => "passed",
        false => "failed",
        null => NotReported
    };

    private static string FormatPresence(bool? value) => value switch
    {
        true => "present",
        false => "not reported",
        null => NotReported
    };

    private static OutcomeDefinition GetDefinition(ModelInspectionOutcome outcome)
    {
        return outcome switch
        {
            ModelInspectionOutcome.Ready => new(
                InspectionOutcomePresentationKind.Ready,
                InspectionOutcomeTone.Success,
                Symbol.Accept,
                "Model inspection complete",
                "The model passed all lightweight inspection checks.",
                "Model inspection complete.",
                InspectionContentCardMode.Hidden,
                InspectionModelBadgeState.Inspected,
                "Inspection complete",
                "Inspection complete",
                "Model inspection completed successfully.",
                "Choose another model if needed.",
                InspectionContentStatus.Passed,
                "Passed",
                InspectionFooterStatus.Complete,
                HasDisclosure: true,
                HasContentDisclosure: false,
                DisclosureSummary: "All 5 inspection checks passed",
                CollapsedDisclosureText: "View inspection details",
                ExpandedDisclosureText: "Hide inspection details",
                DisclosureAutomationName: "Model inspection details",
                ShowTechnicalDetailsAction: false),
            ModelInspectionOutcome.ReadyWithWarnings => new(
                InspectionOutcomePresentationKind.ReadyWithWarnings,
                InspectionOutcomeTone.Warning,
                Symbol.Important,
                "Model inspected with warnings",
                "Inspection completed with a non-blocking warning.",
                "Model inspected with warnings.",
                InspectionContentCardMode.Warnings,
                InspectionModelBadgeState.Inspected,
                "Inspected with warnings",
                "Inspection warnings",
                "Inspection warning",
                "Review the warning before continuing.",
                InspectionContentStatus.Warning,
                "Warning",
                InspectionFooterStatus.Complete,
                HasDisclosure: true,
                HasContentDisclosure: true,
                DisclosureSummary: "Review inspection warning",
                CollapsedDisclosureText: "View full details",
                ExpandedDisclosureText: "Hide full details",
                DisclosureAutomationName: "Inspection warning details",
                ShowTechnicalDetailsAction: false),
            ModelInspectionOutcome.ConversionRequired => new(
                InspectionOutcomePresentationKind.ConversionRequired,
                InspectionOutcomeTone.Information,
                Symbol.Switch,
                "Conversion required",
                "The selected model requires conversion before it can continue.",
                "Model conversion is required.",
                InspectionContentCardMode.ConversionRequired,
                InspectionModelBadgeState.SourceModel,
                "Conversion required",
                "Conversion required",
                "Conversion required",
                "Choose another model or review the expected converted output.",
                InspectionContentStatus.Information,
                "Information",
                InspectionFooterStatus.NotComplete,
                HasDisclosure: true,
                HasContentDisclosure: true,
                DisclosureSummary: "Expected converted model output",
                CollapsedDisclosureText: "View expected output",
                ExpandedDisclosureText: "Hide expected output",
                DisclosureAutomationName: "Expected conversion output",
                ShowTechnicalDetailsAction: false),
            ModelInspectionOutcome.IncompletePackage => new(
                InspectionOutcomePresentationKind.IncompletePackage,
                InspectionOutcomeTone.Warning,
                Symbol.Important,
                "Model package is incomplete",
                "The model package is missing required content.",
                "Model package is incomplete.",
                InspectionContentCardMode.IncompletePackage,
                InspectionModelBadgeState.Incomplete,
                "Incomplete package",
                "Incomplete model package",
                "Required package content was not reported.",
                "Locate the missing file or choose another model.",
                InspectionContentStatus.Warning,
                "Incomplete",
                InspectionFooterStatus.NotComplete,
                HasDisclosure: false,
                HasContentDisclosure: false,
                DisclosureSummary: string.Empty,
                CollapsedDisclosureText: string.Empty,
                ExpandedDisclosureText: string.Empty,
                DisclosureAutomationName: string.Empty,
                ShowTechnicalDetailsAction: true),
            ModelInspectionOutcome.Unsupported => new(
                InspectionOutcomePresentationKind.Unsupported,
                InspectionOutcomeTone.Error,
                Symbol.Cancel,
                "Model is not supported",
                "The selected model is not supported by this inspection route.",
                "Model is not supported.",
                InspectionContentCardMode.Unsupported,
                InspectionModelBadgeState.Unsupported,
                "Unsupported model",
                "Unsupported model",
                "Runtime support was not reported for this model.",
                "Choose another model.",
                InspectionContentStatus.Error,
                "Unsupported",
                InspectionFooterStatus.NotComplete,
                HasDisclosure: false,
                HasContentDisclosure: false,
                DisclosureSummary: string.Empty,
                CollapsedDisclosureText: string.Empty,
                ExpandedDisclosureText: string.Empty,
                DisclosureAutomationName: string.Empty,
                ShowTechnicalDetailsAction: true),
            ModelInspectionOutcome.Invalid => new(
                InspectionOutcomePresentationKind.Invalid,
                InspectionOutcomeTone.Error,
                Symbol.Cancel,
                "Model is invalid",
                "The model package did not pass structural validation.",
                "Model is invalid.",
                InspectionContentCardMode.Invalid,
                InspectionModelBadgeState.Invalid,
                "Invalid model",
                "Invalid model",
                "Structural validation did not produce a usable model result.",
                "Choose another model.",
                InspectionContentStatus.Error,
                "Invalid",
                InspectionFooterStatus.NotComplete,
                HasDisclosure: true,
                HasContentDisclosure: true,
                DisclosureSummary: "Technical validation report",
                CollapsedDisclosureText: "View technical report",
                ExpandedDisclosureText: "Hide technical report",
                DisclosureAutomationName: "Model validation report",
                ShowTechnicalDetailsAction: false),
            _ => throw new ArgumentOutOfRangeException(nameof(outcome), outcome, null)
        };
    }

    private sealed record OutcomeDefinition(
        InspectionOutcomePresentationKind Kind,
        InspectionOutcomeTone Tone,
        Symbol Symbol,
        string Title,
        string FixedMessage,
        string OutcomeAnnouncement,
        InspectionContentCardMode ContentMode,
        InspectionModelBadgeState Badge,
        string ModelStatusSummary,
        string SectionTitle,
        string GenericRowTitle,
        string GenericRowDetail,
        InspectionContentStatus DefaultContentStatus,
        string DefaultStatusText,
        InspectionFooterStatus FooterStatus,
        bool HasDisclosure,
        bool HasContentDisclosure,
        string DisclosureSummary,
        string CollapsedDisclosureText,
        string ExpandedDisclosureText,
        string DisclosureAutomationName,
        bool ShowTechnicalDetailsAction)
    {
        internal string SupportingText => GenericRowDetail;
    }

}
