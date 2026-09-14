using System.Linq;
using GraniteEdgeAI.Features.ModelOptimization.Controls;
using GraniteEdgeAI.UnitTests.Features.ModelOptimization.Fixtures;
using GraniteEdgeAI.UnitTests.Features.ModelInspection.Visual;
using GraniteEdgeAI.UnitTests.Features.ModelHardwareCompatibility;
using GraniteEdgeAI.Features.ModelOptimization.Presentation;
using GraniteEdgeAI.Features.ModelOptimization.Export;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Optimization;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Automation.Peers;
using Microsoft.UI.Xaml.Automation.Provider;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.VisualStudio.TestTools.UnitTesting.AppContainer;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace GraniteEdgeAI.UnitTests.Features.ModelOptimization;

[TestClass]
public sealed class OptimizationDestinationCardTests
{
    [UITestMethod]
    [TestCategory("EstimatedProgress")]
    public void OpaqueExportDisclosesEstimateWithoutClaimingPublication()
    {
        var card = new GraniteEdgeAI.Features.ModelOptimization.OptimizationPage();
        typeof(GraniteEdgeAI.Features.ModelOptimization.OptimizationPage).GetMethod("ApplyExportState", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic,
            null, new[] { typeof(OptimizationExportViewState) }, null)!
            .Invoke(card, [new OptimizationExportViewState(OptimizationExportStateKind.Running, "Verifying", Stage: OptimizationExportStage.Verifying)]);
        var bar = (ProgressBar)card.FindName("OptimizationExportProgress");
        Assert.IsFalse(bar.IsIndeterminate);
        Assert.IsTrue(bar.Value < 100);
        StringAssert.Contains(AutomationProperties.GetItemStatus(bar), "Estimated");
        typeof(GraniteEdgeAI.Features.ModelOptimization.OptimizationPage).GetMethod("ApplyExportState", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic,
            null, new[] { typeof(OptimizationExportViewState) }, null)!
            .Invoke(card, [new OptimizationExportViewState(OptimizationExportStateKind.Running, "Copying", Stage: OptimizationExportStage.Writing, Fraction: 1)]);
        StringAssert.Contains(AutomationProperties.GetItemStatus(bar), "Copying: 100%");
        Assert.IsTrue(bar.Value < 100);
    }

    [UITestMethod]
    public void DestinationUsesTruthfulPersistentAndRuntimeActions()
    {
        OptimizationDestinationCard card = new();

        card.Apply(OptimizationFixtureCatalog.All.Single(item => item.Id == "success-persistent").Presentation);
        Assert.AreEqual("Chat with this model", card.PrimaryActionText);
        CollectionAssert.AreEqual(
            new[] { "Chat with this model", "Import another model", "Save model to this computer" },
            ((OptimizationOutcomeCard)card.FindName("DestinationCore")).VisibleActionTexts.ToArray());

        card.Apply(OptimizationFixtureCatalog.All.Single(item => item.Id == "success-runtime-profile").Presentation);
        Assert.AreEqual("Chat with this model", card.PrimaryActionText);
        CollectionAssert.AreEqual(
            new[] { "Chat with this model", "Import another model", "Save model and settings" },
            ((OptimizationOutcomeCard)card.FindName("DestinationCore")).VisibleActionTexts.ToArray());
    }

    [UITestMethod]
    public void PersistentSaveIsDisabledUntilAnExactVerifiedTargetIsBound()
    {
        OptimizationDestinationCard card = new();
        OptimizationPresentationState presentation = OptimizationFixtureCatalog.All.Single(
            item => item.Id == "success-persistent").Presentation;

        card.Apply(presentation);

        var outcome = (OptimizationOutcomeCard)card.FindName("DestinationCore");
        var actions = (StackPanel)outcome.FindName("ActionsHost");
        Button save = actions.Children.OfType<Button>().Single(
            button => Equals(button.Tag, OptimizationCommand.Save));
        Assert.IsFalse(save.IsEnabled);
    }

    [UITestMethod]
    public void ExactBindingEnablesSaveAndRuntimeOnlyResultRejectsIt()
    {
        OptimizationDestinationCard card = new();
        OptimizationPresentationState persistent = PersistentPresentation();
        card.Apply(persistent);
        Assert.IsFalse(card.BindVerifiedExport(Target(Guid.NewGuid()), new ImmediateExportService()));
        Assert.IsTrue(card.BindVerifiedExport(Target(persistent.OptimizationPlanId), new ImmediateExportService()));
        Assert.IsTrue(card.IsActionEnabled(OptimizationCommand.Save));

        card.Apply(OptimizationFixtureCatalog.All.Single(item => item.Id == "success-runtime-profile").Presentation);
        Assert.IsFalse(card.BindVerifiedExport(Target(persistent.OptimizationPlanId), new ImmediateExportService()));
        Assert.IsFalse(card.IsActionEnabled(OptimizationCommand.Save));
        Assert.IsFalse(card.IsActionEnabled(OptimizationCommand.Done));
        Assert.IsTrue(card.IsActionEnabled(OptimizationCommand.ImportAnotherModel));
    }

    [UITestMethod]
    public async Task PageRetirementWaitsForExportAndDisablesActions()
    {
        var page = new GraniteEdgeAI.Features.ModelOptimization.OptimizationPage();
        OptimizationPresentationState presentation = PersistentPresentation();
        var service = new BlockingExportService();
        page.ApplyPresentation(presentation);
        Assert.IsTrue(page.BindVerifiedExport(Target(presentation.OptimizationPlanId), service));
        Task<bool> operation = page.TryStartExportAsync();
        await service.Started.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Task retirement = page.RetireForNavigationAsync();
        Assert.AreSame(retirement, page.RetireForNavigationAsync());
        Assert.IsFalse(retirement.IsCompleted);
        service.Complete(OptimizationExportResult.Failed(OptimizationExportFailure.CleanupFailure));
        await retirement;
        Assert.IsTrue(await operation);
        Assert.IsFalse(((Button)page.FindName("BtnOptimizationPrimary")).IsEnabled);
        Assert.IsFalse(((Button)page.FindName("BtnOptimizationAlternative")).IsEnabled);
    }

    [UITestMethod]
    public async Task PageOwnsExportBlocksImportButKeepsChatWhileSaving()
    {
        var page = new GraniteEdgeAI.Features.ModelOptimization.OptimizationPage();
        OptimizationPresentationState presentation = PersistentPresentation();
        var service = new BlockingExportService();
        page.ApplyPresentation(presentation);
        Assert.IsTrue(page.BindVerifiedExport(Target(presentation.OptimizationPlanId), service));

        Button back = (Button)page.FindName("BtnOptimizationTerminalBack");
        Button save = (Button)page.FindName("BtnOptimizationAlternative");
        Button chat = (Button)page.FindName("BtnOptimizationPrimary");
        Assert.IsTrue(save.IsEnabled);
        Task<bool> operation = page.TryStartExportAsync();

        Assert.AreEqual(OptimizationExportStateKind.Running, page.ExportState.Kind);
        Assert.IsFalse(save.IsEnabled);
        Assert.AreEqual(OptimizationCommand.ImportAnotherModel, back.Tag);
        Assert.IsFalse(back.IsEnabled);
        Assert.IsTrue(chat.IsEnabled);
        Assert.AreEqual(Visibility.Visible,
            ((Button)page.FindName("BtnCancelExport")).Visibility);

        service.Complete(OptimizationExportResult.Succeeded(
            new OptimizationExportReceipt(Target(presentation.OptimizationPlanId), "published-1")));
        Assert.IsTrue(await operation);
        Assert.AreEqual(OptimizationExportStateKind.Succeeded, page.ExportState.Kind);
        Assert.IsTrue(back.IsEnabled);
    }

    [UITestMethod]
    public async Task PageRejectsRebindUntilDetachedCleanupQuiesces()
    {
        using var release = new ManualResetEventSlim(false);
        var page = new GraniteEdgeAI.Features.ModelOptimization.OptimizationPage();
        OptimizationPresentationState presentation = PersistentPresentation();
        var service = new BlockingCancellationExportService(release);
        VerifiedPersistentExportTarget target = Target(presentation.OptimizationPlanId);
        page.ApplyPresentation(presentation);
        Assert.IsTrue(page.BindVerifiedExport(target, service, TimeSpan.FromMilliseconds(50)));
        Task<bool> operation = page.TryStartExportAsync();
        await service.Started.Task.WaitAsync(TimeSpan.FromSeconds(1));

        Assert.IsTrue(page.TryCancelExport());
        await service.CallbackStarted.Task.WaitAsync(TimeSpan.FromSeconds(1));
        service.Complete(OptimizationExportResult.Cancelled());
        await WaitForPageExportStateAsync(page,
            state => state.Failure == OptimizationExportFailure.CleanupFailure);
        Assert.IsFalse(await page.TryRetryExportAsync());
        Assert.AreEqual(Visibility.Collapsed,
            ((Button)page.FindName("BtnRetryExport")).Visibility);
        page.ApplyPresentation(presentation);
        await page.DetachedExportOperations.WaitAsync(TimeSpan.FromSeconds(1));
        Assert.IsFalse(page.DetachedExportCleanup.IsCompleted);
        Assert.IsFalse(page.BindVerifiedExport(target, new ImmediateExportService()));

        release.Set();
        Assert.IsTrue(await operation.WaitAsync(TimeSpan.FromSeconds(1)));
        await page.DetachedExportCleanup.WaitAsync(TimeSpan.FromSeconds(1));
        Assert.IsTrue(page.BindVerifiedExport(target, new ImmediateExportService()));
    }

    [UITestMethod]
    public async Task QueuedControllerUpdateCannotMutatePageAfterRetirement()
    {
        var page = new GraniteEdgeAI.Features.ModelOptimization.OptimizationPage();
        OptimizationPresentationState presentation = PersistentPresentation();
        page.ApplyPresentation(presentation);
        Assert.IsTrue(page.BindVerifiedExport(
            Target(presentation.OptimizationPlanId), new ImmediateExportService()));
        object controller = typeof(GraniteEdgeAI.Features.ModelOptimization.OptimizationPage)
            .GetField("_exportController",
                System.Reflection.BindingFlags.Instance
                | System.Reflection.BindingFlags.NonPublic)!
            .GetValue(page)!;
        OptimizationExportViewState state = page.ExportState;
        var callback = typeof(GraniteEdgeAI.Features.ModelOptimization.OptimizationPage)
            .GetMethod("ExportController_StateChanged",
                System.Reflection.BindingFlags.Instance
                | System.Reflection.BindingFlags.NonPublic)!;

        using var queued = new ManualResetEventSlim(false);
        Task worker = Task.Run(() =>
        {
            callback.Invoke(page, [controller, state]);
            queued.Set();
        });
        Assert.IsTrue(queued.Wait(TimeSpan.FromSeconds(1)));
        TextBlock status = (TextBlock)page.FindName("OptimizationExportStatus");
        status.Text = "Retired page sentinel";
        await page.RetireForNavigationAsync();
        await worker;
        await Task.Delay(50);

        Assert.AreEqual("Retired page sentinel", status.Text);
        Assert.IsFalse(((Button)page.FindName("BtnOptimizationPrimary")).IsEnabled);
    }

    [UITestMethod]
    public async Task ExportStatesMoveFocusOnlyToVisiblePageOwnedDestinations()
    {
        var page = new GraniteEdgeAI.Features.ModelOptimization.OptimizationPage();
        await using WinUiRenderHost host =
            await WinUiRenderHost.ShowAsync(page, 900, 700);
        OptimizationPresentationState presentation = PersistentPresentation();
        VerifiedPersistentExportTarget target = Target(presentation.OptimizationPlanId);
        var service = new BlockingExportService();
        page.ApplyPresentation(presentation);
        Assert.IsTrue(page.BindVerifiedExport(target, service));

        Task<bool> operation = page.TryStartExportAsync();
        Button cancel = (Button)page.FindName("BtnCancelExport");
        await WaitForFocusAsync(page, cancel);
        Assert.IsTrue(page.TryCancelExport());
        FrameworkElement panel =
            (FrameworkElement)page.FindName("OptimizationExportPanel");
        await WaitForFocusAsync(page, panel);

        service.Complete(OptimizationExportResult.Cancelled());
        Assert.IsTrue(await operation.WaitAsync(TimeSpan.FromSeconds(1)));
        Button retry = (Button)page.FindName("BtnRetryExport");
        await WaitForFocusAsync(page, retry);

        page.ApplyPresentation(presentation);
        await page.DetachedExportOperations.WaitAsync(TimeSpan.FromSeconds(1));
        Assert.IsTrue(page.BindVerifiedExport(target, new ImmediateExportService()));
        Assert.IsTrue(await page.TryStartExportAsync());
        Button chat = (Button)page.FindName("BtnOptimizationPrimary");
        await WaitForFocusAsync(page, chat);
    }

    [UITestMethod]
    public void PageBindingRequiresTheExactPersistentPresentationIdentity()
    {
        var page = new GraniteEdgeAI.Features.ModelOptimization.OptimizationPage();
        OptimizationPresentationState persistent = PersistentPresentation();
        VerifiedPersistentExportTarget exact = Target(persistent.OptimizationPlanId);

        Assert.IsFalse(page.BindVerifiedExport(exact, new ImmediateExportService()));
        page.ApplyPresentation(OptimizationFixtureCatalog.All.Single(
            item => item.Id == "success-runtime-profile").Presentation);
        Assert.IsFalse(page.BindVerifiedExport(exact, new ImmediateExportService()));
        page.ApplyPresentation(OptimizationFixtureCatalog.All.Single(
            item => item.Id == "success-persistent").Presentation);
        Assert.IsFalse(page.BindVerifiedExport(exact, new ImmediateExportService()));

        page.ApplyPresentation(persistent);
        Assert.IsFalse(page.BindVerifiedExport(
            Target(Guid.NewGuid()), new ImmediateExportService()));
        Assert.IsFalse(page.BindVerifiedExport(
            new VerifiedPersistentExportTarget(
                OptimizationRoute.Gguf, persistent.OptimizationPlanId,
                new string('d', 64), Source, true, "output-1", Manifest, 4096),
            new ImmediateExportService()));
        Assert.IsTrue(page.BindVerifiedExport(exact, new ImmediateExportService()));
        Button save = (Button)page.FindName("BtnOptimizationAlternative");
        Assert.AreEqual(OptimizationCommand.Save, save.Tag);
        Assert.IsTrue(save.IsEnabled);
    }

    [UITestMethod]
    public async Task VisibleSaveInvokesProviderOnceWithoutRaisingSaveIntentAndObservesFault()
    {
        var page = new GraniteEdgeAI.Features.ModelOptimization.OptimizationPage(
            OptimizationSelectionHandoffTests.RequiredJourneyEntry());
        OptimizationPresentationState presentation = PersistentPresentation();
        var service = new CountingThrowingExportService();
        var intents = new System.Collections.Generic.List<OptimizationCommand>();
        page.IntentRequested += (_, args) => intents.Add(args.Command);
        page.ApplyPresentation(presentation);
        Assert.IsTrue(page.BindVerifiedExport(
            Target(presentation.OptimizationPlanId), service));

        Button save = (Button)page.FindName("BtnOptimizationAlternative");
        Button chat = (Button)page.FindName("BtnOptimizationPrimary");
        ((IInvokeProvider)new ButtonAutomationPeer(chat)
            .GetPattern(PatternInterface.Invoke)).Invoke();
        var peer = new ButtonAutomationPeer(save);
        var invoke = (IInvokeProvider)peer.GetPattern(PatternInterface.Invoke);
        invoke.Invoke();
        await service.Called.Task.WaitAsync(TimeSpan.FromSeconds(1));
        await page.ObservedExportOperation.WaitAsync(TimeSpan.FromSeconds(1));

        Assert.AreEqual(1, service.CallCount);
        CollectionAssert.AreEqual(
            new[] { OptimizationCommand.Chat }, intents);
        Assert.AreEqual(typeof(InvalidOperationException),
            page.ObservedExportFaultType);
        Assert.AreEqual(OptimizationExportStateKind.Failed,
            page.ExportState.Kind);
    }

    [UITestMethod]
    public async Task ReplacingRunningPresentationWithoutCancelRetainsCleanupGate()
    {
        using var release = new ManualResetEventSlim(false);
        var page = new GraniteEdgeAI.Features.ModelOptimization.OptimizationPage();
        OptimizationPresentationState presentation = PersistentPresentation();
        var service = new BlockingCancellationExportService(release);
        VerifiedPersistentExportTarget target = Target(presentation.OptimizationPlanId);
        page.ApplyPresentation(presentation);
        Assert.IsTrue(page.BindVerifiedExport(target, service,
            TimeSpan.FromMilliseconds(50)));
        Task<bool> operation = page.TryStartExportAsync();
        await service.Started.Task.WaitAsync(TimeSpan.FromSeconds(1));

        page.ApplyPresentation(presentation);
        await service.CallbackStarted.Task.WaitAsync(TimeSpan.FromSeconds(1));
        service.Complete(OptimizationExportResult.Cancelled());
        await page.DetachedExportOperations.WaitAsync(TimeSpan.FromSeconds(1));
        Assert.IsFalse(page.DetachedExportCleanup.IsCompleted);
        Assert.IsFalse(page.BindVerifiedExport(target, new ImmediateExportService()));

        release.Set();
        Assert.IsTrue(await operation.WaitAsync(TimeSpan.FromSeconds(1)));
        await page.DetachedExportCleanup.WaitAsync(TimeSpan.FromSeconds(1));
        Assert.IsTrue(page.BindVerifiedExport(target, new ImmediateExportService()));
    }

    [UITestMethod]
    public async Task SaveButtonObservesUnexpectedProviderFaultWithoutUiEscape()
    {
        OptimizationDestinationCard card = new();
        OptimizationPresentationState presentation = PersistentPresentation();
        var service = new ThrowingExportService();
        card.Apply(presentation);
        Assert.IsTrue(card.BindVerifiedExport(Target(presentation.OptimizationPlanId), service));

        Assert.IsTrue(card.TryRequestAction(OptimizationCommand.Save));
        await service.Called.Task.WaitAsync(TimeSpan.FromSeconds(2));
        await card.ObservedExportOperation.WaitAsync(TimeSpan.FromSeconds(2));

        Assert.AreEqual(OptimizationExportStateKind.Failed, card.ExportState.Kind);
        Assert.AreEqual(OptimizationExportFailure.None, card.ExportState.Failure);
        Assert.AreEqual(typeof(InvalidOperationException), card.ObservedExportFaultType);
    }

    [UITestMethod]
    public async Task PendingCleanupRejectsRebindUntilOriginalCallbackQuiesces()
    {
        using var release = new ManualResetEventSlim(false);
        var card = new OptimizationDestinationCard();
        OptimizationPresentationState presentation = PersistentPresentation();
        var service = new BlockingCancellationExportService(release);
        card.Apply(presentation);
        VerifiedPersistentExportTarget target = Target(presentation.OptimizationPlanId);
        Assert.IsTrue(card.BindVerifiedExport(target, service, TimeSpan.FromMilliseconds(50)));
        Task<bool> operation = card.TryStartExportAsync();
        await service.Started.Task.WaitAsync(TimeSpan.FromSeconds(1));
        Assert.IsTrue(card.TryCancelExport());
        await service.CallbackStarted.Task.WaitAsync(TimeSpan.FromSeconds(1));
        service.Complete(OptimizationExportResult.Cancelled());
        await WaitForExportStateAsync(card, state => state.Failure == OptimizationExportFailure.CleanupFailure);

        Assert.IsFalse(card.BindVerifiedExport(target, new ImmediateExportService()));
        Assert.AreEqual(OptimizationExportFailure.CleanupFailure, card.ExportState.Failure);

        release.Set();
        Assert.IsTrue(await operation.WaitAsync(TimeSpan.FromSeconds(1)));
        await WaitForExportStateAsync(card, state => state.Kind == OptimizationExportStateKind.Cancelled);
        Assert.IsTrue(card.BindVerifiedExport(target, new ImmediateExportService()));
    }

    [UITestMethod]
    public async Task ClearedControllerStillBlocksRebindUntilDetachedCleanupQuiesces()
    {
        using var release = new ManualResetEventSlim(false);
        var card = new OptimizationDestinationCard();
        OptimizationPresentationState presentation = PersistentPresentation();
        var service = new BlockingCancellationExportService(release);
        card.Apply(presentation);
        VerifiedPersistentExportTarget target = Target(presentation.OptimizationPlanId);
        Assert.IsTrue(card.BindVerifiedExport(target, service, TimeSpan.FromMilliseconds(50)));
        Task<bool> operation = card.TryStartExportAsync();
        await service.Started.Task.WaitAsync(TimeSpan.FromSeconds(1));
        Assert.IsTrue(card.TryCancelExport());
        await service.CallbackStarted.Task.WaitAsync(TimeSpan.FromSeconds(1));
        service.Complete(OptimizationExportResult.Cancelled());
        await WaitForExportStateAsync(card, state => state.Failure == OptimizationExportFailure.CleanupFailure);

        card.ClearExportBinding();
        card.Apply(presentation);
        await card.DetachedOperations.WaitAsync(TimeSpan.FromSeconds(1));
        Assert.IsFalse(card.DetachedCleanup.IsCompleted);
        Assert.IsFalse(card.BindVerifiedExport(target, new ImmediateExportService()));

        release.Set();
        Assert.IsTrue(await operation.WaitAsync(TimeSpan.FromSeconds(1)));
        await card.DetachedCleanup.WaitAsync(TimeSpan.FromSeconds(1));
        Assert.IsTrue(card.BindVerifiedExport(target, new ImmediateExportService()));
    }

    [UITestMethod]
    public async Task ClearingRunningExportCreatesDetachedCleanupGateBeforeRebind()
    {
        using var release = new ManualResetEventSlim(false);
        var card = new OptimizationDestinationCard();
        OptimizationPresentationState presentation = PersistentPresentation();
        var service = new BlockingCancellationExportService(release);
        card.Apply(presentation);
        VerifiedPersistentExportTarget target = Target(presentation.OptimizationPlanId);
        Assert.IsTrue(card.BindVerifiedExport(target, service, TimeSpan.FromMilliseconds(50)));
        Task<bool> operation = card.TryStartExportAsync();
        await service.Started.Task.WaitAsync(TimeSpan.FromSeconds(1));

        card.ClearExportBinding();
        await service.CallbackStarted.Task.WaitAsync(TimeSpan.FromSeconds(1));
        service.Complete(OptimizationExportResult.Cancelled());
        card.Apply(presentation);
        await card.DetachedOperations.WaitAsync(TimeSpan.FromSeconds(1));
        Assert.IsFalse(card.DetachedCleanup.IsCompleted);
        Assert.IsFalse(card.BindVerifiedExport(target, new ImmediateExportService()));

        release.Set();
        Assert.IsTrue(await operation.WaitAsync(TimeSpan.FromSeconds(1)));
        await card.DetachedCleanup.WaitAsync(TimeSpan.FromSeconds(1));
        Assert.IsTrue(card.BindVerifiedExport(target, new ImmediateExportService()));
    }

    [UITestMethod]
    public async Task ThrowingExportStateObserverCannotSuppressRenderingOrLaterObservers()
    {
        var card = new OptimizationDestinationCard();
        OptimizationPresentationState presentation = PersistentPresentation();
        card.Apply(presentation);
        Assert.IsTrue(card.BindVerifiedExport(
            Target(presentation.OptimizationPlanId),
            new ImmediateExportService()));
        var observed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        card.ExportStateChanged += (_, _) => throw new InvalidOperationException("synthetic observer failure");
        card.ExportStateChanged += (_, state) =>
        {
            if (state.Kind != OptimizationExportStateKind.Succeeded) return;
            var status = (TextBlock)card.FindName("ExportStatusText");
            var progress = (ProgressBar)card.FindName("ExportProgressBar");
            if (status.Text == state.StatusText && progress.Visibility == Visibility.Collapsed)
                observed.TrySetResult();
        };

        Assert.IsTrue(await card.TryStartExportAsync());
        await observed.Task.WaitAsync(TimeSpan.FromSeconds(1));

        Assert.AreEqual(OptimizationExportStateKind.Succeeded, card.ExportState.Kind);
        Assert.AreEqual(card.ExportState.StatusText, ((TextBlock)card.FindName("ExportStatusText")).Text);
    }

    [UITestMethod]
    public void ExportControlsHaveStableAccessibleSemantics()
    {
        OptimizationDestinationCard card = new();
        var status = (TextBlock)card.FindName("ExportStatusText");
        Assert.AreEqual(AutomationLiveSetting.Polite, AutomationProperties.GetLiveSetting(status));
        Assert.AreEqual("OptimizationExport.Progress", AutomationProperties.GetAutomationId((ProgressBar)card.FindName("ExportProgressBar")));
        Assert.AreEqual("OptimizationExport.Cancel", AutomationProperties.GetAutomationId((Button)card.FindName("CancelExportButton")));
        Assert.AreEqual("OptimizationExport.Retry", AutomationProperties.GetAutomationId((Button)card.FindName("RetryExportButton")));
        Assert.AreEqual(Visibility.Collapsed, ((ProgressBar)card.FindName("ExportProgressBar")).Visibility);
    }

    [UITestMethod]
    public void RecoveryStateShowsBoundedSupportCodeAndActions()
    {
        OptimizationRecoveryCard card = new();
        card.Apply(OptimizationFixtureCatalog.All.Single(item => item.Id == "failed").Presentation);

        Assert.AreEqual("ValidationFailed", card.VisibleSupportCode);
        CollectionAssert.AreEqual(
            new[] { "Try again", "Back" },
            card.VisibleActionTexts.ToArray());
    }

    private const string Configuration = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
    private const string Manifest = "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb";
    private const string Source = "cccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccc";

    private static OptimizationPresentationState PersistentPresentation()
    {
        OptimizationPresentationState seed = OptimizationFixtureCatalog.All.Single(item => item.Id == "success-persistent").Presentation;
        return OptimizationPresentationFactory.Success(seed.Preference!, seed.Configuration,
            Guid.Parse("11111111-1111-1111-1111-111111111111"), Configuration);
    }

    private static VerifiedPersistentExportTarget Target(Guid planId) =>
        new(OptimizationRoute.Gguf, planId, Configuration, Source, true, "output-1", Manifest, 4096);

    private static async Task WaitForExportStateAsync(
        OptimizationDestinationCard card,
        Func<OptimizationExportViewState, bool> predicate)
    {
        if (predicate(card.ExportState)) return;
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        EventHandler<OptimizationExportViewState>? handler = null;
        handler = (_, state) =>
        {
            if (!predicate(state)) return;
            card.ExportStateChanged -= handler;
            completion.TrySetResult();
        };
        card.ExportStateChanged += handler;
        handler!(card, card.ExportState);
        await completion.Task.WaitAsync(TimeSpan.FromSeconds(1));
    }

    private static async Task WaitForPageExportStateAsync(
        GraniteEdgeAI.Features.ModelOptimization.OptimizationPage page,
        Func<OptimizationExportViewState, bool> predicate)
    {
        DateTime deadline = DateTime.UtcNow.AddSeconds(2);
        while (!predicate(page.ExportState) && DateTime.UtcNow < deadline)
            await Task.Delay(10);
        Assert.IsTrue(predicate(page.ExportState));
    }

    private static async Task WaitForFocusAsync(
        FrameworkElement root,
        FrameworkElement expected)
    {
        DateTime deadline = DateTime.UtcNow.AddSeconds(2);
        while (!ReferenceEquals(
                   FocusManager.GetFocusedElement(root.XamlRoot), expected)
               && DateTime.UtcNow < deadline)
            await Task.Delay(10);
        Assert.AreSame(expected, FocusManager.GetFocusedElement(root.XamlRoot));
    }

    private sealed class ImmediateExportService : IOptimizationExportService
    {
        public Task<OptimizationExportResult> ExportAsync(VerifiedPersistentExportTarget target, IProgress<OptimizationExportProgress> progress, CancellationToken cancellationToken) =>
            Task.FromResult(OptimizationExportResult.Succeeded(new OptimizationExportReceipt(target, "published-1")));
    }

    private sealed class BlockingExportService : IOptimizationExportService
    {
        private readonly TaskCompletionSource<OptimizationExportResult> _completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public Task<OptimizationExportResult> ExportAsync(VerifiedPersistentExportTarget target, IProgress<OptimizationExportProgress> progress, CancellationToken cancellationToken)
        {
            Started.TrySetResult();
            return _completion.Task;
        }
        internal void Complete(OptimizationExportResult result) => _completion.SetResult(result);
    }

    private sealed class BlockingCancellationExportService(ManualResetEventSlim release) : IOptimizationExportService
    {
        private readonly TaskCompletionSource<OptimizationExportResult> _completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal TaskCompletionSource CallbackStarted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Task<OptimizationExportResult> ExportAsync(
            VerifiedPersistentExportTarget target,
            IProgress<OptimizationExportProgress> progress,
            CancellationToken cancellationToken)
        {
            cancellationToken.Register(() =>
            {
                CallbackStarted.TrySetResult();
                release.Wait();
            });
            Started.TrySetResult();
            return _completion.Task;
        }

        internal void Complete(OptimizationExportResult result) => _completion.TrySetResult(result);
    }

    [UITestMethod]
    public void CleanupFailureHidesUnavailableRetryAction()
    {
        var card = new OptimizationDestinationCard();
        var state = new OptimizationExportViewState(
            OptimizationExportStateKind.Failed,
            "Cleanup is not confirmed.",
            Failure: OptimizationExportFailure.CleanupFailure);
        typeof(OptimizationDestinationCard).GetMethod("ApplyExportState", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
            .Invoke(card, [state]);

        Assert.AreEqual(Visibility.Collapsed, ((Button)card.FindName("RetryExportButton")).Visibility);
    }

    private sealed class ThrowingExportService : IOptimizationExportService
    {
        internal TaskCompletionSource Called { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public Task<OptimizationExportResult> ExportAsync(VerifiedPersistentExportTarget target, IProgress<OptimizationExportProgress> progress, CancellationToken cancellationToken) =>
            Throw();

        private Task<OptimizationExportResult> Throw()
        {
            Called.TrySetResult();
            throw new InvalidOperationException(@"C:\Users\private\provider.gguf?token=secret");
        }
    }

    private sealed class CountingThrowingExportService : IOptimizationExportService
    {
        internal int CallCount { get; private set; }
        internal TaskCompletionSource Called { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Task<OptimizationExportResult> ExportAsync(
            VerifiedPersistentExportTarget target,
            IProgress<OptimizationExportProgress> progress,
            CancellationToken cancellationToken)
        {
            CallCount++;
            Called.TrySetResult();
            throw new InvalidOperationException(
                @"C:\Users\private\provider.gguf?token=secret");
        }
    }
}
