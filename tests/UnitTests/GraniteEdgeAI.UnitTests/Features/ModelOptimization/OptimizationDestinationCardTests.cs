using System.Linq;
using GraniteEdgeAI.Features.ModelOptimization.Controls;
using GraniteEdgeAI.Features.ModelOptimization.DebugFixtures;
using GraniteEdgeAI.Features.ModelOptimization.Presentation;
using GraniteEdgeAI.Features.ModelOptimization.Export;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Optimization;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Automation.Peers;
using Microsoft.UI.Xaml.Controls;
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
    public void DestinationUsesTruthfulPersistentAndRuntimeActions()
    {
        OptimizationDestinationCard card = new();

        card.Apply(OptimizationFixtureCatalog.All.Single(item => item.Id == "success-persistent").Presentation);
        Assert.AreEqual("Chat with this model", card.PrimaryActionText);
        Assert.AreEqual("Save model to this computer", card.SecondaryActionText);

        card.Apply(OptimizationFixtureCatalog.All.Single(item => item.Id == "success-runtime-profile").Presentation);
        Assert.AreEqual("Chat with this model", card.PrimaryActionText);
        Assert.AreEqual("Done", card.SecondaryActionText);
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
        Assert.IsTrue(card.IsActionEnabled(OptimizationCommand.Done));
    }

    [UITestMethod]
    public async Task PageRetirementWaitsForExportAndDisablesActions()
    {
        var page = new GraniteEdgeAI.Features.ModelOptimization.OptimizationPage();
        OptimizationPresentationState presentation = PersistentPresentation();
        var service = new BlockingExportService();
        page.ApplyPresentation(presentation);
        Assert.IsTrue(page.BindVerifiedExport(Target(presentation.OptimizationPlanId), service));
        var card = (OptimizationDestinationCard)page.FindName("DestinationCard");
        Task<bool> operation = card.TryStartExportAsync();
        Task retirement = page.RetireForNavigationAsync();
        Assert.AreSame(retirement, page.RetireForNavigationAsync());
        Assert.IsFalse(retirement.IsCompleted);
        service.Complete(OptimizationExportResult.Failed(OptimizationExportFailure.CleanupFailure));
        await retirement;
        Assert.IsTrue(await operation);
        Assert.IsFalse(card.IsActionEnabled(OptimizationCommand.Chat));
        Assert.IsFalse(card.IsActionEnabled(OptimizationCommand.Save));
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

    private sealed class ImmediateExportService : IOptimizationExportService
    {
        public Task<OptimizationExportResult> ExportAsync(VerifiedPersistentExportTarget target, IProgress<OptimizationExportProgress> progress, CancellationToken cancellationToken) =>
            Task.FromResult(OptimizationExportResult.Succeeded(new OptimizationExportReceipt(target, "published-1")));
    }

    private sealed class BlockingExportService : IOptimizationExportService
    {
        private readonly TaskCompletionSource<OptimizationExportResult> _completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public Task<OptimizationExportResult> ExportAsync(VerifiedPersistentExportTarget target, IProgress<OptimizationExportProgress> progress, CancellationToken cancellationToken) => _completion.Task;
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
}
