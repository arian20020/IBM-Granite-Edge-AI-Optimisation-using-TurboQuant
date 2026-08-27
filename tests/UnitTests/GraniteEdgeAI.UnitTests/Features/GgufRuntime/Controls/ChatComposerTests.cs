using System.Collections;
using System.Reflection;
using GraniteEdgeAI.Features.GgufRuntime.Attachments;
using GraniteEdgeAI.Features.GgufRuntime.Controls;
using GraniteEdgeAI.UnitTests.Features.ModelInspection.Visual;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Automation.Peers;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using Microsoft.VisualStudio.TestTools.UnitTesting.AppContainer;

namespace GraniteEdgeAI.UnitTests.Features.GgufRuntime.Controls;

[TestClass]
public sealed class ChatComposerTests
{
    [UITestMethod]
    [TestCategory("WinUI")]
    public void ComposerUsesSingleSurfaceAndCenteredGrowingPrompt()
    {
        var composer = new ChatComposer();
        Border surface = Assert.IsInstanceOfType<Border>(
            composer.FindName("ComposerSurface"));
        Border focusVisual = Assert.IsInstanceOfType<Border>(
            composer.FindName("ComposerFocusVisual"));
        Button attachment = Assert.IsInstanceOfType<Button>(
            composer.FindName("AttachmentButton"));
        Grid promptRow = Assert.IsInstanceOfType<Grid>(
            composer.FindName("PromptRow"));
        TextBox prompt = Assert.IsInstanceOfType<TextBox>(
            composer.FindName("PromptTextBox"));
        Button send = Assert.IsInstanceOfType<Button>(
            composer.FindName("SendButton"));
        Button stop = Assert.IsInstanceOfType<Button>(
            composer.FindName("StopButton"));
        Flyout flyout = Assert.IsInstanceOfType<Flyout>(attachment.Flyout);
        Button add = Assert.IsInstanceOfType<Button>(
            composer.FindName("AddFilesFlyoutButton"));
        TextBlock hint = Assert.IsInstanceOfType<TextBlock>(
            composer.FindName("AddFilesHintText"));
        Grid contentGrid = Assert.IsInstanceOfType<Grid>(surface.Child);
        FrameworkElement attachmentPresentation = Assert.IsInstanceOfType<FrameworkElement>(
            composer.FindName("AttachmentPresentation"));

        Assert.IsTrue(attachment.IsEnabled);
        Assert.AreEqual("Attach files", AutomationProperties.GetName(attachment));
        Assert.AreEqual("Attach files", ToolTipService.GetToolTip(attachment));
        Assert.AreEqual(
            "\uE723",
            Assert.IsInstanceOfType<FontIcon>(attachment.Content).Glyph);
        Assert.AreEqual("Add files", AutomationProperties.GetName(add));
        Assert.AreEqual("Text or Markdown · Not indexed", hint.Text);
        Assert.IsTrue(add.IsEnabled);
        Assert.AreEqual(new Thickness(6, 1, 6, 1), surface.Padding);
        Assert.AreEqual(0, contentGrid.RowSpacing);
        Assert.AreEqual(new Thickness(0, 0, 0, 8), attachmentPresentation.Margin);
        Assert.AreEqual(new CornerRadius(20), surface.CornerRadius);
        Assert.AreEqual(40, promptRow.MinHeight);
        Assert.AreEqual(40, attachment.Width);
        Assert.AreEqual(40, attachment.Height);
        Assert.AreEqual(32, prompt.MinHeight);
        Assert.AreEqual(160, prompt.MaxHeight);
        Assert.AreEqual(40, send.Height);
        Assert.AreEqual(
            "\uE724",
            Assert.IsInstanceOfType<FontIcon>(send.Content).Glyph);
        Assert.IsFalse(send.IsEnabled);
        Assert.AreEqual(40, stop.Height);
        Assert.AreSame(
            Application.Current.Resources["GgufChatPrimaryButtonStyle"],
            stop.Style);
        Assert.AreSame(
            Application.Current.Resources["GgufChatPrimaryGradientBrush"],
            stop.Background);
        Assert.IsTrue(double.IsNaN(prompt.Height), "The prompt must be free to grow.");
        Assert.AreEqual(0, prompt.BorderThickness.Left);
        Assert.AreEqual(new Thickness(10, 0, 10, 0), prompt.Padding);
        Assert.AreEqual(VerticalAlignment.Center, prompt.VerticalAlignment);
        Assert.AreEqual(VerticalAlignment.Center, prompt.VerticalContentAlignment);
        Assert.AreEqual("Type a message...", prompt.PlaceholderText);
        Assert.AreEqual(Visibility.Collapsed, focusVisual.Visibility);
        Assert.AreEqual(1, surface.BorderThickness.Left);

        composer.PromptText = "first line\nsecond line";
        Assert.AreEqual(VerticalAlignment.Top, prompt.VerticalContentAlignment);
        composer.PromptText = "one line";
        Assert.AreEqual(VerticalAlignment.Center, prompt.VerticalContentAlignment);
    }

    [UITestMethod]
    [TestCategory("WinUI")]
    public async Task HiddenAttachmentRowLeavesNoResidualComposerGap()
    {
        var picker = new SequenceKnowledgeFilePicker(new[]
        {
            Candidate(@"C:\Knowledge\unsupported.pdf")
        });
        var composer = new ChatComposer(picker);
        Border surface = Assert.IsInstanceOfType<Border>(
            composer.FindName("ComposerSurface"));
        composer.Measure(new Windows.Foundation.Size(700, double.PositiveInfinity));
        double compactHeight = surface.DesiredSize.Height;

        Assert.IsLessThanOrEqualTo(45, compactHeight);

        await composer.AddKnowledgeFilesAsync();
        composer.InvalidateMeasure();
        composer.Measure(new Windows.Foundation.Size(700, double.PositiveInfinity));

        Assert.IsGreaterThan(compactHeight, surface.DesiredSize.Height);
    }

    [UITestMethod]
    [TestCategory("WinUI")]
    public void SendAvailabilityTracksTrimmedPromptAndGenerationState()
    {
        var composer = new ChatComposer();
        Button send = Assert.IsInstanceOfType<Button>(composer.FindName("SendButton"));

        Assert.IsFalse(send.IsEnabled);

        composer.PromptText = "   ";
        Assert.IsFalse(send.IsEnabled);

        composer.PromptText = "Explain this model";
        Assert.IsTrue(send.IsEnabled);

        composer.IsGenerating = true;
        Assert.IsFalse(send.IsEnabled);

        composer.IsGenerating = false;
        Assert.IsTrue(send.IsEnabled);

        InvokeClickHandler(composer, "SendButton_Click", send);
        Assert.AreEqual(string.Empty, composer.PromptText);
        Assert.IsFalse(send.IsEnabled);
    }

    [UITestMethod]
    [TestCategory("WinUI")]
    public void PlainEnterIsTheOnlyKeyboardSubmissionGesture()
    {
        Assert.IsTrue(ChatComposer.IsSendKey(
            Windows.System.VirtualKey.Enter,
            isShiftPressed: false));
        Assert.IsFalse(ChatComposer.IsSendKey(
            Windows.System.VirtualKey.Enter,
            isShiftPressed: true));
        Assert.IsFalse(ChatComposer.IsSendKey(
            Windows.System.VirtualKey.Space,
            isShiftPressed: false));
    }

    [UITestMethod]
    [TestCategory("WinUI")]
    public void PreviewEnterSubmitsWhileShiftEnterPreservesMultilineInput()
    {
        var composer = new ChatComposer();
        var prompts = new List<string>();
        composer.SendRequested += (_, prompt) => prompts.Add(prompt);

        composer.PromptText = "send this";
        Assert.IsTrue(InvokeTryHandlePromptKeyDown(
            composer,
            Windows.System.VirtualKey.Enter,
            isShiftPressed: false));
        CollectionAssert.AreEqual(new[] { "send this" }, prompts);
        Assert.AreEqual(string.Empty, composer.PromptText);

        composer.PromptText = "keep editing";
        Assert.IsFalse(InvokeTryHandlePromptKeyDown(
            composer,
            Windows.System.VirtualKey.Enter,
            isShiftPressed: true));
        Assert.AreEqual("keep editing", composer.PromptText);
        Assert.AreEqual(1, prompts.Count);
    }

    [UITestMethod]
    [TestCategory("WinUI")]
    public void SharedSubmissionPathTrimsAndGuardsKeyboardAndPointerInput()
    {
        var composer = new ChatComposer();
        var prompts = new List<string>();
        composer.SendRequested += (_, prompt) => prompts.Add(prompt);

        composer.PromptText = "  explain this  ";
        Assert.IsTrue(InvokeTrySubmitPrompt(composer));
        CollectionAssert.AreEqual(new[] { "explain this" }, prompts);
        Assert.AreEqual(string.Empty, composer.PromptText);

        composer.PromptText = "   ";
        Assert.IsFalse(InvokeTrySubmitPrompt(composer));
        Assert.AreEqual(1, prompts.Count);

        composer.PromptText = "blocked while generating";
        composer.IsGenerating = true;
        Assert.IsFalse(InvokeTrySubmitPrompt(composer));
        Assert.AreEqual(1, prompts.Count);
    }

    [UITestMethod]
    [TestCategory("WinUI")]
    [DoNotParallelize]
    public async Task PromptFocusUsesOnlyTheOuterComposerFocusVisual()
    {
        var composer = new ChatComposer();
        TextBox prompt = Assert.IsInstanceOfType<TextBox>(
            composer.FindName("PromptTextBox"));
        Border focusVisual = Assert.IsInstanceOfType<Border>(
            composer.FindName("ComposerFocusVisual"));
        Button attachment = Assert.IsInstanceOfType<Button>(
            composer.FindName("AttachmentButton"));
        await using WinUiRenderHost host =
            await WinUiRenderHost.ShowAsync(composer, 700, 180);

        Assert.IsTrue(prompt.Focus(FocusState.Keyboard));
        await WaitForLayoutAsync(prompt);
        Assert.AreSame(prompt, FocusManager.GetFocusedElement(composer.XamlRoot));
        Assert.AreEqual(Visibility.Visible, focusVisual.Visibility);

        Assert.IsTrue(attachment.Focus(FocusState.Keyboard));
        await WaitForLayoutAsync(attachment);
        Assert.AreEqual(Visibility.Collapsed, focusVisual.Visibility);
    }

    [UITestMethod]
    [TestCategory("WinUI")]
    public async Task WrappedParagraphGrowsAndTopAlignsThenShrinksAndCenters()
    {
        var composer = new ChatComposer();
        TextBox prompt = Assert.IsInstanceOfType<TextBox>(
            composer.FindName("PromptTextBox"));
        await using WinUiRenderHost host =
            await WinUiRenderHost.ShowAsync(composer, 360, 260);

        composer.PromptText =
            "This long single paragraph intentionally wraps across several visual lines " +
            "inside a constrained composer width without containing any explicit newline " +
            "characters, so layout rather than text parsing must move content to the top.";
        await WaitForLayoutAsync(prompt);

        Assert.IsFalse(composer.PromptText.Contains('\r'));
        Assert.IsFalse(composer.PromptText.Contains('\n'));
        Assert.IsGreaterThan(prompt.MinHeight, prompt.ActualHeight);
        Assert.AreEqual(VerticalAlignment.Top, prompt.VerticalContentAlignment);

        composer.PromptText = "Short message";
        await WaitForLayoutAsync(prompt);

        Assert.IsLessThanOrEqualTo(prompt.MinHeight + 1, prompt.ActualHeight);
        Assert.AreEqual(VerticalAlignment.Center, prompt.VerticalContentAlignment);
    }

    [UITestMethod]
    [TestCategory("WinUI")]
    public async Task SelectionPreservesPickerOrderAndCancellationLeavesChipsUnchanged()
    {
        var picker = new SequenceKnowledgeFilePicker(
            new[]
            {
                Candidate(@"C:\Knowledge\first.md"),
                Candidate(@"C:\Knowledge\second.txt")
            },
            Array.Empty<KnowledgeFileCandidate>());
        var composer = new ChatComposer(picker);

        await InvokeAddKnowledgeFilesAsync(composer);

        ItemsControl itemsControl = GetAttachmentItems(composer);
        IList attachments = Assert.IsInstanceOfType<IList>(itemsControl.ItemsSource);
        Assert.AreEqual(2, attachments.Count);
        AssertAttachment(attachments[0], "first.md");
        AssertAttachment(attachments[1], "second.txt");
        Assert.AreEqual(Visibility.Visible, itemsControl.Visibility);

        await InvokeAddKnowledgeFilesAsync(composer);

        Assert.AreEqual(2, attachments.Count);
        AssertAttachment(attachments[0], "first.md");
        AssertAttachment(attachments[1], "second.txt");
    }

    [UITestMethod]
    [TestCategory("WinUI")]
    public async Task RemoveActionRemovesOnlyItsSelectedChip()
    {
        var picker = new SequenceKnowledgeFilePicker(new[]
        {
            Candidate(@"C:\Knowledge\first.md"),
            Candidate(@"C:\Knowledge\second.txt")
        });
        var composer = new ChatComposer(picker);
        await InvokeAddKnowledgeFilesAsync(composer);
        IList attachments = GetAttachments(composer);
        object selected = attachments[0]!;

        InvokeClickHandler(
            composer,
            "RemoveAttachment_Click",
            new Button { CommandParameter = selected });

        Assert.AreEqual(1, attachments.Count);
        AssertAttachment(attachments[0], "second.txt");
        Assert.AreEqual(
            Visibility.Visible,
            GetAttachmentItems(composer).Visibility);
    }

    [UITestMethod]
    [TestCategory("WinUI")]
    public async Task LoadedChipShowsSafeLabelsAndExposesAccessibleRemoveAction()
    {
        var picker = new SequenceKnowledgeFilePicker(new[]
        {
            Candidate(@"C:\Private\first.md")
        });
        var composer = new ChatComposer(picker);
        await composer.AddKnowledgeFilesAsync();

        await using WinUiRenderHost host =
            await WinUiRenderHost.ShowAsync(composer, 700, 220);

        string[] labels = Descendants<TextBlock>(composer)
            .Select(textBlock => textBlock.Text)
            .ToArray();
        CollectionAssert.Contains(labels, "first.md");
        CollectionAssert.Contains(labels, "Not indexed");
        Button remove = Descendants<Button>(composer).Single(button =>
            AutomationProperties.GetName(button) == "Remove first.md");
        Assert.IsTrue(remove.IsTabStop);
        Assert.AreEqual(40, remove.Width);
        Assert.AreEqual(40, remove.Height);

        InvokeClickHandler(composer, "RemoveAttachment_Click", remove);

        Assert.AreEqual(0, GetAttachments(composer).Count);
        Assert.AreEqual(Visibility.Collapsed, GetAttachmentItems(composer).Visibility);
    }

    [UITestMethod]
    [TestCategory("WinUI")]
    public async Task AttachmentAutomationTreeNeverExposesPrivatePath()
    {
        const string privateDirectory = @"C:\Private Knowledge\Customer Alpha";
        const string privatePath = privateDirectory + @"\private-notes.md";
        var picker = new SequenceKnowledgeFilePicker(new[]
        {
            Candidate(privatePath)
        });
        var composer = new ChatComposer(picker);
        await composer.AddKnowledgeFilesAsync();
        await using WinUiRenderHost host =
            await WinUiRenderHost.ShowAsync(composer, 700, 220);
        ItemsControl attachmentItems = GetAttachmentItems(composer);

        string[] automationValues = DescendantsAndSelf<FrameworkElement>(attachmentItems)
            .SelectMany(GetAutomationValues)
            .Where(value => !string.IsNullOrEmpty(value))
            .ToArray();

        CollectionAssert.Contains(automationValues, "Selected knowledge files");
        CollectionAssert.Contains(automationValues, "Remove private-notes.md");
        Assert.IsTrue(automationValues.Any(value => value.Contains(
            "private-notes.md",
            StringComparison.Ordinal)));
        foreach (string value in automationValues)
        {
            Assert.IsFalse(value.Contains(privatePath, StringComparison.OrdinalIgnoreCase));
            Assert.IsFalse(value.Contains(privateDirectory, StringComparison.OrdinalIgnoreCase));
            Assert.IsFalse(value.Contains(@"C:\", StringComparison.OrdinalIgnoreCase));
        }
    }

    [UITestMethod]
    [TestCategory("WinUI")]
    public async Task RejectionsUseFixedSafeSummaryWithoutFileNamesOrPaths()
    {
        var picker = new SequenceKnowledgeFilePicker(
            new[] { Candidate(@"C:\Knowledge\existing.md") },
            new[]
            {
                Candidate(@"C:\Knowledge\existing.md"),
                Candidate(@"C:\Private\unsupported.pdf"),
                Candidate(@"C:\Private\oversized.txt", 9L * 1024 * 1024),
                Candidate(@"C:\Private\inaccessible.md", isAccessible: false)
            });
        var composer = new ChatComposer(picker);
        await InvokeAddKnowledgeFilesAsync(composer);

        await InvokeAddKnowledgeFilesAsync(composer);

        TextBlock summary = Assert.IsInstanceOfType<TextBlock>(
            composer.FindName("RejectionSummaryText"));
        Assert.AreEqual(Visibility.Visible, summary.Visibility);
        StringAssert.Contains(summary.Text, "4 knowledge files were not added");
        StringAssert.Contains(summary.Text, "1 duplicate");
        StringAssert.Contains(summary.Text, "1 unsupported type");
        StringAssert.Contains(summary.Text, "1 too large");
        StringAssert.Contains(summary.Text, "1 inaccessible");
        Assert.IsFalse(summary.Text.Contains("existing.md", StringComparison.Ordinal));
        Assert.IsFalse(summary.Text.Contains("unsupported.pdf", StringComparison.Ordinal));
        Assert.IsFalse(summary.Text.Contains(@"C:\Private", StringComparison.Ordinal));
        Assert.AreEqual(1, GetAttachments(composer).Count);
    }

    [UITestMethod]
    [TestCategory("WinUI")]
    public async Task SendRaisesOnlyTrimmedPromptAndKeepsSelectedAttachments()
    {
        const string attachmentPath = @"C:\Private\never-send-this-name.txt";
        var picker = new SequenceKnowledgeFilePicker(
            new[] { Candidate(attachmentPath) });
        var composer = new ChatComposer(picker);
        await InvokeAddKnowledgeFilesAsync(composer);
        string? sentPrompt = null;
        composer.SendRequested += (_, prompt) => sentPrompt = prompt;
        composer.PromptText = "  explain the topic  ";

        InvokeClickHandler(composer, "SendButton_Click", new Button());

        Assert.AreEqual("explain the topic", sentPrompt);
        Assert.AreEqual(string.Empty, composer.PromptText);
        Assert.AreEqual(1, GetAttachments(composer).Count);
        Assert.IsFalse(sentPrompt!.Contains("never-send-this-name", StringComparison.Ordinal));
        Assert.IsFalse(sentPrompt.Contains(attachmentPath, StringComparison.Ordinal));
        Assert.IsFalse(sentPrompt.Contains("Not indexed", StringComparison.Ordinal));
    }

    [UITestMethod]
    [TestCategory("WinUI")]
    public void GeneratingStateDisablesPromptAndAttachmentSelection()
    {
        var composer = new ChatComposer { IsGenerating = true };
        Button attachment = Assert.IsInstanceOfType<Button>(
            composer.FindName("AttachmentButton"));
        TextBox prompt = Assert.IsInstanceOfType<TextBox>(
            composer.FindName("PromptTextBox"));
        Assert.IsInstanceOfType<Flyout>(attachment.Flyout);
        Button add = Assert.IsInstanceOfType<Button>(
            composer.FindName("AddFilesFlyoutButton"));

        Assert.IsFalse(attachment.IsEnabled);
        Assert.IsFalse(add.IsEnabled);
        Assert.IsFalse(prompt.IsEnabled);
        Assert.AreEqual(
            Visibility.Collapsed,
            Assert.IsInstanceOfType<Button>(composer.FindName("SendButton")).Visibility);
        Assert.AreEqual(
            Visibility.Visible,
            Assert.IsInstanceOfType<Button>(composer.FindName("StopButton")).Visibility);
    }

    [UITestMethod]
    [TestCategory("WinUI")]
    public async Task ConcurrentSelectionIsGatedAndRestoresAttachmentAction()
    {
        var picker = new DeferredKnowledgeFilePicker();
        var composer = new ChatComposer(picker);

        Task firstSelection = InvokeAddKnowledgeFilesAsync(composer);
        Task secondSelection = InvokeAddKnowledgeFilesAsync(composer);

        Assert.AreEqual(1, picker.InvocationCount);
        Assert.IsTrue(secondSelection.IsCompleted);
        Assert.IsFalse(
            Assert.IsInstanceOfType<Button>(composer.FindName("AttachmentButton")).IsEnabled);

        picker.Complete(new[] { Candidate(@"C:\Knowledge\guide.md") });
        await firstSelection;

        Assert.AreEqual(1, GetAttachments(composer).Count);
        Assert.IsTrue(
            Assert.IsInstanceOfType<Button>(composer.FindName("AttachmentButton")).IsEnabled);
    }

    [UITestMethod]
    [TestCategory("WinUI")]
    public async Task PickerFailureUsesFixedSafeSummaryAndRestoresSelectionAction()
    {
        const string sensitiveDiagnostic = @"C:\Private\picker-failure.txt";
        var composer = new ChatComposer(new ThrowingKnowledgeFilePicker(
            new InvalidOperationException(sensitiveDiagnostic)));

        await composer.AddKnowledgeFilesAsync();

        TextBlock summary = Assert.IsInstanceOfType<TextBlock>(
            composer.FindName("RejectionSummaryText"));
        Assert.AreEqual(Visibility.Visible, summary.Visibility);
        Assert.AreEqual(
            "Knowledge files could not be selected. Try again.",
            summary.Text);
        Assert.IsFalse(summary.Text.Contains(sensitiveDiagnostic, StringComparison.Ordinal));
        Assert.AreEqual(0, GetAttachments(composer).Count);
        Assert.IsTrue(
            Assert.IsInstanceOfType<Button>(composer.FindName("AttachmentButton")).IsEnabled);
    }

    [UITestMethod]
    [TestCategory("WinUI")]
    public async Task PickerCancellationExceptionLeavesStateUnchangedAndGateReusable()
    {
        var picker = new CancellingThenSuccessfulKnowledgeFilePicker(
            new[] { Candidate(@"C:\Knowledge\after-cancel.md") });
        var composer = new ChatComposer(picker)
        {
            PromptText = "Draft remains"
        };
        TextBlock summary = Assert.IsInstanceOfType<TextBlock>(
            composer.FindName("RejectionSummaryText"));

        await composer.AddKnowledgeFilesAsync();

        Assert.AreEqual(1, picker.InvocationCount);
        Assert.AreEqual(0, GetAttachments(composer).Count);
        Assert.AreEqual("Draft remains", composer.PromptText);
        Assert.AreEqual(string.Empty, summary.Text);
        Assert.AreEqual(Visibility.Collapsed, summary.Visibility);
        Assert.IsTrue(
            Assert.IsInstanceOfType<Button>(composer.FindName("AttachmentButton")).IsEnabled);

        await composer.AddKnowledgeFilesAsync();

        Assert.AreEqual(2, picker.InvocationCount);
        Assert.AreEqual(1, GetAttachments(composer).Count);
        AssertAttachment(GetAttachments(composer)[0], "after-cancel.md");
    }

    [UITestMethod]
    [TestCategory("WinUI")]
    public void StopSquareHasItsOwnCenteredLayoutCell()
    {
        var composer = new ChatComposer { IsGenerating = true };
        Rectangle icon = Assert.IsInstanceOfType<Rectangle>(
            composer.FindName("StopSquare"));
        TextBlock label = Assert.IsInstanceOfType<TextBlock>(
            composer.FindName("StopLabel"));

        Assert.AreEqual(HorizontalAlignment.Center, icon.HorizontalAlignment);
        Assert.AreEqual(VerticalAlignment.Center, icon.VerticalAlignment);
        Assert.AreNotEqual(Grid.GetColumn(icon), Grid.GetColumn(label));
        Assert.AreEqual(Visibility.Visible,
            Assert.IsInstanceOfType<Button>(composer.FindName("StopButton")).Visibility);
    }

    private static KnowledgeFileCandidate Candidate(
        string path,
        long sizeInBytes = 1024,
        bool isAccessible = true) =>
        new(path, sizeInBytes, isAccessible);

    private static Task InvokeAddKnowledgeFilesAsync(ChatComposer composer) =>
        composer.AddKnowledgeFilesAsync();

    private static void InvokeClickHandler(
        ChatComposer composer,
        string methodName,
        Button sender)
    {
        MethodInfo? method = typeof(ChatComposer).GetMethod(
            methodName,
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.IsNotNull(method);
        method.Invoke(composer, new object[] { sender, new RoutedEventArgs() });
    }

    private static bool InvokeTrySubmitPrompt(ChatComposer composer)
    {
        MethodInfo? method = typeof(ChatComposer).GetMethod(
            "TrySubmitPrompt",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.IsNotNull(method);
        return Assert.IsInstanceOfType<bool>(method.Invoke(composer, null));
    }

    private static bool InvokeTryHandlePromptKeyDown(
        ChatComposer composer,
        Windows.System.VirtualKey key,
        bool isShiftPressed)
    {
        MethodInfo? method = typeof(ChatComposer).GetMethod(
            "TryHandlePromptKeyDown",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.IsNotNull(method);
        return Assert.IsInstanceOfType<bool>(method.Invoke(
            composer,
            new object[] { key, isShiftPressed }));
    }

    private static ItemsControl GetAttachmentItems(ChatComposer composer) =>
        Assert.IsInstanceOfType<ItemsControl>(composer.FindName("AttachmentItems"));

    private static IList GetAttachments(ChatComposer composer) =>
        Assert.IsInstanceOfType<IList>(GetAttachmentItems(composer).ItemsSource);

    private static void AssertAttachment(object? value, string expectedFileName)
    {
        KnowledgeAttachment attachment = Assert.IsInstanceOfType<KnowledgeAttachment>(value);
        Assert.AreEqual(expectedFileName, attachment.FileName);
        Assert.AreEqual("Not indexed", attachment.StateText);
    }

    private static IEnumerable<T> Descendants<T>(DependencyObject root)
        where T : DependencyObject
    {
        for (int index = 0; index < VisualTreeHelper.GetChildrenCount(root); index++)
        {
            DependencyObject child = VisualTreeHelper.GetChild(root, index);
            if (child is T match)
            {
                yield return match;
            }

            foreach (T descendant in Descendants<T>(child))
            {
                yield return descendant;
            }
        }
    }

    private static IEnumerable<T> DescendantsAndSelf<T>(DependencyObject root)
        where T : DependencyObject
    {
        if (root is T match)
        {
            yield return match;
        }

        foreach (T descendant in Descendants<T>(root))
        {
            yield return descendant;
        }
    }

    private static IEnumerable<string> GetAutomationValues(FrameworkElement element)
    {
        yield return AutomationProperties.GetName(element);
        yield return AutomationProperties.GetHelpText(element);
        yield return AutomationProperties.GetItemStatus(element);

        AutomationPeer peer =
            FrameworkElementAutomationPeer.FromElement(element) ??
            FrameworkElementAutomationPeer.CreatePeerForElement(element) ??
            new FrameworkElementAutomationPeer(element);
        yield return peer.GetName();
        yield return peer.GetHelpText();
        yield return peer.GetItemStatus();
    }

    private static async Task WaitForLayoutAsync(FrameworkElement element)
    {
        for (int pass = 0; pass < 2; pass++)
        {
            var completion = new TaskCompletionSource<bool>(
                TaskCreationOptions.RunContinuationsAsynchronously);
            void OnLayoutUpdated(object? sender, object eventArguments) =>
                completion.TrySetResult(true);

            element.LayoutUpdated += OnLayoutUpdated;
            try
            {
                element.InvalidateMeasure();
                element.InvalidateArrange();
                if (!element.DispatcherQueue.TryEnqueue(() => element.UpdateLayout()))
                {
                    throw new InvalidOperationException(
                        "The UI dispatcher rejected a layout pass.");
                }

                await completion.Task.WaitAsync(TimeSpan.FromSeconds(5));
            }
            finally
            {
                element.LayoutUpdated -= OnLayoutUpdated;
            }
        }
    }

    private sealed class SequenceKnowledgeFilePicker : IKnowledgeFilePicker
    {
        private readonly Queue<IReadOnlyList<KnowledgeFileCandidate>> selections;

        internal SequenceKnowledgeFilePicker(
            params IReadOnlyList<KnowledgeFileCandidate>[] selections) =>
            this.selections = new Queue<IReadOnlyList<KnowledgeFileCandidate>>(selections);

        public Task<IReadOnlyList<KnowledgeFileCandidate>> PickAsync() =>
            Task.FromResult(selections.Count == 0
                ? (IReadOnlyList<KnowledgeFileCandidate>)Array.Empty<KnowledgeFileCandidate>()
                : selections.Dequeue());
    }

    private sealed class DeferredKnowledgeFilePicker : IKnowledgeFilePicker
    {
        private readonly TaskCompletionSource<IReadOnlyList<KnowledgeFileCandidate>> completion =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        internal int InvocationCount { get; private set; }

        public Task<IReadOnlyList<KnowledgeFileCandidate>> PickAsync()
        {
            InvocationCount++;
            return completion.Task;
        }

        internal void Complete(IReadOnlyList<KnowledgeFileCandidate> candidates) =>
            completion.SetResult(candidates);
    }

    private sealed class ThrowingKnowledgeFilePicker(Exception exception) : IKnowledgeFilePicker
    {
        public Task<IReadOnlyList<KnowledgeFileCandidate>> PickAsync() =>
            Task.FromException<IReadOnlyList<KnowledgeFileCandidate>>(exception);
    }

    private sealed class CancellingThenSuccessfulKnowledgeFilePicker(
        IReadOnlyList<KnowledgeFileCandidate> successfulSelection) : IKnowledgeFilePicker
    {
        internal int InvocationCount { get; private set; }

        public Task<IReadOnlyList<KnowledgeFileCandidate>> PickAsync()
        {
            InvocationCount++;
            return InvocationCount == 1
                ? Task.FromException<IReadOnlyList<KnowledgeFileCandidate>>(
                    new OperationCanceledException())
                : Task.FromResult(successfulSelection);
        }
    }
}
