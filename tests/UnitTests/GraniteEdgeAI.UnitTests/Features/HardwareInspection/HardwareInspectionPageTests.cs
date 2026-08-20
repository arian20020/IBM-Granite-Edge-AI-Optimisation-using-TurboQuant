using GraniteEdgeAI.Features.HardwareInspection;
using GraniteEdgeAI.Features.HardwareInspection.Application;
using GraniteEdgeAI.Features.HardwareInspection.Presentation.Controls;
using GraniteEdgeAI.Features.HardwareInspection.Presentation.Factories;
using GraniteEdgeAI.Features.HardwareInspection.Presentation.State;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation.Peers;
using Microsoft.UI.Xaml.Automation.Provider;
using Microsoft.UI.Xaml.Controls;
using Microsoft.VisualStudio.TestTools.UnitTesting.AppContainer;

namespace GraniteEdgeAI.UnitTests.Features.HardwareInspection;

[TestClass]
public sealed class HardwareInspectionPageTests
{
    private readonly HardwareInspectionPresentationFactory _factory = new();

    [UITestMethod]
    [TestCategory("WinUI")]
    public void Page_DefaultsToApprovedLightPresentationAndProvidesShellFooterSlot()
    {
        HardwareInspectionPage page = new();
        TextBlock footer = new() { Text = "MODEL SETUP · STEP 3 OF 5" };
        page.FooterContent = footer;

        Assert.AreEqual(ElementTheme.Light, page.RequestedTheme);
        Assert.IsNotNull(page.FindName("FooterPresenter"));
        Assert.AreSame(footer, ((ContentPresenter)page.FindName("FooterPresenter")).Content);
    }

    [UITestMethod]
    [TestCategory("WinUI")]
    public void Apply_ActiveShowsOnlyExactProgressSurface()
    {
        HardwareInspectionPage page = new();
        HardwareInspectionPresentationState state = _factory.CreateActive(
            HardwareInspectionStage.DetectingGraphicsHardware);
        page.Apply(state);

        Assert.AreEqual("Hardware inspection", Text(page, "PageTitleTextBlock").Text);
        Assert.AreEqual(state.Subtitle, Text(page, "PageSubtitleTextBlock").Text);
        Assert.AreEqual(Visibility.Visible, Element(page, "ProgressCard").Visibility);
        Assert.AreEqual(Visibility.Collapsed, Element(page, "TerminalPanel").Visibility);
        Assert.AreEqual(state.Title, ((HardwareInspectionProgressCard)Element(page, "ProgressCard")).CurrentState?.Title);
        HardwareInspectionActionCard activeActions =
            (HardwareInspectionActionCard)Element(page, "ActiveActionCard");
        Assert.AreEqual(Visibility.Visible, activeActions.Visibility);
        CollectionAssert.AreEqual(
            new[] { "Cancel inspection" },
            ((StackPanel)activeActions.FindName("ActionsPanel")).Children
                .Cast<Button>()
                .Select(button => button.Content?.ToString())
                .ToArray());
    }

    [UITestMethod]
    [TestCategory("WinUI")]
    public void Apply_WarningUsesFullFactsCompositionAndReviewSection()
    {
        HardwareInspectionPage page = new();
        HardwareInspectionPresentationState state = _factory.CreateTerminal(
            HardwareInspectionOutcome.CompletedWithWarnings);
        HardwareSummaryPresentation summary = HardwareSummaryPresentationFactory.Create(
            HardwareInspectionContractTests.CreateUsableSnapshotForPresentation());

        page.Apply(state, summary, HardwareInspectionDetailsSummaryTests.CreateDetails());

        Assert.AreEqual(Visibility.Visible, Element(page, "SummaryGrid").Visibility);
        Assert.AreEqual(Visibility.Visible, Element(page, "ReviewPanel").Visibility);
        Assert.AreEqual("What needs review", Text(page, "ReviewHeadingTextBlock").Text);
        Assert.AreEqual(Visibility.Visible, Element(page, "LimitationPanel").Visibility);
        HardwareInspectionOutcomeCard outcome =
            (HardwareInspectionOutcomeCard)Element(page, "OutcomeCard");
        Assert.AreEqual(Visibility.Visible, ((FrameworkElement)outcome.FindName("ReviewCountPanel")).Visibility);
        Assert.AreEqual("1", ((TextBlock)outcome.FindName("ReviewCountTextBlock")).Text);
        Assert.AreEqual(
            HorizontalAlignment.Center,
            ((FrameworkElement)outcome.FindName("ReviewCountPanel")).HorizontalAlignment);
        Assert.AreEqual(
            HorizontalAlignment.Center,
            ((FrameworkElement)outcome.FindName("ReviewCountTextBlock")).HorizontalAlignment);
        TextBlock reviewCountLabel =
            (TextBlock)outcome.FindName("ReviewCountLabelTextBlock");
        Assert.AreEqual(HorizontalAlignment.Center, reviewCountLabel.HorizontalAlignment);
        Assert.AreEqual(TextAlignment.Center, reviewCountLabel.TextAlignment);
    }

    [UITestMethod]
    [TestCategory("WinUI")]
    public void Apply_CompletedUsesApprovedThreeCardHierarchyAndFullWidthDetails()
    {
        HardwareInspectionPage page = new();
        HardwareInspectionPresentationState state = _factory.CreateTerminal(
            HardwareInspectionOutcome.Completed,
            hasUsableHandoff: true,
            block3RouteRegistered: false);
        HardwareSummaryPresentation summary = HardwareSummaryPresentationFactory.Create(
            HardwareInspectionContractTests.CreateUsableSnapshotForPresentation());
        page.Apply(state, summary, HardwareInspectionDetailsSummaryTests.CreateDetails());

        Assert.AreEqual(Visibility.Collapsed, Element(page, "ProgressCard").Visibility);
        Assert.AreEqual(Visibility.Visible, Element(page, "TerminalPanel").Visibility);
        Assert.AreEqual("This computer", ((HardwareInspectionSummaryCard)Element(page, "ComputerSummaryCard")).CardTitle);
        Assert.AreEqual("Local AI tools", ((HardwareInspectionSummaryCard)Element(page, "RuntimeSummaryCard")).CardTitle);
        Assert.AreEqual("Information sources", ((HardwareInspectionSummaryCard)Element(page, "SourcesSummaryCard")).CardTitle);
        Assert.IsTrue(((HardwareInspectionSummaryCard)Element(page, "ComputerSummaryCard")).FactItems.All(
            fact => fact.Group is "Processor" or "Memory" or "Graphics" or "Storage"));
        Assert.IsTrue(((HardwareInspectionSummaryCard)Element(page, "RuntimeSummaryCard")).FactItems.All(
            fact => fact.Group == "Local AI tools"));
        Assert.IsTrue(((HardwareInspectionSummaryCard)Element(page, "SourcesSummaryCard")).FactItems.All(
            fact => fact.Group == "Information sources"));
        Assert.AreEqual(Visibility.Visible, Element(page, "DetailsCard").Visibility);
        Assert.AreEqual(Visibility.Visible, Element(page, "ActionCard").Visibility);
    }

    [UITestMethod]
    [TestCategory("WinUI")]
    public void Apply_InvalidShowsOnlyBoundedOutcomeAndAction()
    {
        HardwareInspectionPage page = new();
        page.Apply(_factory.CreateInvalidHandoff());

        Assert.AreEqual(Visibility.Visible, Element(page, "TerminalPanel").Visibility);
        Assert.AreEqual(Visibility.Collapsed, Element(page, "SummaryGrid").Visibility);
        Assert.AreEqual(Visibility.Collapsed, Element(page, "DetailsCard").Visibility);
        Button[] actions = ((StackPanel)((HardwareInspectionActionCard)Element(page, "ActionCard"))
            .FindName("ActionsPanel")).Children.Cast<Button>().ToArray();
        CollectionAssert.AreEqual(new[] { "Back to model inspection" }, actions.Select(b => b.Content?.ToString()).ToArray());
    }

    [UITestMethod]
    [TestCategory("WinUI")]
    public void Apply_RecoveryStatesUseApprovedGuidanceAndStoppingStaysBounded()
    {
        HardwareInspectionPage page = new();
        page.Apply(
            _factory.CreateTerminal(
                HardwareInspectionOutcome.Failed,
                HardwareInspectionFailureClass.TransientOperation),
            details: HardwareInspectionDetailsSummaryTests.CreateDetails());

        HardwareInspectionRecoveryCard recovery =
            (HardwareInspectionRecoveryCard)Element(page, "RecoveryPanel");
        Assert.AreEqual(Visibility.Visible, recovery.Visibility);
        Assert.AreEqual("What you can do", ((TextBlock)recovery.FindName("RecoveryHeadingTextBlock")).Text);
        ItemsControl recoveryItems =
            (ItemsControl)recovery.FindName("RecoveryItemsControl");
        Assert.AreEqual(2, recovery.Items.Count);
        Border recoveryStepSurface =
            (Border)recoveryItems.ItemTemplate.LoadContent();
        Assert.AreEqual(new Thickness(16, 14, 16, 14), recoveryStepSurface.Padding);
        Assert.AreEqual(new CornerRadius(10), recoveryStepSurface.CornerRadius);
        Assert.IsNotNull(recoveryStepSurface.Background);
        Assert.IsNotNull(recoveryStepSurface.BorderBrush);
        Border recoveryStepMarker =
            (Border)recoveryStepSurface.FindName("RecoveryStepMarker");
        Assert.AreEqual(32d, recoveryStepMarker.Width);
        Assert.AreEqual(32d, recoveryStepMarker.Height);
        Assert.AreEqual(Visibility.Visible, Element(page, "LocalProcessingPanel").Visibility);
        Assert.AreEqual(
            "This hardware inspection ran locally and did not upload hardware information.",
            Text(page, "LocalProcessingTextBlock").Text);

        page.Apply(_factory.CreateStopping());
        Assert.AreEqual("Why this may take a moment", ((TextBlock)recovery.FindName("RecoveryHeadingTextBlock")).Text);
        Assert.AreEqual(Visibility.Collapsed, Element(page, "DetailsCard").Visibility);
        Assert.AreEqual(Visibility.Collapsed, Element(page, "LocalProcessingPanel").Visibility);
        Assert.AreEqual(Visibility.Visible, Element(page, "ActionCard").Visibility);
    }

    [UITestMethod]
    [TestCategory("WinUI")]
    public void Apply_RejectsIncompleteStableTerminalBundleBeforeChangingPage()
    {
        HardwareInspectionPage page = new();
        HardwareInspectionPresentationState completed = _factory.CreateTerminal(HardwareInspectionOutcome.Completed);
        Assert.Throws<ArgumentException>(() => page.Apply(completed));

        HardwareInspectionPresentationState failed = _factory.CreateTerminal(
            HardwareInspectionOutcome.Failed,
            HardwareInspectionFailureClass.TransientOperation);
        Assert.Throws<ArgumentException>(() => page.Apply(failed));
    }

    [UITestMethod]
    [TestCategory("WinUI")]
    public void Page_BubblesTypedActionWithoutOwningNavigation()
    {
        HardwareInspectionPage page = new();
        HardwareInspectionActionKind? requested = null;
        page.ActionRequested += (_, args) => requested = args.Kind;
        page.Apply(_factory.CreateInvalidHandoff());

        Button button = ((StackPanel)((HardwareInspectionActionCard)Element(page, "ActionCard"))
            .FindName("ActionsPanel")).Children.Cast<Button>().Single();
        IInvokeProvider invoke = (IInvokeProvider)new ButtonAutomationPeer(button).GetPattern(PatternInterface.Invoke)!;
        invoke.Invoke();
        Assert.AreEqual(HardwareInspectionActionKind.BackToModelInspection, requested);
    }

    private static FrameworkElement Element(HardwareInspectionPage page, string name) =>
        (FrameworkElement)page.FindName(name);

    private static TextBlock Text(HardwareInspectionPage page, string name) =>
        (TextBlock)page.FindName(name);
}
