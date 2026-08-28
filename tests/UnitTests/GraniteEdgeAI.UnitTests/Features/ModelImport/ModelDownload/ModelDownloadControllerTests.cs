using GraniteEdgeAI.Features.ModelImport.ModelDownload;
using GraniteEdgeAI.Features.ModelImport.Selection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace GraniteEdgeAI.UnitTests.Features.ModelImport.ModelDownload;

[TestClass]
public sealed class ModelDownloadControllerTests
{
    private const string Digest =
        "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";

    [TestMethod]
    public void CompletedDownloadRejectsUnverifiedIdentity()
    {
        ModelSelectionInput selection = new(
            @"C:\private\granite.gguf", "granite.gguf", isFolder: false);

        Assert.ThrowsExactly<ArgumentException>(() => new CompletedModelDownload(
            selection, "not-a-digest", 1024, "publication-1"));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => new CompletedModelDownload(
            selection, Digest, 0, "publication-1"));
        Assert.ThrowsExactly<ArgumentException>(() => new CompletedModelDownload(
            selection, Digest, 1024, @"C:\private\publication"));
        Assert.ThrowsExactly<ArgumentException>(() => new CompletedModelDownload(
            new ModelSelectionInput(
                @"C:\private\granite.gguf",
                @"C:\Users\person\granite.gguf",
                isFolder: false),
            Digest,
            1024,
            "publication-1"));
    }

    [TestMethod]
    public void OfferRequiresEveryApprovedDisplayFieldToBePrivacySafe()
    {
        Assert.ThrowsExactly<ArgumentException>(() => new RecommendedModelOffer(
            "granite-offer-1",
            "Granite recommended model",
            "1.94 GB download",
            "Description containing https://provider/model?token=secret",
            "GGUF",
            "Q4_K_M",
            "2.8 GB estimated memory",
            "128K tokens"));
    }

    [TestMethod]
    public void ProgressRejectsUnboundedFractions()
    {
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() =>
            new ModelDownloadProgress(ModelDownloadStage.Transferring, -0.01));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() =>
            new ModelDownloadProgress(ModelDownloadStage.Transferring, 1.01));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() =>
            new ModelDownloadProgress(ModelDownloadStage.Transferring, double.NaN));
    }

    [TestMethod]
    public async Task DuplicateActivationRunsOneOperationAndPublishesVerifiedCompletion()
    {
        var service = new ControlledDownloadService();
        var controller = new ModelDownloadController(service);
        RecommendedModelOffer offer = Offer();

        Task<bool> first = controller.TryStartAsync(offer, preferenceValue: 50);
        Task<bool> duplicate = controller.TryStartAsync(offer, preferenceValue: 50);

        Assert.IsFalse(await duplicate);
        Assert.AreEqual(ModelDownloadStateKind.Running, controller.State.Kind);
        service.Complete(ModelDownloadResult.Succeeded(Completion()));

        Assert.IsTrue(await first);
        Assert.AreEqual(ModelDownloadStateKind.Succeeded, controller.State.Kind);
        Assert.AreSame(service.Requests[0], controller.State.Request);
        Assert.AreEqual(Digest, controller.State.CompletedDownload!.Sha256);
        Assert.AreEqual(1, service.Requests.Count);
    }

    [TestMethod]
    public async Task CancellationCannotBeOverwrittenByLateSuccess()
    {
        var service = new ControlledDownloadService(ignoreCancellation: true);
        var controller = new ModelDownloadController(service);
        Task<bool> operation = controller.TryStartAsync(Offer(), 50);

        Assert.IsTrue(controller.TryCancel());
        Assert.AreEqual(ModelDownloadStateKind.Cancelling, controller.State.Kind);
        service.Complete(ModelDownloadResult.Succeeded(Completion()));

        Assert.IsTrue(await operation);
        Assert.AreEqual(ModelDownloadStateKind.Cancelled, controller.State.Kind);
        Assert.IsNull(controller.State.CompletedDownload);
    }

    [TestMethod]
    public async Task RetryUsesNewOperationIdentityAfterBoundedFailure()
    {
        var service = new SequencedDownloadService(
            ModelDownloadResult.Failed(ModelDownloadFailure.IntegrityMismatch),
            ModelDownloadResult.Succeeded(Completion()));
        var controller = new ModelDownloadController(service);

        Assert.IsTrue(await controller.TryStartAsync(Offer(), 40));
        Assert.AreEqual(ModelDownloadStateKind.Failed, controller.State.Kind);
        Assert.AreEqual(
            ModelDownloadFailure.IntegrityMismatch,
            controller.State.Failure);

        Assert.IsTrue(await controller.TryRetryAsync());
        Assert.AreEqual(ModelDownloadStateKind.Succeeded, controller.State.Kind);
        Assert.AreEqual(2, service.Requests.Count);
        Assert.AreNotEqual(
            service.Requests[0].OperationId,
            service.Requests[1].OperationId);
        Assert.AreEqual(service.Requests[0].OfferId, service.Requests[1].OfferId);
        Assert.AreEqual(service.Requests[0].PreferenceValue, service.Requests[1].PreferenceValue);
    }

    [TestMethod]
    public async Task ServiceExceptionBecomesPrivacySafePublicationFailure()
    {
        var controller = new ModelDownloadController(
            new ThrowingDownloadService(
                @"provider failed at C:\Users\person\model?token=secret"));

        Assert.IsTrue(await controller.TryStartAsync(Offer(), 60));

        Assert.AreEqual(ModelDownloadStateKind.Failed, controller.State.Kind);
        Assert.AreEqual(ModelDownloadFailure.PublicationFailure, controller.State.Failure);
        Assert.IsFalse(controller.State.StatusText.Contains("C:\\", StringComparison.Ordinal));
        Assert.IsFalse(controller.State.StatusText.Contains("token", StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(controller.State.StatusText.Contains("provider", StringComparison.OrdinalIgnoreCase));
    }

    [TestMethod]
    public async Task NullServiceResultFailsClosedAndDoesNotRemainActive()
    {
        var controller = new ModelDownloadController(new NullDownloadService());

        Assert.IsTrue(await controller.TryStartAsync(Offer(), 50));

        Assert.AreEqual(ModelDownloadStateKind.Failed, controller.State.Kind);
        Assert.AreEqual(ModelDownloadFailure.PublicationFailure, controller.State.Failure);
        Assert.IsTrue(await controller.TryRetryAsync());
    }

    [TestMethod]
    public async Task CancellationDoesNotMaskCleanupFailure()
    {
        var service = new ControlledDownloadService(ignoreCancellation: true);
        var controller = new ModelDownloadController(service);
        Task<bool> operation = controller.TryStartAsync(Offer(), 50);

        Assert.IsTrue(controller.TryCancel());
        service.Complete(ModelDownloadResult.Failed(ModelDownloadFailure.CleanupFailure));

        Assert.IsTrue(await operation);
        Assert.AreEqual(ModelDownloadStateKind.Failed, controller.State.Kind);
        Assert.AreEqual(ModelDownloadFailure.CleanupFailure, controller.State.Failure);
    }

    [TestMethod]
    public async Task RetirementWaitsForNonCooperativeDownloadAndRejectsLateSuccess()
    {
        var service = new ControlledDownloadService(ignoreCancellation: true);
        var controller = new ModelDownloadController(service);
        Task<bool> operation = controller.TryStartAsync(Offer(), 50);
        Task retirement = controller.RetireAsync();
        Assert.IsFalse(retirement.IsCompleted,
            "Retirement must observe a non-cooperative active download.");
        service.Complete(ModelDownloadResult.Succeeded(Completion()));

        await retirement;
        Assert.IsTrue(await operation);
        Assert.AreEqual(ModelDownloadStateKind.Unavailable, controller.State.Kind);
        Assert.IsNull(controller.State.CompletedDownload);
        Assert.IsFalse(await controller.TryStartAsync(Offer(), 50));
    }

    [TestMethod]
    public async Task ThrowingCancellationCallbackCannotBypassDownloadRetirement()
    {
        var service = new ThrowingCancellationDownloadService();
        var controller = new ModelDownloadController(service);
        Task<bool> operation = controller.TryStartAsync(Offer(), 50);

        Task retirement = controller.RetireAsync();
        Assert.IsFalse(retirement.IsCompleted);
        service.Complete(ModelDownloadResult.Failed(
            ModelDownloadFailure.CleanupFailure));

        await retirement;
        Assert.IsTrue(await operation);
        Assert.AreEqual(ModelDownloadStateKind.Unavailable, controller.State.Kind);
    }

    [TestMethod]
    public async Task ThrowingCancellationCallbackMapsToDownloadCleanupFailure()
    {
        var service = new ThrowingCancellationDownloadService();
        var controller = new ModelDownloadController(service);
        Task<bool> operation = controller.TryStartAsync(Offer(), 50);

        Assert.IsTrue(controller.TryCancel());
        service.Complete(ModelDownloadResult.Succeeded(Completion()));

        Assert.IsTrue(await operation);
        Assert.AreEqual(ModelDownloadStateKind.Failed, controller.State.Kind);
        Assert.AreEqual(ModelDownloadFailure.CleanupFailure, controller.State.Failure);
    }

    [TestMethod]
    public async Task EveryServiceFailureRemainsBoundedAndRetryable()
    {
        ModelDownloadFailure[] failures =
        [
            ModelDownloadFailure.IntegrityMismatch,
            ModelDownloadFailure.InsufficientSpace,
            ModelDownloadFailure.PublicationFailure,
            ModelDownloadFailure.CleanupFailure,
        ];

        foreach (ModelDownloadFailure failure in failures)
        {
            var controller = new ModelDownloadController(
                new SequencedDownloadService(ModelDownloadResult.Failed(failure)));

            Assert.IsTrue(await controller.TryStartAsync(Offer(), 50));
            Assert.AreEqual(ModelDownloadStateKind.Failed, controller.State.Kind);
            Assert.AreEqual(failure, controller.State.Failure);
            Assert.IsFalse(string.IsNullOrWhiteSpace(controller.State.StatusText));
            Assert.IsFalse(controller.State.StatusText.Contains("C:\\", StringComparison.Ordinal));
        }
    }

    private static RecommendedModelOffer Offer() => new(
        "granite-offer-1",
        "Granite recommended model",
        "1.94 GB download",
        "Verified instruction model",
        "GGUF",
        "Q4_K_M",
        "2.8 GB estimated memory",
        "128K tokens");

    private static CompletedModelDownload Completion() => new(
        new ModelSelectionInput(
            @"C:\private\granite.gguf", "granite.gguf", isFolder: false),
        Digest,
        1024,
        "publication-1");

    private sealed class ControlledDownloadService(bool ignoreCancellation = false)
        : IRecommendedModelDownloadService
    {
        private readonly TaskCompletionSource<ModelDownloadResult> _completion =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        internal List<RecommendedModelDownloadRequest> Requests { get; } = [];

        public async Task<ModelDownloadResult> DownloadAsync(
            RecommendedModelDownloadRequest request,
            IProgress<ModelDownloadProgress> progress,
            CancellationToken cancellationToken)
        {
            Requests.Add(request);
            progress.Report(new(
                ModelDownloadStage.Transferring,
                fraction: 0.25));
            if (!ignoreCancellation)
            {
                return await _completion.Task.WaitAsync(cancellationToken);
            }
            return await _completion.Task;
        }

        internal void Complete(ModelDownloadResult result) =>
            _completion.SetResult(result);
    }

    private sealed class SequencedDownloadService(params ModelDownloadResult[] results)
        : IRecommendedModelDownloadService
    {
        private readonly Queue<ModelDownloadResult> _results = new(results);

        internal List<RecommendedModelDownloadRequest> Requests { get; } = [];

        public Task<ModelDownloadResult> DownloadAsync(
            RecommendedModelDownloadRequest request,
            IProgress<ModelDownloadProgress> progress,
            CancellationToken cancellationToken)
        {
            Requests.Add(request);
            return Task.FromResult(_results.Dequeue());
        }
    }

    private sealed class ThrowingDownloadService(string message)
        : IRecommendedModelDownloadService
    {
        public Task<ModelDownloadResult> DownloadAsync(
            RecommendedModelDownloadRequest request,
            IProgress<ModelDownloadProgress> progress,
            CancellationToken cancellationToken) =>
            throw new InvalidOperationException(message);
    }

    private sealed class NullDownloadService : IRecommendedModelDownloadService
    {
        public Task<ModelDownloadResult> DownloadAsync(
            RecommendedModelDownloadRequest request,
            IProgress<ModelDownloadProgress> progress,
            CancellationToken cancellationToken) =>
            Task.FromResult<ModelDownloadResult>(null!);
    }

    private sealed class ThrowingCancellationDownloadService
        : IRecommendedModelDownloadService
    {
        private readonly TaskCompletionSource<ModelDownloadResult> _completion =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public async Task<ModelDownloadResult> DownloadAsync(
            RecommendedModelDownloadRequest request,
            IProgress<ModelDownloadProgress> progress,
            CancellationToken cancellationToken)
        {
            using CancellationTokenRegistration registration =
                cancellationToken.Register(
                    () => throw new InvalidOperationException(
                        "Synthetic cancellation callback failure."));
            return await _completion.Task;
        }

        internal void Complete(ModelDownloadResult result) =>
            _completion.SetResult(result);
    }
}
