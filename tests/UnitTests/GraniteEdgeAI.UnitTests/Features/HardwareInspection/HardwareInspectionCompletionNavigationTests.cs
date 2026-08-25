using GraniteEdgeAI.Features.HardwareInspection;
using GraniteEdgeAI.Features.HardwareInspection.Application;
using GraniteEdgeAI.Features.HardwareInspection.Domain;
using GraniteEdgeAI.Features.HardwareInspection.ViewModels;
using GraniteEdgeAI.Features.ModelInspection.Contracts;
using GraniteEdgeAI.Features.ModelInspection.Handoff;
using GraniteEdgeAI.UnitTests.Features.ModelInspection.Presentation;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.VisualStudio.TestTools.UnitTesting.AppContainer;
using System.Reflection;

namespace GraniteEdgeAI.UnitTests.Features.HardwareInspection;

[TestClass]
public sealed class HardwareInspectionCompletionNavigationTests
{
    private static readonly Guid ModelRunId =
        Guid.Parse("22222222-2222-4222-8222-222222222222");
    private static readonly Guid HardwareRunId =
        Guid.Parse("33333333-3333-4333-8333-333333333333");

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
        page.InspectionCompleted += (_, args) =>
        {
            completions++;
            published = args.Handoff;
        };

        Load(page);
        page.AuthorizeStart();
        await WaitForAsync(() => completions == 1);

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

        apply.Invoke(page, null);
        Assert.AreEqual(1, completions);
    }

    private static async Task WaitForAsync(Func<bool> predicate)
    {
        DateTimeOffset deadline = DateTimeOffset.UtcNow.AddSeconds(3);
        while (!predicate() && DateTimeOffset.UtcNow < deadline)
        {
            await Task.Delay(10);
        }
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
                HardwareInspectionContractTests.CreateUsableSnapshotForPresentation();
            return Task.FromResult(HardwareInspectionRunResult.CreateCompleted(
                inspectionId,
                HardwareInspectionOutcome.Completed,
                snapshot));
        }
    }
}
