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
