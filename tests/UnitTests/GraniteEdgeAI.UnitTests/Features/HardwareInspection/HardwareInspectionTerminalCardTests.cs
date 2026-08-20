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
public sealed class HardwareInspectionTerminalCardTests
{
    private readonly HardwareInspectionPresentationFactory _factory = new();

    [UITestMethod]
    [TestCategory("WinUI")]
    public void OutcomeCard_AppliesExactSuccessWarningAndFailureCopy()
    {
        HardwareInspectionOutcomeCard card = new();
        HardwareInspectionPresentationState success = _factory.CreateTerminal(HardwareInspectionOutcome.Completed);
        card.Apply(success);
        Assert.AreEqual(success.Kicker, Text(card, "KickerTextBlock").Text);
        Assert.AreEqual(success.Title, Text(card, "TitleTextBlock").Text);
        Assert.AreEqual(success.Body, Text(card, "BodyTextBlock").Text);
        Assert.AreEqual(HardwareInspectionOutcomeTone.Success, card.CurrentTone);
        FontIcon glyph = (FontIcon)card.FindName("GlyphFontIcon");
        Assert.AreEqual("\uE73E", glyph.Glyph);
        Assert.AreEqual(HorizontalAlignment.Center, glyph.HorizontalAlignment);
        Assert.AreEqual(VerticalAlignment.Center, glyph.VerticalAlignment);

        HardwareInspectionPresentationState warning = _factory.CreateTerminal(HardwareInspectionOutcome.CompletedWithWarnings);
        card.Apply(warning);
        Assert.AreEqual(warning.Body, Text(card, "BodyTextBlock").Text);
        Assert.AreEqual(HardwareInspectionOutcomeTone.Warning, card.CurrentTone);
        Assert.AreEqual("\uE7BA", glyph.Glyph);

        HardwareInspectionPresentationState failed = _factory.CreateTerminal(
            HardwareInspectionOutcome.Failed,
            HardwareInspectionFailureClass.TransientOperation);
        card.Apply(failed);
        Assert.AreEqual(failed.Title, Text(card, "TitleTextBlock").Text);
        Assert.AreEqual(HardwareInspectionOutcomeTone.Error, card.CurrentTone);
        Assert.AreEqual("\uE711", glyph.Glyph);
    }

    [UITestMethod]
    [TestCategory("WinUI")]
    public void OutcomeCard_UsesNeutralToneForStoppingInvalidAndCancelled()
    {
        HardwareInspectionOutcomeCard card = new();
        foreach (HardwareInspectionPresentationState state in new[]
        {
            _factory.CreateInvalidHandoff(),
            _factory.CreateStopping(),
            _factory.CreateTerminal(HardwareInspectionOutcome.Cancelled),
        })
        {
            card.Apply(state);
            Assert.AreEqual(HardwareInspectionOutcomeTone.Neutral, card.CurrentTone);
            Assert.AreEqual(state.Announcement, card.AccessibleAnnouncement);
        }
    }

    [UITestMethod]
    [TestCategory("WinUI")]
    public void OutcomeCard_StoppingUsesOrbitAndCancelledUsesMinusGlyph()
    {
        HardwareInspectionOutcomeCard card = new();
        card.Apply(_factory.CreateStopping());

        ProgressRing orbit = (ProgressRing)card.FindName("OutcomeProgressRing");
        FontIcon glyph = (FontIcon)card.FindName("GlyphFontIcon");
        Assert.AreEqual(Visibility.Visible, orbit.Visibility);
        Assert.IsTrue(orbit.IsActive);
        Assert.AreEqual(Visibility.Collapsed, glyph.Visibility);

        card.Apply(_factory.CreateTerminal(HardwareInspectionOutcome.Cancelled));
        Assert.AreEqual(Visibility.Collapsed, orbit.Visibility);
        Assert.IsFalse(orbit.IsActive);
        Assert.AreEqual(Visibility.Visible, glyph.Visibility);
        Assert.AreEqual("\uE738", glyph.Glyph);
    }

    [UITestMethod]
    [TestCategory("WinUI")]
    public void OutcomeCard_RejectsActiveState()
    {
        HardwareInspectionOutcomeCard card = new();
        Assert.Throws<ArgumentException>(() => card.Apply(
            _factory.CreateActive(HardwareInspectionStage.StartingHardwareInspection)));
    }

    [UITestMethod]
    [TestCategory("WinUI")]
    public void ActionCard_RendersExactCompletedActionsAndDisabledHelp()
    {
        HardwareInspectionActionCard card = new();
        card.Apply(_factory.CreateTerminal(
            HardwareInspectionOutcome.Completed,
            hasUsableHandoff: true,
            block3RouteRegistered: false));

        Button[] buttons = Buttons(card);
        CollectionAssert.AreEqual(
            new[] { "Run inspection again", "Continue to compatibility" },
            buttons.Select(button => button.Content?.ToString()).ToArray());
        Assert.IsTrue(buttons[0].IsEnabled);
        Assert.IsFalse(buttons[1].IsEnabled);
        Assert.AreEqual(HardwareInspectionCopyCatalog.ContinueUnavailableHelp, card.ActionItems[1].AccessibleHelp);
    }

    [UITestMethod]
    [TestCategory("WinUI")]
    public void ActionCard_ReplacesStaleButtonsAndRaisesTypedRequest()
    {
        HardwareInspectionActionCard card = new();
        card.Apply(_factory.CreateTerminal(HardwareInspectionOutcome.Completed));
        card.Apply(_factory.CreateTerminal(
            HardwareInspectionOutcome.Failed,
            HardwareInspectionFailureClass.TransientOperation));

        CollectionAssert.AreEqual(
            new[] { "Back", "Try again" },
            Buttons(card).Select(button => button.Content?.ToString()).ToArray());

        HardwareInspectionActionKind? requested = null;
        card.ActionRequested += (_, args) => requested = args.Kind;
        Button tryAgain = Buttons(card)[1];
        IInvokeProvider invoke = (IInvokeProvider)new ButtonAutomationPeer(tryAgain).GetPattern(PatternInterface.Invoke)!;
        invoke.Invoke();
        Assert.AreEqual(HardwareInspectionActionKind.TryAgain, requested);
    }

    [UITestMethod]
    [TestCategory("WinUI")]
    public void ActionCard_StacksButtonsAtCompactWidthWithoutChangingActions()
    {
        HardwareInspectionActionCard card = new();
        HardwareInspectionPresentationState state = _factory.CreateTerminal(
            HardwareInspectionOutcome.Completed,
            hasUsableHandoff: true,
            block3RouteRegistered: true);
        card.Apply(state);

        card.ApplyAvailableWidth(448);
        StackPanel panel = (StackPanel)card.FindName("ActionsPanel");
        Assert.AreEqual(Orientation.Vertical, panel.Orientation);
        Assert.AreEqual(HorizontalAlignment.Stretch, panel.HorizontalAlignment);
        Assert.IsTrue(panel.Children.Cast<Button>().All(button =>
            button.HorizontalAlignment == HorizontalAlignment.Stretch));

        card.ApplyAvailableWidth(840);
        Assert.AreEqual(Orientation.Horizontal, panel.Orientation);
        Assert.AreEqual(HorizontalAlignment.Center, panel.HorizontalAlignment);
        Assert.IsTrue(panel.Children.Cast<Button>().All(button =>
            button.HorizontalAlignment == HorizontalAlignment.Center));
        CollectionAssert.AreEqual(
            state.Actions.Select(action => action.Label).ToArray(),
            panel.Children.Cast<Button>().Select(button => button.Content?.ToString()).ToArray());
    }

    private static TextBlock Text(HardwareInspectionOutcomeCard card, string name) =>
        (TextBlock)card.FindName(name);

    private static Button[] Buttons(HardwareInspectionActionCard card) =>
        ((StackPanel)card.FindName("ActionsPanel")).Children.Cast<Button>().ToArray();
}
