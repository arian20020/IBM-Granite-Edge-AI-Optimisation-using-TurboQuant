using System.Diagnostics;
using System.Reflection;
using GraniteEdgeAI.Features.ModelHardwareCompatibility;
using GraniteEdgeAI.Features.ModelHardwareCompatibility.Infrastructure;
using GraniteEdgeAI.Features.ModelHardwareCompatibility.Presentation;
using GraniteEdgeAI.Features.ModelHardwareCompatibility.ViewModels;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Candidates;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Contracts;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.FitAssessment;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Presentation;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Domain;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Routes.Gguf;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Automation.Peers;
using Microsoft.UI.Xaml.Controls;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.VisualStudio.TestTools.UnitTesting.AppContainer;

namespace GraniteEdgeAI.UnitTests.Features.ModelHardwareCompatibility;

[TestClass]
[DoNotParallelize]
public sealed class CompatibilityMemoryRecoveryTests
{
    [TestMethod]
    public void RecoveryPort_HasOnlyTheTwoApprovedMethods()
    {
        Type port = typeof(ICompatibilityMemoryRecovery);

        Assert.IsTrue(port.IsInterface);
        Assert.IsTrue(port.IsNotPublic);
        MethodInfo[] methods = port.GetMethods();
        CollectionAssert.AreEquivalent(
            new[]
            {
                "ReleaseApplicationMemoryAsync",
                "OpenTaskManagerAsync"
            },
            methods.Select(method => method.Name).ToArray());
        Assert.IsTrue(methods.All(method => method.ReturnType == typeof(Task)));
        Assert.IsTrue(methods.All(method =>
            method.GetParameters() is [{ ParameterType: var type }]
            && type == typeof(CancellationToken)));
        Assert.AreEqual(0, port.GetProperties().Length);
        Assert.AreEqual(0, port.GetEvents().Length);
    }

    [TestMethod]
    public async Task EmptyProductionRegistry_RefreshesWithoutCollectionOrReleaseClaim()
    {
        bool collected = false;
        var recovery = new WindowsCompatibilityMemoryRecovery(
            [], new RecordingTaskManagerStarter(), () => collected = true);

        await recovery.ReleaseApplicationMemoryAsync(CancellationToken.None);

        Assert.IsFalse(collected);
        string xaml = File.ReadAllText(Path.Combine(
            FindRepositoryRoot(), "IBM Granite with TurboQuant (Intel)",
            "Features", "ModelHardwareCompatibility", "CompatibilityPage.xaml"));
        StringAssert.Contains(xaml, "Refresh memory and check again");
        Assert.IsFalse(xaml.Contains("release its own temporary memory",
            StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(xaml.Contains("Release app memory",
            StringComparison.OrdinalIgnoreCase));
    }

    [TestMethod]
    public async Task Release_RunsTrustedCallbacksInRegistrationOrderThenCollects()
    {
        List<string> calls = [];
        var recovery = new WindowsCompatibilityMemoryRecovery(
            [
                _ => { calls.Add("first"); return Task.CompletedTask; },
                _ => { calls.Add("second"); return Task.CompletedTask; }
            ],
            new RecordingTaskManagerStarter(),
            () => calls.Add("collect"));

        await recovery.ReleaseApplicationMemoryAsync(CancellationToken.None);

        CollectionAssert.AreEqual(new[] { "first", "second", "collect" }, calls);
    }

    [TestMethod]
    public async Task Release_StopsOnCallbackFailureAndDoesNotCollectOrRunLaterCallbacks()
    {
        List<string> calls = [];
        var recovery = new WindowsCompatibilityMemoryRecovery(
            [
                _ => { calls.Add("first"); return Task.FromException(new InvalidOperationException("private")); },
                _ => { calls.Add("second"); return Task.CompletedTask; }
            ],
            new RecordingTaskManagerStarter(),
            () => calls.Add("collect"));

        await Assert.ThrowsExactlyAsync<InvalidOperationException>(() =>
            recovery.ReleaseApplicationMemoryAsync(CancellationToken.None));

        CollectionAssert.AreEqual(new[] { "first" }, calls);
    }

    [TestMethod]
    public async Task Release_ObservesCancellationBetweenCallbacksAndDoesNotCollect()
    {
        using var cancellation = new CancellationTokenSource();
        List<string> calls = [];
        var recovery = new WindowsCompatibilityMemoryRecovery(
            [
                _ =>
                {
                    calls.Add("first");
                    cancellation.Cancel();
                    return Task.CompletedTask;
                },
                _ => { calls.Add("second"); return Task.CompletedTask; }
            ],
            new RecordingTaskManagerStarter(),
            () => calls.Add("collect"));

        await Assert.ThrowsExactlyAsync<OperationCanceledException>(() =>
            recovery.ReleaseApplicationMemoryAsync(cancellation.Token));

        CollectionAssert.AreEqual(new[] { "first" }, calls);
    }

    [TestMethod]
    public async Task EmptyRegistry_DoesNotForceCollection()
    {
        bool collected = false;
        var recovery = new WindowsCompatibilityMemoryRecovery(
            [],
            new RecordingTaskManagerStarter(),
            () => collected = true);

        await recovery.ReleaseApplicationMemoryAsync(CancellationToken.None);

        Assert.IsFalse(collected);
    }

    [TestMethod]
    public async Task TaskManager_UsesOnlyTheFixedShellLaunchWithoutArguments()
    {
        string expectedExecutable = Path.Combine(
            Environment.SystemDirectory,
            "Taskmgr.exe");
        var starter = new RecordingTaskManagerStarter();
        var recovery = new WindowsCompatibilityMemoryRecovery(
            [],
            starter,
            () => { });

        await recovery.OpenTaskManagerAsync(CancellationToken.None);

        Assert.IsNotNull(starter.Request);
        Assert.AreEqual(expectedExecutable, starter.Request.FileName);
        Assert.IsTrue(Path.IsPathFullyQualified(starter.Request.FileName));
        Assert.IsTrue(starter.Request.UseShellExecute);
        Assert.AreEqual(string.Empty, starter.Request.Arguments);
        Assert.AreEqual(string.Empty, starter.Request.Verb);
        Assert.AreEqual(ProcessWindowStyle.Normal, starter.Request.WindowStyle);

        ConstructorInfo[] constructors = typeof(WindowsCompatibilityMemoryRecovery)
            .GetConstructors(BindingFlags.Instance | BindingFlags.NonPublic);
        ParameterInfo[] parameters = constructors
            .SelectMany(constructor => constructor.GetParameters()).ToArray();
        Assert.IsTrue(parameters.Any(parameter =>
            parameter.ParameterType == typeof(ITaskManagerProcessStarter)));
        Assert.IsFalse(parameters.Any(parameter =>
            parameter.ParameterType == typeof(string)
            || parameter.ParameterType == typeof(ProcessStartInfo)
            || (typeof(Delegate).IsAssignableFrom(parameter.ParameterType)
                && !(parameter.Name == "collect"
                    && parameter.ParameterType == typeof(Action)))));

        ConstructorInfo[] starterConstructors = typeof(WindowsTaskManagerProcessStarter)
            .GetConstructors(BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.IsTrue(starterConstructors.All(constructor =>
            constructor.GetParameters().Length == 0));
        Assert.IsFalse(typeof(WindowsTaskManagerProcessStarter)
            .GetFields(BindingFlags.Instance | BindingFlags.NonPublic)
            .Any(field => typeof(Delegate).IsAssignableFrom(field.FieldType)
                || field.FieldType == typeof(string)
                || field.FieldType == typeof(ProcessStartInfo)));

        ProcessStartInfo first = TaskManagerLaunchRequest.Fixed.CreateProcessStartInfo();
        first.FileName = "mutated-by-test.exe";
        ProcessStartInfo second = TaskManagerLaunchRequest.Fixed.CreateProcessStartInfo();
        Assert.AreEqual(expectedExecutable, second.FileName,
            "Each pure descriptor is rebuilt from the immutable fixed request.");
        Assert.AreEqual(string.Empty, second.Arguments);
        Assert.AreEqual(string.Empty, second.Verb);
        Assert.IsTrue(second.UseShellExecute);
    }

    [TestMethod]
    public async Task TaskManager_CancellationBeforeLaunchPreventsLaunch()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var starter = new RecordingTaskManagerStarter();
        var recovery = new WindowsCompatibilityMemoryRecovery(
            [],
            starter,
            () => { });

        await Assert.ThrowsExactlyAsync<OperationCanceledException>(() =>
            recovery.OpenTaskManagerAsync(cancellation.Token));

        Assert.IsNull(starter.Request);
    }

    [TestMethod]
    public void FixedStarter_HasNoInjectableProcessLaunchCapability()
    {
        Type starter = typeof(WindowsTaskManagerProcessStarter);

        Assert.IsTrue(starter.GetConstructors(BindingFlags.Instance | BindingFlags.NonPublic)
            .All(constructor => constructor.GetParameters().Length == 0));
        Assert.IsFalse(starter.GetFields(BindingFlags.Instance | BindingFlags.NonPublic)
            .Any(field => typeof(Delegate).IsAssignableFrom(field.FieldType)));

        string source = File.ReadAllText(Path.Combine(
            FindRepositoryRoot(), "IBM Granite with TurboQuant (Intel)",
            "Features", "ModelHardwareCompatibility", "Infrastructure",
            "WindowsCompatibilityMemoryRecovery.cs"));
        StringAssert.Contains(source,
            "using Process process = Process.Start(request.CreateProcessStartInfo())");
        Assert.IsFalse(source.Contains("Func<Process", StringComparison.Ordinal));
    }

    [TestMethod]
    public async Task ReleaseThenRecheck_IsOneOperationAndPublishesFreshResult()
    {
        int evaluations = 0;
        var recovery = new RecordingRecovery();
        var viewModel = new CompatibilityViewModel(
            _ => Task.FromResult(++evaluations == 1
                ? MemoryPressureScreen()
                : Screen(CompatibilityScreenState.EstimatedCompatible)),
            continueDestinationAvailable: true,
            recovery);
        await viewModel.StartAsync();

        await viewModel.RefreshMemoryAndRetryAsync();

        Assert.AreEqual(1, recovery.ReleaseCalls);
        Assert.AreEqual(2, evaluations);
        Assert.AreEqual("Yes — this model should run", viewModel.Presentation.OutcomeTitle);
    }

    [TestMethod]
    public async Task NewerCheck_RetiresNonCooperativeRecoveryBeforeItCanEvaluateOrPublish()
    {
        var releaseRecovery = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var recovery = new RecordingRecovery(releaseRecovery.Task);
        int evaluations = 0;
        var viewModel = new CompatibilityViewModel(
            _ => Task.FromResult(++evaluations == 1
                ? MemoryPressureScreen()
                : Screen(CompatibilityScreenState.NotEstablished)),
            continueDestinationAvailable: true,
            recovery);
        await viewModel.StartAsync();

        Task stale = viewModel.RefreshMemoryAndRetryAsync();
        await recovery.ReleaseStarted.Task;
        await viewModel.StartAsync();
        releaseRecovery.SetResult();
        await stale;

        Assert.AreEqual(2, evaluations, "The stale recovery must not begin a third evaluation.");
        Assert.AreEqual("We can't answer this yet", viewModel.Presentation.OutcomeTitle);
    }

    [TestMethod]
    public async Task RepeatedReleaseClicks_AreDebouncedUntilRecoveryAndRecheckFinish()
    {
        var releaseRecovery = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var recovery = new RecordingRecovery(releaseRecovery.Task);
        int evaluations = 0;
        var viewModel = new CompatibilityViewModel(
            _ => Task.FromResult(++evaluations == 1
                ? MemoryPressureScreen()
                : Screen(CompatibilityScreenState.EstimatedCompatible)),
            continueDestinationAvailable: true,
            recovery);
        await viewModel.StartAsync();

        Task first = viewModel.RefreshMemoryAndRetryAsync();
        await recovery.ReleaseStarted.Task;
        Task second = viewModel.RefreshMemoryAndRetryAsync();
        releaseRecovery.SetResult();
        await Task.WhenAll(first, second);

        Assert.AreEqual(1, recovery.ReleaseCalls);
        Assert.AreEqual(2, evaluations);
    }

    [TestMethod]
    public async Task RetiringPage_CancelsRecoveryAndPreventsTheRecheck()
    {
        var releaseRecovery = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var recovery = new RecordingRecovery(releaseRecovery.Task);
        int evaluations = 0;
        var viewModel = new CompatibilityViewModel(
            _ => Task.FromResult(++evaluations == 1
                ? MemoryPressureScreen()
                : Screen(CompatibilityScreenState.EstimatedCompatible)),
            continueDestinationAvailable: true,
            recovery);
        await viewModel.StartAsync();

        Task operation = viewModel.RefreshMemoryAndRetryAsync();
        await recovery.ReleaseStarted.Task;
        viewModel.RetireAttempt();
        releaseRecovery.SetResult();
        await operation;

        Assert.IsTrue(recovery.LastReleaseToken.IsCancellationRequested);
        Assert.AreEqual(1, evaluations);
    }

    [TestMethod]
    public async Task NewCompatibilityAttempt_CancelsPendingTaskManagerBeforeItCanLaunch()
    {
        var openGate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var recovery = new RecordingRecovery(openTaskManager: openGate.Task);
        var viewModel = new CompatibilityViewModel(
            _ => Task.FromResult(MemoryPressureScreen()),
            continueDestinationAvailable: true,
            recovery);
        await viewModel.StartAsync();

        Task pendingOpen = viewModel.OpenTaskManagerAsync();
        await recovery.OpenStarted.Task;
        await viewModel.StartAsync();
        openGate.SetResult();
        await Assert.ThrowsExactlyAsync<OperationCanceledException>(async () =>
            await pendingOpen);

        Assert.IsTrue(recovery.LastOpenToken.IsCancellationRequested);
        Assert.AreEqual(0, recovery.CompletedOpenCalls,
            "A stale auxiliary operation must not reach its launch boundary.");
    }

    [TestMethod]
    public void ThrowingAndReentrantCancellationCallbacks_AreContainedAndDisposedByOwner()
    {
        var cancellation = new CompatibilityViewModel.AttemptCancellation();
        CancellationToken token = cancellation.Token;
        using CancellationTokenRegistration registration = token.Register(() =>
        {
            cancellation.Cancel();
            throw new InvalidOperationException("private callback content");
        });

        cancellation.Cancel();
        cancellation.Complete();

        Assert.ThrowsExactly<ObjectDisposedException>(() =>
            cancellation.Token.Register(static () => { }));
    }

    [TestMethod]
    public async Task NewStart_RetiresNonCooperativeRecoveryBusyOwnerImmediately()
    {
        var releaseGate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var recovery = new RecordingRecovery(release: releaseGate.Task);
        var viewModel = new CompatibilityViewModel(
            _ => Task.FromResult(MemoryPressureScreen()), true, recovery);
        await viewModel.StartAsync();

        Task old = viewModel.RefreshMemoryAndRetryAsync();
        await recovery.ReleaseStarted.Task;
        await viewModel.StartAsync();

        Assert.IsTrue(viewModel.RefreshMemoryCommand.CanExecute(null));
        releaseGate.SetResult();
        await old;
        Assert.IsTrue(viewModel.RefreshMemoryCommand.CanExecute(null));
    }

    [TestMethod]
    public async Task ThrowingCancellationCallback_DuringNewStartCannotStrandTheNewAttempt()
    {
        var firstStarted = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseFirst = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        int evaluations = 0;
        var viewModel = new CompatibilityViewModel(async token =>
        {
            if (Interlocked.Increment(ref evaluations) == 1)
            {
                using CancellationTokenRegistration registration = token.Register(
                    () => throw new InvalidOperationException("private callback content"));
                firstStarted.SetResult();
                await releaseFirst.Task;
            }

            return MemoryPressureScreen();
        }, true, new RecordingRecovery());

        Task stale = viewModel.StartAsync();
        await firstStarted.Task;
        await viewModel.StartAsync();
        releaseFirst.SetResult();
        await stale;

        Assert.AreEqual(2, evaluations);
        Assert.AreEqual(CompatibilityMemoryRecoveryReason.SystemMemoryPressure,
            viewModel.Presentation.MemoryRecoveryReason);
        Assert.IsTrue(viewModel.RefreshMemoryCommand.CanExecute(null));
        Assert.IsTrue(viewModel.OpenTaskManagerCommand.CanExecute(null));
    }

    [TestMethod]
    public async Task ThrowingCancellationCallback_DuringRetireCannotPreventReloadedAttempt()
    {
        var firstStarted = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseFirst = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        int evaluations = 0;
        var viewModel = new CompatibilityViewModel(async token =>
        {
            if (Interlocked.Increment(ref evaluations) == 1)
            {
                using CancellationTokenRegistration registration = token.Register(
                    () => throw new InvalidOperationException("private callback content"));
                firstStarted.SetResult();
                await releaseFirst.Task;
            }

            return MemoryPressureScreen();
        }, true, new RecordingRecovery());

        Task retired = viewModel.StartAsync();
        await firstStarted.Task;
        viewModel.RetireAttempt();
        await viewModel.StartAsync();
        releaseFirst.SetResult();
        await retired;

        Assert.AreEqual(2, evaluations);
        Assert.AreEqual(CompatibilityMemoryRecoveryReason.SystemMemoryPressure,
            viewModel.Presentation.MemoryRecoveryReason);
        Assert.IsTrue(viewModel.RefreshMemoryCommand.CanExecute(null));
    }

    [TestMethod]
    public async Task StaleTaskManagerFinally_CannotClearTheNewTaskManagerBusyOwner()
    {
        var recovery = new SequencedTaskManagerRecovery();
        var viewModel = new CompatibilityViewModel(
            _ => Task.FromResult(MemoryPressureScreen()), true, recovery);
        await viewModel.StartAsync();

        Task stale = viewModel.OpenTaskManagerAsync();
        await recovery.FirstStarted.Task;
        await viewModel.StartAsync();
        Task current = viewModel.OpenTaskManagerAsync();
        await recovery.SecondStarted.Task;

        Assert.IsFalse(viewModel.OpenTaskManagerCommand.CanExecute(null));
        recovery.ReleaseFirst.SetResult();
        await Assert.ThrowsExactlyAsync<OperationCanceledException>(async () => await stale);
        Assert.IsFalse(viewModel.OpenTaskManagerCommand.CanExecute(null),
            "The stale finally block must not release the newer operation owner.");

        recovery.ReleaseSecond.SetResult();
        await current;
        Assert.IsTrue(viewModel.OpenTaskManagerCommand.CanExecute(null));
    }

    [TestMethod]
    public async Task RetireAndReload_RetiresAuxiliaryBusyOwnerBeforeStartingNewOne()
    {
        var recovery = new SequencedTaskManagerRecovery();
        var viewModel = new CompatibilityViewModel(
            _ => Task.FromResult(MemoryPressureScreen()), true, recovery);
        await viewModel.StartAsync();

        Task retired = viewModel.OpenTaskManagerAsync();
        await recovery.FirstStarted.Task;
        viewModel.RetireAttempt();
        await viewModel.StartAsync();
        Task reloaded = viewModel.OpenTaskManagerAsync();
        await recovery.SecondStarted.Task;

        Assert.IsFalse(viewModel.OpenTaskManagerCommand.CanExecute(null));
        recovery.ReleaseFirst.SetResult();
        await Assert.ThrowsExactlyAsync<OperationCanceledException>(async () => await retired);
        Assert.IsFalse(viewModel.OpenTaskManagerCommand.CanExecute(null));
        recovery.ReleaseSecond.SetResult();
        await reloaded;
        Assert.IsTrue(viewModel.OpenTaskManagerCommand.CanExecute(null));
    }

    [TestMethod]
    public async Task TaskManagerFailure_IsBoundedDoesNotReplaceResultAndRemainsRetryable()
    {
        var recovery = new FailingTaskManagerRecovery();
        var viewModel = new CompatibilityViewModel(
            _ => Task.FromResult(MemoryPressureScreen()), true, recovery);
        await viewModel.StartAsync();
        string outcome = viewModel.Presentation.OutcomeTitle;

        await viewModel.OpenTaskManagerAsync();

        Assert.AreEqual(outcome, viewModel.Presentation.OutcomeTitle);
        Assert.AreEqual(CompatibilityAuxiliaryStatusKind.Error,
            viewModel.AuxiliaryStatus.Kind);
        Assert.AreEqual("Task Manager could not be opened. You can try again.",
            viewModel.AuxiliaryStatus.Message);
        Assert.IsTrue(viewModel.OpenTaskManagerCommand.CanExecute(null));
        Assert.IsFalse(viewModel.AuxiliaryStatus.Message.Contains("taskmgr",
            StringComparison.OrdinalIgnoreCase));
    }

    [TestMethod]
    public async Task RecoveryActions_FailClosedWhenOutcomeIsNotMemoryPressure()
    {
        var recovery = new RecordingRecovery();
        var viewModel = new CompatibilityViewModel(
            _ => Task.FromResult(Screen(CompatibilityScreenState.EstimatedCompatible)),
            continueDestinationAvailable: true,
            recovery);
        await viewModel.StartAsync();

        await viewModel.RefreshMemoryAndRetryAsync();
        await viewModel.OpenTaskManagerAsync();

        Assert.AreEqual(0, recovery.ReleaseCalls);
        Assert.AreEqual(0, recovery.OpenCalls);
    }

    [TestMethod]
    public void NoSafeStateWithoutTypedSystemMemoryPressure_HidesRecoveryActions()
    {
        var presentation = GraniteEdgeAI.Features.ModelHardwareCompatibility.Presentation
            .CompatibilityPresentationFactory.From(
                Screen(CompatibilityScreenState.NoEstimatedSafeConfiguration));

        Assert.AreEqual(
            GraniteEdgeAI.Features.ModelHardwareCompatibility.Presentation
                .CompatibilityMemoryRecoveryReason.None,
            presentation.MemoryRecoveryReason);
    }

    [TestMethod]
    public void BlockingFinding_HidesRecoveryEvenWhenSetupShowsMemoryPressure()
    {
        CompatibilityScreenModel screen = CompatibilityScreenModel.ForPresentation(
            CompatibilityScreenState.NoEstimatedSafeConfiguration,
            [new CompatibilityFindingView(
                CompatibilityFindingCode.HardwareFactsUnavailable,
                FindingSeverity.Blocking)],
            [],
            BaselineExclusionReason.None,
            useCurrentModelAvailable: false,
            continueEnabled: false,
            MemoryPressureSetup(CompatibilityFitState.DoesNotFit));

        var presentation = GraniteEdgeAI.Features.ModelHardwareCompatibility.Presentation
            .CompatibilityPresentationFactory.From(screen);

        Assert.AreEqual(
            GraniteEdgeAI.Features.ModelHardwareCompatibility.Presentation
                .CompatibilityMemoryRecoveryReason.None,
            presentation.MemoryRecoveryReason);
    }

    [TestMethod]
    public void UnsupportedSetup_HidesRecoveryEvenWhenItsNumbersExceedTheBudget()
    {
        CompatibilityScreenModel screen = CompatibilityScreenModel.ForPresentation(
            CompatibilityScreenState.NoEstimatedSafeConfiguration,
            [],
            [],
            BaselineExclusionReason.None,
            useCurrentModelAvailable: false,
            continueEnabled: false,
            MemoryPressureSetup(CompatibilityFitState.Unsupported));

        var presentation = GraniteEdgeAI.Features.ModelHardwareCompatibility.Presentation
            .CompatibilityPresentationFactory.From(screen);

        Assert.AreEqual(
            GraniteEdgeAI.Features.ModelHardwareCompatibility.Presentation
                .CompatibilityMemoryRecoveryReason.None,
            presentation.MemoryRecoveryReason);
    }

    [TestMethod]
    public void MixedOrIncompleteDedicatedEvidence_FailsClosed()
    {
        CompatibilityPresentation presentation = PresentationForSetup(
            SetupWithDedicated(required: 512, safe: null, headroom: null));

        Assert.AreEqual(CompatibilityMemoryRecoveryReason.None,
            presentation.MemoryRecoveryReason);
    }

    [TestMethod]
    public void DedicatedMemoryBlocker_FailsClosed()
    {
        CompatibilityPresentation presentation = PresentationForSetup(
            SetupWithDedicated(required: 1024, safe: 512, headroom: 0));

        Assert.AreEqual(CompatibilityMemoryRecoveryReason.None,
            presentation.MemoryRecoveryReason);
    }

    [TestMethod]
    public void SourceCanary_HasNoEnumerationTerminationElevationOrConsumerDisclosure()
    {
        string featureRoot = Path.Combine(
            FindRepositoryRoot(),
            "IBM Granite with TurboQuant (Intel)",
            "Features",
            "ModelHardwareCompatibility");
        string source = string.Join('\n', Directory.EnumerateFiles(
            featureRoot, "*.*", SearchOption.AllDirectories)
            .Where(path => path.EndsWith(".cs", StringComparison.OrdinalIgnoreCase)
                || path.EndsWith(".xaml", StringComparison.OrdinalIgnoreCase))
            .Select(File.ReadAllText));

        string[] forbidden =
        [
            "Process.GetProcesses", ".Kill(", "taskkill", "Win32_Process",
            "ManagementObjectSearcher", "Verb = \"runas\"", "process name",
            "memory consumer", "largest process"
        ];
        foreach (string token in forbidden)
        {
            Assert.IsFalse(source.Contains(token, StringComparison.OrdinalIgnoreCase), token);
        }
    }

#if DEBUG
    [UITestMethod]
    [TestCategory("WinUI")]
    public void RealPage_MemoryActionsUseAccessibleModernExistingTree()
    {
        CompatibilityPage page = new() { StartAutomatically = false };
        CompatibilityPresentation optimization = GraniteEdgeAI.Features
            .ModelHardwareCompatibility.DebugFixtures.CompatibilityFixtureCatalogue.All
            .Single(fixture => fixture.Id == "CMP-020").Presentation;
        Assert.AreEqual(CompatibilityMemoryRecoveryReason.SystemMemoryPressure,
            optimization.MemoryRecoveryReason);
        page.ViewModel.ShowFixture(optimization);

        FrameworkElement card = Element<FrameworkElement>(page, "RecoveryCard");
        FrameworkElement actions = Element<FrameworkElement>(page, "MemoryRecoveryActions");
        Button release = Element<Button>(page, "RefreshMemoryAction");
        Button taskManager = Element<Button>(page, "OpenTaskManagerAction");

        Assert.AreEqual(Visibility.Visible, card.Visibility);
        Assert.AreEqual(Visibility.Visible, actions.Visibility);
        Assert.IsTrue(release.Command.CanExecute(null));
        Assert.IsTrue(taskManager.Command.CanExecute(null));
        Assert.IsGreaterThanOrEqualTo(44d, release.MinHeight);
        Assert.IsGreaterThanOrEqualTo(44d, taskManager.MinHeight);
        Assert.AreEqual("Refresh memory and check again", AutomationProperties.GetName(release));
        Assert.AreEqual("Open Task Manager", AutomationProperties.GetName(taskManager));
        StringAssert.Contains(AutomationProperties.GetHelpText(taskManager),
            "you choose which applications to close");
        Assert.AreSame(page.ViewModel.RefreshMemoryCommand, release.Command);
        Assert.AreSame(page.ViewModel.OpenTaskManagerCommand, taskManager.Command);
    }

    [UITestMethod]
    [TestCategory("WinUI")]
    public void RealPage_MixedIncompleteAndDedicatedBlockersCollapseAndDisableRecovery()
    {
        CompatibilitySetupView[] rejected =
        [
            SetupWithDedicated(required: 512, safe: null, headroom: null),
            SetupWithDedicated(required: 1024, safe: 512, headroom: 0)
        ];

        foreach (CompatibilitySetupView setup in rejected)
        {
            CompatibilityPage page = new() { StartAutomatically = false };
            page.ViewModel.ShowFixture(PresentationForSetup(setup));

            Assert.AreEqual(Visibility.Collapsed,
                Element<FrameworkElement>(page, "MemoryRecoveryActions").Visibility);
            Assert.IsFalse(page.ViewModel.RefreshMemoryCommand.CanExecute(null));
            Assert.IsFalse(page.ViewModel.OpenTaskManagerCommand.CanExecute(null));
        }
    }

    [UITestMethod]
    [TestCategory("WinUI")]
    public async Task RealPage_TaskManagerFailureIsAnAccessibleBoundedAnnouncement()
    {
        var viewModel = new CompatibilityViewModel(
            _ => Task.FromResult(MemoryPressureScreen()), true,
            new FailingTaskManagerRecovery());
        ConstructorInfo constructor = typeof(CompatibilityPage)
            .GetConstructors(BindingFlags.Instance | BindingFlags.NonPublic)
            .Single(candidate => candidate.GetParameters() is
                [{ ParameterType: var type }] && type == typeof(CompatibilityViewModel));
        var page = (CompatibilityPage)constructor.Invoke([viewModel]);
        await viewModel.StartAsync();

        await viewModel.OpenTaskManagerAsync();

        TextBlock status = Element<TextBlock>(page, "MemoryRecoveryStatus");
        Assert.AreEqual(Visibility.Visible, status.Visibility);
        Assert.AreEqual("Task Manager could not be opened. You can try again.", status.Text);
        Assert.AreEqual(AutomationLiveSetting.Polite,
            AutomationProperties.GetLiveSetting(status));
    }
#endif

    private static CompatibilityScreenModel Screen(CompatibilityScreenState state) =>
        CompatibilityScreenModel.ForPresentation(
            state,
            [],
            [],
            BaselineExclusionReason.None,
            useCurrentModelAvailable: false,
            continueEnabled: state == CompatibilityScreenState.EstimatedCompatible);

    private static CompatibilityScreenModel MemoryPressureScreen() =>
        CompatibilityScreenModel.ForPresentation(
            CompatibilityScreenState.NoEstimatedSafeConfiguration,
            [],
            [],
            BaselineExclusionReason.None,
            useCurrentModelAvailable: false,
            continueEnabled: false,
            MemoryPressureSetup(CompatibilityFitState.DoesNotFit));

    private static CompatibilitySetupView MemoryPressureSetup(
        CompatibilityFitState fit) =>
        CompatibilitySetupView.ForPresentation(
                RuntimeRouteId.LlamaCpp,
                CompatibilityBackend.Cpu,
                DeviceRouteId.Cpu,
                WeightQuantisation.Q4_K_M,
                contextTokens: 4096,
                fit,
                requiredBytes: 5_368_709_120,
                safeBudgetBytes: 4_294_967_296,
                headroomBytes: 0,
                uncertaintyAllowanceBytes: 268_435_456,
                isExperimental: false,
                requiresConversion: false,
                [],
                ggufKvCache: GgufKvCacheFormat.F16);

    private static CompatibilityPresentation PresentationForSetup(
        CompatibilitySetupView setup) =>
        CompatibilityPresentationFactory.From(
            CompatibilityScreenModel.ForPresentation(
                CompatibilityScreenState.NoEstimatedSafeConfiguration,
                [], [], BaselineExclusionReason.None,
                useCurrentModelAvailable: false,
                continueEnabled: false,
                setup));

    private static CompatibilitySetupView SetupWithDedicated(
        ulong? required,
        ulong? safe,
        ulong? headroom)
    {
        ConstructorInfo constructor = typeof(CompatibilitySetupView)
            .GetConstructors(BindingFlags.Instance | BindingFlags.NonPublic)
            .Single(candidate => candidate.GetParameters().Length == 18);
        return (CompatibilitySetupView)constructor.Invoke(
        [
            RuntimeRouteId.LlamaCpp,
            CompatibilityBackend.Cpu,
            DeviceRouteId.Cpu,
            WeightQuantisation.Q4_K_M,
            4096,
            CompatibilityFitState.DoesNotFit,
            5_368_709_120UL,
            4_294_967_296UL,
            0UL,
            268_435_456UL,
            false,
            false,
            Array.Empty<CompatibilityComponentView>(),
            required,
            safe,
            headroom,
            GgufKvCacheFormat.F16,
            null
        ]);
    }

    private static T Element<T>(FrameworkElement root, string name)
        where T : class
    {
        object? found = root.FindName(name);
        Assert.IsInstanceOfType<T>(found);
        return (T)found;
    }

    private static string FindRepositoryRoot(
        [System.Runtime.CompilerServices.CallerFilePath] string source = "")
    {
        DirectoryInfo? directory = new FileInfo(source).Directory;
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(
                    directory.FullName,
                    "IBM Granite with TurboQuant (Intel).slnx")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Repository root was not found.");
    }

    private sealed class RecordingRecovery : ICompatibilityMemoryRecovery
    {
        private readonly Task _release;
        private readonly Task _openTaskManager;

        internal RecordingRecovery(Task? release = null, Task? openTaskManager = null)
        {
            _release = release ?? Task.CompletedTask;
            _openTaskManager = openTaskManager ?? Task.CompletedTask;
        }

        internal int ReleaseCalls { get; private set; }

        internal int OpenCalls { get; private set; }

        internal int CompletedOpenCalls { get; private set; }

        internal CancellationToken LastReleaseToken { get; private set; }

        internal CancellationToken LastOpenToken { get; private set; }

        internal TaskCompletionSource ReleaseStarted { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        internal TaskCompletionSource OpenStarted { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public async Task ReleaseApplicationMemoryAsync(CancellationToken cancellationToken)
        {
            ReleaseCalls++;
            LastReleaseToken = cancellationToken;
            ReleaseStarted.TrySetResult();
            await _release;
            cancellationToken.ThrowIfCancellationRequested();
        }

        public async Task OpenTaskManagerAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            OpenCalls++;
            LastOpenToken = cancellationToken;
            OpenStarted.TrySetResult();
            await _openTaskManager;
            cancellationToken.ThrowIfCancellationRequested();
            CompletedOpenCalls++;
        }
    }

    private sealed class RecordingTaskManagerStarter : ITaskManagerProcessStarter
    {
        internal TaskManagerLaunchRequest? Request { get; private set; }

        public void Start(TaskManagerLaunchRequest request) => Request = request;
    }

    private sealed class FailingTaskManagerRecovery : ICompatibilityMemoryRecovery
    {
        public Task ReleaseApplicationMemoryAsync(CancellationToken cancellationToken) =>
            Task.CompletedTask;

        public Task OpenTaskManagerAsync(CancellationToken cancellationToken) =>
            Task.FromException(new InvalidOperationException("private executable path"));
    }

    private sealed class SequencedTaskManagerRecovery : ICompatibilityMemoryRecovery
    {
        private int _calls;

        internal TaskCompletionSource FirstStarted { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        internal TaskCompletionSource SecondStarted { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        internal TaskCompletionSource ReleaseFirst { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        internal TaskCompletionSource ReleaseSecond { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Task ReleaseApplicationMemoryAsync(CancellationToken cancellationToken) =>
            Task.CompletedTask;

        public async Task OpenTaskManagerAsync(CancellationToken cancellationToken)
        {
            int call = Interlocked.Increment(ref _calls);
            Task gate = call switch
            {
                1 => ReleaseFirst.Task,
                2 => ReleaseSecond.Task,
                _ => throw new InvalidOperationException("Unexpected auxiliary operation.")
            };
            (call == 1 ? FirstStarted : SecondStarted).SetResult();
            await gate;
            cancellationToken.ThrowIfCancellationRequested();
        }
    }
}
