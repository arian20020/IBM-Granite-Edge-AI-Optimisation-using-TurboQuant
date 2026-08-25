using System.Diagnostics;
using System.Reflection;
using GraniteEdgeAI.Features.ModelHardwareCompatibility;
using GraniteEdgeAI.Features.ModelHardwareCompatibility.Infrastructure;
using GraniteEdgeAI.Features.ModelHardwareCompatibility.ViewModels;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Candidates;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Contracts;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.FitAssessment;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Presentation;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Domain;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Routes.Gguf;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
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
    public async Task Release_RunsTrustedCallbacksInRegistrationOrderThenCollects()
    {
        List<string> calls = [];
        var recovery = new WindowsCompatibilityMemoryRecovery(
            [
                _ => { calls.Add("first"); return Task.CompletedTask; },
                _ => { calls.Add("second"); return Task.CompletedTask; }
            ],
            () => throw new AssertFailedException("Task Manager must stay separate."),
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
            () => { },
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
            () => { },
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
            () => { },
            () => collected = true);

        await recovery.ReleaseApplicationMemoryAsync(CancellationToken.None);

        Assert.IsFalse(collected);
    }

    [TestMethod]
    public async Task TaskManager_UsesOnlyTheFixedShellLaunchWithoutArguments()
    {
        bool launched = false;
        var recovery = new WindowsCompatibilityMemoryRecovery(
            [],
            () => launched = true,
            () => { });

        await recovery.OpenTaskManagerAsync(CancellationToken.None);

        Assert.IsTrue(launched);
        MethodInfo factory = typeof(WindowsCompatibilityMemoryRecovery).GetMethod(
            "CreateTrustedTaskManagerStartInfo",
            BindingFlags.NonPublic | BindingFlags.Static)!;
        var startInfo = (ProcessStartInfo)factory.Invoke(null, null)!;
        Assert.AreEqual("taskmgr.exe", startInfo.FileName);
        Assert.IsTrue(startInfo.UseShellExecute);
        Assert.AreEqual(string.Empty, startInfo.Arguments);
        Assert.AreEqual(string.Empty, startInfo.Verb);
    }

    [TestMethod]
    public async Task TaskManager_CancellationBeforeLaunchPreventsLaunch()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        bool launched = false;
        var recovery = new WindowsCompatibilityMemoryRecovery(
            [],
            () => launched = true,
            () => { });

        await Assert.ThrowsExactlyAsync<OperationCanceledException>(() =>
            recovery.OpenTaskManagerAsync(cancellation.Token));

        Assert.IsFalse(launched);
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

        await viewModel.ReleaseApplicationMemoryAndRetryAsync();

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

        Task stale = viewModel.ReleaseApplicationMemoryAndRetryAsync();
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

        Task first = viewModel.ReleaseApplicationMemoryAndRetryAsync();
        await recovery.ReleaseStarted.Task;
        Task second = viewModel.ReleaseApplicationMemoryAndRetryAsync();
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

        Task operation = viewModel.ReleaseApplicationMemoryAndRetryAsync();
        await recovery.ReleaseStarted.Task;
        viewModel.RetireAttempt();
        releaseRecovery.SetResult();
        await operation;

        Assert.IsTrue(recovery.LastReleaseToken.IsCancellationRequested);
        Assert.AreEqual(1, evaluations);
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

        await viewModel.ReleaseApplicationMemoryAndRetryAsync();
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
        page.Apply(GraniteEdgeAI.Features.ModelHardwareCompatibility.Presentation
            .CompatibilityPresentationFactory.From(
                MemoryPressureScreen()));

        FrameworkElement actions = Element<FrameworkElement>(page, "MemoryRecoveryActions");
        Button release = Element<Button>(page, "ReleaseApplicationMemoryAction");
        Button taskManager = Element<Button>(page, "OpenTaskManagerAction");

        Assert.AreEqual(Visibility.Visible, actions.Visibility);
        Assert.IsGreaterThanOrEqualTo(44d, release.MinHeight);
        Assert.IsGreaterThanOrEqualTo(44d, taskManager.MinHeight);
        Assert.AreEqual("Release app memory and check again", AutomationProperties.GetName(release));
        Assert.AreEqual("Open Task Manager", AutomationProperties.GetName(taskManager));
        StringAssert.Contains(AutomationProperties.GetHelpText(taskManager),
            "you choose which applications to close");
        Assert.AreSame(page.ViewModel.ReleaseMemoryCommand, release.Command);
        Assert.AreSame(page.ViewModel.OpenTaskManagerCommand, taskManager.Command);
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

        internal RecordingRecovery(Task? release = null) =>
            _release = release ?? Task.CompletedTask;

        internal int ReleaseCalls { get; private set; }

        internal int OpenCalls { get; private set; }

        internal CancellationToken LastReleaseToken { get; private set; }

        internal TaskCompletionSource ReleaseStarted { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public async Task ReleaseApplicationMemoryAsync(CancellationToken cancellationToken)
        {
            ReleaseCalls++;
            LastReleaseToken = cancellationToken;
            ReleaseStarted.TrySetResult();
            await _release;
            cancellationToken.ThrowIfCancellationRequested();
        }

        public Task OpenTaskManagerAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            OpenCalls++;
            return Task.CompletedTask;
        }
    }
}
