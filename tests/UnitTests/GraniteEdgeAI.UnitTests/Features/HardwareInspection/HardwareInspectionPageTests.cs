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
