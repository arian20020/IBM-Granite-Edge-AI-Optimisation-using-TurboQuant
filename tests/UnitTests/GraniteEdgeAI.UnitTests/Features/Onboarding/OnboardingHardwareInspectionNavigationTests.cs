using GraniteEdgeAI.Features.HardwareInspection;
using GraniteEdgeAI.Features.HardwareInspection.Application;
using GraniteEdgeAI.Features.HardwareInspection.Presentation.Controls;
using GraniteEdgeAI.Features.HardwareInspection.Presentation.Factories;
using GraniteEdgeAI.Features.HardwareInspection.ViewModels;
using GraniteEdgeAI.Features.ModelInspection;
using GraniteEdgeAI.Features.ModelInspection.Contracts;
using GraniteEdgeAI.Features.ModelInspection.Handoff;
using GraniteEdgeAI.Features.Onboarding;
using GraniteEdgeAI.UnitTests.Features.ModelInspection.Presentation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Automation.Peers;
using Microsoft.UI.Xaml.Automation.Provider;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.VisualStudio.TestTools.UnitTesting.AppContainer;

namespace GraniteEdgeAI.UnitTests;

[TestClass]
public sealed class OnboardingHardwareInspectionNavigationTests
{
    private static readonly Guid ModelRunId =
        Guid.Parse("22222222-2222-4222-8222-222222222222");

    [UITestMethod]
    [TestCategory("WinUI")]
    public void ValidTypedHandoff_ClaimsNavigatesThenAuthorizesOneRun()
    {
        var service = new RecordingHardwareService();
        var shell = new OnboardingShellPage(
            static (frame, request) => frame.Navigate(
                typeof(ModelInspectionPage),
                request),
            service);
        var source = new ModelInspectionPage();
        shell.AttachModelInspectionPage(source);
        ModelInspectionHandoff handoff = CreateHandoff();

        bool navigated = shell.NavigateToHardwareInspection(source, handoff);

        Assert.IsTrue(navigated);
        Assert.AreEqual(OnboardingStage.CheckHardwareFit, shell.CurrentStage);
        var frame = (Frame)shell.FindName("StageFrame");
        var hardwarePage = frame.Content as HardwareInspectionPage;
        Assert.IsNotNull(hardwarePage);
        Assert.AreSame(handoff, hardwarePage.OpaqueModelHandoff);
        Assert.IsTrue(hardwarePage.IsStartAuthorized);
        Assert.AreEqual(
            ModelInspectionHandoffLifecycleState.BoundToHardwareRun,
            shell.GetModelHandoffState(handoff.ModelInspectionHandoffId));

        hardwarePage.Apply(
            new HardwareInspectionPresentationFactory().CreateInvalidHandoff());
        var actionCard = (HardwareInspectionActionCard)hardwarePage.FindName(
            "ActionCard");
        Button back = ((StackPanel)actionCard.FindName("ActionsPanel"))
            .Children.Cast<Button>()
            .Single();
        var invoke = (IInvokeProvider)new ButtonAutomationPeer(back)
            .GetPattern(PatternInterface.Invoke)!;

        invoke.Invoke();

        Assert.AreSame(source, frame.Content);
        Assert.AreEqual(OnboardingStage.InspectModel, shell.CurrentStage);
        Assert.AreEqual(
            ModelInspectionHandoffLifecycleState.Invalidated,
            shell.GetModelHandoffState(handoff.ModelInspectionHandoffId));
    }

    [UITestMethod]
    [TestCategory("WinUI")]
    public void FailedNavigation_RollsBackWithoutAuthorizingOrStartingHardware()
    {
        var service = new RecordingHardwareService();
        var shell = new OnboardingShellPage(
            static (frame, request) => frame.Navigate(
                typeof(ModelInspectionPage),
                request),
            service,
            static (_, _, _) => false);
        var source = new ModelInspectionPage();
        shell.AttachModelInspectionPage(source);
        ModelInspectionHandoff handoff = CreateHandoff();

        bool navigated = shell.NavigateToHardwareInspection(source, handoff);

        Assert.IsFalse(navigated);
        Assert.AreEqual(0, service.CallCount);
        Assert.AreEqual(
            ModelInspectionHandoffLifecycleState.Issued,
            shell.GetModelHandoffState(handoff.ModelInspectionHandoffId));
        Assert.AreEqual(OnboardingStage.ImportModel, shell.CurrentStage);
    }

    [UITestMethod]
    [TestCategory("WinUI")]
    public void StaleSenderOrDuplicateHandoff_CannotNavigateTwice()
    {
        var service = new RecordingHardwareService();
        var shell = new OnboardingShellPage(
            static (frame, request) => frame.Navigate(
                typeof(ModelInspectionPage),
                request),
            service);
        var active = new ModelInspectionPage();
        var stale = new ModelInspectionPage();
        shell.AttachModelInspectionPage(active);
        ModelInspectionHandoff handoff = CreateHandoff();

        Assert.IsFalse(shell.NavigateToHardwareInspection(stale, handoff));
        Assert.IsTrue(shell.NavigateToHardwareInspection(active, handoff));
        Assert.IsFalse(shell.NavigateToHardwareInspection(active, handoff));
    }

    private static ModelInspectionHandoff CreateHandoff()
    {
        Assert.IsTrue(ModelInspectionHandoffProjector.TryProject(
            ModelRunId,
            ModelRunId,
            ModelInspectionExecutionResult.Completed(
                PresentationTestData.CreateResult(ModelInspectionOutcome.Ready)),
            out ModelInspectionHandoff? handoff));
        Assert.IsNotNull(handoff);
        return handoff;
    }

    private sealed class RecordingHardwareService : IHardwareInspectionService
    {
        internal int CallCount { get; private set; }

        public Task<HardwareInspectionRunResult> RunAsync(
            Guid inspectionId,
            IProgress<HardwareInspectionRunProgress> progress,
            CancellationToken cancellationToken)
        {
            CallCount++;
            return Task.FromResult(
                HardwareInspectionRunResult.CreateCancelled(inspectionId));
        }
    }
}
