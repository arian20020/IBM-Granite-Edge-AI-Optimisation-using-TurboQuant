using GraniteEdgeAI.Features.HardwareInspection;
using GraniteEdgeAI.Features.HardwareInspection.Application;
using GraniteEdgeAI.Features.HardwareInspection.Domain;
using GraniteEdgeAI.Features.HardwareInspection.Presentation.Controls;
using GraniteEdgeAI.Features.HardwareInspection.Presentation.State;
using GraniteEdgeAI.Features.HardwareInspection.ViewModels;
using GraniteEdgeAI.Features.ModelInspection.Contracts;
using GraniteEdgeAI.Features.ModelInspection.Handoff;
using GraniteEdgeAI.UnitTests.Features.ModelInspection.Presentation;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.VisualStudio.TestTools.UnitTesting.AppContainer;
using System.Reflection;

namespace GraniteEdgeAI.UnitTests.Features.HardwareInspection;

[TestClass]
[DoNotParallelize]
public sealed class HardwareInspectionCompletionNavigationTests
{
    private static readonly Guid ModelRunId =
        Guid.Parse("22222222-2222-4222-8222-222222222222");
    private static readonly Guid HardwareRunId =
        Guid.Parse("33333333-3333-4333-8333-333333333333");
    private ResourceDictionary? _journeyResources;

    [TestInitialize]
    public void InstallJourneyResources()
    {
        _journeyResources = new ResourceDictionary
        {
            Source = new Uri(
                "ms-appx:///Features/Onboarding/Presentation/GraniteJourneyActionPalette.xaml")
        };
        Application.Current.Resources.MergedDictionaries.Add(_journeyResources);
    }

    [TestCleanup]
    public void RemoveJourneyResources()
    {
        if (_journeyResources is not null)
        {
            Application.Current.Resources.MergedDictionaries.Remove(_journeyResources);
            _journeyResources = null;
        }
    }

    [UITestMethod]
    [TestCategory("WinUI")]
    public async Task CompletedAttempt_RaisesExactlyOneImmutableCompletion()
    {
        var viewModel = new HardwareInspectionViewModel(
            new CompletedService(),
            HardwareRunId,
            new ImmediateHardwareInspectionStagePacer());
        var page = new HardwareInspectionPage(viewModel, ModelHandoff());
        int completions = 0;
        HardwareInspectionHandoff? published = null;
        bool terminalSnapshotWasAppliedBeforeCompletion = false;
        bool reportWasAvailableAtCompletion = false;
        HardwareInspectionPresentationState? reportPresentation = null;
        HardwareSummaryPresentation? reportSummary = null;
        HardwareInspectionDetailsState? reportDetails = null;
        bool valueEquivalentHandoffWasRejected = false;
        page.InspectionCompleted += (_sender, args) =>
        {
            completions++;
            published = args.Handoff;
            terminalSnapshotWasAppliedBeforeCompletion =
                page.CurrentState?.Kind is
                    GraniteEdgeAI.Features.HardwareInspection.Presentation.State.HardwareInspectionPresentationKind.Completed
                    or GraniteEdgeAI.Features.HardwareInspection.Presentation.State.HardwareInspectionPresentationKind.CompletedWithWarnings
                && AppliedRevision(page) == viewModel.Snapshot.Revision;
            reportWasAvailableAtCompletion = page.TryGetCompletedReport(
                args.Handoff,
                out reportPresentation,
                out reportSummary,
                out reportDetails);
            HardwareInspectionHandoff differentOwner =
                HardwareInspectionHandoff.Create(
                    args.Handoff.InspectionId,
                    HardwareInspectionOutcome.Completed,
                    args.Handoff.Snapshot);
            valueEquivalentHandoffWasRejected = !page.TryGetCompletedReport(
                differentOwner,
                out _,
                out _,
                out _);
            Unload(page);
        };

        Window window = new() { Content = page };
        try
        {
            window.Activate();
            page.AuthorizeStart();
            await WaitForAsync(() => completions == 1);
        }
        finally
        {
            window.Content = null;
            window.Close();
        }

        Assert.IsNotNull(viewModel.Snapshot.Handoff);
        Assert.AreEqual(
            GraniteEdgeAI.Features.HardwareInspection.Presentation.State.HardwareInspectionPresentationKind.Completed,
            viewModel.Snapshot.Presentation.Kind);

        MethodInfo apply = typeof(HardwareInspectionPage).GetMethod(
            "ApplyLatestSnapshot",
            BindingFlags.Instance | BindingFlags.NonPublic)!;
        apply.Invoke(page, null);

        Assert.AreEqual(1, completions);
        Assert.IsNotNull(published);
        Assert.AreEqual(HardwareRunId, published.InspectionId);
        Assert.IsTrue(
            terminalSnapshotWasAppliedBeforeCompletion,
            "The terminal presentation and revision must be applied before a completion handler can synchronously detach the page.");
        Assert.IsTrue(reportWasAvailableAtCompletion);
        Assert.AreSame(viewModel.Snapshot.Presentation, reportPresentation);
        Assert.AreSame(viewModel.Snapshot.Summary, reportSummary);
        Assert.AreSame(viewModel.Snapshot.Details, reportDetails);
        Assert.AreEqual(7, reportDetails!.Rows.Count);

        Assert.IsTrue(
            valueEquivalentHandoffWasRejected,
            "A value-equivalent handoff from another owner must not expose the report.");
        Assert.IsFalse(page.TryGetCompletedReport(
            published!,
            out _,
            out _,
            out _),
            "A report from the prior applied revision must not survive page retirement.");

        apply.Invoke(page, null);
        Assert.AreEqual(1, completions);
    }

    [UITestMethod]
    [TestCategory("WinUI")]
    public async Task AcceptedCancel_DoesNotPublishCompletionWhenServiceReturnsSuccess()
    {
        var service = new CancelThenSuccessService();
        var viewModel = new HardwareInspectionViewModel(
            service,
            HardwareRunId,
            new ImmediateHardwareInspectionStagePacer());
        var page = new HardwareInspectionPage(viewModel, ModelHandoff());
        int completions = 0;
        page.InspectionCompleted += (_sender, _args) => completions++;

        Load(page);
        page.AuthorizeStart();
        await service.Started.Task;
        viewModel.Cancel();
        Assert.AreEqual(HardwareInspectionPresentationKind.Stopping,
            viewModel.Snapshot.Presentation.Kind);

        service.CompleteSuccessfully();
        await WaitForAsync(() =>
            viewModel.Snapshot.Presentation.Kind ==
                HardwareInspectionPresentationKind.Cancelled);
        await WaitForAsync(() =>
            page.CurrentState?.Kind ==
                HardwareInspectionPresentationKind.Cancelled);

        Assert.AreEqual(HardwareInspectionPresentationKind.Cancelled,
            page.CurrentState?.Kind);
        Assert.AreEqual(
            Visibility.Visible,
            ((FrameworkElement)page.FindName("ProgressPanel")).Visibility);
        Assert.AreEqual(
            Visibility.Collapsed,
            ((FrameworkElement)page.FindName("TerminalPanel")).Visibility);
        Assert.AreEqual(
            HardwareInspectionPresentationKind.Cancelled,
            ((HardwareInspectionProgressCard)page.FindName(
                "ProgressCard")).CurrentState?.Kind);
        Assert.IsTrue(
            ((StackPanel)((FrameworkElement)page.FindName("ActiveActionCard"))
                .FindName("ActionsPanel"))
                .Children.Cast<Button>().All(button => button.IsTabStop));
        Assert.AreEqual(0, completions);
        Assert.IsNull(viewModel.Snapshot.Handoff);
        Unload(page);
    }

    private static async Task WaitForAsync(Func<bool> predicate)
    {
        DateTimeOffset deadline = DateTimeOffset.UtcNow.AddSeconds(30);
        while (!predicate() && DateTimeOffset.UtcNow < deadline)
        {
            await Task.Delay(10);
        }
        Assert.IsTrue(predicate(), "The expected inspection state was not reached before the timeout.");
    }

    private static ModelInspectionHandoff ModelHandoff()
    {
        ModelInspectionExecutionResult terminal = ModelInspectionExecutionResult.Completed(
            PresentationTestData.CreateResult(ModelInspectionOutcome.Ready));
        Assert.IsTrue(ModelInspectionHandoffProjector.TryProject(
            ModelRunId,
            ModelRunId,
            terminal,
            out ModelInspectionHandoff? handoff));
        return handoff!;
    }

    private static void Load(HardwareInspectionPage page)
    {
        MethodInfo loaded = typeof(HardwareInspectionPage).GetMethod(
            "OnLoaded",
            BindingFlags.Instance | BindingFlags.NonPublic)!;
        loaded.Invoke(page, [page, new Microsoft.UI.Xaml.RoutedEventArgs()]);
    }

    private static void Unload(HardwareInspectionPage page)
    {
        MethodInfo unloaded = typeof(HardwareInspectionPage).GetMethod(
            "OnUnloaded",
            BindingFlags.Instance | BindingFlags.NonPublic)!;
        unloaded.Invoke(page, [page, new Microsoft.UI.Xaml.RoutedEventArgs()]);
    }

    private static long AppliedRevision(HardwareInspectionPage page)
    {
        FieldInfo appliedRevision = typeof(HardwareInspectionPage).GetField(
            "_appliedRevision",
            BindingFlags.Instance | BindingFlags.NonPublic)!;
        return (long)appliedRevision.GetValue(page)!;
    }

    private sealed class CompletedService : IHardwareInspectionService
    {
        public Task<HardwareInspectionRunResult> RunAsync(
            Guid inspectionId,
            IProgress<HardwareInspectionRunProgress> progress,
            CancellationToken cancellationToken)
        {
            long sequence = 0;
            foreach (HardwareInspectionRunStage stage in Enum.GetValues<HardwareInspectionRunStage>())
            {
                progress.Report(new HardwareInspectionRunProgress(
                    inspectionId,
                    ++sequence,
                    stage));
            }

            HardwareSnapshot snapshot =
                HardwareInspectionContractTests.CreateUsableSnapshotForPresentation(inspectionId);
            return Task.FromResult(HardwareInspectionRunResult.CreateCompleted(
                inspectionId,
                HardwareInspectionOutcome.Completed,
                snapshot));
        }
    }

    private sealed class CancelThenSuccessService : IHardwareInspectionService
    {
        private readonly TaskCompletionSource<HardwareInspectionRunResult> _terminal =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        private Guid _inspectionId;

        internal TaskCompletionSource Started { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Task<HardwareInspectionRunResult> RunAsync(
            Guid inspectionId,
            IProgress<HardwareInspectionRunProgress> progress,
            CancellationToken cancellationToken)
        {
            _inspectionId = inspectionId;
            Started.TrySetResult();
            return _terminal.Task;
        }

        internal void CompleteSuccessfully() =>
            _terminal.TrySetResult(HardwareInspectionRunResult.CreateCompleted(
                _inspectionId,
                HardwareInspectionOutcome.Completed,
                HardwareInspectionContractTests.CreateUsableSnapshotForPresentation(
                    _inspectionId)));
    }
}
