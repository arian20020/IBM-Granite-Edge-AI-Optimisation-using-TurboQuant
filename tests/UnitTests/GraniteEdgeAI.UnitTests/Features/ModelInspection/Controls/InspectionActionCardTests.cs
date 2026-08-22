using GraniteEdgeAI.Features.ModelInspection.Controls;
using GraniteEdgeAI.Features.ModelInspection.Models;
using GraniteEdgeAI.Features.ModelInspection.Presentation;
using GraniteEdgeAI.Features.ModelInspection.ViewModels;
using GraniteEdgeAI.UnitTests.Features.ModelInspection.Presentation;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.VisualStudio.TestTools.UnitTesting.AppContainer;

namespace GraniteEdgeAI.UnitTests.Features.ModelInspection.Controls;

[TestClass]
[DoNotParallelize]
public sealed class InspectionActionCardTests
{
    [UITestMethod]
    [TestCategory("WinUI")]
    public async Task ResultActions_KeepApprovedSlotOrderAndTargetSizes()
    {
        InspectionActionCardPresentation presentation =
            CreateResultPresentation();
        var control = new InspectionActionCard
        {
            Width = 840,
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top,
            Presentation = presentation
        };
        var loaded = new TaskCompletionSource<bool>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        control.Loaded += (_, _) => loaded.TrySetResult(true);
        var window = new Window { Content = control };

        try
        {
            window.Activate();
            await loaded.Task.WaitAsync(TimeSpan.FromSeconds(10));
            control.UpdateLayout();
            Button secondaryOne = FindButton(control, "SecondaryActionOneButton");
            Button secondaryTwo = FindButton(control, "SecondaryActionTwoButton");
            Button primary = FindButton(control, "PrimaryActionButton");
            Border result = Assert.IsInstanceOfType<Border>(
                control.FindName("ResultView"));
            Grid resultLayout = Assert.IsInstanceOfType<Grid>(
                VisualTreeHelper.GetChild(result, 0));
            TextBlock heading = EnumerateDescendants(resultLayout)
                .OfType<TextBlock>()
                .Single(text =>
                    ReferenceEquals(VisualTreeHelper.GetParent(text), resultLayout) &&
                    Grid.GetRow(text) == 0);
            TextBlock helper = EnumerateDescendants(resultLayout)
                .OfType<TextBlock>()
                .Single(text =>
                    ReferenceEquals(VisualTreeHelper.GetParent(text), resultLayout) &&
                    Grid.GetRow(text) == 1);
            Grid panel = Assert.IsInstanceOfType<Grid>(
                control.FindName("ResultButtonPanel"));

            AssertSharedJourneyPalette(control);

            AssertActionStyle(
                control,
                secondaryOne,
                "InspectionSecondaryActionButtonStyle",
                "DefaultButtonStyle");
            AssertActionStyle(
                control,
                secondaryTwo,
                "InspectionSecondaryActionButtonStyle",
                "DefaultButtonStyle");
            AssertActionStyle(
                control,
                primary,
                "InspectionPrimaryActionButtonStyle",
                "AccentButtonStyle");
            AssertActionLabel(
                secondaryOne,
                presentation.SecondaryActionOne.Text);
            AssertActionLabel(
                secondaryTwo,
                presentation.SecondaryActionTwo.Text);
            AssertActionLabel(primary, presentation.PrimaryAction.Text);
            Assert.IsTrue(secondaryOne.Focus(FocusState.Keyboard));
            Assert.AreEqual(FocusState.Keyboard, secondaryOne.FocusState);
            Assert.AreEqual(840d, control.ActualWidth, 0.01);
            Assert.AreEqual(0d, result.MinHeight, 0.01d,
                "the action surface must use its natural content height");
            Assert.AreEqual(new Thickness(20d, 18d, 20d, 18d), result.Padding,
                "balanced result action padding");
            Assert.AreEqual(6d, VerticalGap(heading, helper, result), 1d,
                "heading to recovery guidance");
            Assert.AreEqual(12d, VerticalGap(helper, panel, result), 1d,
                "recovery guidance to actions");
            Assert.AreEqual(
                result.ActualWidth / 2d,
                HorizontalCentre(heading, result),
                1d,
                "result heading is centred");
            Assert.AreEqual(
                result.ActualWidth / 2d,
                HorizontalCentre(helper, result),
                1d,
                "result guidance is centred");
            Assert.IsGreaterThanOrEqualTo(44d, secondaryOne.MinHeight);
            Assert.AreEqual(46d, secondaryOne.MinHeight, 0.01);
            Assert.AreEqual(46d, secondaryTwo.MinHeight, 0.01);
            Assert.AreEqual(46d, primary.MinHeight, 0.01);
            AssertActionGeometry(secondaryOne);
            AssertActionGeometry(secondaryTwo);
            AssertActionGeometry(primary);
            AssertSecondaryActionSemantics(secondaryOne);
            AssertSecondaryActionSemantics(secondaryTwo);
            AssertPrimaryActionSemantics(primary);
            AssertActionBinding(
                secondaryOne,
                presentation.SecondaryActionOne);
            AssertActionBinding(
                secondaryTwo,
                presentation.SecondaryActionTwo);
            AssertActionBinding(primary, presentation.PrimaryAction);
            CollectionAssert.AreEqual(
                new[] { 0, 1, 2 },
                new[]
                {
                    secondaryOne.TabIndex,
                    secondaryTwo.TabIndex,
                    primary.TabIndex
                });
            Assert.AreEqual(secondaryOne.ActualWidth, secondaryTwo.ActualWidth, 1d);
            Assert.AreEqual(secondaryOne.ActualWidth, primary.ActualWidth, 1d);
            AssertFutureHelpGeometry(
                control,
                "SecondaryActionTwo",
                secondaryTwo);
            AssertFutureHelpGeometry(control, "PrimaryAction", primary);
        }
        finally
        {
            window.Content = null;
            window.Close();
        }
    }

    [UITestMethod]
    [TestCategory("WinUI")]
    public void DisabledFutureActions_ExposeButtonTooltipAndAdjacentHelp()
    {
        var control = new InspectionActionCard
        {
            Presentation = CreateResultPresentation()
        };

        AssertFutureHelp(control, "SecondaryActionTwo", "View technical report");
        AssertFutureHelp(control, "PrimaryAction", "Check hardware fit");

        Button active = FindButton(control, "SecondaryActionOneButton");
        TextBlock activeHelp = (TextBlock)control.FindName(
            "SecondaryActionOneFutureHelpText");
        Assert.IsTrue(active.IsEnabled);
        Assert.AreEqual(string.Empty, AutomationProperties.GetHelpText(active));
        Assert.AreEqual(Visibility.Collapsed, activeHelp.Visibility);
    }

    [UITestMethod]
    [TestCategory("WinUI")]
    public async Task InspectingCancel_DoesNotReceiveFutureActionHelp()
    {
        ModelInspectionPagePresentation page =
            ModelInspectionPresentationFactory.Create(
                PresentationTestData.CreateRequest(),
                ModelInspectionViewSnapshot.Initial,
                new ModelInspectionPresentationCommands(
                    PresentationTestData.CreateCommand(),
                    PresentationTestData.CreateCommand(),
                    PresentationTestData.CreateCommand()),
                isDisclosureExpanded: false,
                new InspectionProgressRows());
        var control = new InspectionActionCard
        {
            Width = 840,
            Presentation = page.ActionCard
        };
        var loaded = new TaskCompletionSource<bool>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        control.Loaded += (_, _) => loaded.TrySetResult(true);
        var window = new Window { Content = control };

        try
        {
            window.Activate();
            await loaded.Task.WaitAsync(TimeSpan.FromSeconds(10));
            control.UpdateLayout();

            Border inspecting = Assert.IsInstanceOfType<Border>(
                control.FindName("InspectingCardSurface"));
            Grid layout = Assert.IsInstanceOfType<Grid>(
                control.FindName("InspectingLayout"));
            TextBlock reassurance = Assert.IsInstanceOfType<TextBlock>(
                control.FindName("InspectingMessage"));
            Button cancel = FindButton(control, "CancelActionButton");
            Windows.Foundation.Point reassuranceOrigin = reassurance
                .TransformToVisual(inspecting)
                .TransformPoint(default);
            Windows.Foundation.Point cancelOrigin = cancel
                .TransformToVisual(inspecting)
                .TransformPoint(default);

            Assert.AreEqual(840d, inspecting.ActualWidth, 0.01d);
            AssertActionLabel(cancel, page.ActionCard.CancelAction.Text);
            AssertActionStyle(
                control,
                cancel,
                "InspectionSecondaryActionButtonStyle",
                "DefaultButtonStyle");
            Assert.AreEqual(new Thickness(24d), inspecting.Padding,
                "inspecting action card padding");
            Assert.AreEqual(12d, inspecting.CornerRadius.TopLeft, 0.01d);
            Assert.AreEqual(new Thickness(1d), inspecting.BorderThickness);
            Assert.IsNotNull(inspecting.Background);
            Assert.IsNotNull(inspecting.BorderBrush);
            Assert.AreEqual(2, layout.RowDefinitions.Count);
            Assert.IsTrue(string.IsNullOrEmpty(page.ActionCard.Title),
                "the real inspecting presentation does not invent a heading");
            Assert.IsNull(control.FindName("InspectingTitle"));
            Assert.AreEqual(0, Grid.GetRow(reassurance));
            Assert.AreEqual(1, Grid.GetRow(cancel));
            Assert.IsLessThan(cancelOrigin.Y, reassuranceOrigin.Y,
                "reassurance is presented before the cancel action");
            Assert.AreEqual(
                inspecting.ActualWidth / 2d,
                cancelOrigin.X + (cancel.ActualWidth / 2d),
                1d,
                "cancel action is centred in the inspecting card");
            Assert.IsGreaterThanOrEqualTo(44d, cancel.ActualHeight);
            AssertActionGeometry(cancel);
            AssertSecondaryActionSemantics(cancel);
            AssertActionBinding(cancel, page.ActionCard.CancelAction);
            Assert.IsFalse(cancel.IsEnabled);
            Assert.AreEqual(string.Empty, AutomationProperties.GetHelpText(cancel));
            Assert.IsNull(ToolTipService.GetToolTip(cancel));
        }
        finally
        {
            window.Content = null;
            window.Close();
        }
    }

    [UITestMethod]
    [TestCategory("WinUI")]
    public async Task ResultActions_ReflowAtExactClientBreakpointsWithoutReplacement()
    {
        var control = new InspectionActionCard
        {
            VerticalAlignment = VerticalAlignment.Top,
            Presentation = CreateResultPresentation()
        };
        var loaded = new TaskCompletionSource<bool>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        control.Loaded += (_, _) => loaded.TrySetResult(true);
        var host = new ScrollViewer
        {
            Content = control,
            HorizontalScrollMode = ScrollMode.Disabled,
            VerticalScrollMode = ScrollMode.Enabled
        };
        var window = new Window { Content = host };
        FrameworkElement secondaryOne = (FrameworkElement)control.FindName(
            "SecondaryActionOneHost");
        FrameworkElement secondaryTwo = (FrameworkElement)control.FindName(
            "SecondaryActionTwoHost");
        FrameworkElement primary = (FrameworkElement)control.FindName(
            "PrimaryActionHost");

        try
        {
            window.Activate();
            await loaded.Task.WaitAsync(TimeSpan.FromSeconds(10));

            FrameworkElement panel = (FrameworkElement)control.FindName(
                "ResultButtonPanel");
            FrameworkElement[] allHosts = [secondaryOne, secondaryTwo, primary];
            foreach (double width in new[] { 888d, 887d, 600d, 599d })
            {
                await ResizeClientAndWaitAsync(window, control, width);
                for (int count = 1; count <= 3; count++)
                {
                    control.Presentation = CreateResultPresentation(count);
                    control.UpdateLayout();
                    await Task.Yield();
                    control.UpdateLayout();

                    FrameworkElement[] visible = allHosts.Take(count).ToArray();
                    Assert.IsTrue(allHosts.Skip(count).All(host =>
                        host.Visibility == Visibility.Collapsed),
                        $"hidden host visibility at width={width}, count={count}");
                    if (width >= 600d)
                    {
                        AssertHorizontalActionLayout(panel, visible);
                    }
                    else
                    {
                        AssertVerticalActionLayout(panel, visible);
                    }

                    Border result = Assert.IsInstanceOfType<Border>(
                        control.FindName("ResultView"));
                    foreach (FrameworkElement visibleHost in visible)
                    {
                        AssertElementContained(
                            visibleHost,
                            result,
                            $"action host at width={width}, count={count}");
                    }
                }
            }

            Assert.AreSame(secondaryOne, control.FindName("SecondaryActionOneHost"));
            Assert.AreSame(secondaryTwo, control.FindName("SecondaryActionTwoHost"));
            Assert.AreSame(primary, control.FindName("PrimaryActionHost"));

            control.Presentation = CreateResultPresentation();
            await ResizeClientAndWaitAsync(window, control, 260d);
            Button[] buttons =
            [
                FindButton(control, "SecondaryActionOneButton"),
                FindButton(control, "SecondaryActionTwoButton"),
                FindButton(control, "PrimaryActionButton")
            ];
            foreach (Button button in buttons)
            {
                TextBlock label = Assert.IsInstanceOfType<TextBlock>(
                    button.Content);
                label.FontSize *= 2d;
            }
            control.UpdateLayout();
            await Task.Yield();
            control.UpdateLayout();

            AssertVerticalActionLayout(panel, allHosts);
            Assert.IsTrue(buttons.Any(button =>
                    button.ActualHeight > button.MinHeight + 1d),
                "representative 200% labels must grow rather than clip");
            foreach (Button button in buttons)
            {
                AssertButtonContentContained(button);
            }
        }
        finally
        {
            window.Content = null;
            window.Close();
        }
    }

    private static InspectionActionCardPresentation CreateResultPresentation(
        int visibleCount = 3) =>
        new()
        {
            Mode = InspectionActionCardMode.Result,
            Title = "Next step",
            Message = "Choose what to do next.",
            SecondaryActionOne = visibleCount >= 1
                ? VisibleAction(
                    "Choose another",
                    "secondary-one",
                    enabled: true)
                : InspectionActionPresentation.Hidden,
            SecondaryActionTwo = visibleCount >= 2
                ? VisibleAction(
                    "View technical report",
                    "secondary-two",
                    enabled: false,
                    help: "Coming later")
                : InspectionActionPresentation.Hidden,
            PrimaryAction = visibleCount >= 3
                ? VisibleAction(
                    "Check hardware fit",
                    "primary",
                    enabled: false,
                    help: "Coming later")
                : InspectionActionPresentation.Hidden
        };

    private static InspectionActionPresentation VisibleAction(
        string text,
        string actionId,
        bool enabled,
        string help = "") =>
        new()
        {
            Text = text,
            Command = PresentationTestData.CreateCommand(enabled),
            CommandParameter = $"{actionId}-parameter",
            AutomationName = text,
            ActionId = actionId,
            AutomationHelpText = help,
            IsEnabled = enabled,
            Visibility = Visibility.Visible
        };

    private static void AssertFutureHelp(
        InspectionActionCard control,
        string slot,
        string expectedLabel)
    {
        Button button = FindButton(control, $"{slot}Button");
        TextBlock help = (TextBlock)control.FindName($"{slot}FutureHelpText");

        Assert.IsFalse(button.IsEnabled);
        Assert.AreEqual(expectedLabel, AutomationProperties.GetName(button));
        Assert.AreEqual("Coming later", AutomationProperties.GetHelpText(button));
        Assert.AreEqual("Coming later", ToolTipService.GetToolTip(button));
        Assert.AreEqual(Visibility.Visible, help.Visibility);
        Assert.AreEqual("Coming later", help.Text);
        Assert.AreEqual(
            $"{expectedLabel}. Coming later",
            AutomationProperties.GetName(help));
        Assert.AreEqual("Coming later", AutomationProperties.GetHelpText(help));
    }

    private static Button FindButton(InspectionActionCard control, string name) =>
        (Button)control.FindName(name);

    private static void AssertFutureHelpGeometry(
        InspectionActionCard control,
        string slot,
        Button button)
    {
        TextBlock help = Assert.IsInstanceOfType<TextBlock>(
            control.FindName($"{slot}FutureHelpText"));
        StackPanel host = Assert.IsInstanceOfType<StackPanel>(
            control.FindName($"{slot}Host"));

        Assert.AreEqual(Visibility.Visible, help.Visibility);
        Assert.AreEqual(4d, VerticalGap(button, help, host), 1d,
            $"{slot} button-to-help gap");
        Assert.AreEqual(
            host.ActualWidth / 2d,
            HorizontalCentre(help, host),
            1d,
            $"{slot} future help is centred");
    }

    private static IEnumerable<DependencyObject> EnumerateDescendants(
        DependencyObject root)
    {
        int count = VisualTreeHelper.GetChildrenCount(root);
        for (int index = 0; index < count; index++)
        {
            DependencyObject child = VisualTreeHelper.GetChild(root, index);
            yield return child;
            foreach (DependencyObject descendant in EnumerateDescendants(child))
            {
                yield return descendant;
            }
        }
    }

    private static double VerticalGap(
        FrameworkElement upper,
        FrameworkElement lower,
        UIElement root)
    {
        Windows.Foundation.Point upperOrigin = upper.TransformToVisual(root)
            .TransformPoint(default);
        Windows.Foundation.Point lowerOrigin = lower.TransformToVisual(root)
            .TransformPoint(default);
        return lowerOrigin.Y - (upperOrigin.Y + upper.ActualHeight);
    }

    private static double HorizontalCentre(
        FrameworkElement element,
        UIElement root)
    {
        Windows.Foundation.Point origin = element.TransformToVisual(root)
            .TransformPoint(default);
        return origin.X + (element.ActualWidth / 2d);
    }

    private static void AssertElementContained(
        FrameworkElement element,
        FrameworkElement container,
        string context)
    {
        Windows.Foundation.Point origin = element.TransformToVisual(container)
            .TransformPoint(default);
        Assert.IsTrue(origin.X >= -0.01d && origin.Y >= -0.01d,
            $"{context} begins inside its result card");
        Assert.IsTrue(
            origin.X + element.ActualWidth <= container.ActualWidth + 0.01d &&
            origin.Y + element.ActualHeight <= container.ActualHeight + 0.01d,
            $"{context} ends inside its result card");
    }

    private static void AssertActionStyle(
        InspectionActionCard control,
        Button button,
        string resourceKey,
        string expectedNativeStyleResourceKey)
    {
        Assert.IsTrue(
            control.Resources.ContainsKey(resourceKey),
            $"{resourceKey} must be owned by InspectionActionCard");
        Style style = Assert.IsInstanceOfType<Style>(
            control.Resources[resourceKey]);

        Assert.AreSame(style, button.Style);
        Assert.AreEqual(typeof(Button), style.TargetType);
        Assert.AreSame(
            Application.Current.Resources[expectedNativeStyleResourceKey],
            style.BasedOn,
            $"{resourceKey} must derive from {expectedNativeStyleResourceKey}");
        Assert.IsTrue(button.UseSystemFocusVisuals);
    }

    private static void AssertActionLabel(Button button, string expectedText)
    {
        TextBlock label = Assert.IsInstanceOfType<TextBlock>(button.Content);

        Assert.AreEqual(expectedText, label.Text);
        Assert.AreEqual(
            (ushort)600,
            label.FontWeight.Weight,
            $"{button.Name} label must use semibold weight");
    }

    private static void AssertActionGeometry(Button button)
    {
        Assert.AreEqual(new CornerRadius(11d), button.CornerRadius);
        Assert.AreEqual(new Thickness(18d, 10d, 18d, 10d), button.Padding);
        Assert.IsGreaterThanOrEqualTo(44d, button.MinHeight);
        Assert.AreEqual(46d, button.MinHeight, 0.01d);
    }

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

    private static void AssertSecondaryActionSemantics(Button button)
    {
        AssertBrushColor(Colors.White, button.Background,
            $"{button.Name} secondary background");
        AssertBrushColor(
            ColorHelper.FromArgb(0xFF, 0xC9, 0xD7, 0xE8),
            button.BorderBrush,
            $"{button.Name} secondary border");
        AssertBrushColor(
            ColorHelper.FromArgb(0xFF, 0x11, 0x18, 0x27),
            button.Foreground,
            $"{button.Name} secondary foreground");
        AssertButtonResourceColor(button, "ButtonBackgroundPointerOver", 0xF8, 0xFA, 0xFC);
        AssertButtonResourceColor(button, "ButtonBackgroundPressed", 0xEE, 0xF2, 0xF7);
        AssertButtonResourceColor(button, "ButtonBackgroundDisabled", 0xE5, 0xE7, 0xEB);
        AssertButtonResourceColor(button, "ButtonForegroundDisabled", 0x9C, 0xA3, 0xAF);
        AssertButtonResourceColor(button, "ButtonBorderBrushDisabled", 0xD1, 0xD5, 0xDB);
    }

    private static void AssertPrimaryActionSemantics(Button button)
    {
        AssertBrushColor(
            ColorHelper.FromArgb(0xFF, 0x25, 0x63, 0xEB),
            button.Background,
            "primary background");
        AssertBrushColor(
            ColorHelper.FromArgb(0xFF, 0x25, 0x63, 0xEB),
            button.BorderBrush,
            "primary border");
        AssertBrushColor(Colors.White, button.Foreground, "primary foreground");
        AssertButtonResourceColor(button, "ButtonBackgroundPointerOver", 0x1D, 0x4E, 0xD8);
        AssertButtonResourceColor(button, "ButtonBackgroundPressed", 0x1E, 0x40, 0xAF);
        AssertButtonResourceColor(button, "ButtonBackgroundDisabled", 0xE5, 0xE7, 0xEB);
        AssertButtonResourceColor(button, "ButtonForegroundDisabled", 0x9C, 0xA3, 0xAF);
        AssertButtonResourceColor(button, "ButtonBorderBrushDisabled", 0xD1, 0xD5, 0xDB);
    }

    private static void AssertBrushColor(
        Windows.UI.Color expected,
        object? actual,
        string message)
    {
        SolidColorBrush brush = Assert.IsInstanceOfType<SolidColorBrush>(actual);
        Assert.AreEqual(expected, brush.Color, message);
    }

    private static void AssertButtonResourceColor(
        Button button,
        string key,
        byte red,
        byte green,
        byte blue)
    {
        SolidColorBrush brush = Assert.IsInstanceOfType<SolidColorBrush>(
            button.Resources[key]);
        Assert.AreEqual(
            ColorHelper.FromArgb(0xFF, red, green, blue),
            brush.Color,
            $"{button.Name} {key}");
    }

    private static void AssertActionBinding(
        Button button,
        InspectionActionPresentation presentation)
    {
        Assert.AreSame(presentation.Command, button.Command);
        Assert.AreEqual(
            presentation.CommandParameter,
            button.CommandParameter);
        Assert.AreEqual(presentation.ActionId, button.Tag);
        Assert.AreEqual(presentation.IsEnabled, button.IsEnabled);
        Assert.AreEqual(presentation.Visibility, button.Visibility);
        Assert.AreEqual(
            presentation.AutomationName,
            AutomationProperties.GetName(button));
        Assert.AreEqual(
            presentation.AutomationHelpText,
            AutomationProperties.GetHelpText(button));
    }

    private static void AssertButtonContentContained(Button button)
    {
        TextBlock label = Assert.IsInstanceOfType<TextBlock>(button.Content);
        Windows.Foundation.Point origin = label.TransformToVisual(button)
            .TransformPoint(default);

        Assert.IsGreaterThanOrEqualTo(0d, origin.X);
        Assert.IsGreaterThanOrEqualTo(0d, origin.Y);
        Assert.IsLessThanOrEqualTo(
            button.ActualWidth,
            origin.X + label.ActualWidth,
            $"{button.Name} doubled label horizontal containment");
        Assert.IsLessThanOrEqualTo(
            button.ActualHeight,
            origin.Y + label.ActualHeight,
            $"{button.Name} doubled label vertical containment");
    }

    private static void AssertHostGridPosition(
        FrameworkElement host,
        int row,
        int column,
        int span)
    {
        Assert.AreEqual(row, Grid.GetRow(host));
        Assert.AreEqual(column, Grid.GetColumn(host));
        Assert.AreEqual(span, Grid.GetColumnSpan(host));
    }

    private static void AssertHorizontalActionLayout(
        FrameworkElement panel,
        FrameworkElement[] visible)
    {
        for (int index = 0; index < visible.Length; index++)
        {
            int expectedColumn = visible.Length == 1 ? 1 : index;
            AssertHostGridPosition(
                visible[index],
                row: 0,
                column: expectedColumn,
                span: 1);
        }

        Assert.IsTrue(visible.Skip(1).All(host =>
            Math.Abs(host.ActualWidth - visible[0].ActualWidth) <= 1d),
            $"horizontal widths: {string.Join(", ", visible.Select(host => host.ActualWidth))}");
        Assert.AreEqual(
            visible.Max(host => host.ActualHeight),
            panel.ActualHeight,
            1d,
            "horizontal actions must not leave unused row-spacing tails");
        if (visible.Length == 1)
        {
            Windows.Foundation.Point origin = visible[0].TransformToVisual(panel)
                .TransformPoint(new Windows.Foundation.Point());
            Assert.AreEqual(
                panel.ActualWidth / 2d,
                origin.X + (visible[0].ActualWidth / 2d),
                1d);
            Assert.IsLessThanOrEqualTo(280d, visible[0].ActualWidth);
        }
        else
        {
            Windows.Foundation.Point first = visible[0].TransformToVisual(panel)
                .TransformPoint(new Windows.Foundation.Point());
            FrameworkElement lastHost = visible[^1];
            Windows.Foundation.Point last = lastHost.TransformToVisual(panel)
                .TransformPoint(new Windows.Foundation.Point());
            Assert.AreEqual(
                first.X,
                panel.ActualWidth - last.X - lastHost.ActualWidth,
                1d,
                "the visible horizontal action group must be centred");
        }
    }

    private static void AssertVerticalActionLayout(
        FrameworkElement panel,
        FrameworkElement[] visible)
    {
        double previousBottom = 0d;
        for (int index = 0; index < visible.Length; index++)
        {
            AssertHostGridPosition(visible[index], row: index, column: 0, span: 3);
            Assert.IsGreaterThan(panel.ActualWidth * 0.9d, visible[index].ActualWidth);
            Windows.Foundation.Point origin = visible[index]
                .TransformToVisual(panel)
                .TransformPoint(default);
            Assert.IsGreaterThanOrEqualTo(
                previousBottom,
                origin.Y,
                $"compact action {index} must not overlap its predecessor");
            previousBottom = origin.Y + visible[index].ActualHeight;
        }

        Assert.AreEqual(
            visible.Sum(host => host.ActualHeight) + (12d * (visible.Length - 1)),
            panel.ActualHeight,
            1d,
            "vertical actions must contain only visible hosts and their gutters");
    }

    private static async Task ResizeClientAndWaitAsync(
        Window window,
        InspectionActionCard control,
        double width)
    {
        var reached = new TaskCompletionSource<bool>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        void CompleteWhenReady(object? sender, object eventArguments)
        {
            if (Math.Abs(control.XamlRoot.Size.Width - width) <= 1d)
            {
                reached.TrySetResult(true);
            }
        }

        void RootChanged(XamlRoot sender, XamlRootChangedEventArgs eventArguments) =>
            CompleteWhenReady(sender, eventArguments);

        control.LayoutUpdated += CompleteWhenReady;
        control.XamlRoot.Changed += RootChanged;
        try
        {
            double scale = control.XamlRoot.RasterizationScale;
            window.AppWindow.ResizeClient(new Windows.Graphics.SizeInt32(
                (int)Math.Round(width * scale),
                (int)Math.Round(720d * scale)));
            CompleteWhenReady(null, EventArgs.Empty);
            await reached.Task.WaitAsync(TimeSpan.FromSeconds(10));
            control.UpdateLayout();
        }
        finally
        {
            control.LayoutUpdated -= CompleteWhenReady;
            control.XamlRoot.Changed -= RootChanged;
        }
    }
}
