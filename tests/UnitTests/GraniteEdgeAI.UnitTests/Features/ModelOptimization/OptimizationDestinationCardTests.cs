using System.Linq;
using GraniteEdgeAI.Features.ModelOptimization.Controls;
using GraniteEdgeAI.Features.ModelOptimization.DebugFixtures;
using GraniteEdgeAI.Features.ModelOptimization.Export;
using GraniteEdgeAI.Features.ModelOptimization.Presentation;
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
    public void SaveIsNotInvocableUntilExactVerifiedTargetIsBound()
    {
        OptimizationDestinationCard card = new();
        OptimizationPresentationState presentation = PersistentPresentation();
        card.Apply(presentation);

        Assert.IsFalse(card.IsActionEnabled(OptimizationCommand.Save));
        Assert.IsFalse(card.TryRequestAction(OptimizationCommand.Save));
        Assert.IsFalse(card.BindVerifiedExport(
            Target(Guid.NewGuid()),
            new ImmediateExportService()));
        Assert.IsFalse(card.IsActionEnabled(OptimizationCommand.Save));

        Assert.IsTrue(card.BindVerifiedExport(
            Target(presentation.OptimizationPlanId),
            new ImmediateExportService()));
        Assert.IsTrue(card.IsActionEnabled(OptimizationCommand.Save));
    }

    [UITestMethod]
    public void RuntimeOnlyResultRejectsPersistentExportBinding()
    {
        OptimizationDestinationCard card = new();
        OptimizationPresentationState runtimeOnly = OptimizationFixtureCatalog.All.Single(
            item => item.Id == "success-runtime-profile").Presentation;
        card.Apply(runtimeOnly);

        Assert.IsFalse(card.BindVerifiedExport(
            Target(Guid.Parse("11111111-1111-1111-1111-111111111111")),
            new ImmediateExportService()));
        Assert.IsFalse(card.TryRequestAction(OptimizationCommand.Save));
        Assert.AreEqual(OptimizationExportStateKind.Unbound, card.ExportState.Kind);
        Assert.IsTrue(card.TryRequestAction(OptimizationCommand.Done));
    }

    [UITestMethod]
    public async Task VerifiedExportRejectsDuplicateActivationAndShowsBoundedSuccess()
    {
        OptimizationDestinationCard card = new();
        OptimizationPresentationState presentation = PersistentPresentation();
        var service = new BlockingExportService();
        card.Apply(presentation);
        Assert.IsTrue(card.BindVerifiedExport(
            Target(presentation.OptimizationPlanId),
            service));

        Task<bool> first = card.TryStartExportAsync();
        Assert.IsFalse(await card.TryStartExportAsync());
        service.Complete(OptimizationExportResult.Succeeded(
            new OptimizationExportReceipt(Manifest, 4096)));

        Assert.IsTrue(await first);
        Assert.AreEqual(
            OptimizationExportStateKind.Succeeded,
            card.ExportState.Kind);
        string status = ((TextBlock)card.FindName("ExportStatusText")).Text;
        Assert.IsFalse(status.Contains(@"C:\", StringComparison.Ordinal));
        Assert.IsFalse(card.IsActionEnabled(OptimizationCommand.Save));
    }

    [UITestMethod]
    public void ApplyingAnotherResultInvalidatesPreviouslyVerifiedExport()
    {
        OptimizationDestinationCard card = new();
        OptimizationPresentationState presentation = PersistentPresentation();
        card.Apply(presentation);
        Assert.IsTrue(card.BindVerifiedExport(
            Target(presentation.OptimizationPlanId),
            new ImmediateExportService()));

        card.Apply(presentation);

        Assert.IsFalse(card.IsActionEnabled(OptimizationCommand.Save));
        Assert.IsFalse(card.TryRequestAction(OptimizationCommand.Save));
        Assert.AreEqual(OptimizationExportStateKind.Unbound, card.ExportState.Kind);
    }

    [UITestMethod]
    public void ExportStatesHaveStableAccessibleSemantics()
    {
        OptimizationDestinationCard card = new();
        var status = (TextBlock)card.FindName("ExportStatusText");
        var progress = (ProgressBar)card.FindName("ExportProgressBar");
        var cancel = (Button)card.FindName("CancelExportButton");
        var retry = (Button)card.FindName("RetryExportButton");

        Assert.AreEqual(
            AutomationLiveSetting.Polite,
            AutomationProperties.GetLiveSetting(status));
        Assert.AreEqual(
            "OptimizationExport.Progress",
            AutomationProperties.GetAutomationId(progress));
        Assert.AreEqual(
            "OptimizationExport.Cancel",
            AutomationProperties.GetAutomationId(cancel));
        Assert.AreEqual(
            "OptimizationExport.Retry",
            AutomationProperties.GetAutomationId(retry));
        Assert.AreEqual(Visibility.Collapsed, progress.Visibility);
        Assert.AreEqual(Visibility.Collapsed, cancel.Visibility);
        Assert.AreEqual(Visibility.Collapsed, retry.Visibility);
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

    private const string Configuration =
        "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
    private const string Manifest =
        "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb";

    private static OptimizationPresentationState PersistentPresentation()
    {
        OptimizationPresentationState seed = OptimizationFixtureCatalog.All.Single(
            item => item.Id == "success-persistent").Presentation;
        return OptimizationPresentationFactory.Success(
            seed.Preference!,
            seed.Configuration,
            Guid.Parse("11111111-1111-1111-1111-111111111111"),
            Configuration);
    }

    private static VerifiedPersistentExportTarget Target(Guid planId) => new(
        OptimizationRoute.Gguf,
        planId,
        Configuration,
        "output-1",
        Manifest,
        4096);

    private sealed class ImmediateExportService : IOptimizationExportService
    {
        public Task<OptimizationExportResult> ExportAsync(
            VerifiedPersistentExportTarget target,
            IProgress<OptimizationExportProgress> progress,
            CancellationToken cancellationToken) =>
            Task.FromResult(OptimizationExportResult.Succeeded(
                new OptimizationExportReceipt(Manifest, 4096)));
    }

    private sealed class BlockingExportService : IOptimizationExportService
    {
        private readonly TaskCompletionSource<OptimizationExportResult> _completion =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Task<OptimizationExportResult> ExportAsync(
            VerifiedPersistentExportTarget target,
            IProgress<OptimizationExportProgress> progress,
            CancellationToken cancellationToken) => _completion.Task;

        internal void Complete(OptimizationExportResult result) =>
            _completion.SetResult(result);
    }
}
