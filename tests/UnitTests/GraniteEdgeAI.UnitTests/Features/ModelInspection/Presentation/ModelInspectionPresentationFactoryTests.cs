using GraniteEdgeAI.Features.ModelInspection.Contracts;
using GraniteEdgeAI.Features.ModelInspection.Models;
using GraniteEdgeAI.Features.ModelInspection.Presentation;
using Microsoft.UI.Xaml;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.VisualStudio.TestTools.UnitTesting.AppContainer;
using System.Windows.Input;

namespace GraniteEdgeAI.UnitTests.Features.ModelInspection.Presentation;

[TestClass]
public sealed class ModelInspectionPresentationFactoryTests
{
    [UITestMethod]
    [TestCategory("WinUI")]
    public void CreateInitial_UsesTheRequestAndLiveCancelCommand()
    {
        ModelInspectionRequest request = PresentationTestData.CreateRequest();
        RecordingCommand cancelCommand = PresentationTestData.CreateCommand();

        ModelInspectionPagePresentation presentation =
            ModelInspectionPresentationFactory.CreateInitial(
                request,
                cancelCommand);

        Assert.AreEqual("granite.gguf", presentation.ModelCard.ModelName);
        Assert.AreEqual(
            InspectionModelBadgeState.ModelSelected,
            presentation.ModelCard.BadgeState);
        Assert.AreEqual(
            InspectionOutcomePresentationKind.Hidden,
            presentation.OutcomeCard.Kind);
        Assert.AreEqual(
            InspectionContentCardMode.Progress,
            presentation.ContentCard.Mode);
        Assert.AreEqual(
            InspectionActionCardMode.Inspecting,
            presentation.ActionCard.Mode);
        Assert.AreSame(
            cancelCommand,
            presentation.ActionCard.CancelAction.Command);
        Assert.AreEqual(
            Visibility.Visible,
            presentation.ActionCard.CancelAction.Visibility);
        Assert.IsTrue(presentation.ActionCard.CancelAction.IsEnabled);
    }

    [UITestMethod]
    [TestCategory("WinUI")]
    public void CreateProgress_ProjectsTheLatestFiveStageSnapshot()
    {
        ModelInspectionRequest request = PresentationTestData.CreateRequest();
        RecordingCommand cancelCommand =
            PresentationTestData.CreateCommand(canExecute: false);
        ModelInspectionProgress progress = new(
            ModelInspectionStage.ValidateTokenizerAndChatSetup,
            ModelInspectionStageStatus.Active,
            completedStageCount: 2,
            totalStageCount: 5,
            stageFraction: 0.5,
            userMessage: "Checking tokenizer evidence.");

        ModelInspectionPagePresentation presentation =
            ModelInspectionPresentationFactory.CreateProgress(
                request,
                progress,
                cancelCommand);

        Assert.AreEqual("2 of 5 checks complete", presentation.ContentCard.ProgressSummary);
        Assert.AreEqual(0.5, presentation.ContentCard.Items[2].StageFraction);
        Assert.AreSame(cancelCommand, presentation.ActionCard.CancelAction.Command);
        Assert.IsFalse(presentation.ActionCard.CancelAction.IsEnabled);
        Assert.AreEqual(
            InspectionOutcomePresentationKind.Hidden,
            presentation.OutcomeCard.Kind);
    }

    [TestMethod]
    [TestCategory("WinUI")]
    [DataRow(
        (int)ModelInspectionOutcome.Ready,
        InspectionOutcomePresentationKind.Ready,
        InspectionOutcomeTone.Success,
        InspectionContentCardMode.Hidden,
        InspectionModelBadgeState.Inspected)]
    [DataRow(
        (int)ModelInspectionOutcome.ReadyWithWarnings,
        InspectionOutcomePresentationKind.ReadyWithWarnings,
        InspectionOutcomeTone.Warning,
        InspectionContentCardMode.Warnings,
        InspectionModelBadgeState.Inspected)]
    [DataRow(
        (int)ModelInspectionOutcome.ConversionRequired,
        InspectionOutcomePresentationKind.ConversionRequired,
        InspectionOutcomeTone.Information,
        InspectionContentCardMode.ConversionRequired,
        InspectionModelBadgeState.SourceModel)]
    [DataRow(
        (int)ModelInspectionOutcome.IncompletePackage,
        InspectionOutcomePresentationKind.IncompletePackage,
        InspectionOutcomeTone.Warning,
        InspectionContentCardMode.IncompletePackage,
        InspectionModelBadgeState.Incomplete)]
    [DataRow(
        (int)ModelInspectionOutcome.Unsupported,
        InspectionOutcomePresentationKind.Unsupported,
        InspectionOutcomeTone.Error,
        InspectionContentCardMode.Unsupported,
        InspectionModelBadgeState.Unsupported)]
    [DataRow(
        (int)ModelInspectionOutcome.Invalid,
        InspectionOutcomePresentationKind.Invalid,
        InspectionOutcomeTone.Error,
        InspectionContentCardMode.Invalid,
        InspectionModelBadgeState.Invalid)]
    public void CreateTerminal_MapsEveryCompletedModelOutcome(
        int outcomeValue,
        InspectionOutcomePresentationKind expectedKind,
        InspectionOutcomeTone expectedTone,
        InspectionContentCardMode expectedContentMode,
        InspectionModelBadgeState expectedBadge)
    {
        ModelInspectionOutcome outcome = (ModelInspectionOutcome)outcomeValue;
        ModelInspectionExecutionResult execution =
            ModelInspectionExecutionResult.Completed(
                PresentationTestData.CreateResult(outcome));

        ModelInspectionPagePresentation presentation = CreateTerminal(execution);

        Assert.AreEqual(expectedKind, presentation.OutcomeCard.Kind);
        Assert.AreEqual(expectedTone, presentation.OutcomeCard.Tone);
        Assert.AreEqual(expectedContentMode, presentation.ContentCard.Mode);
        Assert.AreEqual(expectedBadge, presentation.ModelCard.BadgeState);
        Assert.AreEqual(InspectionActionCardMode.Result, presentation.ActionCard.Mode);
    }

    [TestMethod]
    [TestCategory("WinUI")]
    [DataRow((int)ModelInspectionOutcome.Ready)]
    [DataRow((int)ModelInspectionOutcome.ReadyWithWarnings)]
    [DataRow((int)ModelInspectionOutcome.ConversionRequired)]
    [DataRow((int)ModelInspectionOutcome.IncompletePackage)]
    [DataRow((int)ModelInspectionOutcome.Unsupported)]
    [DataRow((int)ModelInspectionOutcome.Invalid)]
    public void CreateTerminal_CompletedOutcomeOffersOnlyChooseAnother(
        int outcomeValue)
    {
        ModelInspectionOutcome outcome = (ModelInspectionOutcome)outcomeValue;
        RecordingCommand retryCommand = PresentationTestData.CreateCommand();
        RecordingCommand chooseCommand = PresentationTestData.CreateCommand();
        ModelInspectionExecutionResult execution =
            ModelInspectionExecutionResult.Completed(
                PresentationTestData.CreateResult(outcome));

        ModelInspectionPagePresentation presentation =
            ModelInspectionPresentationFactory.CreateTerminal(
                PresentationTestData.CreateRequest(),
                execution,
                retryCommand,
                chooseCommand);

        InspectionActionPresentation primary =
            presentation.ActionCard.PrimaryAction;
        Assert.AreEqual(Visibility.Visible, primary.Visibility);
        Assert.AreEqual("Choose another model", primary.Text);
        Assert.AreSame(chooseCommand, primary.Command);
        Assert.AreEqual(
            Visibility.Collapsed,
            presentation.ActionCard.SecondaryActionOne.Visibility);
        Assert.AreEqual(
            Visibility.Collapsed,
            presentation.ActionCard.SecondaryActionTwo.Visibility);
        Assert.AreNotSame(retryCommand, primary.Command);

        string allActionText = string.Join(
            " ",
            primary.Text,
            presentation.ActionCard.SecondaryActionOne.Text,
            presentation.ActionCard.SecondaryActionTwo.Text);
        Assert.IsFalse(allActionText.Contains("hardware", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(allActionText.Contains("convert", StringComparison.OrdinalIgnoreCase));
    }

    [UITestMethod]
    [TestCategory("WinUI")]
    public void CreateTerminal_CooperativeCancellationOffersRetryAndChoose()
    {
        ModelInspectionPagePresentation presentation = CreateTerminal(
            ModelInspectionExecutionResult.Cancelled(cooperative: true));

        Assert.AreEqual(
            InspectionOutcomePresentationKind.Cancelled,
            presentation.OutcomeCard.Kind);
        Assert.AreEqual(InspectionOutcomeTone.Neutral, presentation.OutcomeCard.Tone);
        Assert.AreEqual(
            InspectionContentCardMode.Cancelled,
            presentation.ContentCard.Mode);
        Assert.AreEqual(
            InspectionModelBadgeState.NotInspected,
            presentation.ModelCard.BadgeState);
        AssertRecoveryActions(presentation.ActionCard);
    }

    [UITestMethod]
    [TestCategory("WinUI")]
    public void CreateTerminal_OperationalFailureShowsOnlySafeMessageAndStableCode()
    {
        string forbiddenTechnicalDetail =
            $"{PresentationTestData.SensitiveTechnicalMarker}: stderr; " +
            "System.Exception -> inner; request_id=123.";
        ModelInspectionOperationalFailure failure =
            PresentationTestData.CreateFailure(forbiddenTechnicalDetail);

        ModelInspectionPagePresentation presentation = CreateTerminal(
            ModelInspectionExecutionResult.OperationalFailure(failure));

        Assert.AreEqual(
            InspectionOutcomePresentationKind.OperationalFailure,
            presentation.OutcomeCard.Kind);
        Assert.AreEqual(InspectionOutcomeTone.Error, presentation.OutcomeCard.Tone);
        Assert.AreEqual(
            InspectionContentCardMode.OperationalFailure,
            presentation.ContentCard.Mode);
        Assert.AreEqual(
            InspectionModelBadgeState.ResultUnknown,
            presentation.ModelCard.BadgeState);
        Assert.AreEqual(
            "Inspection could not be completed safely.",
            presentation.OutcomeCard.Message);
        Assert.AreEqual("MI-OP-TEST", presentation.ContentCard.DiagnosticCode);
        Assert.AreEqual(
            Visibility.Visible,
            presentation.ContentCard.DiagnosticCodeVisibility);
        AssertRecoveryActions(presentation.ActionCard);

        StringAssert.DoesNotContain(
            FlattenVisibleText(presentation),
            forbiddenTechnicalDetail,
            StringComparison.Ordinal);
    }

    [UITestMethod]
    [TestCategory("WinUI")]
    public void CreateTerminal_DoesNotExposePathsOrFindingTechnicalDetail()
    {
        ModelInspectionResult result = PresentationTestData.CreateResult(
            ModelInspectionOutcome.ReadyWithWarnings,
            technicalDetail:
                $"{PresentationTestData.SensitiveTechnicalMarker}: " +
                "raw chat template; stderr; exception chain; request ID.");

        ModelInspectionPagePresentation presentation = CreateTerminal(
            ModelInspectionExecutionResult.Completed(result));
        string visibleText = FlattenVisibleText(presentation);

        StringAssert.DoesNotContain(
            visibleText,
            PresentationTestData.PrivateDirectoryMarker,
            StringComparison.Ordinal);
        StringAssert.DoesNotContain(
            visibleText,
            PresentationTestData.SensitiveTechnicalMarker,
            StringComparison.Ordinal);
        Assert.AreEqual("granite.gguf", presentation.ModelCard.ModelName);
    }

    private static ModelInspectionPagePresentation CreateTerminal(
        ModelInspectionExecutionResult execution)
    {
        return ModelInspectionPresentationFactory.CreateTerminal(
            PresentationTestData.CreateRequest(),
            execution,
            PresentationTestData.CreateCommand(),
            PresentationTestData.CreateCommand());
    }

    private static void AssertRecoveryActions(
        InspectionActionCardPresentation actions)
    {
        Assert.AreEqual(InspectionActionCardMode.Result, actions.Mode);
        Assert.AreEqual("Choose another model", actions.SecondaryActionOne.Text);
        Assert.AreEqual(Visibility.Visible, actions.SecondaryActionOne.Visibility);
        Assert.AreEqual("Retry inspection", actions.PrimaryAction.Text);
        Assert.AreEqual(Visibility.Visible, actions.PrimaryAction.Visibility);
        Assert.AreEqual(Visibility.Collapsed, actions.SecondaryActionTwo.Visibility);
    }

    private static string FlattenVisibleText(
        ModelInspectionPagePresentation presentation)
    {
        IEnumerable<string> modelText =
        [
            presentation.ModelCard.ModelName,
            presentation.ModelCard.CompactSummary,
            presentation.ModelCard.FormatShortName,
            presentation.ModelCard.OverviewFormatBadgeText,
            presentation.ModelCard.Publisher,
            presentation.ModelCard.FormatName,
            presentation.ModelCard.Quantisation,
            presentation.ModelCard.ParameterCount,
            presentation.ModelCard.ModelType,
            presentation.ModelCard.DeclaredContext,
            presentation.ModelCard.FileSize,
            presentation.ModelCard.InspectionChecksSummary
        ];
        IEnumerable<string> outcomeText =
        [
            presentation.OutcomeCard.Title,
            presentation.OutcomeCard.Message,
            presentation.OutcomeCard.AutomationName
        ];
        IEnumerable<string> contentText =
        [
            presentation.ContentCard.SectionTitle,
            presentation.ContentCard.ProgressSummary,
            presentation.ContentCard.SupportingText,
            presentation.ContentCard.TertiaryText,
            presentation.ContentCard.DiagnosticCode,
            presentation.ContentCard.DisclosureSummary
        ];
        IEnumerable<string> itemText = presentation.ContentCard.Items
            .Concat(presentation.ContentCard.ExpandedItems)
            .SelectMany(item => new[]
            {
                item.StageNumber,
                item.Title,
                item.Detail,
                item.StatusText,
                item.AutomationName
            });
        IEnumerable<string> actionText =
        [
            presentation.ActionCard.Title,
            presentation.ActionCard.Message,
            presentation.ActionCard.AutomationName,
            presentation.ActionCard.CancelAction.Text,
            presentation.ActionCard.SecondaryActionOne.Text,
            presentation.ActionCard.SecondaryActionTwo.Text,
            presentation.ActionCard.PrimaryAction.Text
        ];

        return string.Join(
            "\n",
            modelText
                .Concat(outcomeText)
                .Concat(contentText)
                .Concat(itemText)
                .Concat(actionText));
    }
}
