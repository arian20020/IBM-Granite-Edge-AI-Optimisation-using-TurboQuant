using GraniteEdgeAI.Features.ModelInspection.Contracts;
using GraniteEdgeAI.Features.ModelInspection.Models;
using GraniteEdgeAI.Features.ModelInspection.Presentation;
using GraniteEdgeAI.Features.ModelInspection.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.VisualStudio.TestTools.UnitTesting.AppContainer;
using System.Reflection;
using System.Windows.Input;

namespace GraniteEdgeAI.UnitTests.Features.ModelInspection.Presentation;

[TestClass]
public sealed class ModelInspectionPresentationFactoryTests
{
    [UITestMethod]
    [TestCategory("WinUI")]
    public void Create_InitialSnapshotUsesSafeMetadataAndDisablesCancelBeforeRun()
    {
        ModelInspectionRequest request = PresentationTestData.CreateRequest();
        RecordingCommand cancelCommand = PresentationTestData.CreateCommand();

        ModelInspectionPagePresentation presentation =
            ModelInspectionPresentationFactory.Create(
                request,
                ModelInspectionViewSnapshot.Initial,
                new ModelInspectionPresentationCommands(
                    cancelCommand,
                    PresentationTestData.CreateCommand(),
                    PresentationTestData.CreateCommand()),
                isDisclosureExpanded: false,
                new InspectionProgressRows());

        Assert.AreEqual("Granite 4.1 3B", presentation.ModelCard.ModelName);
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
        Assert.IsFalse(presentation.ActionCard.CancelAction.IsEnabled);
        AssertStartupPresentation(
            presentation.ContentCard,
            Visibility.Collapsed,
            expectedSummary: string.Empty,
            expectedAutomationName: string.Empty);
    }

    [UITestMethod]
    [TestCategory("WinUI")]
    public void Create_ProgressSnapshotProjectsTheLatestFiveStageUpdate()
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
        var progressRows = new InspectionProgressRows();
        progressRows.Reset(new ModelInspectionRenderKey(1, 0));
        ModelInspectionViewSnapshot snapshot = new(
            new ModelInspectionRenderKey(1, 3),
            isRunActive: true,
            isCancellationRequested: false,
            progress,
            terminalResult: null);
        var startingRows = new InspectionProgressRows();
        startingRows.Reset(new ModelInspectionRenderKey(1, 0));
        ModelInspectionViewSnapshot starting = new(
            new ModelInspectionRenderKey(1, 0),
            isRunActive: true,
            isCancellationRequested: false,
            progress: null,
            terminalResult: null);

        ModelInspectionPagePresentation startingPresentation =
            ModelInspectionPresentationFactory.Create(
                request,
                starting,
                new ModelInspectionPresentationCommands(
                    PresentationTestData.CreateCommand(),
                    PresentationTestData.CreateCommand(),
                    PresentationTestData.CreateCommand()),
                isDisclosureExpanded: false,
                startingRows);
        startingRows.Apply(startingPresentation.ProgressRowsUpdate);

        Assert.AreEqual(
            "Starting secure inspection…",
            startingPresentation.ModelCard.InspectionChecksSummary);
        AssertStartupPresentation(
            startingPresentation.ContentCard,
            Visibility.Visible,
            "Starting secure inspection…",
            "Model inspection is starting.");
        Assert.AreEqual(
            "Model inspection is starting.",
            startingPresentation.ProgressAnnouncement);
        Assert.AreEqual(
            "0 of 5 checks complete",
            startingPresentation.ContentCard.ProgressSummary);
        Assert.HasCount(5, startingPresentation.ContentCard.Items);
        Assert.IsTrue(startingPresentation.ContentCard.Items.All(row =>
            row.Status == InspectionContentStatus.Waiting && !row.IsActive));

        ModelInspectionPagePresentation laterStartingPresentation =
            ModelInspectionPresentationFactory.Create(
                request,
                new ModelInspectionViewSnapshot(
                    new ModelInspectionRenderKey(1, 1),
                    isRunActive: true,
                    isCancellationRequested: true,
                    progress: null,
                    terminalResult: null),
                new ModelInspectionPresentationCommands(
                    PresentationTestData.CreateCommand(),
                    PresentationTestData.CreateCommand(),
                    PresentationTestData.CreateCommand()),
                isDisclosureExpanded: false,
                startingRows);
        Assert.AreEqual(
            string.Empty,
            laterStartingPresentation.ProgressAnnouncement,
            "Startup must announce only the first active null-progress revision.");

        ModelInspectionPagePresentation presentation =
            ModelInspectionPresentationFactory.Create(
                request,
                snapshot,
                new ModelInspectionPresentationCommands(
                    cancelCommand,
                    PresentationTestData.CreateCommand(),
                    PresentationTestData.CreateCommand()),
                isDisclosureExpanded: false,
                progressRows);
        progressRows.Apply(presentation.ProgressRowsUpdate);

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
    public void Create_TerminalSnapshotMapsEveryCompletedModelOutcome(
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
    [DataRow((int)ModelInspectionOutcome.Ready, 1, 2)]
    [DataRow((int)ModelInspectionOutcome.ReadyWithWarnings, 1, 2)]
    [DataRow((int)ModelInspectionOutcome.ConversionRequired, 1, 2)]
    [DataRow((int)ModelInspectionOutcome.IncompletePackage, 2, 1)]
    [DataRow((int)ModelInspectionOutcome.Unsupported, 1, 1)]
    [DataRow((int)ModelInspectionOutcome.Invalid, 1, 1)]
    public void Create_CompletedOutcomeKeepsNavigationActiveAndFutureActionsExplicit(
        int outcomeValue,
        int expectedActiveCount,
        int expectedFutureCount)
    {
        ModelInspectionOutcome outcome = (ModelInspectionOutcome)outcomeValue;
        RecordingCommand retryCommand = PresentationTestData.CreateCommand();
        RecordingCommand chooseCommand = PresentationTestData.CreateCommand();
        ModelInspectionExecutionResult execution =
            ModelInspectionExecutionResult.Completed(
                PresentationTestData.CreateResult(outcome));

        ModelInspectionPagePresentation presentation =
            CreateTerminal(execution, retryCommand, chooseCommand);

        InspectionActionPresentation[] visible = new[]
        {
            presentation.ActionCard.SecondaryActionOne,
            presentation.ActionCard.SecondaryActionTwo,
            presentation.ActionCard.PrimaryAction
        }
        .Where(action => action.Visibility == Visibility.Visible)
        .ToArray();
        InspectionActionPresentation[] active = visible
            .Where(action => action.Command is not null)
            .ToArray();
        InspectionActionPresentation[] future = visible
            .Where(action => action.Command is null)
            .ToArray();

        Assert.AreEqual(expectedActiveCount, active.Length);
        Assert.IsTrue(active.All(action =>
            ReferenceEquals(chooseCommand, action.Command) &&
            action.IsEnabled));
        Assert.AreEqual(expectedFutureCount, future.Length);
        Assert.IsTrue(future.All(action =>
            action.Visibility == Visibility.Visible &&
            !action.IsEnabled &&
            action.AutomationHelpText == "Coming later"));
        Assert.IsTrue(visible.All(action => !ReferenceEquals(retryCommand, action.Command)));
    }

    [UITestMethod]
    [TestCategory("WinUI")]
    public void Create_CooperativeCancellationOffersRetryAndChoose()
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
        AssertRecoveryActions(
            presentation.ActionCard,
            expectedPrimaryText: "Restart inspection",
            expectedFutureCount: 0);
    }

    [UITestMethod]
    [TestCategory("WinUI")]
    public void Create_OperationalFailureShowsOnlySafeMessageAndStableCode()
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
        AssertRecoveryActions(
            presentation.ActionCard,
            expectedPrimaryText: "Retry inspection",
            expectedFutureCount: 1);

        StringAssert.DoesNotContain(
            FlattenVisibleText(presentation),
            forbiddenTechnicalDetail,
            StringComparison.Ordinal);
    }

    [UITestMethod]
    [TestCategory("WinUI")]
    public void Create_DoesNotExposePathsOrFindingTechnicalDetail()
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
        Assert.AreEqual("Granite 4.1 3B", presentation.ModelCard.ModelName);
    }

    private static ModelInspectionPagePresentation CreateTerminal(
        ModelInspectionExecutionResult execution,
        ICommand? retryCommand = null,
        ICommand? chooseCommand = null)
    {
        var snapshot = new ModelInspectionViewSnapshot(
            new ModelInspectionRenderKey(1, 4),
            isRunActive: false,
            isCancellationRequested: false,
            progress: null,
            terminalResult: execution);
        return ModelInspectionPresentationFactory.Create(
            PresentationTestData.CreateRequest(),
            snapshot,
            new ModelInspectionPresentationCommands(
                PresentationTestData.CreateCommand(),
                retryCommand ?? PresentationTestData.CreateCommand(),
                chooseCommand ?? PresentationTestData.CreateCommand()),
            isDisclosureExpanded: false,
            new InspectionProgressRows());
    }

    [TestMethod]
    [TestCategory("WinUI")]
    public void UnifiedCreate_IsTheOnlyNonPrivateFactoryEntryPoint()
    {
        MethodInfo[] entryPoints = typeof(ModelInspectionPresentationFactory)
            .GetMethods(
                BindingFlags.Static |
                BindingFlags.Public |
                BindingFlags.NonPublic |
                BindingFlags.DeclaredOnly)
            .Where(method => !method.IsPrivate && !method.IsSpecialName)
            .ToArray();

        Assert.HasCount(1, entryPoints);
        Assert.AreEqual(nameof(ModelInspectionPresentationFactory.Create),
            entryPoints[0].Name);
    }

    private static void AssertRecoveryActions(
        InspectionActionCardPresentation actions,
        string expectedPrimaryText,
        int expectedFutureCount)
    {
        Assert.AreEqual(InspectionActionCardMode.Result, actions.Mode);
        Assert.AreEqual("Choose another model", actions.SecondaryActionOne.Text);
        Assert.AreEqual(Visibility.Visible, actions.SecondaryActionOne.Visibility);
        Assert.AreEqual(expectedPrimaryText, actions.PrimaryAction.Text);
        Assert.AreEqual(Visibility.Visible, actions.PrimaryAction.Visibility);
        InspectionActionPresentation[] future =
        [
            actions.SecondaryActionOne,
            actions.SecondaryActionTwo,
            actions.PrimaryAction
        ];
        Assert.AreEqual(
            expectedFutureCount,
            future.Count(action => action.Command is null &&
                action.Visibility == Visibility.Visible));
    }

    private static void AssertStartupPresentation(
        InspectionContentCardPresentation content,
        Visibility expectedVisibility,
        string expectedSummary,
        string expectedAutomationName)
    {
        PropertyInfo? startupProperty = typeof(InspectionContentCardPresentation)
            .GetProperty("Startup", BindingFlags.Instance | BindingFlags.Public);
        Assert.IsNotNull(
            startupProperty,
            "The content presentation must expose a dedicated startup model.");
        object? startup = startupProperty.GetValue(content);
        Assert.IsNotNull(startup);
        Type startupType = startup.GetType();
        Assert.AreEqual(
            expectedVisibility,
            startupType.GetProperty("Visibility")!.GetValue(startup));
        Assert.AreEqual(
            expectedSummary,
            startupType.GetProperty("Summary")!.GetValue(startup));
        Assert.AreEqual(
            expectedAutomationName,
            startupType.GetProperty("AutomationName")!.GetValue(startup));
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
        IEnumerable<string> checkText = presentation.ModelCard.InspectionChecks
            .SelectMany(check => new[]
            {
                check.Title,
                check.Detail,
                check.StatusText,
                check.AutomationName
            });
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
            presentation.ContentCard.DisclosureSummary,
            presentation.ContentCard.TechnicalDetailsActionText,
            presentation.ContentCard.TechnicalDetailsAutomationName,
            presentation.ContentCard.TechnicalDetailsAutomationHelpText
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
            presentation.ActionCard.PrimaryAction.Text,
            presentation.ActionCard.CancelAction.AutomationHelpText,
            presentation.ActionCard.SecondaryActionOne.AutomationHelpText,
            presentation.ActionCard.SecondaryActionTwo.AutomationHelpText,
            presentation.ActionCard.PrimaryAction.AutomationHelpText,
            presentation.ProgressAnnouncement,
            presentation.OutcomeAnnouncement
        ];

        return string.Join(
            "\n",
            modelText
                .Concat(checkText)
                .Concat(outcomeText)
                .Concat(contentText)
                .Concat(itemText)
                .Concat(actionText));
    }
}
