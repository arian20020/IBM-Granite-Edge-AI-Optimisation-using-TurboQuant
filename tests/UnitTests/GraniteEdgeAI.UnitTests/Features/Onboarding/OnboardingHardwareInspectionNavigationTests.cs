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
using System.Reflection;

namespace GraniteEdgeAI.UnitTests;

[TestClass]
[TestCategory("HardwareInspectionGate8Acceptance")]
public sealed class OnboardingHardwareInspectionNavigationTests
{
    private static readonly Guid ModelRunId =
        Guid.Parse("22222222-2222-4222-8222-222222222222");

    [UITestMethod]
    [TestCategory("WinUI")]
    public void ValidTypedHandoff_ClaimsNavigatesThenAuthorizesOneRun()
    {
        var service = new RecordingHardwareService();
        int navigationAttempts = 0;
        var shell = new OnboardingShellPage(
            static (frame, request) => frame.Navigate(
                typeof(ModelInspectionPage),
                request),
            service,
            (frame, viewModel, retryHandoff) =>
            {
                navigationAttempts++;
                if (navigationAttempts == 2)
                {
                    return false;
                }

                frame.Content = new HardwareInspectionPage(
                    viewModel,
                    retryHandoff);
                return true;
            },
            hardwareHandoffReissuer: static _ => CreateHandoff());
        ModelInspectionPage source = CreateSourcePage();
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
        PropertyInfo? currentRunProperty = typeof(OnboardingShellPage).GetProperty(
            "CurrentProductHardwareRunId",
            BindingFlags.Instance | BindingFlags.NonPublic);
        PropertyInfo? configuredRunProperty = typeof(HardwareInspectionPage).GetProperty(
            "ConfiguredInspectionId",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.IsNotNull(currentRunProperty);
        Assert.IsNotNull(configuredRunProperty);
        Guid firstHardwareRunId = (Guid)currentRunProperty.GetValue(shell)!;
        Assert.AreNotEqual(Guid.Empty, firstHardwareRunId);
        Assert.AreEqual(
            firstHardwareRunId,
            (Guid)configuredRunProperty.GetValue(hardwarePage)!);
        Load(hardwarePage);
        Assert.AreEqual(firstHardwareRunId, service.LastInspectionId);
        Assert.AreEqual(
            ModelInspectionHandoffLifecycleState.BoundToHardwareRun,
            shell.GetModelHandoffState(handoff.ModelInspectionHandoffId));

        MethodInfo? retryMethod = typeof(OnboardingShellPage).GetMethod(
            "RetryHardwareInspection",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.IsNotNull(retryMethod);
        Assert.IsFalse((bool)retryMethod.Invoke(shell, [hardwarePage])!);
        Assert.AreSame(hardwarePage, frame.Content);
        Assert.AreEqual(1, service.CallCount);
        Assert.IsTrue((bool)retryMethod.Invoke(shell, [hardwarePage])!);
        var retriedPage = frame.Content as HardwareInspectionPage;
        Assert.IsNotNull(retriedPage);
        Assert.AreNotSame(hardwarePage, retriedPage);
        Guid retryHardwareRunId = (Guid)currentRunProperty.GetValue(shell)!;
        Assert.AreNotEqual(firstHardwareRunId, retryHardwareRunId);
        Assert.AreEqual(
            retryHardwareRunId,
            (Guid)configuredRunProperty.GetValue(retriedPage)!);
        Load(retriedPage);
        Assert.AreEqual(retryHardwareRunId, service.LastInspectionId);
        Assert.AreNotSame(handoff, retriedPage.OpaqueModelHandoff);
        Assert.AreEqual(
            ModelInspectionHandoffLifecycleState.Invalidated,
            shell.GetModelHandoffState(handoff.ModelInspectionHandoffId));
        Assert.AreEqual(
            ModelInspectionHandoffLifecycleState.BoundToHardwareRun,
            shell.GetModelHandoffState(
                retriedPage.OpaqueModelHandoff!.ModelInspectionHandoffId));

        retriedPage.Apply(
            new HardwareInspectionPresentationFactory().CreateInvalidHandoff());
        var actionCard = (HardwareInspectionActionCard)retriedPage.FindName(
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
            shell.GetModelHandoffState(
                retriedPage.OpaqueModelHandoff.ModelInspectionHandoffId));
    }

    [UITestMethod]
    [TestCategory("WinUI")]
    public void FailedNavigation_RollsBackWithoutAuthorizingOrStartingHardware()
    {
        var service = new RecordingHardwareService();
        int navigationAttempts = 0;
        var shell = new OnboardingShellPage(
            static (frame, request) => frame.Navigate(
                typeof(ModelInspectionPage),
                request),
            service,
            (frame, viewModel, retryHandoff) =>
            {
                navigationAttempts++;
                if (navigationAttempts == 1)
                {
                    return false;
                }

                frame.Content = new HardwareInspectionPage(
                    viewModel,
                    retryHandoff);
                return true;
            });
        ModelInspectionPage source = CreateSourcePage();
        shell.AttachModelInspectionPage(source);
        ModelInspectionHandoff handoff = CreateHandoff();

        bool navigated = shell.NavigateToHardwareInspection(source, handoff);

        Assert.IsFalse(navigated);
        Assert.AreEqual(0, service.CallCount);
        Assert.AreEqual(
            ModelInspectionHandoffLifecycleState.Issued,
            shell.GetModelHandoffState(handoff.ModelInspectionHandoffId));
        Assert.AreEqual(OnboardingStage.ImportModel, shell.CurrentStage);

        Assert.IsTrue(shell.NavigateToHardwareInspection(source, handoff));
        Load((HardwareInspectionPage)((Frame)shell.FindName("StageFrame")).Content);
        Assert.AreEqual(1, service.CallCount);
        Assert.AreEqual(OnboardingStage.CheckHardwareFit, shell.CurrentStage);
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
        ModelInspectionPage active = CreateSourcePage();
        var stale = new ModelInspectionPage();
        shell.AttachModelInspectionPage(active);
        ModelInspectionHandoff handoff = CreateHandoff();

        Assert.IsFalse(shell.NavigateToHardwareInspection(stale, handoff));
        Assert.IsTrue(shell.NavigateToHardwareInspection(active, handoff));
        Assert.IsFalse(shell.NavigateToHardwareInspection(active, handoff));

        var frame = (Frame)shell.FindName("StageFrame");
        var hardwarePage = (HardwareInspectionPage)frame.Content;
        MethodInfo? unloaded = typeof(HardwareInspectionPage).GetMethod(
            "OnUnloaded",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.IsNotNull(unloaded);
        unloaded.Invoke(hardwarePage, [hardwarePage, new Microsoft.UI.Xaml.RoutedEventArgs()]);
        Assert.AreEqual(
            ModelInspectionHandoffLifecycleState.Invalidated,
            shell.GetModelHandoffState(handoff.ModelInspectionHandoffId));
        Assert.AreEqual(Guid.Empty, shell.CurrentProductHardwareRunId);
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

    private static ModelInspectionPage CreateSourcePage()
    {
        var page = new ModelInspectionPage();
        MethodInfo? activate = typeof(ModelInspectionPage).GetMethod(
            "ActivateRequest",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.IsNotNull(activate);
        activate.Invoke(page, [PresentationTestData.CreateRequest()]);
        return page;
    }

    private static void Load(HardwareInspectionPage page)
    {
        MethodInfo? loaded = typeof(HardwareInspectionPage).GetMethod(
            "OnLoaded",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.IsNotNull(loaded);
        loaded.Invoke(page, [page, new Microsoft.UI.Xaml.RoutedEventArgs()]);
    }

    private sealed class RecordingHardwareService : IHardwareInspectionService
    {
        internal int CallCount { get; private set; }

        internal Guid LastInspectionId { get; private set; }

        public Task<HardwareInspectionRunResult> RunAsync(
            Guid inspectionId,
            IProgress<HardwareInspectionRunProgress> progress,
            CancellationToken cancellationToken)
        {
            CallCount++;
            LastInspectionId = inspectionId;
            return Task.FromResult(
                HardwareInspectionRunResult.CreateCancelled(inspectionId));
        }
    }
}
