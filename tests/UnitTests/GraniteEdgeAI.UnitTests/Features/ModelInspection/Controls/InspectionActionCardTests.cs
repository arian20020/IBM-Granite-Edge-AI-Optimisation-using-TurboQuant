using GraniteEdgeAI.Features.ModelInspection.Controls;
using GraniteEdgeAI.Features.ModelInspection.Models;
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
        var control = new InspectionActionCard
        {
            Width = 840,
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top,
            Presentation = CreateResultPresentation()
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
            Assert.AreEqual(
                140d,
                ((Border)control.FindName("ResultView")).ActualHeight,
                1d,
                "The standard desktop action surface must retain the approved height.");
            Assert.IsGreaterThanOrEqualTo(44d, secondaryOne.MinHeight);
            Assert.AreEqual(46d, secondaryOne.MinHeight, 0.01);
            Assert.AreEqual(46d, secondaryTwo.MinHeight, 0.01);
            Assert.AreEqual(46d, primary.MinHeight, 0.01);
            CollectionAssert.AreEqual(
                new[] { 0, 1, 2 },
                new[]
                {
                    secondaryOne.TabIndex,
                    secondaryTwo.TabIndex,
                    primary.TabIndex
                });
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
    public void InspectingCancel_DoesNotReceiveFutureActionHelp()
    {
        var control = new InspectionActionCard
        {
            Presentation = new InspectionActionCardPresentation
            {
                Mode = InspectionActionCardMode.Inspecting,
                CancelAction = VisibleAction("Cancel inspection", enabled: false)
            }
        };
        Button cancel = FindButton(control, "CancelActionButton");

        Assert.IsFalse(cancel.IsEnabled);
        Assert.AreEqual(string.Empty, AutomationProperties.GetHelpText(cancel));
        Assert.IsNull(ToolTipService.GetToolTip(cancel));
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

            await ResizeClientAndWaitAsync(window, control, 888d, [0, 0, 0]);
            AssertHostGridPosition(secondaryOne, row: 0, column: 0, span: 1);
            AssertHostGridPosition(secondaryTwo, row: 0, column: 1, span: 1);
            AssertHostGridPosition(primary, row: 0, column: 2, span: 1);
            double wideHeight = ((Border)control.FindName("ResultView")).ActualHeight;

            await ResizeClientAndWaitAsync(window, control, 600d, [0, 0, 1]);
            AssertHostGridPosition(secondaryOne, row: 0, column: 0, span: 1);
            AssertHostGridPosition(secondaryTwo, row: 0, column: 1, span: 1);
            AssertHostGridPosition(primary, row: 1, column: 0, span: 2);
            FrameworkElement panel = (FrameworkElement)control.FindName(
                "ResultButtonPanel");
            Assert.IsGreaterThan(panel.ActualWidth * 0.4d, secondaryOne.ActualWidth);
            Assert.IsGreaterThan(panel.ActualWidth * 0.4d, secondaryTwo.ActualWidth);
            Assert.IsGreaterThan(panel.ActualWidth * 0.9d, primary.ActualWidth);
            double mediumHeight = ((Border)control.FindName("ResultView")).ActualHeight;

            await ResizeClientAndWaitAsync(window, control, 599d, [0, 1, 2]);
            AssertHostGridPosition(secondaryOne, row: 0, column: 0, span: 3);
            AssertHostGridPosition(secondaryTwo, row: 1, column: 0, span: 3);
            AssertHostGridPosition(primary, row: 2, column: 0, span: 3);
            Assert.IsGreaterThan(panel.ActualWidth * 0.9d, secondaryOne.ActualWidth);
            Assert.IsGreaterThan(panel.ActualWidth * 0.9d, secondaryTwo.ActualWidth);
            Assert.IsGreaterThan(panel.ActualWidth * 0.9d, primary.ActualWidth);
            double narrowHeight = ((Border)control.FindName("ResultView")).ActualHeight;

            Assert.AreSame(secondaryOne, control.FindName("SecondaryActionOneHost"));
            Assert.AreSame(secondaryTwo, control.FindName("SecondaryActionTwoHost"));
            Assert.AreSame(primary, control.FindName("PrimaryActionHost"));
            Assert.AreEqual(140d, wideHeight, 1d);
            Assert.IsGreaterThan(wideHeight, mediumHeight);
            Assert.IsGreaterThan(mediumHeight, narrowHeight);
        }
        finally
        {
            window.Content = null;
            window.Close();
        }
    }

    private static InspectionActionCardPresentation CreateResultPresentation() =>
        new()
        {
            Mode = InspectionActionCardMode.Result,
            Title = "Next step",
            Message = "Choose what to do next.",
            SecondaryActionOne = VisibleAction("Choose another", enabled: true),
            SecondaryActionTwo = VisibleAction(
                "View technical report",
                enabled: false,
                help: "Coming later"),
            PrimaryAction = VisibleAction(
                "Check hardware fit",
                enabled: false,
                help: "Coming later")
        };

    private static InspectionActionPresentation VisibleAction(
        string text,
        bool enabled,
        string help = "") =>
        new()
        {
            Text = text,
            AutomationName = text,
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

    private static async Task ResizeClientAndWaitAsync(
        Window window,
        InspectionActionCard control,
        double width,
        int[] expectedRows)
    {
        var reached = new TaskCompletionSource<bool>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        void CompleteWhenReady(object? sender, object eventArguments)
        {
            int[] actualRows =
            [
                Grid.GetRow((FrameworkElement)control.FindName("SecondaryActionOneHost")),
                Grid.GetRow((FrameworkElement)control.FindName("SecondaryActionTwoHost")),
                Grid.GetRow((FrameworkElement)control.FindName("PrimaryActionHost"))
            ];
            if (Math.Abs(control.XamlRoot.Size.Width - width) <= 1d &&
                actualRows.SequenceEqual(expectedRows))
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
