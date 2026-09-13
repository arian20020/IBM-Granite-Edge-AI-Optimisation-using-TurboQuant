using GraniteEdgeAI.Features.GgufRuntime;
using GraniteEdgeAI.Features.GgufRuntime.History;
using GraniteEdgeAI.Features.GgufRuntime.Services;
using GraniteEdgeAI.Features.ModelImport.FileImport;
using GraniteEdgeAI.Features.Onboarding;
using GraniteEdgeAI.Features.Onboarding.Controls;
using GraniteEdgeAI.UnitTests.Features.ModelInspection.Visual;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation.Peers;
using Microsoft.UI.Xaml.Automation.Provider;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.VisualStudio.TestTools.UnitTesting.AppContainer;

namespace GraniteEdgeAI.UnitTests.Features.GgufRuntime;

[TestClass]
[DoNotParallelize]
public sealed class ChatPerfectionTests
{
    [UITestMethod]
    [TestCategory("SidebarMemoryContinuation")]
    public async Task ConversationNavigationRetainsOpenSidebarWithoutLoadingFade()
    {
        var page = new ChatPage();
        page.AddHistoryConversation(Guid.NewGuid(), "Saved conversation", true);
        await using var host = await WinUiRenderHost.ShowAsync(page, 1000, 600);
        var toggle = (Button)page.FindName("CompactNavigationButton");
        ((IInvokeProvider)new ButtonAutomationPeer(toggle).GetPattern(PatternInterface.Invoke)).Invoke();
        var pane = (FrameworkElement)page.FindName("HistoryRail");
        var list = (ListView)page.FindName("ChatHistoryList");
        int selections = 0;
        page.ConversationSelected += (_, _) => selections++;
        var item = (GraniteEdgeAI.Features.GgufRuntime.Controls.ChatHistoryItem)list.Items[0];
        Assert.AreEqual(Visibility.Visible, pane.Visibility);
        page.SetHistoryLoading(true);
        Assert.IsTrue(list.IsEnabled, "Loading must not fade the whole history list.");
        item.RaiseSelected();
        Assert.AreEqual(0, selections);
        page.SetHistoryLoading(false);
        page.CompleteConversationNavigation();
        Assert.AreEqual(Visibility.Visible, pane.Visibility);
        page.SetSessionPreparing(true);
        Assert.IsTrue(list.IsEnabled);
        item.RaiseSelected();
        Assert.AreEqual(0, selections);
        page.SetSessionPreparing(false);
        item.RaiseSelected();
        Assert.AreEqual(1, selections);
    }

    public TestContext TestContext { get; set; } = null!;
    [UITestMethod]
    public async Task SwitchingKeepsDispatcherResponsiveDuringSynchronousAdapterPreparation()
    {
        var page = new ChatPage();
        await using var host = await WinUiRenderHost.ShowAsync(page, 1100, 750);
        await using var controller = await ChatDemoController.CreateInitializedAsync(page, new Store(), new Session(),
            "old", "profile", "Old", "llama.cpp", null, CancellationToken.None);
        var replacement = new BlockingSession();
        Task switching = controller.SwitchModelAsync(replacement, "New", "OpenVINO", CancellationToken.None, "new", "profile");
        try
        {
            await replacement.Entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            var heartbeat = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            Assert.IsTrue(page.DispatcherQueue.TryEnqueue(() => heartbeat.SetResult()));
            await heartbeat.Task.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.IsFalse(switching.IsCompleted);
            Assert.IsFalse(((Button)page.FindName("HeaderNewChatButton")).IsEnabled);
        }
        finally { replacement.Release.Set(); }
        await switching;
        Assert.IsTrue(((Button)page.FindName("HeaderNewChatButton")).IsEnabled);
    }
    [UITestMethod]
    public async Task IndicatorAndImportDialogFollowThemeChanges()
    {
        var indicator = new OnboardingStageIndicator();
        var root = new Grid { RequestedTheme = ElementTheme.Light }; root.Children.Add(indicator);
        await using var host = await WinUiRenderHost.ShowAsync(root, 1000, 400);
        var box = (Border)indicator.FindName("ChooseModelStepBox");
        var light = ((SolidColorBrush)box.Background).Color;
        TestContext.AddResultFile(await (await host.CaptureAsync()).SavePngAsync("chat-perfection-indicator-light.png"));
        root.RequestedTheme = ElementTheme.Dark; await Task.Delay(100);
        Assert.AreEqual(ElementTheme.Dark, indicator.ActualTheme);
        Assert.AreNotEqual(light, ((SolidColorBrush)box.Background).Color);
        TestContext.AddResultFile(await (await host.CaptureAsync()).SavePngAsync("chat-perfection-indicator-dark.png"));
        var dialog = new ModelFormatSelectionCard { XamlRoot = root.XamlRoot, RequestedTheme = root.ActualTheme };
        var showing = dialog.ShowAsync(); await Task.Delay(100);
        Assert.AreEqual(ElementTheme.Dark, dialog.ActualTheme);
        Assert.IsTrue(((SolidColorBrush)dialog.Background).Color.R < 100);
        dialog.Hide(); await showing;
        root.RequestedTheme = ElementTheme.Light; await Task.Delay(100);
        Assert.AreEqual(light, ((SolidColorBrush)box.Background).Color);
        root.RequestedTheme = ElementTheme.Default;
        Assert.AreEqual(ElementTheme.Default, root.RequestedTheme);
    }

    [UITestMethod]
    public void ShellDoesNotExposeFixtureButtons()
    {
        var shell = new OnboardingShellPage();
        Assert.IsNull(shell.FindName("FixtureGalleryButton"));
        Assert.IsNull(shell.FindName("HardwareFixtureGalleryButton"));
        Assert.AreEqual(3, ((Grid)shell.Content).RowDefinitions.Count);
    }

    [UITestMethod]
    public async Task HistoryContainerHasNativeCommandsAndDialogsRespectCancellation()
    {
        var page = new ChatPage(); var id = Guid.NewGuid();
        page.AddHistoryGroup("Today"); page.AddHistoryConversation(id, "Notes", true);
        await using var host = await WinUiRenderHost.ShowAsync(page, 1100, 750);
        page.RequestedTheme = ElementTheme.Dark;
        var navigation = (Button)page.FindName("CompactNavigationButton");
        ((IInvokeProvider)new ButtonAutomationPeer(navigation).GetPattern(PatternInterface.Invoke)).Invoke();
        await Task.Delay(100);
        TestContext.AddResultFile(await (await host.CaptureAsync()).SavePngAsync("chat-perfection-history-dark.png"));
        var list = (ListView)page.FindName("ChatHistoryList"); list.UpdateLayout();
        var row = (ListViewItem)list.ContainerFromIndex(0);
        var menu = Assert.IsInstanceOfType<MenuFlyout>(row.ContextFlyout);
        CollectionAssert.AreEqual(new[] { "Rename", "Delete" }, menu.Items.OfType<MenuFlyoutItem>().Select(item => item.Text).ToArray());
        page.SetHistoryCommandsEnabled(false);
        Assert.IsTrue(menu.Items.OfType<MenuFlyoutItem>().All(item => !item.IsEnabled));
        page.SetHistoryCommandsEnabled(true);
        using var cancellation = new CancellationTokenSource();
        Task<string?> rename = page.RequestConversationTitleAsync("Notes", cancellation.Token);
        await Task.Delay(100); cancellation.Cancel();
        Assert.IsNull(await rename.WaitAsync(TimeSpan.FromSeconds(5)));
        using var deleteCancellation = new CancellationTokenSource();
        Task<bool> delete = page.ConfirmDeleteConversationAsync("Notes", deleteCancellation.Token);
        await Task.Delay(100); deleteCancellation.Cancel();
        Assert.IsFalse(await delete.WaitAsync(TimeSpan.FromSeconds(5)));
    }

    [UITestMethod]
    public async Task HeaderPlusUsesSuccessfullySwitchedModelIdentity()
    {
        var page = new ChatPage(); var session = new Session();
        await using var host = await WinUiRenderHost.ShowAsync(page, 1100, 750);
        await using var controller = await ChatDemoController.CreateInitializedAsync(page, new Store(), session,
            "old", "old-profile", "Old", "llama.cpp", null, CancellationToken.None);
        var replacement = new Session();
        await controller.SwitchModelAsync(replacement, "New", "OpenVINO", CancellationToken.None, "new", "new-profile");
        var plus = (Button)page.FindName("HeaderNewChatButton");
        var peer = new ButtonAutomationPeer(plus);
        ((IInvokeProvider)peer.GetPattern(PatternInterface.Invoke)).Invoke();
        await Task.Delay(100);
        Assert.AreEqual("new", replacement.Prepared!.ModelId);
        Assert.AreEqual("new-profile", replacement.Prepared.ProfileId);
    }

    private sealed class Store : IChatHistoryStore
    {
        public Task<ChatHistoryLoadResult> LoadAsync(CancellationToken token) => Task.FromResult(new ChatHistoryLoadResult([], false));
        public Task SaveAsync(ChatConversation conversation, CancellationToken token) => Task.CompletedTask;
        public Task DeleteAsync(Guid id, CancellationToken token) => Task.CompletedTask;
        public Task ClearAsync(CancellationToken token) => Task.CompletedTask;
    }
    private sealed class Session : IGgufChatSession
    {
        internal ChatConversation? Prepared;
        public ValueTask PrepareConversationAsync(ChatConversation chat, CancellationToken token) { Prepared = chat; return ValueTask.CompletedTask; }
        public async IAsyncEnumerable<GgufChatEvent> GenerateAsync(string prompt, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken token)
        { await Task.CompletedTask; yield break; }
        public ValueTask StopAsync(CancellationToken token) => ValueTask.CompletedTask;
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
    private sealed class BlockingSession : IGgufChatSession
    {
        internal TaskCompletionSource Entered = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal ManualResetEventSlim Release = new(false);
        public ValueTask PrepareConversationAsync(ChatConversation chat, CancellationToken token)
        {
            Entered.SetResult();
            if (!Release.Wait(TimeSpan.FromSeconds(10), token)) throw new TimeoutException("Test activation was not released.");
            return ValueTask.CompletedTask;
        }
        public async IAsyncEnumerable<GgufChatEvent> GenerateAsync(string prompt, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken token)
        { await Task.CompletedTask; yield break; }
        public ValueTask StopAsync(CancellationToken token) => ValueTask.CompletedTask;
        public ValueTask DisposeAsync() { Release.Dispose(); return ValueTask.CompletedTask; }
    }
}
