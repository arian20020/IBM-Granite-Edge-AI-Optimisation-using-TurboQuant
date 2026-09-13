using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using GraniteEdgeAI.Features.ModelImport.Selection;
using Microsoft.UI.Xaml;
using Windows.ApplicationModel.DataTransfer;
using Windows.Storage;

namespace GraniteEdgeAI.Features.ModelImport.DragDropRoute;

/// <summary>
/// Performs the bounded StorageItems extraction for one Explorer drop.
/// Page state and selection classification intentionally remain outside this route.
/// </summary>
internal sealed class ModelImportDropHandler
{
    private readonly ModelSelectionInputNormalizer _normalizer;
    private readonly IModelImportDropCallbackDispatcher _callbackDispatcher;

    internal ModelImportDropHandler(
        ModelSelectionInputNormalizer normalizer,
        IModelImportDropCallbackDispatcher? callbackDispatcher = null)
    {
        _normalizer = normalizer;
        _callbackDispatcher = callbackDispatcher ??
            SynchronizationContextModelImportDropCallbackDispatcher.CaptureCurrent();
    }

    /// <summary>
    /// Provides only the synchronous DragOver hint. It never reads dropped items.
    /// </summary>
    internal static ModelImportDropPrecheck Precheck(bool hasStorageItems)
    {
        return hasStorageItems
            ? new ModelImportDropPrecheck(true, ModelImportDropOperation.Copy)
            : new ModelImportDropPrecheck(false, ModelImportDropOperation.None);
    }

    internal void HandleDragOver(DragEventArgs args)
    {
        ArgumentNullException.ThrowIfNull(args);

        ModelImportDropPrecheck precheck = Precheck(
            args.DataView.Contains(StandardDataFormats.StorageItems));
        args.AcceptedOperation = ToNativeOperation(precheck.AcceptedOperation);
    }

    internal Task HandleDropAsync(
        DragEventArgs args,
        Func<ModelSelectionInput, Task> accept,
        Func<ModelSelectionDiagnostic, Task> reject,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(args);

        return HandleDropAsync(
            new WinUiModelImportDropRequest(args, cancellationToken),
            accept,
            reject,
            cancellationToken);
    }

    internal async Task HandleDropAsync(
        IModelImportDropRequest request,
        Func<ModelSelectionInput, Task> accept,
        Func<ModelSelectionDiagnostic, Task> reject,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(accept);
        ArgumentNullException.ThrowIfNull(reject);

        IModelImportDropDeferral deferral = request.GetDeferral();
        try
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!request.HasStorageItems)
            {
                await DispatchTerminalCallbackAsync(
                    () => reject(NoStorageItemsDiagnostic()),
                    cancellationToken);
                return;
            }

            IReadOnlyList<ModelImportDroppedItem> items =
                await request.GetStorageItemsAsync();
            cancellationToken.ThrowIfCancellationRequested();

            if (items.Count == 0)
            {
                await DispatchTerminalCallbackAsync(
                    () => reject(NoStorageItemsDiagnostic()),
                    cancellationToken);
                return;
            }

            if (items.Count != 1)
            {
                await DispatchTerminalCallbackAsync(
                    () => reject(MultipleItemsDiagnostic()),
                    cancellationToken);
                return;
            }

            ModelImportDroppedItem item = items[0];
            if (!item.IsSupported)
            {
                await DispatchTerminalCallbackAsync(
                    () => reject(UnsupportedItemDiagnostic()),
                    cancellationToken);
                return;
            }

            ModelSelectionInput input = _normalizer.FromPickerPath(
                item.LocalPath,
                item.IsFolder);
            cancellationToken.ThrowIfCancellationRequested();

            await DispatchTerminalCallbackAsync(() =>
            {
                request.SetAcceptedOperation(ModelImportDropOperation.Copy);
                return accept(input);
            }, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // A cancelled operation is non-terminal for the UI; the caller owns replacement state.
        }
        finally
        {
            deferral.Complete();
        }
    }

    private Task DispatchTerminalCallbackAsync(
        Func<Task> callback,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        return _callbackDispatcher.InvokeAsync(() =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            return callback();
        });
    }

    private static DataPackageOperation ToNativeOperation(ModelImportDropOperation operation)
    {
        return operation == ModelImportDropOperation.Copy
            ? DataPackageOperation.Copy
            : DataPackageOperation.None;
    }

    private static ModelSelectionDiagnostic NoStorageItemsDiagnostic() =>
        new("selection-no-storage-items", "Drop one model file or model folder to continue.");

    private static ModelSelectionDiagnostic MultipleItemsDiagnostic() =>
        new("selection-multiple-items", "Drop only one model file or model folder at a time.");

    private static ModelSelectionDiagnostic UnsupportedItemDiagnostic() =>
        new("selection-unsupported-file", "Drop a model file or model folder to continue.");

    private sealed class WinUiModelImportDropRequest : IModelImportDropRequest
    {
        private readonly DragEventArgs _args;
        private readonly CancellationToken _cancellationToken;

        internal WinUiModelImportDropRequest(DragEventArgs args, CancellationToken cancellationToken)
        {
            _args = args;
            _cancellationToken = cancellationToken;
        }

        public bool HasStorageItems => _args.DataView.Contains(StandardDataFormats.StorageItems);

        public IModelImportDropDeferral GetDeferral() =>
            new WinUiModelImportDropDeferral(_args.GetDeferral());

        public async Task<IReadOnlyList<ModelImportDroppedItem>> GetStorageItemsAsync()
        {
            IReadOnlyList<IStorageItem> storageItems = await _args.DataView
                .GetStorageItemsAsync()
                .AsTask(_cancellationToken);

            return storageItems.Select(ModelImportDroppedItem.FromStorageItem).ToArray();
        }

        public void SetAcceptedOperation(ModelImportDropOperation operation)
        {
            _args.AcceptedOperation = ToNativeOperation(operation);
        }
    }

    private sealed class WinUiModelImportDropDeferral : IModelImportDropDeferral
    {
        private readonly DragOperationDeferral _deferral;

        internal WinUiModelImportDropDeferral(DragOperationDeferral deferral)
        {
            _deferral = deferral;
        }

        public void Complete() => _deferral.Complete();
    }
}

internal enum ModelImportDropOperation
{
    None,
    Copy,
}

internal readonly record struct ModelImportDropPrecheck(
    bool IsPotentiallyValid,
    ModelImportDropOperation AcceptedOperation);

internal interface IModelImportDropRequest
{
    bool HasStorageItems { get; }

    IModelImportDropDeferral GetDeferral();

    Task<IReadOnlyList<ModelImportDroppedItem>> GetStorageItemsAsync();

    void SetAcceptedOperation(ModelImportDropOperation operation);
}

internal interface IModelImportDropDeferral
{
    void Complete();
}

internal interface IModelImportDropCallbackDispatcher
{
    Task InvokeAsync(Func<Task> callback);
}

internal sealed class SynchronizationContextModelImportDropCallbackDispatcher :
    IModelImportDropCallbackDispatcher
{
    private readonly SynchronizationContext? _context;

    private SynchronizationContextModelImportDropCallbackDispatcher(SynchronizationContext? context)
    {
        _context = context;
    }

    internal static SynchronizationContextModelImportDropCallbackDispatcher CaptureCurrent() =>
        new(SynchronizationContext.Current);

    public Task InvokeAsync(Func<Task> callback)
    {
        ArgumentNullException.ThrowIfNull(callback);

        if (_context is null || ReferenceEquals(SynchronizationContext.Current, _context))
        {
            return callback();
        }

        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        _context.Post(async _ =>
        {
            try
            {
                await callback();
                completion.SetResult();
            }
            catch (Exception exception)
            {
                completion.SetException(exception);
            }
        }, null);
        return completion.Task;
    }
}

internal sealed class ModelImportDroppedItem
{
    private ModelImportDroppedItem(string localPath, bool isFolder, bool isSupported)
    {
        LocalPath = localPath;
        IsFolder = isFolder;
        IsSupported = isSupported;
    }

    internal string LocalPath { get; }

    internal bool IsFolder { get; }

    internal bool IsSupported { get; }

    internal static ModelImportDroppedItem File(string localPath) =>
        new(localPath, isFolder: false, isSupported: true);

    internal static ModelImportDroppedItem Folder(string localPath) =>
        new(localPath, isFolder: true, isSupported: true);

    internal static ModelImportDroppedItem Unsupported(string safeName) =>
        new(safeName, isFolder: false, isSupported: false);

    internal static ModelImportDroppedItem FromStorageItem(IStorageItem item)
    {
        ArgumentNullException.ThrowIfNull(item);

        return item switch
        {
            StorageFile file => File(file.Path),
            StorageFolder folder => Folder(folder.Path),
            _ => Unsupported(item.Name),
        };
    }
}
