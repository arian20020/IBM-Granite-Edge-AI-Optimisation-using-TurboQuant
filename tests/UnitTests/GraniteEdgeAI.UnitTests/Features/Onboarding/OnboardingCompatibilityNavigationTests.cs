using GraniteEdgeAI.Features.HardwareInspection;
using GraniteEdgeAI.Features.HardwareInspection.Application;
using GraniteEdgeAI.Features.HardwareInspection.ViewModels;
using GraniteEdgeAI.Features.ModelHardwareCompatibility;
using GraniteEdgeAI.Features.ModelHardwareCompatibility.Infrastructure;
using GraniteEdgeAI.Features.ModelInspection;
using GraniteEdgeAI.Features.ModelInspection.Contracts;
using GraniteEdgeAI.Features.ModelInspection.Handoff;
using GraniteEdgeAI.Features.Onboarding;
using GraniteEdgeAI.HardwareInspection.Foundation.Windows;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Presentation;
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
            new FreshResourcesSource(),
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
            new FreshResourcesSource(),
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

    [UITestMethod]
    [TestCategory("WinUI")]
    public async Task EveryCompatibilityAttempt_CapturesFreshResourcesAndCanChangeOutcome()
    {
        ModelInspectionExecutionResult terminal = Terminal();
        ModelInspectionHandoff modelHandoff = ModelHandoff(terminal);
        var source = new ModelInspectionPage();
        var fresh = new SequencedFreshResourcesSource(
            FreshResources(1),
            FreshResources(24UL * 1024 * 1024 * 1024));
        var shell = new OnboardingShellPage(
            static (frame, request) => frame.Navigate(typeof(ModelInspectionPage), request),
            new CountingHardwareService(), null, null, fresh,
            (_, _) => terminal,
            (frame, page) =>
            {
                page.StartAutomatically = false;
                frame.Content = page;
                return true;
            });
        shell.AttachModelInspectionPage(source);
        Assert.IsTrue(shell.NavigateToHardwareInspection(source, modelHandoff));
        var frame = (Frame)shell.FindName("StageFrame");
        var hardwarePage = (HardwareInspectionPage)frame.Content;
        HardwareInspectionHandoff hardwareHandoff = HardwareInspectionHandoff.Create(
            shell.CurrentProductHardwareRunId,
            HardwareInspectionOutcome.Completed,
            HardwareInspectionContractTests.CreateUsableSnapshotForPresentation());

        Assert.IsTrue(await shell.NavigateToCompatibilityAsync(
            hardwarePage, new HardwareInspectionCompletedEventArgs(hardwareHandoff)));
        var compatibilityPage = (CompatibilityPage)frame.Content;
        Assert.AreEqual(0, fresh.CallCount,
            "Navigation must not freeze a resource snapshot for later attempts.");

        await compatibilityPage.ViewModel.StartAsync();
        GraniteEdgeAI.Features.ModelHardwareCompatibility.Presentation
            .CompatibilityOutcomeTone first = compatibilityPage.ViewModel.Presentation.Tone;
        await compatibilityPage.ViewModel.StartAsync();

        Assert.AreEqual(2, fresh.CallCount);
        Assert.AreNotEqual(
            GraniteEdgeAI.Features.ModelHardwareCompatibility.Presentation
                .CompatibilityOutcomeTone.Positive,
            first);
        Assert.AreEqual(
            GraniteEdgeAI.Features.ModelHardwareCompatibility.Presentation
                .CompatibilityOutcomeTone.Positive,
            compatibilityPage.ViewModel.Presentation.Tone);
    }

    [TestMethod]
    public async Task WindowsFreshResourceSource_UsesCurrentProvidersAndInjectedClock()
    {
        DateTimeOffset memoryAt = new(2026, 8, 25, 12, 0, 0, TimeSpan.Zero);
        DateTimeOffset storageAt = memoryAt.AddSeconds(1);
        DateTimeOffset observedAt = memoryAt.AddSeconds(2);
        var source = new WindowsCompatibilityFreshResourcesSource(
            new FixedMemoryProvider(7_000_000_000, memoryAt),
            _ => ValueTask.FromResult(WindowsStorageEvidence.Available(
                100_000_000_000, 40_000_000_000, storageAt)),
            new FixedTimeProvider(observedAt));

        CompatibilityFreshResourcesInput captured =
            await source.CaptureAsync(CancellationToken.None);

        Assert.AreEqual(7_000_000_000UL, captured.AvailableSystemMemoryBytes);
        Assert.AreEqual(40_000_000_000UL, captured.AvailableStorageBytes);
        Assert.AreEqual(memoryAt, captured.ObservedAtUtc,
            "The aggregate must retain the oldest accepted provider observation.");
        Assert.IsFalse(captured.DedicatedDeviceMemoryEstablished,
            "No fresh dedicated-memory provider exists, so the source must stay unknown.");
    }

    [TestMethod]
    public async Task WindowsFreshResourceSource_RejectsStaleMemoryEvidence()
    {
        DateTimeOffset now = new(2026, 8, 25, 12, 0, 0, TimeSpan.Zero);
        WindowsCompatibilityFreshResourcesSource source = FreshSource(
            now.AddSeconds(-31), now, now);

        await Assert.ThrowsExactlyAsync<InvalidOperationException>(async () =>
            await source.CaptureAsync(CancellationToken.None));
    }

    [TestMethod]
    public async Task WindowsFreshResourceSource_RejectsStaleStorageEvidence()
    {
        DateTimeOffset now = new(2026, 8, 25, 12, 0, 0, TimeSpan.Zero);
        WindowsCompatibilityFreshResourcesSource source = FreshSource(
            now, now.AddSeconds(-31), now);

        await Assert.ThrowsExactlyAsync<InvalidOperationException>(async () =>
            await source.CaptureAsync(CancellationToken.None));
    }

    [TestMethod]
    public async Task WindowsFreshResourceSource_RejectsFutureEvidence()
    {
        DateTimeOffset now = new(2026, 8, 25, 12, 0, 0, TimeSpan.Zero);
        WindowsCompatibilityFreshResourcesSource source = FreshSource(
            now.AddSeconds(6), now, now);

        await Assert.ThrowsExactlyAsync<InvalidOperationException>(async () =>
            await source.CaptureAsync(CancellationToken.None));
    }

    [TestMethod]
    public async Task WindowsFreshResourceSource_RejectsExcessiveProviderSkew()
    {
        DateTimeOffset now = new(2026, 8, 25, 12, 0, 0, TimeSpan.Zero);
        WindowsCompatibilityFreshResourcesSource source = FreshSource(
            now.AddSeconds(-10), now, now);

        await Assert.ThrowsExactlyAsync<InvalidOperationException>(async () =>
            await source.CaptureAsync(CancellationToken.None));
    }

    [TestMethod]
    public async Task WindowsFreshResourceSource_AcceptsCloseEvidenceWithoutRestampingIt()
    {
        DateTimeOffset now = new(2026, 8, 25, 12, 0, 0, TimeSpan.Zero);
        DateTimeOffset memoryAt = now.AddSeconds(-2);
        WindowsCompatibilityFreshResourcesSource source = FreshSource(
            memoryAt, now.AddSeconds(-1), now);

        CompatibilityFreshResourcesInput captured =
            await source.CaptureAsync(CancellationToken.None);

        Assert.AreEqual(memoryAt, captured.ObservedAtUtc);
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

    private static CompatibilityFreshResourcesInput FreshResources(ulong memory) =>
        CompatibilityFreshResourcesInput.Create(
            memory,
            availableDedicatedDeviceMemoryBytes: null,
            availableStorageBytes: 64UL * 1024 * 1024 * 1024,
            DateTimeOffset.UtcNow);

    private static WindowsCompatibilityFreshResourcesSource FreshSource(
        DateTimeOffset memoryAt,
        DateTimeOffset storageAt,
        DateTimeOffset now) =>
        new(
            new FixedMemoryProvider(7_000_000_000, memoryAt),
            _ => ValueTask.FromResult(WindowsStorageEvidence.Available(
                100_000_000_000, 40_000_000_000, storageAt)),
            new FixedTimeProvider(now));

    private sealed class FreshResourcesSource : ICompatibilityFreshResourcesSource
    {
        public ValueTask<CompatibilityFreshResourcesInput> CaptureAsync(
            CancellationToken cancellationToken) =>
            ValueTask.FromResult(FreshResources(24UL * 1024 * 1024 * 1024));
    }

    private sealed class SequencedFreshResourcesSource : ICompatibilityFreshResourcesSource
    {
        private readonly Queue<CompatibilityFreshResourcesInput> _snapshots;

        internal SequencedFreshResourcesSource(
            params CompatibilityFreshResourcesInput[] snapshots) =>
            _snapshots = new Queue<CompatibilityFreshResourcesInput>(snapshots);

        internal int CallCount { get; private set; }

        public ValueTask<CompatibilityFreshResourcesInput> CaptureAsync(
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            CallCount++;
            return ValueTask.FromResult(_snapshots.Dequeue());
        }
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

    private sealed class FixedMemoryProvider(
        ulong availableBytes,
        DateTimeOffset capturedAtUtc) : IAvailableMemoryProvider
    {
        public ValueTask<AvailableMemorySnapshot> CaptureAsync(
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return ValueTask.FromResult(
                new AvailableMemorySnapshot(availableBytes, capturedAtUtc));
        }
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
