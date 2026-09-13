using GraniteEdgeAI.Features.ModelOptimization.Export;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Optimization;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace GraniteEdgeAI.UnitTests.Features.ModelOptimization;

[TestClass]
public sealed class OptimizationExportControllerTests
{
    private const string Configuration = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
    private const string Manifest = "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb";
    private const string Source = "cccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccc";

    [TestMethod]
    public void TargetAndProgressRejectInvalidIdentityAndBounds()
    {
        Assert.ThrowsExactly<ArgumentException>(() => new VerifiedPersistentExportTarget(OptimizationRoute.Gguf, Guid.Empty, Configuration, Source, true, "output-1", Manifest, 4096));
        Assert.ThrowsExactly<ArgumentException>(() => new VerifiedPersistentExportTarget(OptimizationRoute.Gguf, Guid.NewGuid(), "invalid", Source, true, "output-1", Manifest, 4096));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new VerifiedPersistentExportTarget(OptimizationRoute.Gguf, Guid.NewGuid(), Configuration, Source, true, "output-1", Manifest, 0));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new OptimizationExportProgress(OptimizationExportStage.Writing, double.NaN));
    }

    [TestMethod]
    public async Task DuplicateActivationRunsOneExactTargetExport()
    {
        var service = new ControlledExportService();
        var controller = new OptimizationExportController(service);
        VerifiedPersistentExportTarget target = Target();
        controller.Bind(target);
        Task<bool> first = controller.TryStartAsync();
        Assert.IsFalse(await controller.TryStartAsync());
        service.Complete(OptimizationExportResult.Succeeded(Receipt(target)));
        Assert.IsTrue(await first);
        Assert.AreEqual(1, service.CallCount);
        Assert.AreSame(target, service.LastTarget);
        Assert.AreEqual(OptimizationExportStateKind.Succeeded, controller.State.Kind);
    }

    [TestMethod]
    public async Task RuntimeBundleDuplicateActivationRunsOneDistinctTargetExport()
    {
        (OptimizationExecutionPlan plan, OptimizationExecutionResult result) =
            GgufRuntimeProfileBundleExportServiceTests.RuntimeProfileResult();
        VerifiedGgufRuntimeBundleExportTarget target =
            VerifiedGgufRuntimeBundleExportTarget.FromExecution(plan, result);
        var service = new ControlledRuntimeBundleService();
        var controller = new OptimizationExportController(service);
        controller.Bind(target);

        Task<bool> first = controller.TryStartAsync();
        Assert.IsFalse(await controller.TryStartAsync());
        service.Complete(OptimizationExportResult.Succeeded(
            RuntimeReceipt(target)));

        Assert.IsTrue(await first);
        Assert.AreEqual(1, service.CallCount);
        Assert.AreSame(target, service.LastTarget);
        Assert.AreEqual(OptimizationExportStateKind.Succeeded,
            controller.State.Kind);
        Assert.IsNull(controller.State.Target);
        Assert.AreSame(target, controller.State.RuntimeBundleTarget);
        Assert.IsNotNull(controller.State.RuntimeBundleReceipt);
    }

    [TestMethod]
    public async Task RuntimeBundleReceiptFromForeignExecutionFailsClosed()
    {
        (OptimizationExecutionPlan plan, OptimizationExecutionResult result) =
            GgufRuntimeProfileBundleExportServiceTests.RuntimeProfileResult();
        (OptimizationExecutionPlan foreignPlan,
            OptimizationExecutionResult foreignResult) =
            GgufRuntimeProfileBundleExportServiceTests.RuntimeProfileResult();
        VerifiedGgufRuntimeBundleExportTarget target =
            VerifiedGgufRuntimeBundleExportTarget.FromExecution(plan, result);
        VerifiedGgufRuntimeBundleExportTarget foreign =
            VerifiedGgufRuntimeBundleExportTarget.FromExecution(
                foreignPlan, foreignResult);
        var controller = new OptimizationExportController(
            new ImmediateRuntimeBundleService(
                OptimizationExportResult.Succeeded(RuntimeReceipt(foreign))));
        controller.Bind(target);

        Assert.IsTrue(await controller.TryStartAsync());

        Assert.AreEqual(OptimizationExportStateKind.Failed,
            controller.State.Kind);
        Assert.AreEqual(OptimizationExportFailure.IntegrityMismatch,
            controller.State.Failure);
        Assert.IsNull(controller.State.Receipt);
        Assert.IsNull(controller.State.RuntimeBundleReceipt);
    }

    [TestMethod]
    public async Task RuntimeBundleCancellationCannotPublishLateSuccess()
    {
        (OptimizationExecutionPlan plan, OptimizationExecutionResult result) =
            GgufRuntimeProfileBundleExportServiceTests.RuntimeProfileResult();
        VerifiedGgufRuntimeBundleExportTarget target =
            VerifiedGgufRuntimeBundleExportTarget.FromExecution(plan, result);
        var service = new ControlledRuntimeBundleService();
        var controller = new OptimizationExportController(service);
        controller.Bind(target);
        Task<bool> operation = controller.TryStartAsync();
        await service.Started.Task.WaitAsync(TimeSpan.FromSeconds(1));

        Assert.IsTrue(controller.TryCancel());
        service.Complete(OptimizationExportResult.Succeeded(
            RuntimeReceipt(target)));

        Assert.IsTrue(await operation);
        Assert.AreEqual(OptimizationExportStateKind.Failed,
            controller.State.Kind);
        Assert.AreEqual(OptimizationExportFailure.CleanupFailure,
            controller.State.Failure);
        Assert.IsNull(controller.State.RuntimeBundleReceipt);
    }

    [TestMethod]
    public void CrossKindControllerBindingIsRejectedBeforeStart()
    {
        (OptimizationExecutionPlan plan, OptimizationExecutionResult result) =
            GgufRuntimeProfileBundleExportServiceTests.RuntimeProfileResult();
        VerifiedGgufRuntimeBundleExportTarget runtime =
            VerifiedGgufRuntimeBundleExportTarget.FromExecution(plan, result);
        var persistentController = new OptimizationExportController(
            new ImmediateService(
                OptimizationExportResult.Failed(
                    OptimizationExportFailure.PublicationFailure)));
        var runtimeController = new OptimizationExportController(
            new ImmediateRuntimeBundleService(
                OptimizationExportResult.Failed(
                    OptimizationExportFailure.PublicationFailure)));

        Assert.ThrowsExactly<InvalidOperationException>(() =>
            persistentController.Bind(runtime));
        Assert.ThrowsExactly<InvalidOperationException>(() =>
            runtimeController.Bind(Target()));
        Assert.AreEqual(OptimizationExportStateKind.Unbound,
            persistentController.State.Kind);
        Assert.AreEqual(OptimizationExportStateKind.Unbound,
            runtimeController.State.Kind);
    }

    [TestMethod]
    public async Task CrossKindSuccessReceiptIsAnIntegrityMismatch()
    {
        (OptimizationExecutionPlan plan, OptimizationExecutionResult result) =
            GgufRuntimeProfileBundleExportServiceTests.RuntimeProfileResult();
        VerifiedGgufRuntimeBundleExportTarget runtimeTarget =
            VerifiedGgufRuntimeBundleExportTarget.FromExecution(plan, result);
        var persistentController = new OptimizationExportController(
            new ImmediateService(OptimizationExportResult.Succeeded(
                RuntimeReceipt(runtimeTarget))));
        persistentController.Bind(Target());
        var runtimeController = new OptimizationExportController(
            new ImmediateRuntimeBundleService(
                OptimizationExportResult.Succeeded(Receipt(Target()))));
        runtimeController.Bind(runtimeTarget);

        Assert.IsTrue(await persistentController.TryStartAsync());
        Assert.IsTrue(await runtimeController.TryStartAsync());

        Assert.AreEqual(OptimizationExportFailure.IntegrityMismatch,
            persistentController.State.Failure);
        Assert.AreEqual(OptimizationExportFailure.IntegrityMismatch,
            runtimeController.State.Failure);
    }

    [TestMethod]
    public async Task WrongDigestFailsClosed()
    {
        var mismatch = new OptimizationExportController(new ImmediateService(
            OptimizationExportResult.Succeeded(new OptimizationExportReceipt(
                new VerifiedPersistentExportTarget(OptimizationRoute.Gguf, Guid.Parse("11111111-1111-1111-1111-111111111111"), Configuration, Source, true, "output-1",
                    "dddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddd", 4096), "published-1"))));
        mismatch.Bind(Target());
        Assert.IsTrue(await mismatch.TryStartAsync());
        Assert.AreEqual(OptimizationExportFailure.IntegrityMismatch, mismatch.State.Failure);

    }

    [TestMethod]
    public async Task TypedPublicationFailureRemainsAnOperationalFailure()
    {
        var controller = new OptimizationExportController(new ImmediateService(
            OptimizationExportResult.Failed(OptimizationExportFailure.PublicationFailure)));
        controller.Bind(Target());

        Assert.IsTrue(await controller.TryStartAsync());

        Assert.AreEqual(OptimizationExportStateKind.Failed, controller.State.Kind);
        Assert.AreEqual(OptimizationExportFailure.PublicationFailure, controller.State.Failure);
    }

    [TestMethod]
    public async Task NullProviderResultIsAnUnexpectedFaultNotPublicationFailure()
    {
        var controller = new OptimizationExportController(new NullResultService());
        controller.Bind(Target());

        await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => controller.TryStartAsync());

        Assert.AreEqual(OptimizationExportStateKind.Failed, controller.State.Kind);
        Assert.AreEqual(OptimizationExportFailure.None, controller.State.Failure);
    }

    [TestMethod]
    public async Task ProviderOwnedCancellationIsAnUnexpectedFault()
    {
        var controller = new OptimizationExportController(new ForeignCancellationService());
        controller.Bind(Target());

        await Assert.ThrowsExactlyAsync<OperationCanceledException>(() => controller.TryStartAsync());

        Assert.AreEqual(OptimizationExportStateKind.Failed, controller.State.Kind);
        Assert.AreEqual(OptimizationExportFailure.None, controller.State.Failure);
    }

    [TestMethod]
    public async Task UnexpectedProviderFaultPropagatesAndSettlesForRetry()
    {
        var service = new FaultThenSuccessService();
        var controller = new OptimizationExportController(service);
        VerifiedPersistentExportTarget target = Target();
        controller.Bind(target);

        InvalidOperationException fault = await Assert.ThrowsExactlyAsync<InvalidOperationException>(
            () => controller.TryStartAsync());

        Assert.AreEqual(FaultThenSuccessService.PrivateFault, fault.Message);
        Assert.AreEqual(OptimizationExportStateKind.Failed, controller.State.Kind);
        Assert.AreEqual(OptimizationExportFailure.None, controller.State.Failure);
        Assert.IsFalse(controller.State.StatusText.Contains("C:\\", StringComparison.Ordinal));
        Assert.IsTrue(await controller.TryRetryAsync());
        Assert.AreEqual(2, service.CallCount);
        Assert.AreEqual(OptimizationExportStateKind.Succeeded, controller.State.Kind);
    }

    [TestMethod]
    public async Task LateProgressFromFaultedGenerationCannotReplaceRetrySuccess()
    {
        var service = new FaultThenSuccessService();
        var controller = new OptimizationExportController(service);
        controller.Bind(Target());
        await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => controller.TryStartAsync());
        Assert.IsTrue(await controller.TryRetryAsync());

        service.ReportFirstGenerationProgress();

        Assert.AreEqual(OptimizationExportStateKind.Succeeded, controller.State.Kind);
    }

    [TestMethod]
    public async Task UnexpectedProviderFaultAllowsExplicitRebind()
    {
        var controller = new OptimizationExportController(new FaultThenSuccessService());
        controller.Bind(Target());
        await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => controller.TryStartAsync());

        controller.Bind(Target());

        Assert.AreEqual(OptimizationExportStateKind.Ready, controller.State.Kind);
        Assert.IsTrue(await controller.TryStartAsync());
        Assert.AreEqual(OptimizationExportStateKind.Succeeded, controller.State.Kind);
    }

    [TestMethod]
    public async Task RetirementDuringUnexpectedFaultIsBoundedAndLeavesUnboundState()
    {
        var service = new DeferredFaultService();
        var controller = new OptimizationExportController(service);
        controller.Bind(Target());
        Task<bool> operation = controller.TryStartAsync();
        await service.Started.WaitAsync(TimeSpan.FromSeconds(2));

        Task retirement = controller.RetireAsync();
        service.Fault();

        await retirement.WaitAsync(TimeSpan.FromSeconds(2));
        await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => operation);
        Assert.AreEqual(OptimizationExportStateKind.Unbound, controller.State.Kind);
        Assert.IsFalse(await controller.TryStartAsync());
    }

    [TestMethod]
    public async Task CancellationCannotPublishLateSuccessAndCleanupFailureWins()
    {
        var service = new ControlledExportService();
        var controller = new OptimizationExportController(service);
        controller.Bind(Target());
        Task<bool> operation = controller.TryStartAsync();
        await service.Started.Task.WaitAsync(TimeSpan.FromSeconds(1));
        Assert.IsTrue(controller.TryCancel());
        service.Complete(OptimizationExportResult.Failed(OptimizationExportFailure.CleanupFailure));
        Assert.IsTrue(await operation);
        Assert.AreEqual(OptimizationExportStateKind.Failed, controller.State.Kind);
        Assert.AreEqual(OptimizationExportFailure.CleanupFailure, controller.State.Failure);
    }

    [TestMethod]
    public async Task ObserverCancellationBeforeProviderPreventsProviderInvocation()
    {
        var service = new ControlledExportService();
        var controller = new OptimizationExportController(service);
        controller.Bind(Target());
        controller.StateChanged += (_, state) =>
        {
            if (state.Kind == OptimizationExportStateKind.Running) controller.TryCancel();
        };

        Assert.IsTrue(await controller.TryStartAsync().WaitAsync(TimeSpan.FromSeconds(2)));

        Assert.AreEqual(0, service.CallCount);
        Assert.AreEqual(OptimizationExportStateKind.Cancelled, controller.State.Kind);
    }

    [TestMethod]
    public async Task RetirementIsStableAndWaitsForNonCooperativeProvider()
    {
        var service = new ControlledExportService();
        var controller = new OptimizationExportController(service);
        controller.Bind(Target());
        Task<bool> operation = controller.TryStartAsync();
        await service.Started.Task.WaitAsync(TimeSpan.FromSeconds(1));
        Task retirement = controller.RetireAsync();
        Assert.AreSame(retirement, controller.RetireAsync());
        Assert.IsFalse(retirement.IsCompleted);
        service.Complete(OptimizationExportResult.Failed(OptimizationExportFailure.CleanupFailure));
        await retirement;
        Assert.IsTrue(await operation);
        Assert.AreEqual(OptimizationExportStateKind.Unbound, controller.State.Kind);
        Assert.IsFalse(await controller.TryStartAsync());
    }

    [TestMethod]
    public async Task RetirementBoundsAProviderWhoseCancellationCallbackNeverReturns()
    {
        using var release = new ManualResetEventSlim(false);
        var service = new BlockingCancellationExportService(release);
        var controller = new OptimizationExportController(service, TimeSpan.FromMilliseconds(50));
        controller.Bind(Target());
        Task<bool> operation = controller.TryStartAsync();
        await service.Started.Task.WaitAsync(TimeSpan.FromSeconds(2));

        try
        {
            Assert.IsTrue(controller.TryCancel());
            await service.CallbackStarted.Task.WaitAsync(TimeSpan.FromSeconds(1));
            service.Complete(OptimizationExportResult.Cancelled());
            await controller.RetireAsync().WaitAsync(TimeSpan.FromSeconds(1));
            Assert.AreEqual(OptimizationExportStateKind.Unbound, controller.State.Kind);
            Assert.AreEqual(1, service.CallbackCount);
        }
        finally { release.Set(); }
        Assert.IsTrue(await operation.WaitAsync(TimeSpan.FromSeconds(1)));
    }

    [TestMethod]
    public async Task UserCancellationTimesOutToCleanupFailureAndObservesDelayedCallbackFailure()
    {
        using var release = new ManualResetEventSlim(false);
        var service = new BlockingCancellationExportService(release, throwAfterRelease: true);
        var controller = new OptimizationExportController(service, TimeSpan.FromMilliseconds(50));
        var failed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        controller.StateChanged += (_, state) =>
        {
            if (state.Kind == OptimizationExportStateKind.Failed) failed.TrySetResult();
        };
        controller.Bind(Target());
        Task<bool> operation = controller.TryStartAsync();
        await service.Started.Task.WaitAsync(TimeSpan.FromSeconds(1));

        Assert.IsTrue(controller.TryCancel());
        await service.CallbackStarted.Task.WaitAsync(TimeSpan.FromSeconds(1));
        service.Complete(OptimizationExportResult.Cancelled());
        await failed.Task.WaitAsync(TimeSpan.FromSeconds(1));
        Assert.AreEqual(OptimizationExportFailure.CleanupFailure, controller.State.Failure);

        release.Set();
        Assert.IsTrue(await operation.WaitAsync(TimeSpan.FromSeconds(1)));
        Assert.AreEqual(OptimizationExportFailure.CleanupFailure, controller.State.Failure);
        Assert.AreEqual(1, service.CallbackCount);
    }

    [TestMethod]
    public async Task UserCancellationOfNeverSettlingProviderBecomesNonRetryableCleanupFailure()
    {
        using var release = new ManualResetEventSlim(false);
        var service = new BlockingCancellationExportService(release);
        var controller = new OptimizationExportController(service, TimeSpan.FromMilliseconds(50));
        var failed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        controller.StateChanged += (_, state) =>
        {
            if (state.Kind == OptimizationExportStateKind.Failed) failed.TrySetResult();
        };
        controller.Bind(Target());
        Task<bool> operation = controller.TryStartAsync();
        await service.Started.Task.WaitAsync(TimeSpan.FromSeconds(1));

        try
        {
            Assert.IsTrue(controller.TryCancel());
            await failed.Task.WaitAsync(TimeSpan.FromSeconds(1));
            Assert.AreEqual(OptimizationExportFailure.CleanupFailure, controller.State.Failure);
            Assert.IsFalse(await controller.TryRetryAsync());
            Assert.IsFalse(operation.IsCompleted);
        }
        finally { release.Set(); }
    }

    [TestMethod]
    public async Task PromptCancellationCallbackStillBoundsANonCooperativeProvider()
    {
        var service = new ControlledExportService();
        var controller = new OptimizationExportController(service, TimeSpan.FromMilliseconds(50));
        controller.Bind(Target());
        Task<bool> operation = controller.TryStartAsync();
        await service.Started.Task.WaitAsync(TimeSpan.FromSeconds(1));
        Task pending = WaitForCleanupFailureAsync(controller);

        Assert.IsTrue(controller.TryCancel());
        await pending.WaitAsync(TimeSpan.FromSeconds(1));

        Assert.IsFalse(operation.IsCompleted);
        Assert.IsTrue(controller.IsCleanupPending);
        Assert.IsFalse(await controller.TryRetryAsync());
        service.Complete(OptimizationExportResult.Cancelled());
        Assert.IsTrue(await operation.WaitAsync(TimeSpan.FromSeconds(1)));
        await controller.CleanupReconciliation.WaitAsync(TimeSpan.FromSeconds(1));
        Assert.AreEqual(OptimizationExportStateKind.Cancelled, controller.State.Kind);
    }

    [TestMethod]
    public async Task PromptCancellationAndPromptProviderFaultStillClearCleanupGate()
    {
        var service = new ControlledExportService();
        var controller = new OptimizationExportController(service, TimeSpan.FromSeconds(1));
        controller.Bind(Target());
        Task<bool> operation = controller.TryStartAsync();
        await service.Started.Task.WaitAsync(TimeSpan.FromSeconds(1));

        Assert.IsTrue(controller.TryCancel());
        service.Fault();
        await Assert.ThrowsExactlyAsync<InvalidOperationException>(async () => await operation);
        await controller.CleanupReconciliation.WaitAsync(TimeSpan.FromSeconds(1));

        Assert.IsFalse(controller.IsCleanupPending);
        Assert.AreEqual(OptimizationExportStateKind.Failed, controller.State.Kind);
        Assert.AreEqual(OptimizationExportFailure.None, controller.State.Failure);
        controller.Bind(Target());
        Assert.AreEqual(OptimizationExportStateKind.Ready, controller.State.Kind);
    }

    [TestMethod]
    public async Task DelayedSuccessfulCleanupReconcilesToRetryableCancellation()
    {
        using var release = new ManualResetEventSlim(false);
        var service = new BlockingCancellationExportService(release);
        var controller = new OptimizationExportController(service, TimeSpan.FromMilliseconds(50));
        var reconciled = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var pending = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        controller.StateChanged += (_, state) =>
        {
            if (state.Kind == OptimizationExportStateKind.Cancelled) reconciled.TrySetResult();
            if (state.Kind == OptimizationExportStateKind.Failed && state.Failure == OptimizationExportFailure.CleanupFailure) pending.TrySetResult();
        };
        controller.Bind(Target());
        Task<bool> operation = controller.TryStartAsync();
        await service.Started.Task.WaitAsync(TimeSpan.FromSeconds(1));
        Assert.IsTrue(controller.TryCancel());
        await service.CallbackStarted.Task.WaitAsync(TimeSpan.FromSeconds(1));
        service.Complete(OptimizationExportResult.Cancelled());
        await pending.Task.WaitAsync(TimeSpan.FromSeconds(1));
        Assert.IsFalse(await controller.TryRetryAsync());

        release.Set();
        await operation.WaitAsync(TimeSpan.FromSeconds(1));
        await reconciled.Task.WaitAsync(TimeSpan.FromSeconds(1));
        Assert.AreEqual(OptimizationExportStateKind.Cancelled, controller.State.Kind);
    }

    [TestMethod]
    public async Task DelayedCleanupPreservesUnexpectedProviderFaultAndClearsRetryGate()
    {
        using var release = new ManualResetEventSlim(false);
        var service = new BlockingCancellationExportService(release);
        var controller = new OptimizationExportController(service, TimeSpan.FromMilliseconds(50));
        controller.Bind(Target());
        Task<bool> operation = controller.TryStartAsync();
        await service.Started.Task.WaitAsync(TimeSpan.FromSeconds(1));
        Task pending = WaitForCleanupFailureAsync(controller);
        Assert.IsTrue(controller.TryCancel());
        await service.CallbackStarted.Task.WaitAsync(TimeSpan.FromSeconds(1));
        await pending.WaitAsync(TimeSpan.FromSeconds(1));

        service.Fault();
        await Assert.ThrowsExactlyAsync<InvalidOperationException>(async () => await operation);
        release.Set();
        await controller.CleanupReconciliation.WaitAsync(TimeSpan.FromSeconds(1));

        Assert.AreEqual(OptimizationExportStateKind.Failed, controller.State.Kind);
        Assert.AreEqual(OptimizationExportFailure.None, controller.State.Failure);
        Assert.IsFalse(controller.IsCleanupPending);
        controller.Bind(Target());
        Assert.AreEqual(OptimizationExportStateKind.Ready, controller.State.Kind);
    }

    [TestMethod]
    public async Task DelayedCleanupPreservesLateSuccessAsCleanupFailure()
    {
        using var release = new ManualResetEventSlim(false);
        var service = new BlockingCancellationExportService(release);
        var controller = new OptimizationExportController(service, TimeSpan.FromMilliseconds(50));
        VerifiedPersistentExportTarget target = Target();
        controller.Bind(target);
        Task<bool> operation = controller.TryStartAsync();
        await service.Started.Task.WaitAsync(TimeSpan.FromSeconds(1));
        Task pending = WaitForCleanupFailureAsync(controller);
        Assert.IsTrue(controller.TryCancel());
        await service.CallbackStarted.Task.WaitAsync(TimeSpan.FromSeconds(1));
        await pending.WaitAsync(TimeSpan.FromSeconds(1));

        service.Complete(OptimizationExportResult.Succeeded(Receipt(target)));
        Assert.IsTrue(await operation.WaitAsync(TimeSpan.FromSeconds(1)));
        release.Set();
        await controller.CleanupReconciliation.WaitAsync(TimeSpan.FromSeconds(1));

        Assert.AreEqual(OptimizationExportStateKind.Failed, controller.State.Kind);
        Assert.AreEqual(OptimizationExportFailure.CleanupFailure, controller.State.Failure);
    }

    [TestMethod]
    public async Task DelayedCleanupPreservesTypedProviderFailure()
    {
        using var release = new ManualResetEventSlim(false);
        var service = new BlockingCancellationExportService(release);
        var controller = new OptimizationExportController(service, TimeSpan.FromMilliseconds(50));
        controller.Bind(Target());
        Task<bool> operation = controller.TryStartAsync();
        await service.Started.Task.WaitAsync(TimeSpan.FromSeconds(1));
        Task pending = WaitForCleanupFailureAsync(controller);
        Assert.IsTrue(controller.TryCancel());
        await service.CallbackStarted.Task.WaitAsync(TimeSpan.FromSeconds(1));
        await pending.WaitAsync(TimeSpan.FromSeconds(1));

        service.Complete(OptimizationExportResult.Failed(OptimizationExportFailure.DestinationUnavailable));
        Assert.IsTrue(await operation.WaitAsync(TimeSpan.FromSeconds(1)));
        release.Set();
        await controller.CleanupReconciliation.WaitAsync(TimeSpan.FromSeconds(1));

        Assert.AreEqual(OptimizationExportStateKind.Failed, controller.State.Kind);
        Assert.AreEqual(OptimizationExportFailure.DestinationUnavailable, controller.State.Failure);
    }

    [TestMethod]
    public async Task ObserverFailureDoesNotBlockOtherObserversOrCompletion()
    {
        var controller = new OptimizationExportController(new ImmediateService(
            OptimizationExportResult.Succeeded(Receipt(Target()))));
        int observed = 0;
        controller.StateChanged += (_, _) => throw new InvalidOperationException("observer");
        controller.StateChanged += (_, _) => observed++;
        controller.Bind(Target());
        Assert.IsTrue(await controller.TryStartAsync());
        Assert.IsTrue(observed >= 2);
    }

    private static VerifiedPersistentExportTarget Target() => new(
        OptimizationRoute.Gguf, Guid.Parse("11111111-1111-1111-1111-111111111111"), Configuration, Source, true, "output-1", Manifest, 4096);

    private static OptimizationExportReceipt Receipt(VerifiedPersistentExportTarget target) => new(target, "published-1");

    private static GgufRuntimeBundleExportReceipt RuntimeReceipt(
        VerifiedGgufRuntimeBundleExportTarget target) => new(
            target,
            Manifest,
            512,
            target.SourceLengthBytes + 1024);

    private static Task WaitForCleanupFailureAsync(OptimizationExportController controller)
    {
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        EventHandler<OptimizationExportViewState>? handler = null;
        handler = (_, state) =>
        {
            if (state.Kind != OptimizationExportStateKind.Failed
                || state.Failure != OptimizationExportFailure.CleanupFailure) return;
            controller.StateChanged -= handler;
            completion.TrySetResult();
        };
        controller.StateChanged += handler;
        handler!(controller, controller.State);
        return completion.Task;
    }

    private sealed class ControlledExportService : IOptimizationExportService
    {
        private readonly TaskCompletionSource<OptimizationExportResult> _completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal int CallCount { get; private set; }
        internal VerifiedPersistentExportTarget? LastTarget { get; private set; }
        public Task<OptimizationExportResult> ExportAsync(VerifiedPersistentExportTarget target, IProgress<OptimizationExportProgress> progress, CancellationToken cancellationToken)
        { CallCount++; LastTarget = target; Started.TrySetResult(); return _completion.Task; }
        internal void Complete(OptimizationExportResult result) => _completion.SetResult(result);
        internal void Fault() => _completion.SetException(new InvalidOperationException(FaultThenSuccessService.PrivateFault));
    }

    private sealed class ControlledRuntimeBundleService
        : IGgufRuntimeBundleExportService
    {
        private readonly TaskCompletionSource<OptimizationExportResult>
            _completion = new(TaskCreationOptions.RunContinuationsAsynchronously);

        internal int CallCount { get; private set; }
        internal TaskCompletionSource Started { get; } = new(
            TaskCreationOptions.RunContinuationsAsynchronously);
        internal VerifiedGgufRuntimeBundleExportTarget? LastTarget {
            get; private set;
        }

        public Task<OptimizationExportResult> ExportAsync(
            VerifiedGgufRuntimeBundleExportTarget target,
            IProgress<OptimizationExportProgress> progress,
            CancellationToken cancellationToken)
        {
            CallCount++;
            LastTarget = target;
            Started.TrySetResult();
            return _completion.Task;
        }

        internal void Complete(OptimizationExportResult result) =>
            _completion.TrySetResult(result);
    }

    private sealed class ImmediateRuntimeBundleService(
        OptimizationExportResult result) : IGgufRuntimeBundleExportService
    {
        public Task<OptimizationExportResult> ExportAsync(
            VerifiedGgufRuntimeBundleExportTarget target,
            IProgress<OptimizationExportProgress> progress,
            CancellationToken cancellationToken) => Task.FromResult(result);
    }

    private sealed class BlockingCancellationExportService(ManualResetEventSlim release, bool throwAfterRelease = false) : IOptimizationExportService
    {
        internal TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal TaskCompletionSource CallbackStarted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource<OptimizationExportResult> _completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal int CallbackCount { get; private set; }

        public Task<OptimizationExportResult> ExportAsync(VerifiedPersistentExportTarget target, IProgress<OptimizationExportProgress> progress, CancellationToken cancellationToken)
        {
            cancellationToken.Register(() =>
            {
                CallbackCount++;
                CallbackStarted.TrySetResult();
                release.Wait();
                if (throwAfterRelease) throw new InvalidOperationException("delayed callback failure");
            });
            Started.TrySetResult();
            return _completion.Task;
        }

        internal void Complete(OptimizationExportResult result) => _completion.TrySetResult(result);
        internal void Fault() => _completion.TrySetException(new InvalidOperationException(FaultThenSuccessService.PrivateFault));
    }

    private sealed class ImmediateService(OptimizationExportResult result) : IOptimizationExportService
    {
        public Task<OptimizationExportResult> ExportAsync(VerifiedPersistentExportTarget target, IProgress<OptimizationExportProgress> progress, CancellationToken cancellationToken) => Task.FromResult(result);
    }

    private sealed class FaultThenSuccessService : IOptimizationExportService
    {
        internal const string PrivateFault = @"C:\Users\person\output.gguf?token=secret";
        private IProgress<OptimizationExportProgress>? _firstProgress;
        internal int CallCount { get; private set; }

        public Task<OptimizationExportResult> ExportAsync(VerifiedPersistentExportTarget target, IProgress<OptimizationExportProgress> progress, CancellationToken cancellationToken)
        {
            CallCount++;
            if (CallCount == 1)
            {
                _firstProgress = progress;
                throw new InvalidOperationException(PrivateFault);
            }
            return Task.FromResult(OptimizationExportResult.Succeeded(Receipt(target)));
        }

        internal void ReportFirstGenerationProgress() =>
            _firstProgress!.Report(new OptimizationExportProgress(OptimizationExportStage.Writing, 0.25));
    }

    private sealed class NullResultService : IOptimizationExportService
    {
        public Task<OptimizationExportResult> ExportAsync(VerifiedPersistentExportTarget target, IProgress<OptimizationExportProgress> progress, CancellationToken cancellationToken) =>
            Task.FromResult<OptimizationExportResult>(null!);
    }

    private sealed class ForeignCancellationService : IOptimizationExportService
    {
        public Task<OptimizationExportResult> ExportAsync(VerifiedPersistentExportTarget target, IProgress<OptimizationExportProgress> progress, CancellationToken cancellationToken)
        {
            using var foreign = new CancellationTokenSource();
            foreign.Cancel();
            throw new OperationCanceledException(foreign.Token);
        }
    }

    private sealed class DeferredFaultService : IOptimizationExportService
    {
        private readonly TaskCompletionSource _started = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource<OptimizationExportResult> _completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal Task Started => _started.Task;

        public Task<OptimizationExportResult> ExportAsync(VerifiedPersistentExportTarget target, IProgress<OptimizationExportProgress> progress, CancellationToken cancellationToken)
        {
            _started.TrySetResult();
            return _completion.Task;
        }

        internal void Fault() => _completion.TrySetException(new InvalidOperationException(FaultThenSuccessService.PrivateFault));
    }
}
