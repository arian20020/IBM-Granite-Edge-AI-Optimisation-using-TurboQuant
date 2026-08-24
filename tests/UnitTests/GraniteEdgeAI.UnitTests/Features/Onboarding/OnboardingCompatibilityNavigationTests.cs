using GraniteEdgeAI.Features.HardwareInspection;
using GraniteEdgeAI.Features.HardwareInspection.Application;
using GraniteEdgeAI.Features.HardwareInspection.ViewModels;
using GraniteEdgeAI.Features.ModelHardwareCompatibility;
using GraniteEdgeAI.Features.ModelHardwareCompatibility.Infrastructure;
using GraniteEdgeAI.Features.ModelInspection;
using GraniteEdgeAI.Features.ModelInspection.Contracts;
using GraniteEdgeAI.Features.ModelInspection.Handoff;
using GraniteEdgeAI.Features.Onboarding;
using GraniteEdgeAI.UnitTests.Features.HardwareInspection;
using GraniteEdgeAI.UnitTests.Features.ModelInspection.Presentation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.VisualStudio.TestTools.UnitTesting.AppContainer;

namespace GraniteEdgeAI.UnitTests.Features.Onboarding;

[TestClass]
public sealed class OnboardingCompatibilityNavigationTests
{
    private static readonly Guid ModelRunId =
        Guid.Parse("22222222-2222-4222-8222-222222222222");

    [UITestMethod]
    [TestCategory("WinUI")]
    public async Task MatchingCompletedJourney_NavigatesOnceAndBackDoesNotRerunHardware()
    {
        ModelInspectionExecutionResult terminal = Terminal();
        ModelInspectionHandoff modelHandoff = ModelHandoff(terminal);
        var source = new ModelInspectionPage();
        var service = new CountingHardwareService();
        var shell = new OnboardingShellPage(
            static (frame, request) => frame.Navigate(typeof(ModelInspectionPage), request),
            service,
            hardwareInspectionNavigator: null,
            hardwareHandoffReissuer: null,
            new FreshMemorySource(),
            (_, handoff) => handoff.ModelInspectionRunId == ModelRunId ? terminal : null,
            compatibilityNavigator: null);
        shell.AttachModelInspectionPage(source);
        Assert.IsTrue(shell.NavigateToHardwareInspection(source, modelHandoff));
        var frame = (Frame)shell.FindName("StageFrame");
        var hardwarePage = (HardwareInspectionPage)frame.Content;
        HardwareInspectionHandoff hardwareHandoff = HardwareInspectionHandoff.Create(
            shell.CurrentProductHardwareRunId,
            HardwareInspectionOutcome.Completed,
            HardwareInspectionContractTests.CreateUsableSnapshotForPresentation());

        Assert.IsTrue(await shell.NavigateToCompatibilityAsync(
            hardwarePage,
            new HardwareInspectionCompletedEventArgs(hardwareHandoff)));
        var compatibilityPage = frame.Content as CompatibilityPage;
        Assert.IsNotNull(compatibilityPage);
        await compatibilityPage.ViewModel.StartAsync();
        Assert.IsFalse(
            compatibilityPage.ViewModel.Presentation.PrimaryActionEnabled,
            "Continue must remain unavailable until the optimisation destination is integrated.");

        compatibilityPage.ViewModel.BackCommand.Execute(null);

        Assert.AreSame(hardwarePage, frame.Content);
        Assert.AreEqual(0, service.CallCount);
    }

    [UITestMethod]
    [TestCategory("WinUI")]
    public async Task MismatchedHardwareRun_FailsClosedWithoutNavigation()
    {
        ModelInspectionExecutionResult terminal = Terminal();
        ModelInspectionHandoff modelHandoff = ModelHandoff(terminal);
        var source = new ModelInspectionPage();
        var shell = new OnboardingShellPage(
            static (frame, request) => frame.Navigate(typeof(ModelInspectionPage), request),
            new CountingHardwareService(),
            hardwareInspectionNavigator: null,
            hardwareHandoffReissuer: null,
            new FreshMemorySource(),
            (_, _) => terminal,
            compatibilityNavigator: null);
        shell.AttachModelInspectionPage(source);
        Assert.IsTrue(shell.NavigateToHardwareInspection(source, modelHandoff));
        var frame = (Frame)shell.FindName("StageFrame");
        var hardwarePage = (HardwareInspectionPage)frame.Content;
        HardwareInspectionHandoff mismatch = HardwareInspectionHandoff.Create(
            Guid.NewGuid(),
            HardwareInspectionOutcome.Completed,
            HardwareInspectionContractTests.CreateUsableSnapshotForPresentation());

        Assert.IsFalse(await shell.NavigateToCompatibilityAsync(
            hardwarePage,
            new HardwareInspectionCompletedEventArgs(mismatch)));
        Assert.AreSame(hardwarePage, frame.Content);
    }

    private static ModelInspectionExecutionResult Terminal() =>
        ModelInspectionExecutionResult.Completed(
            PresentationTestData.CreateResult(ModelInspectionOutcome.Ready));

    private static ModelInspectionHandoff ModelHandoff(ModelInspectionExecutionResult terminal)
    {
        Assert.IsTrue(ModelInspectionHandoffProjector.TryProject(
            ModelRunId,
            ModelRunId,
            terminal,
            out ModelInspectionHandoff? handoff));
        return handoff!;
    }

    private sealed class FreshMemorySource : ICompatibilityFreshMemorySource
    {
        public ValueTask<AvailableMemorySnapshot> CaptureAsync(CancellationToken cancellationToken) =>
            ValueTask.FromResult(new AvailableMemorySnapshot(
                24UL * 1024 * 1024 * 1024,
                DateTimeOffset.UtcNow));
    }

    private sealed class CountingHardwareService : IHardwareInspectionService
    {
        internal int CallCount { get; private set; }

        public Task<HardwareInspectionRunResult> RunAsync(
            Guid inspectionId,
            IProgress<HardwareInspectionRunProgress> progress,
            CancellationToken cancellationToken)
        {
            CallCount++;
            return Task.FromResult(HardwareInspectionRunResult.CreateCancelled(inspectionId));
        }
    }
}
