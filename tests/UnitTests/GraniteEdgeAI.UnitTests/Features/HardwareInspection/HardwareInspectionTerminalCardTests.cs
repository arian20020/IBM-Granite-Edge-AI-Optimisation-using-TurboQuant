using GraniteEdgeAI.Features.HardwareInspection.Application;
using GraniteEdgeAI.Features.HardwareInspection.Presentation.Controls;
using GraniteEdgeAI.Features.HardwareInspection.Presentation.Factories;
using GraniteEdgeAI.Features.HardwareInspection.Presentation.State;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation.Peers;
using Microsoft.UI.Xaml.Automation.Provider;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
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
    public void ActionCard_UsesApprovedModernPrimaryAndSecondaryStyles()
    {
        HardwareInspectionActionCard card = new();
        HardwareInspectionPresentationState completed = _factory.CreateTerminal(
            HardwareInspectionOutcome.Completed,
            hasUsableHandoff: true,
            block3RouteRegistered: true);
        card.Apply(completed);

        Style primaryStyle = (Style)card.Resources["HardwareInspectionPrimaryActionStyle"];
        Style secondaryStyle = (Style)card.Resources["HardwareInspectionSecondaryActionStyle"];
        Assert.IsNotNull(primaryStyle.BasedOn);
        Assert.IsNotNull(secondaryStyle.BasedOn);
        Assert.AreNotSame(primaryStyle, secondaryStyle);

        AssertSharedJourneyPalette(card);

        AssertStyleSetter(primaryStyle, Control.MinHeightProperty, 46d);
        AssertStyleSetter(primaryStyle, Control.PaddingProperty, new Thickness(18, 10, 18, 10));
        AssertStyleSetter(primaryStyle, Control.CornerRadiusProperty, new CornerRadius(11));
        AssertStyleSetter(secondaryStyle, Control.MinHeightProperty, 46d);
        AssertStyleSetter(secondaryStyle, Control.PaddingProperty, new Thickness(18, 10, 18, 10));
        AssertStyleSetter(secondaryStyle, Control.CornerRadiusProperty, new CornerRadius(11));

        Button[] buttons = Buttons(card);
        Assert.AreSame(secondaryStyle, buttons[0].Style);
        Assert.AreSame(primaryStyle, buttons[1].Style);
        CollectionAssert.AreEqual(
            new[] { "Run inspection again", "Continue to compatibility" },
            buttons.Select(button => button.Content?.ToString()).ToArray());

        HardwareInspectionPresentationState[] allStates =
        [
            _factory.CreateInvalidHandoff(),
            .. Enum.GetValues<HardwareInspectionStage>()
                .Select(_factory.CreateActive),
            _factory.CreateStopping(),
            completed,
            _factory.CreateTerminal(HardwareInspectionOutcome.CompletedWithWarnings),
            _factory.CreateTerminal(
                HardwareInspectionOutcome.Failed,
                HardwareInspectionFailureClass.CriticalEvidence,
                criticalFailureRetryable: true),
            _factory.CreateTerminal(
                HardwareInspectionOutcome.Failed,
                HardwareInspectionFailureClass.TransientOperation),
            _factory.CreateTerminal(
                HardwareInspectionOutcome.Failed,
                HardwareInspectionFailureClass.ApplicationRepairRequired),
            _factory.CreateTerminal(HardwareInspectionOutcome.Cancelled),
        ];
        Assert.HasCount(15, allStates);

        foreach (HardwareInspectionPresentationState state in allStates)
        {
            card.Apply(state);
            Button[] stateButtons = Buttons(card);
            CollectionAssert.AreEqual(
                state.Actions.Where(action => action.IsVisible)
                    .Select(action => action.Label)
                    .ToArray(),
                stateButtons.Select(button => button.Content?.ToString()).ToArray(),
                state.Kind.ToString());
            foreach (Button button in stateButtons)
            {
                Assert.AreEqual(46d, button.MinHeight, 0.01d, state.Kind.ToString());
                Assert.AreEqual(new CornerRadius(11), button.CornerRadius, state.Kind.ToString());
                Assert.AreEqual((ushort)600, button.FontWeight.Weight, state.Kind.ToString());
                Assert.IsTrue(button.UseSystemFocusVisuals, state.Kind.ToString());
                Assert.IsNotNull(button.Resources["ButtonBackgroundPointerOver"]);
                Assert.IsNotNull(button.Resources["ButtonBackgroundPressed"]);
                Assert.IsNotNull(button.Resources["ButtonBackgroundDisabled"]);
                Assert.IsNotNull(button.Resources["ButtonForegroundDisabled"]);
                Assert.IsNotNull(button.Resources["ButtonBorderBrushDisabled"]);
            }
        }
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

    private static void AssertSharedJourneyPalette(FrameworkElement owner)
    {
        Assert.AreEqual(
            ColorHelper.FromArgb(0xFF, 0x25, 0x63, 0xEB),
            ((SolidColorBrush)owner.Resources[
                "GraniteJourneyPrimaryBackgroundBrush"]).Color);
        Assert.AreEqual(
            ColorHelper.FromArgb(0xFF, 0x1D, 0x4E, 0xD8),
            ((SolidColorBrush)owner.Resources[
                "GraniteJourneyPrimaryPointerOverBrush"]).Color);
        Assert.AreEqual(
            ColorHelper.FromArgb(0xFF, 0x1E, 0x40, 0xAF),
            ((SolidColorBrush)owner.Resources[
                "GraniteJourneyPrimaryPressedBrush"]).Color);
        Assert.AreEqual(
            Colors.White,
            ((SolidColorBrush)owner.Resources[
                "GraniteJourneySecondaryBackgroundBrush"]).Color);
        Assert.AreEqual(
            ColorHelper.FromArgb(0xFF, 0xC9, 0xD7, 0xE8),
            ((SolidColorBrush)owner.Resources[
                "GraniteJourneySecondaryBorderBrush"]).Color);
    }

    private static void AssertStyleSetter(Style style, DependencyProperty property, object expected)
    {
        Setter setter = style.Setters.OfType<Setter>().Single(item => item.Property == property);
        Assert.AreEqual(expected, setter.Value);
    }
}
