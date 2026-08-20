using GraniteEdgeAI.Features.ModelInspection.Controls;
using GraniteEdgeAI.Features.ModelInspection.Models;
using GraniteEdgeAI.Features.ModelInspection.Presentation;
using GraniteEdgeAI.Features.ModelInspection.ViewModels;
using GraniteEdgeAI.UnitTests.Features.ModelInspection.Presentation;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
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

            Assert.AreEqual(840d, control.ActualWidth, 0.01);
            Border result = (Border)control.FindName("ResultView");
            Assert.AreEqual(0d, result.MinHeight, 0.01d,
                "the action surface must use its natural content height");
            Assert.AreEqual(new Thickness(20d, 17d, 20d, 17d), result.Padding,
                "compact result action padding");
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

    private static void AssertActionGeometry(Button button)
    {
        CornerRadius expectedCornerRadius =
            (CornerRadius)Application.Current.Resources[
                "InspectionActionCornerRadius"];
        Thickness expectedPadding =
            (Thickness)Application.Current.Resources[
                "InspectionActionPadding"];

        Assert.AreEqual(expectedCornerRadius, button.CornerRadius);
        Assert.AreEqual(new CornerRadius(10d), button.CornerRadius);
        Assert.AreEqual(expectedPadding, button.Padding);
        Assert.AreEqual(new Thickness(18d, 10d, 18d, 10d), button.Padding);
        Assert.IsGreaterThanOrEqualTo(44d, button.MinHeight);
    }

    private static void AssertSecondaryActionSemantics(Button button)
    {
        AssertUsesResource(
            "InspectionSurfaceBrush",
            button.Background,
            $"{button.Name} secondary background");
        AssertUsesResource(
            "InspectionBorderControlBrush",
            button.BorderBrush,
            $"{button.Name} secondary border");
        AssertUsesResource(
            "InspectionTextSecondaryStrongBrush",
            button.Foreground,
            $"{button.Name} secondary foreground");
    }

    private static void AssertPrimaryActionSemantics(Button button)
    {
        AssertUsesResource(
            "InspectionPrimaryBlueBrush",
            button.Background,
            "primary background");
        AssertUsesResource(
            "InspectionPrimaryBlueBrush",
            button.BorderBrush,
            "primary border");
        AssertUsesResource(
            "InspectionSurfaceBrush",
            button.Foreground,
            "primary foreground");
    }

    private static void AssertUsesResource(
        string resourceKey,
        object? actual,
        string message)
    {
        Assert.AreSame(
            Application.Current.Resources[resourceKey],
            actual,
            message);
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
