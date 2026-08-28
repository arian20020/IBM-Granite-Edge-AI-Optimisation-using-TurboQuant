using GraniteEdgeAI.Features.ModelOptimization.Export;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Optimization;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace GraniteEdgeAI.UnitTests.Features.ModelOptimization;

[TestClass]
public sealed class OptimizationExportControllerTests
{
    private const string Configuration =
        "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
    private const string Manifest =
        "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb";

    [TestMethod]
    public void TargetRejectsRuntimeOnlyMissingAndMismatchedIdentity()
    {
        Assert.ThrowsExactly<ArgumentException>(() => new VerifiedPersistentExportTarget(
            OptimizationRoute.Gguf,
            Guid.Empty,
            Configuration,
            "output-1",
            Manifest,
            4096));
        Assert.ThrowsExactly<ArgumentException>(() => new VerifiedPersistentExportTarget(
            OptimizationRoute.Gguf,
            Guid.NewGuid(),
            "invalid",
            "output-1",
            Manifest,
            4096));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new VerifiedPersistentExportTarget(
            OptimizationRoute.Gguf,
            Guid.NewGuid(),
            Configuration,
            "output-1",
            Manifest,
            0));
    }

    [TestMethod]
    public void ProgressRejectsUnboundedFractions()
    {
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() =>
            new OptimizationExportProgress(OptimizationExportStage.Writing, -0.01));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() =>
            new OptimizationExportProgress(OptimizationExportStage.Writing, 1.01));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() =>
            new OptimizationExportProgress(OptimizationExportStage.Writing, double.NaN));
    }

    [TestMethod]
    public async Task DuplicateActivationRunsOneExactTargetExport()
    {
        var service = new ControlledExportService();
        var controller = new OptimizationExportController(service);
        VerifiedPersistentExportTarget target = Target();
        controller.Bind(target);

        Task<bool> first = controller.TryStartAsync();
        Task<bool> duplicate = controller.TryStartAsync();

        Assert.IsFalse(await duplicate);
        Assert.AreEqual(OptimizationExportStateKind.Running, controller.State.Kind);
        service.Complete(OptimizationExportResult.Succeeded(
            new OptimizationExportReceipt(Manifest, 4096)));

        Assert.IsTrue(await first);
        Assert.AreEqual(OptimizationExportStateKind.Succeeded, controller.State.Kind);
        Assert.AreSame(target, service.Targets[0]);
        Assert.AreEqual(1, service.Targets.Count);
    }

    [TestMethod]
    public async Task SuccessWithWrongDigestFailsClosedAsIntegrityMismatch()
    {
        var controller = new OptimizationExportController(
            new ImmediateExportService(OptimizationExportResult.Succeeded(
                new OptimizationExportReceipt(
                    "cccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccc",
                    4096))));
        controller.Bind(Target());

        Assert.IsTrue(await controller.TryStartAsync());

        Assert.AreEqual(OptimizationExportStateKind.Failed, controller.State.Kind);
        Assert.AreEqual(
            OptimizationExportFailure.IntegrityMismatch,
            controller.State.Failure);
    }

    [TestMethod]
    public async Task CancellationCannotPublishLateSuccessAndRetryIsAllowed()
    {
        var service = new ControlledExportService(ignoreCancellation: true);
        var controller = new OptimizationExportController(service);
        controller.Bind(Target());
        Task<bool> operation = controller.TryStartAsync();

        Assert.IsTrue(controller.TryCancel());
        service.Complete(OptimizationExportResult.Succeeded(
            new OptimizationExportReceipt(Manifest, 4096)));

        Assert.IsTrue(await operation);
        Assert.AreEqual(OptimizationExportStateKind.Cancelled, controller.State.Kind);
        Assert.IsNotNull(controller.State.Target);
    }

    [TestMethod]
    public async Task ProviderExceptionMapsToBoundedPublicationFailure()
    {
        var controller = new OptimizationExportController(
            new ThrowingExportService(
                @"failed at C:\Users\person\output.gguf?token=secret"));
        controller.Bind(Target());

        Assert.IsTrue(await controller.TryStartAsync());

        Assert.AreEqual(OptimizationExportStateKind.Failed, controller.State.Kind);
        Assert.AreEqual(
            OptimizationExportFailure.PublicationFailure,
            controller.State.Failure);
        Assert.IsFalse(controller.State.StatusText.Contains("C:\\", StringComparison.Ordinal));
        Assert.IsFalse(controller.State.StatusText.Contains("token", StringComparison.OrdinalIgnoreCase));
    }

    [TestMethod]
    public async Task NullServiceResultFailsClosedAndDoesNotRemainActive()
    {
        var controller = new OptimizationExportController(new NullExportService());
        controller.Bind(Target());

        Assert.IsTrue(await controller.TryStartAsync());

        Assert.AreEqual(OptimizationExportStateKind.Failed, controller.State.Kind);
        Assert.AreEqual(OptimizationExportFailure.PublicationFailure, controller.State.Failure);
        Assert.IsTrue(await controller.TryRetryAsync());
    }

    [TestMethod]
    public async Task CancellationDoesNotMaskCleanupFailure()
    {
        var service = new ControlledExportService(ignoreCancellation: true);
        var controller = new OptimizationExportController(service);
        controller.Bind(Target());
        Task<bool> operation = controller.TryStartAsync();

        Assert.IsTrue(controller.TryCancel());
        service.Complete(OptimizationExportResult.Failed(
            OptimizationExportFailure.CleanupFailure));

        Assert.IsTrue(await operation);
        Assert.AreEqual(OptimizationExportStateKind.Failed, controller.State.Kind);
        Assert.AreEqual(OptimizationExportFailure.CleanupFailure, controller.State.Failure);
    }

    [TestMethod]
    public async Task RetirementWaitsForNonCooperativeExportAndLeavesNoReusableTarget()
    {
        var service = new ControlledExportService(ignoreCancellation: true);
        var controller = new OptimizationExportController(service);
        controller.Bind(Target());
        Task<bool> operation = controller.TryStartAsync();
        Task retirement = controller.RetireAsync();
        Assert.IsFalse(retirement.IsCompleted,
            "Retirement must observe a non-cooperative active export.");
        service.Complete(OptimizationExportResult.Failed(
            OptimizationExportFailure.CleanupFailure));

        await retirement;
        Assert.IsTrue(await operation);
        Assert.AreEqual(OptimizationExportStateKind.Unbound, controller.State.Kind);
        Assert.IsNull(controller.State.Target);
        Assert.IsFalse(await controller.TryStartAsync());
    }

    [TestMethod]
    public async Task ThrowingCancellationCallbackCannotBypassExportRetirement()
    {
        var service = new ThrowingCancellationExportService(
            completeDuringCancellation: false);
        var controller = new OptimizationExportController(service);
        controller.Bind(Target());
        Task<bool> operation = controller.TryStartAsync();

        Task retirement = controller.RetireAsync();
        Assert.IsFalse(retirement.IsCompleted);
        service.Complete(OptimizationExportResult.Failed(
            OptimizationExportFailure.CleanupFailure));

        await retirement;
        Assert.IsTrue(await operation);
        Assert.AreEqual(OptimizationExportStateKind.Unbound, controller.State.Kind);
    }

    [TestMethod]
    public async Task ThrowingCancellationCallbackMapsToExportCleanupFailure()
    {
        var service = new ThrowingCancellationExportService(
            completeDuringCancellation: true);
        var controller = new OptimizationExportController(service);
        controller.Bind(Target());
        Task<bool> operation = controller.TryStartAsync();

        Assert.IsTrue(controller.TryCancel());

        Assert.IsTrue(await operation);
        Assert.AreEqual(OptimizationExportStateKind.Failed, controller.State.Kind);
        Assert.AreEqual(
            OptimizationExportFailure.CleanupFailure,
            controller.State.Failure);
    }

    [TestMethod]
    public async Task EveryServiceFailureRemainsBoundedAndRetryable()
    {
        OptimizationExportFailure[] failures =
        [
            OptimizationExportFailure.IntegrityMismatch,
            OptimizationExportFailure.InsufficientSpace,
            OptimizationExportFailure.DestinationUnavailable,
            OptimizationExportFailure.PublicationFailure,
            OptimizationExportFailure.CleanupFailure,
        ];

        foreach (OptimizationExportFailure failure in failures)
        {
            var controller = new OptimizationExportController(
                new ImmediateExportService(OptimizationExportResult.Failed(failure)));
            controller.Bind(Target());

            Assert.IsTrue(await controller.TryStartAsync());
            Assert.AreEqual(OptimizationExportStateKind.Failed, controller.State.Kind);
            Assert.AreEqual(failure, controller.State.Failure);
            Assert.IsFalse(string.IsNullOrWhiteSpace(controller.State.StatusText));
            Assert.IsFalse(controller.State.StatusText.Contains("C:\\", StringComparison.Ordinal));
        }
    }

    [TestMethod]
    public async Task RetryKeepsExactTargetAfterFailure()
    {
        var service = new SequencedExportService(
            OptimizationExportResult.Failed(OptimizationExportFailure.InsufficientSpace),
            OptimizationExportResult.Succeeded(new OptimizationExportReceipt(Manifest, 4096)));
        var controller = new OptimizationExportController(service);
        VerifiedPersistentExportTarget target = Target();
        controller.Bind(target);

        Assert.IsTrue(await controller.TryStartAsync());
        Assert.IsTrue(await controller.TryRetryAsync());

        Assert.AreEqual(OptimizationExportStateKind.Succeeded, controller.State.Kind);
        Assert.AreEqual(2, service.Targets.Count);
        Assert.AreSame(target, service.Targets[0]);
        Assert.AreSame(target, service.Targets[1]);
    }

    private static VerifiedPersistentExportTarget Target() => new(
        OptimizationRoute.Gguf,
        Guid.Parse("11111111-1111-1111-1111-111111111111"),
        Configuration,
        "output-1",
        Manifest,
        4096);

    private sealed class ControlledExportService(bool ignoreCancellation = false)
        : IOptimizationExportService
    {
        private readonly TaskCompletionSource<OptimizationExportResult> _completion =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        internal List<VerifiedPersistentExportTarget> Targets { get; } = [];

        public async Task<OptimizationExportResult> ExportAsync(
            VerifiedPersistentExportTarget target,
            IProgress<OptimizationExportProgress> progress,
            CancellationToken cancellationToken)
        {
            Targets.Add(target);
            progress.Report(new(OptimizationExportStage.Writing, 0.5));
            return ignoreCancellation
                ? await _completion.Task
                : await _completion.Task.WaitAsync(cancellationToken);
        }

        internal void Complete(OptimizationExportResult result) =>
            _completion.SetResult(result);
    }

    private sealed class ImmediateExportService(OptimizationExportResult result)
        : IOptimizationExportService
    {
        public Task<OptimizationExportResult> ExportAsync(
            VerifiedPersistentExportTarget target,
            IProgress<OptimizationExportProgress> progress,
            CancellationToken cancellationToken) => Task.FromResult(result);
    }

    private sealed class SequencedExportService(params OptimizationExportResult[] results)
        : IOptimizationExportService
    {
        private readonly Queue<OptimizationExportResult> _results = new(results);

        internal List<VerifiedPersistentExportTarget> Targets { get; } = [];

        public Task<OptimizationExportResult> ExportAsync(
            VerifiedPersistentExportTarget target,
            IProgress<OptimizationExportProgress> progress,
            CancellationToken cancellationToken)
        {
            Targets.Add(target);
            return Task.FromResult(_results.Dequeue());
        }
    }

    private sealed class ThrowingExportService(string message)
        : IOptimizationExportService
    {
        public Task<OptimizationExportResult> ExportAsync(
            VerifiedPersistentExportTarget target,
            IProgress<OptimizationExportProgress> progress,
            CancellationToken cancellationToken) =>
            throw new InvalidOperationException(message);
    }

    private sealed class NullExportService : IOptimizationExportService
    {
        public Task<OptimizationExportResult> ExportAsync(
            VerifiedPersistentExportTarget target,
            IProgress<OptimizationExportProgress> progress,
            CancellationToken cancellationToken) =>
            Task.FromResult<OptimizationExportResult>(null!);
    }

    private sealed class ThrowingCancellationExportService(
        bool completeDuringCancellation)
        : IOptimizationExportService
    {
        private readonly TaskCompletionSource<OptimizationExportResult> _completion =
            new();

        public async Task<OptimizationExportResult> ExportAsync(
            VerifiedPersistentExportTarget target,
            IProgress<OptimizationExportProgress> progress,
            CancellationToken cancellationToken)
        {
            using CancellationTokenRegistration registration =
                cancellationToken.Register(
                    () =>
                    {
                        if (completeDuringCancellation)
                        {
                            _completion.TrySetResult(
                                OptimizationExportResult.Succeeded(
                                    new OptimizationExportReceipt(
                                        Manifest,
                                        4096)));
                        }
                        throw new InvalidOperationException(
                            "Synthetic cancellation callback failure.");
                    });
            return await _completion.Task;
        }

        internal void Complete(OptimizationExportResult result) =>
            _completion.SetResult(result);
    }
}
