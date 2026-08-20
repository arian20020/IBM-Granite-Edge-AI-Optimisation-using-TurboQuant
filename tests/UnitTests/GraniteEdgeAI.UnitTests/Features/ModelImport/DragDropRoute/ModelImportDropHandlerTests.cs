using GraniteEdgeAI.Features.ModelImport.DragDropRoute;
using GraniteEdgeAI.Features.ModelImport.Selection;
using Microsoft.VisualStudio.TestTools.UnitTesting.AppContainer;

namespace GraniteEdgeAI.UnitTests;

[TestClass]
public sealed class ModelImportDropHandlerTests
{
    [TestMethod]
    public async Task HandleDropAsync_OneFile_ForwardsNormalizedInputAsCopyAndCompletesDeferral()
    {
        var request = new FakeDropRequest(
            hasStorageItems: true,
            [ModelImportDroppedItem.File(@"C:\Models\granite.gguf")]);
        ModelSelectionInput? accepted = null;
        var handler = new ModelImportDropHandler(new ModelSelectionInputNormalizer());

        await handler.HandleDropAsync(
            request,
            input =>
            {
                accepted = input;
                return Task.CompletedTask;
            },
            _ => Task.CompletedTask,
            CancellationToken.None);

        Assert.AreEqual(ModelImportDropOperation.Copy, request.AcceptedOperation);
        Assert.AreEqual(1, request.Deferral.CompleteCount);
        Assert.IsNotNull(accepted);
        Assert.AreEqual("granite.gguf", accepted.DisplayName);
        Assert.IsFalse(accepted.IsFolder);
    }

    [TestMethod]
    public async Task HandleDropAsync_OneFolder_ForwardsNormalizedFolderInput()
    {
        var request = new FakeDropRequest(
            hasStorageItems: true,
            [ModelImportDroppedItem.Folder(@"C:\Models\OpenVino")]);
        ModelSelectionInput? accepted = null;
        var handler = new ModelImportDropHandler(new ModelSelectionInputNormalizer());

        await handler.HandleDropAsync(request, input =>
        {
            accepted = input;
            return Task.CompletedTask;
        }, _ => Task.CompletedTask, CancellationToken.None);

        Assert.IsNotNull(accepted);
        Assert.AreEqual("OpenVino", accepted.DisplayName);
        Assert.IsTrue(accepted.IsFolder);
        Assert.AreEqual(ModelImportDropOperation.Copy, request.AcceptedOperation);
    }

    [TestMethod]
    public async Task HandleDropAsync_WithoutStorageFormat_RejectsWithoutAcceptance()
    {
        var request = new FakeDropRequest(hasStorageItems: false, []);
        ModelSelectionDiagnostic? diagnostic = null;
        var handler = new ModelImportDropHandler(new ModelSelectionInputNormalizer());

        await handler.HandleDropAsync(request, _ => Task.CompletedTask, value =>
        {
            diagnostic = value;
            return Task.CompletedTask;
        }, CancellationToken.None);

        Assert.AreEqual(ModelImportDropOperation.None, request.AcceptedOperation);
        Assert.AreEqual(1, request.Deferral.CompleteCount);
        Assert.IsNotNull(diagnostic);
        Assert.AreEqual("selection-no-storage-items", diagnostic.Code);
    }

    [TestMethod]
    public async Task HandleDropAsync_EmptyStorageItems_RejectsWithoutAcceptance()
    {
        var request = new FakeDropRequest(hasStorageItems: true, []);
        ModelSelectionDiagnostic? diagnostic = null;
        var handler = new ModelImportDropHandler(new ModelSelectionInputNormalizer());

        await handler.HandleDropAsync(request, _ => Task.CompletedTask, value =>
        {
            diagnostic = value;
            return Task.CompletedTask;
        }, CancellationToken.None);

        Assert.AreEqual(ModelImportDropOperation.None, request.AcceptedOperation);
        Assert.AreEqual(1, request.Deferral.CompleteCount);
        Assert.IsNotNull(diagnostic);
        Assert.AreEqual("selection-no-storage-items", diagnostic.Code);
    }

    [TestMethod]
    public async Task HandleDropAsync_MultipleOrMixedItems_RejectsWithoutAcceptance()
    {
        var request = new FakeDropRequest(
            hasStorageItems: true,
            [
                ModelImportDroppedItem.File(@"C:\Models\one.gguf"),
                ModelImportDroppedItem.Unsupported("archive.zip"),
            ]);
        ModelSelectionDiagnostic? diagnostic = null;
        var handler = new ModelImportDropHandler(new ModelSelectionInputNormalizer());

        await handler.HandleDropAsync(request, _ => Task.CompletedTask, value =>
        {
            diagnostic = value;
            return Task.CompletedTask;
        }, CancellationToken.None);

        Assert.AreEqual(ModelImportDropOperation.None, request.AcceptedOperation);
        Assert.IsNotNull(diagnostic);
        Assert.AreEqual("selection-multiple-items", diagnostic.Code);
    }

    [TestMethod]
    public async Task HandleDropAsync_UnsupportedItem_RejectsWithoutAcceptance()
    {
        var request = new FakeDropRequest(true, [ModelImportDroppedItem.Unsupported("archive.zip")]);
        ModelSelectionDiagnostic? diagnostic = null;
        var handler = new ModelImportDropHandler(new ModelSelectionInputNormalizer());

        await handler.HandleDropAsync(request, _ => Task.CompletedTask, value =>
        {
            diagnostic = value;
            return Task.CompletedTask;
        }, CancellationToken.None);

        Assert.AreEqual(ModelImportDropOperation.None, request.AcceptedOperation);
        Assert.IsNotNull(diagnostic);
        Assert.AreEqual("selection-unsupported-file", diagnostic.Code);
    }

    [TestMethod]
    public async Task HandleDropAsync_AcceptException_CompletesDeferralExactlyOnce()
    {
        var request = new FakeDropRequest(true, [ModelImportDroppedItem.File(@"C:\Models\granite.gguf")]);
        var handler = new ModelImportDropHandler(new ModelSelectionInputNormalizer());

        await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => handler.HandleDropAsync(
            request,
            _ => throw new InvalidOperationException("accept failed"),
            _ => Task.CompletedTask,
            CancellationToken.None));

        Assert.AreEqual(1, request.Deferral.CompleteCount);
    }

    [TestMethod]
    public async Task HandleDropAsync_ItemRetrievalException_CompletesDeferralWithoutDispatch()
    {
        var request = new FakeDropRequest(
            hasStorageItems: true,
            [],
            getItemsException: new InvalidOperationException("storage unavailable"));
        var handler = new ModelImportDropHandler(new ModelSelectionInputNormalizer());
        var accepted = false;
        var rejected = false;

        await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => handler.HandleDropAsync(
            request,
            _ =>
            {
                accepted = true;
                return Task.CompletedTask;
            },
            _ =>
            {
                rejected = true;
                return Task.CompletedTask;
            },
            CancellationToken.None));

        Assert.AreEqual(1, request.Deferral.CompleteCount);
        Assert.IsFalse(accepted);
        Assert.IsFalse(rejected);
    }

    [TestMethod]
    public async Task HandleDropAsync_CancellationDuringItemRetrieval_CompletesDeferralAndDoesNotDispatch()
    {
        using var cancellation = new CancellationTokenSource();
        var request = new FakeDropRequest(
            hasStorageItems: true,
            [ModelImportDroppedItem.File(@"C:\Models\granite.gguf")],
            onGetItems: cancellation.Cancel);
        var handler = new ModelImportDropHandler(new ModelSelectionInputNormalizer());
        var accepted = false;
        var rejected = false;

        await handler.HandleDropAsync(request, _ =>
        {
            accepted = true;
            return Task.CompletedTask;
        }, _ =>
        {
            rejected = true;
            return Task.CompletedTask;
        }, cancellation.Token);

        Assert.AreEqual(1, request.Deferral.CompleteCount);
        Assert.IsFalse(accepted);
        Assert.IsFalse(rejected);
        Assert.AreEqual(ModelImportDropOperation.None, request.AcceptedOperation);
    }

    [TestMethod]
    public async Task HandleDropAsync_PreCancelledRequest_CompletesDeferralWithoutTerminalCallback()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var request = new FakeDropRequest(hasStorageItems: false, []);
        var handler = new ModelImportDropHandler(new ModelSelectionInputNormalizer());
        var terminalCallbackCount = 0;

        await handler.HandleDropAsync(request, _ =>
        {
            terminalCallbackCount++;
            return Task.CompletedTask;
        }, _ =>
        {
            terminalCallbackCount++;
            return Task.CompletedTask;
        }, cancellation.Token);

        Assert.AreEqual(1, request.Deferral.CompleteCount);
        Assert.AreEqual(0, terminalCallbackCount);
        Assert.AreEqual(ModelImportDropOperation.None, request.AcceptedOperation);
    }

    [TestMethod]
    public async Task HandleDropAsync_WorkerCompletion_DispatchesTerminalWorkThroughCapturedDispatcher()
    {
        var request = new FakeDropRequest(
            hasStorageItems: true,
            [ModelImportDroppedItem.File(@"C:\Models\granite.gguf")],
            completeItemsOnWorker: true);
        var dispatcher = new RecordingDispatcher();
        var handler = new ModelImportDropHandler(new ModelSelectionInputNormalizer(), dispatcher);
        var callbackRanThroughDispatcher = false;

        await handler.HandleDropAsync(request, _ =>
        {
            callbackRanThroughDispatcher = dispatcher.IsDispatching;
            return Task.CompletedTask;
        }, _ => Task.CompletedTask, CancellationToken.None);

        Assert.IsTrue(request.CompletedItemsOnWorker);
        Assert.AreEqual(1, dispatcher.InvocationCount);
        Assert.IsTrue(callbackRanThroughDispatcher);
        Assert.AreEqual(ModelImportDropOperation.Copy, request.AcceptedOperation);
    }

    [TestMethod]
    public void Precheck_HasStorageItems_OffersCopyOnlyWithoutRetrievingItems()
    {
        ModelImportDropPrecheck precheck = ModelImportDropHandler.Precheck(hasStorageItems: true);

        Assert.IsTrue(precheck.IsPotentiallyValid);
        Assert.AreEqual(ModelImportDropOperation.Copy, precheck.AcceptedOperation);
    }

    private sealed class FakeDropRequest : IModelImportDropRequest
    {
        private readonly IReadOnlyList<ModelImportDroppedItem> _items;
        private readonly Action? _onGetItems;
        private readonly Exception? _getItemsException;
        private readonly bool _completeItemsOnWorker;

        internal FakeDropRequest(
            bool hasStorageItems,
            IReadOnlyList<ModelImportDroppedItem> items,
            Action? onGetItems = null,
            Exception? getItemsException = null,
            bool completeItemsOnWorker = false)
        {
            HasStorageItems = hasStorageItems;
            _items = items;
            _onGetItems = onGetItems;
            _getItemsException = getItemsException;
            _completeItemsOnWorker = completeItemsOnWorker;
        }

        internal FakeDeferral Deferral { get; } = new();

        internal ModelImportDropOperation AcceptedOperation { get; private set; }

        internal bool CompletedItemsOnWorker { get; private set; }

        public bool HasStorageItems { get; }

        public IModelImportDropDeferral GetDeferral() => Deferral;

        public Task<IReadOnlyList<ModelImportDroppedItem>> GetStorageItemsAsync()
        {
            _onGetItems?.Invoke();
            if (_getItemsException is not null)
            {
                return Task.FromException<IReadOnlyList<ModelImportDroppedItem>>(_getItemsException);
            }

            if (!_completeItemsOnWorker)
            {
                return Task.FromResult(_items);
            }

            return Task.Run<IReadOnlyList<ModelImportDroppedItem>>(() =>
            {
                CompletedItemsOnWorker = true;
                return _items;
            });
        }

        public void SetAcceptedOperation(ModelImportDropOperation operation) => AcceptedOperation = operation;
    }

    private sealed class FakeDeferral : IModelImportDropDeferral
    {
        internal int CompleteCount { get; private set; }

        public void Complete() => CompleteCount++;
    }

    private sealed class RecordingDispatcher : IModelImportDropCallbackDispatcher
    {
        internal int InvocationCount { get; private set; }

        internal bool IsDispatching { get; private set; }

        public async Task InvokeAsync(Func<Task> callback)
        {
            InvocationCount++;
            IsDispatching = true;
            try
            {
                await callback();
            }
            finally
            {
                IsDispatching = false;
            }
        }
    }
}
