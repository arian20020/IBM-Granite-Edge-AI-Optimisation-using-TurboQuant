using System.Collections;
using System.Reflection;
using GraniteEdgeAI.Features.GgufRuntime.Attachments;
using GraniteEdgeAI.Features.GgufRuntime.Controls;
using GraniteEdgeAI.UnitTests.Features.ModelInspection.Visual;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using Microsoft.VisualStudio.TestTools.UnitTesting.AppContainer;

namespace GraniteEdgeAI.UnitTests.Features.GgufRuntime.Controls;

[TestClass]
public sealed class ChatComposerTests
{
    [UITestMethod]
    [TestCategory("WinUI")]
    public void ComposerExposesKnowledgeAttachmentActionAndCenteredGrowingPrompt()
    {
        var composer = new ChatComposer();
        Button attachment = Assert.IsInstanceOfType<Button>(
            composer.FindName("AttachmentButton"));
        TextBox prompt = Assert.IsInstanceOfType<TextBox>(
            composer.FindName("PromptTextBox"));
        MenuFlyout flyout = Assert.IsInstanceOfType<MenuFlyout>(attachment.Flyout);
        MenuFlyoutItem add = Assert.IsInstanceOfType<MenuFlyoutItem>(flyout.Items.Single());

        Assert.IsTrue(attachment.IsEnabled);
        Assert.AreEqual("Add knowledge files", AutomationProperties.GetName(attachment));
        Assert.AreEqual("Add knowledge files...", add.Text);
        Assert.IsTrue(add.IsEnabled);
        Assert.AreEqual(44, prompt.MinHeight);
        Assert.AreEqual(160, prompt.MaxHeight);
        Assert.IsTrue(double.IsNaN(prompt.Height), "The prompt must be free to grow.");
        Assert.AreEqual(VerticalAlignment.Center, prompt.VerticalAlignment);
        Assert.AreEqual(VerticalAlignment.Center, prompt.VerticalContentAlignment);
        Assert.AreEqual("Type a message...", prompt.PlaceholderText);

        composer.PromptText = "first line\nsecond line";
        Assert.AreEqual(VerticalAlignment.Top, prompt.VerticalContentAlignment);
        composer.PromptText = "one line";
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
        MenuFlyout flyout = Assert.IsInstanceOfType<MenuFlyout>(attachment.Flyout);
        MenuFlyoutItem add = Assert.IsInstanceOfType<MenuFlyoutItem>(flyout.Items.Single());

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
}
