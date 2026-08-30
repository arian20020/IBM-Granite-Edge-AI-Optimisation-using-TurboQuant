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
    public async Task WrongDigestFailsClosedAndProviderDetailsStayBounded()
    {
        var mismatch = new OptimizationExportController(new ImmediateService(
            OptimizationExportResult.Succeeded(new OptimizationExportReceipt(
                new VerifiedPersistentExportTarget(OptimizationRoute.Gguf, Guid.Parse("11111111-1111-1111-1111-111111111111"), Configuration, Source, true, "output-1",
                    "dddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddddd", 4096), "published-1"))));
        mismatch.Bind(Target());
        Assert.IsTrue(await mismatch.TryStartAsync());
        Assert.AreEqual(OptimizationExportFailure.IntegrityMismatch, mismatch.State.Failure);

        var throwing = new OptimizationExportController(new ThrowingService());
        throwing.Bind(Target());
        Assert.IsTrue(await throwing.TryStartAsync());
        Assert.AreEqual(OptimizationExportFailure.PublicationFailure, throwing.State.Failure);
        Assert.IsFalse(throwing.State.StatusText.Contains("C:\\", StringComparison.Ordinal));
    }

    [TestMethod]
    public async Task CancellationCannotPublishLateSuccessAndCleanupFailureWins()
    {
        var service = new ControlledExportService();
        var controller = new OptimizationExportController(service);
        controller.Bind(Target());
        Task<bool> operation = controller.TryStartAsync();
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

    private sealed class ControlledExportService : IOptimizationExportService
    {
        private readonly TaskCompletionSource<OptimizationExportResult> _completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal int CallCount { get; private set; }
        internal VerifiedPersistentExportTarget? LastTarget { get; private set; }
        public Task<OptimizationExportResult> ExportAsync(VerifiedPersistentExportTarget target, IProgress<OptimizationExportProgress> progress, CancellationToken cancellationToken)
        { CallCount++; LastTarget = target; return _completion.Task; }
        internal void Complete(OptimizationExportResult result) => _completion.SetResult(result);
    }

    private sealed class ImmediateService(OptimizationExportResult result) : IOptimizationExportService
    {
        public Task<OptimizationExportResult> ExportAsync(VerifiedPersistentExportTarget target, IProgress<OptimizationExportProgress> progress, CancellationToken cancellationToken) => Task.FromResult(result);
    }

    private sealed class ThrowingService : IOptimizationExportService
    {
        public Task<OptimizationExportResult> ExportAsync(VerifiedPersistentExportTarget target, IProgress<OptimizationExportProgress> progress, CancellationToken cancellationToken) =>
            throw new InvalidOperationException(@"C:\Users\person\output.gguf?token=secret");
    }
}
