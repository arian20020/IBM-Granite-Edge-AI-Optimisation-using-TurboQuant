using System.Globalization;
using System.Windows.Input;
using GraniteEdgeAI.Features.ModelInspection.Contracts;
using GraniteEdgeAI.Features.ModelInspection.Models;
using GraniteEdgeAI.Features.OpenVinoRoute;
using GraniteEdgeAI.Features.OpenVinoRoute.Inspection;
using Microsoft.UI.Xaml;

namespace GraniteEdgeAI.Features.ModelInspection.Presentation;

internal sealed record OpenVinoReadyPresentation(
    InspectionModelCardPresentation Model,
    InspectionContentCardPresentation Content,
    InspectionOutcomePresentation Outcome,
    InspectionActionCardPresentation Actions);

internal static class OpenVinoModelInspectionPresentationFactory
{
    private const string MissingChatTemplateWarningCode =
        "MI-WARN-CHAT-TEMPLATE-MISSING";

    internal static ModelInspectionProgress CreateProgress(
        OpenVinoRouteInspectionProgress progress)
    {
        ArgumentNullException.ThrowIfNull(progress);
        ModelInspectionStage stage = (ModelInspectionStage)((int)progress.Stage + 1);
        ModelInspectionStageStatus status = progress.Status ==
            OpenVinoRouteInspectionStageStatus.Completed
                ? ModelInspectionStageStatus.Completed
                : ModelInspectionStageStatus.Active;
        int completed = status == ModelInspectionStageStatus.Completed
            ? (int)stage
            : (int)stage - 1;
        return new ModelInspectionProgress(
            stage,
            status,
            completed,
            totalStageCount: 5,
            status == ModelInspectionStageStatus.Active
                ? progress.StageFraction
                : null,
            Message(stage, status));
    }

    internal static OpenVinoReadyPresentation CreateReady(
        string displayName,
        OpenVinoStaticPackageEvidence evidence,
        bool hasWarnings,
        bool isInspectionDetailsExpanded,
        ICommand chooseAnother,
        ICommand? checkHardware)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(displayName);
        ArgumentNullException.ThrowIfNull(evidence);
        ArgumentNullException.ThrowIfNull(chooseAnother);

        InspectionModelCardPresentation model = new()
        {
            DisplayMode = hasWarnings
                ? InspectionModelCardMode.Compact
                : InspectionModelCardMode.Detailed,
            BadgeState = InspectionModelBadgeState.Inspected,
            ModelName = displayName,
            CompactSummary = $"OpenVINO · {evidence.Precision} · {FormatBytes(evidence.ModelLengthBytes)}",
            FormatShortName = "OV",
            OverviewFormatBadgeText = "OpenVINO",
            Publisher = "Not reported",
            FormatName = "OpenVINO GenAI IR",
            Quantisation = evidence.Precision,
            ParameterCount = "Not reported",
            ModelType = evidence.ModelType,
            DeclaredContext = evidence.ContextLength.ToString("N0", CultureInfo.InvariantCulture),
            FileSize = FormatBytes(evidence.ModelLengthBytes),
            InspectionChecksSummary = hasWarnings
                ? "Inspected with warnings"
                : "All 5 inspection checks passed",
            InspectionChecks = hasWarnings
                ? Array.Empty<InspectionCheckPresentation>()
                : CreateChecks(evidence, hasWarnings: false),
            InspectionDetailsVisibility = hasWarnings
                ? Visibility.Collapsed
                : Visibility.Visible,
            IsInspectionDetailsExpanded = !hasWarnings &&
                isInspectionDetailsExpanded
        };
        IReadOnlyList<InspectionContentItemPresentation> warningItems =
        [
            new InspectionContentItemPresentation
            {
                Title = "Chat template not reported",
                DefaultDetail = "The model does not report a chat template. Chat formatting may require manual configuration.",
                Status = InspectionContentStatus.Warning,
                StatusText = "Warning",
                AutomationName = "Chat template not reported. Warning."
            }
        ];
        InspectionContentCardPresentation content = hasWarnings
            ? new InspectionContentCardPresentation
            {
                Mode = InspectionContentCardMode.Warnings,
                SectionTitle = "Inspection warnings",
                Items = warningItems,
                SupportingText = "Review the warning before continuing.",
                SupportingTextVisibility = Visibility.Visible,
                DiagnosticCode = MissingChatTemplateWarningCode,
                DiagnosticStatus = InspectionContentStatus.Warning,
                DiagnosticCodeVisibility = Visibility.Visible,
                DisclosureStatus = InspectionContentStatus.Warning,
                DisclosureSummary = "Review inspection warning",
                CollapsedDisclosureText = "View full details",
                ExpandedDisclosureText = "Hide full details",
                DisclosureAutomationName = "Inspection warning details",
                DisclosureVisibility = Visibility.Visible,
                IsExpanded = isInspectionDetailsExpanded,
                ExpandedItems = isInspectionDetailsExpanded
                    ? warningItems
                    : Array.Empty<InspectionContentItemPresentation>()
            }
            : InspectionContentCardPresentation.Hidden;
        InspectionOutcomePresentation outcome = new()
        {
            Kind = hasWarnings
                ? InspectionOutcomePresentationKind.ReadyWithWarnings
                : InspectionOutcomePresentationKind.Ready,
            Tone = hasWarnings ? InspectionOutcomeTone.Warning : InspectionOutcomeTone.Success,
            GlyphKind = hasWarnings
                ? InspectionStatusGlyphKind.Warning
                : InspectionStatusGlyphKind.Success,
            Title = hasWarnings ? "Model inspected with warnings" : "Model inspection complete",
            Message = hasWarnings
                ? "Inspection completed with a non-blocking warning."
                : "The model passed all lightweight inspection checks.",
            AutomationName = hasWarnings
                ? "Model inspected with warnings."
                : "Model inspection complete."
        };
        InspectionActionPresentation choose = ActiveAction(
            "choose-another",
            "Choose another model",
            "Choose another model",
            chooseAnother);
        InspectionActionPresentation report = new()
        {
            Text = "View technical report",
            AutomationName = "View technical inspection report",
            ActionId = "technical-report",
            Visibility = Visibility.Visible,
            IsEnabled = false,
            AutomationHelpText = "Coming later"
        };
        string hardwareId = hasWarnings ? "continue-hardware" : "hardware-fit";
        string hardwareText = hasWarnings
            ? "Continue to hardware check"
            : "Check hardware fit";
        string hardwareAutomationName = hasWarnings
            ? "Continue to model hardware check"
            : "Check model hardware fit";
        InspectionActionPresentation hardware = checkHardware is not null &&
            checkHardware.CanExecute(null)
                ? ActiveAction(
                    hardwareId,
                    hardwareText,
                    hardwareAutomationName,
                    checkHardware)
                : new InspectionActionPresentation
                {
                    Text = hardwareText,
                    AutomationName = hardwareAutomationName,
                    ActionId = hardwareId,
                    Visibility = Visibility.Visible,
                    IsEnabled = false,
                    AutomationHelpText = "Hardware inspection is unavailable in this build."
                };
        InspectionActionCardPresentation actions = new()
        {
            Mode = InspectionActionCardMode.Result,
            Title = hasWarnings ? "Model is ready with warnings" : "Model is ready",
            Message = "Choose another model or review future next steps.",
            AutomationName = "Actions after model inspection",
            SecondaryActionOne = choose,
            SecondaryActionTwo = report,
            PrimaryAction = hardware
        };
        return new OpenVinoReadyPresentation(model, content, outcome, actions);
    }

    private static IReadOnlyList<InspectionCheckPresentation> CreateChecks(
        OpenVinoStaticPackageEvidence evidence,
        bool hasWarnings) =>
    [
        Check("Model package", "Required OpenVINO resources and secure hashes were verified."),
        Check("Model configuration", $"{evidence.Architecture} configuration was read."),
        new InspectionCheckPresentation
        {
            Title = "Tokenizer and chat setup",
            Detail = hasWarnings
                ? "Tokenizer resources passed; no embedded chat template was reported."
                : "Tokenizer, detokenizer, and chat-template resources passed.",
            Status = hasWarnings ? InspectionCheckStatus.Warning : InspectionCheckStatus.Passed,
            StatusText = hasWarnings ? "Warning" : "Passed",
            AutomationName = hasWarnings
                ? "Tokenizer and chat setup. Warning."
                : "Tokenizer and chat setup. Passed."
        },
        Check("Model structure", "The OpenVINO model graph passed native parsing."),
        Check("Core runtime compatibility", "The verified OpenVINO runtime accepted the package.")
    ];

    private static InspectionCheckPresentation Check(string title, string detail) => new()
    {
        Title = title,
        Detail = detail,
        Status = InspectionCheckStatus.Passed,
        StatusText = "Passed",
        AutomationName = $"{title}. Passed. {detail}"
    };

    private static InspectionActionPresentation ActiveAction(
        string id,
        string text,
        string automationName,
        ICommand command) => new()
    {
        Text = text,
        AutomationName = automationName,
        ActionId = id,
        Visibility = Visibility.Visible,
        IsEnabled = command.CanExecute(null),
        Command = command
    };

    private static string Message(
        ModelInspectionStage stage,
        ModelInspectionStageStatus status)
    {
        bool complete = status == ModelInspectionStageStatus.Completed;
        return stage switch
        {
            ModelInspectionStage.CheckModelPackage => complete
                ? "Package files and secure hashes verified."
                : "Verifying package files and secure hashes.",
            ModelInspectionStage.ReadModelConfiguration => complete
                ? "Model configuration read."
                : "Reading model configuration.",
            ModelInspectionStage.ValidateTokenizerAndChatSetup => complete
                ? "Tokenizer and chat setup validated."
                : "Validating tokenizer and chat setup.",
            ModelInspectionStage.ValidateModelStructure => complete
                ? "Model structure validated."
                : "Validating model structure.",
            ModelInspectionStage.ConfirmCoreRuntimeCompatibility => complete
                ? "Core runtime compatibility confirmed."
                : "Confirming core runtime compatibility.",
            _ => throw new ArgumentOutOfRangeException(nameof(stage), stage, null)
        };
    }

    private static string FormatBytes(long bytes)
    {
        double gib = bytes / (1024d * 1024d * 1024d);
        return $"{gib:0.0} GB";
    }
}
