using GraniteEdgeAI.Features.GgufRuntime;
using GraniteEdgeAI.Features.GgufRuntime.Attachments;
using GraniteEdgeAI.Features.GgufRuntime.Controls;
using GraniteEdgeAI.Features.GgufRuntime.History;
using GraniteEdgeAI.UnitTests.Features.ModelInspection.Visual;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Automation.Peers;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.VisualStudio.TestTools.UnitTesting.AppContainer;

namespace GraniteEdgeAI.UnitTests.Features.GgufRuntime;

[TestClass]
public sealed class ChatAccessibilityTests
{
    [UITestMethod]
    [TestCategory("WinUI")]
    public async Task PageActionsExposeUniqueNamesAndAcceptKeyboardFocus()
    {
        var page = new ChatPage();
        await using WinUiRenderHost host = await WinUiRenderHost.ShowAsync(page, 1200, 800);
        var navigationPeer = new ButtonAutomationPeer(FindButton(page, "CompactNavigationButton"));
        ((Microsoft.UI.Xaml.Automation.Provider.IInvokeProvider)navigationPeer.GetPattern(PatternInterface.Invoke)).Invoke();
        await Task.Delay(50);
        Button[] actions =
        [
            FindButton(page, "NewChatButton"),
            FindButton(page, "ImportModelButton"),
            FindButton(page, "SettingsButton")
        ];

        CollectionAssert.AreEquivalent(
            new[] { "Start a new chat", "Import another model", "Settings" },
            actions.Select(AutomationProperties.GetName).ToArray());
        AssertUniqueNonEmptyNames(actions);
        foreach (Button action in actions)
        {
            Assert.IsTrue(action.IsTabStop);
            Assert.IsTrue(action.Focus(FocusState.Keyboard));
            Assert.AreSame(action, FocusManager.GetFocusedElement(page.XamlRoot));
        }

        ListView transcript = Assert.IsInstanceOfType<ListView>(page.FindName("TranscriptList"));
        Assert.AreEqual("Conversation messages", AutomationProperties.GetName(transcript));
        Assert.AreEqual("Off", AutomationProperties.GetLiveSetting(transcript).ToString(),
            "The whole transcript must not announce every streamed token.");
        Guid conversationId = Guid.NewGuid();
        ChatMessage assistant = ChatMessage.Assistant("Partial", ChatCompletionStatus.Streaming, DateTimeOffset.UtcNow);
        page.SynchronizeTranscript(conversationId, [assistant], false);
        var bubble = (ChatMessageBubble)transcript.Items[0];
        Assert.AreEqual(Microsoft.UI.Xaml.Automation.Peers.AutomationLiveSetting.Off,
            AutomationProperties.GetLiveSetting(bubble));
        page.SynchronizeTranscript(conversationId, [assistant.WithContent("Completed reply", ChatCompletionStatus.Completed)], false);
        Assert.AreEqual(Microsoft.UI.Xaml.Automation.Peers.AutomationLiveSetting.Polite,
            AutomationProperties.GetLiveSetting(bubble));
    }

    [UITestMethod]
    [TestCategory("WinUI")]
    public async Task ComposerActionsExposeUniqueNamesAndAcceptKeyboardFocus()
    {
        var composer = new ChatComposer();
        composer.PromptText = "Ready to send";
        await using WinUiRenderHost host = await WinUiRenderHost.ShowAsync(composer, 700, 220);
        Button attachment = FindButton(composer, "AttachmentButton");
        Button send = FindButton(composer, "SendButton");

        AssertUniqueNonEmptyNames([attachment, send]);
        AssertKeyboardFocusable(composer, attachment);
        AssertKeyboardFocusable(composer, send);

        composer.IsGenerating = true;
        Button stop = FindButton(composer, "StopButton");

        CollectionAssert.AreEquivalent(
            new[] { "Attach files", "Send message", "Stop generation" },
            new[]
            {
                AutomationProperties.GetName(attachment),
                AutomationProperties.GetName(send),
                AutomationProperties.GetName(stop)
            });
        AssertUniqueNonEmptyNames([attachment, send, stop]);
        AssertKeyboardFocusable(composer, stop);
    }

    [UITestMethod]
    [TestCategory("WinUI")]
    public async Task AttachmentCollectionAndRealizedRemoveActionsAreAccessibleWithoutExtraTabStops()
    {
        var composer = new ChatComposer(new FixedKnowledgeFilePicker(
            new KnowledgeFileCandidate(@"C:\Private\guide.md", 1024, true)));
        await composer.AddKnowledgeFilesAsync();
        await using WinUiRenderHost host = await WinUiRenderHost.ShowAsync(composer, 700, 240);
        ItemsControl collection = Assert.IsInstanceOfType<ItemsControl>(
            composer.FindName("AttachmentItems"));
        Button remove = Descendants<Button>(collection).Single(button =>
            AutomationProperties.GetName(button) == "Remove guide.md");

        Assert.AreEqual("Selected attachments", AutomationProperties.GetName(collection));
        Assert.IsFalse(collection.IsTabStop);
        Assert.AreEqual("Remove guide.md", AutomationProperties.GetName(remove));
        AssertKeyboardFocusable(composer, remove);
    }

    [UITestMethod]
    [TestCategory("WinUI")]
    public void BrandImagesAreDecorativeAndDoNotDuplicateTheApplicationName()
    {
        var page = new ChatPage();
        Image[] brandImages =
        [
            Assert.IsInstanceOfType<Image>(page.FindName("BrandLockup")),
            Assert.IsInstanceOfType<Image>(page.FindName("EmptyStateBrandMark"))
        ];

        foreach (Image image in brandImages)
        {
            Assert.AreEqual(
                AccessibilityView.Raw,
                AutomationProperties.GetAccessibilityView(image));
            Assert.AreEqual(string.Empty, AutomationProperties.GetName(image));
            Assert.IsFalse(image.IsTabStop);
        }
    }

    [UITestMethod]
    [TestCategory("WinUI")]
    public async Task OnlyLatestLimitedAssistantOffersKeyboardContinuation()
    {
        var page = new ChatPage();
        ChatMessage first = ChatMessage.Assistant(
            "First partial",
            ChatCompletionStatus.LimitReached,
            DateTimeOffset.UtcNow);
        ChatMessage latest = ChatMessage.Assistant(
            "Latest partial",
            ChatCompletionStatus.LimitReached,
            DateTimeOffset.UtcNow.AddSeconds(1));
        page.SynchronizeTranscript(
            Guid.NewGuid(),
            [first, latest],
            forceFollowLatest: false);
        await using WinUiRenderHost host =
            await WinUiRenderHost.ShowAsync(page, 900, 520);
        ListView transcript = Assert.IsInstanceOfType<ListView>(
            page.FindName("TranscriptList"));
        Button firstAction = Assert.IsInstanceOfType<Button>(
            Assert.IsInstanceOfType<ChatMessageBubble>(transcript.Items[0])
                .FindName("ContinueButton"));
        Button latestAction = Assert.IsInstanceOfType<Button>(
            Assert.IsInstanceOfType<ChatMessageBubble>(transcript.Items[1])
                .FindName("ContinueButton"));

        Assert.AreEqual(Visibility.Collapsed, firstAction.Visibility);
        Assert.AreEqual(Visibility.Visible, latestAction.Visibility);
        Assert.AreEqual("Continue generating", AutomationProperties.GetName(latestAction));
        AssertKeyboardFocusable(page, latestAction);
    }

    private static Button FindButton(FrameworkElement root, string name) =>
        Assert.IsInstanceOfType<Button>(root.FindName(name));

    private static void AssertUniqueNonEmptyNames(IEnumerable<Button> actions)
    {
        string[] names = actions.Select(AutomationProperties.GetName).ToArray();
        Assert.IsTrue(names.All(name => !string.IsNullOrWhiteSpace(name)));
        Assert.AreEqual(names.Length, names.Distinct(StringComparer.Ordinal).Count());
    }

    private static void AssertKeyboardFocusable(FrameworkElement root, Button action)
    {
        Assert.IsTrue(action.IsTabStop);
        Assert.IsTrue(action.Focus(FocusState.Keyboard));
        Assert.AreSame(action, FocusManager.GetFocusedElement(root.XamlRoot));
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

    private sealed class FixedKnowledgeFilePicker(
        params KnowledgeFileCandidate[] candidates) : IKnowledgeFilePicker
    {
        public Task<IReadOnlyList<KnowledgeFileCandidate>> PickAsync() =>
            Task.FromResult<IReadOnlyList<KnowledgeFileCandidate>>(candidates);
    }
}
