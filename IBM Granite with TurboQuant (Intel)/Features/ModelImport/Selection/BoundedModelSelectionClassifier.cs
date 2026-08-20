using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace GraniteEdgeAI.Features.ModelImport.Selection;

internal sealed class BoundedModelSelectionClassifier : IModelSelectionClassifier
{
    private const string AccessMessage = "This model could not be opened. Check that it is available on this computer, then try again.";
    private const string UnsupportedFileMessage = "This model format is not supported here. Choose a GGUF file or a supported model folder.";
    private const string UnsupportedFolderMessage = "This folder does not contain a supported model at its top level. Choose another folder.";
    private readonly Func<string, FileAttributes> attributesReader;
    private readonly Func<string, bool> directoryExists;
    private readonly Action? beforeContinuityCheck;
    private readonly Func<bool>? timeoutReached;
    private readonly Action? afterMetadataRead;
    private readonly TimeSpan maximumElapsed;

    internal BoundedModelSelectionClassifier(
        Func<string, FileAttributes>? attributesReader = null,
        Action? beforeContinuityCheck = null,
        Func<bool>? timeoutReached = null,
        Action? afterMetadataRead = null,
        Func<string, bool>? directoryExists = null,
        TimeSpan? maximumElapsed = null)
    {
        this.attributesReader = attributesReader ?? File.GetAttributes;
        this.directoryExists = directoryExists ?? Directory.Exists;
        this.beforeContinuityCheck = beforeContinuityCheck;
        this.timeoutReached = timeoutReached;
        this.afterMetadataRead = afterMetadataRead;
        this.maximumElapsed = maximumElapsed ?? ModelSelectionLimits.MaximumElapsed;
    }

    public async Task<ModelSelectionResult> ClassifyAsync(
        ModelSelectionOperationId operationId,
        ModelSelectionInput input,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(input);
        cancellationToken.ThrowIfCancellationRequested();

        using var timeout = new CancellationTokenSource(maximumElapsed);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeout.Token);
        CancellationToken token = linked.Token;
        var stopwatch = Stopwatch.StartNew();
        Task<ModelSelectionResult> worker = Task.Run(
            () => ClassifyCoreAsync(operationId, input, cancellationToken, token, stopwatch),
            CancellationToken.None);

        try
        {
            return await worker.WaitAsync(maximumElapsed, cancellationToken).ConfigureAwait(false);
        }
        catch (TimeoutException)
        {
            linked.Cancel();
            ObserveLateFailure(worker);
            return Failure(operationId, input.DisplayName, "selection-timeout", "This model took too long to check safely. Choose a more specific item.");
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            await AwaitCooperativeCancellationOrObserveLateFailureAsync(worker).ConfigureAwait(false);
            throw new OperationCanceledException(cancellationToken);
        }
    }

    private async Task<ModelSelectionResult> ClassifyCoreAsync(
        ModelSelectionOperationId operationId,
        ModelSelectionInput input,
        CancellationToken callerToken,
        CancellationToken token,
        Stopwatch stopwatch)
    {
        try
        {
            return input.IsFolder
                ? await ClassifyFolderAsync(operationId, input, token, stopwatch).ConfigureAwait(false)
                : ClassifyFile(operationId, input, token, stopwatch);
        }
        catch (OperationCanceledException) when (callerToken.IsCancellationRequested)
        {
            throw;
        }
        catch (OperationCanceledException)
        {
            return Failure(operationId, input.DisplayName, "selection-timeout", "This model took too long to check safely. Choose a more specific item.");
        }
        catch (DirectoryTooLargeException)
        {
            return Failure(operationId, input.DisplayName, "selection-enumeration-limit", "This folder has too many items to check safely. Choose a more specific folder.");
        }
        catch (CandidateChangedException)
        {
            return Failure(operationId, input.DisplayName, "model-selection-changed", "The selected model changed after validation. Choose the model again.");
        }
        catch (UnsafeCandidateException unsafeCandidate)
        {
            return ModelSelectionResult.Failure(operationId, input.DisplayName, unsafeCandidate.Diagnostic);
        }
        catch (UnauthorizedAccessException)
        {
            return Failure(operationId, input.DisplayName, "selection-access-denied", AccessMessage);
        }
        catch (IOException)
        {
            return Failure(operationId, input.DisplayName, "selection-access-denied", AccessMessage);
        }
    }

    private ModelSelectionResult ClassifyFile(
        ModelSelectionOperationId operationId,
        ModelSelectionInput input,
        CancellationToken token,
        Stopwatch stopwatch)
    {
        token.ThrowIfCancellationRequested();
        ThrowIfTimedOut(stopwatch, token);
        ModelSelectionDiagnostic? unsafeDiagnostic = GetUnsafeLocationDiagnostic(input.LocalPath, isFolder: false);
        if (unsafeDiagnostic is not null)
        {
            return ModelSelectionResult.Failure(operationId, input.DisplayName, unsafeDiagnostic);
        }

        if (!File.Exists(input.LocalPath))
        {
            return Failure(operationId, input.DisplayName, "selection-access-denied", AccessMessage);
        }

        return string.Equals(Path.GetExtension(input.LocalPath), ".gguf", StringComparison.OrdinalIgnoreCase)
            ? ModelSelectionResult.Accepted(operationId, ModelSelectionRoute.Gguf, input.DisplayName)
            : Failure(operationId, input.DisplayName, "selection-unsupported-file", UnsupportedFileMessage);
    }

    private async Task<ModelSelectionResult> ClassifyFolderAsync(
        ModelSelectionOperationId operationId,
        ModelSelectionInput input,
        CancellationToken token,
        Stopwatch stopwatch)
    {
        ModelSelectionDiagnostic? unsafeDiagnostic = GetUnsafeLocationDiagnostic(input.LocalPath, isFolder: true);
        if (unsafeDiagnostic is not null)
        {
            return ModelSelectionResult.Failure(operationId, input.DisplayName, unsafeDiagnostic);
        }

        if (!directoryExists(input.LocalPath))
        {
            return Failure(operationId, input.DisplayName, "selection-access-denied", AccessMessage);
        }

        string[] children = EnumerateDirectChildren(input.LocalPath, token, stopwatch);
        DirectorySnapshot snapshot = DirectorySnapshot.Capture(input.LocalPath, children, attributesReader, directoryExists, token, stopwatch, timeoutReached, maximumElapsed);
        ThrowIfTimedOut(stopwatch, token);

        var xmlStems = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var binStems = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        string? configPath = null;
        string? indexPath = null;
        var safetensorsPaths = new List<string>();

        foreach (string child in children)
        {
            token.ThrowIfCancellationRequested();
            ThrowIfTimedOut(stopwatch, token);
            string name = Path.GetFileName(child);
            if (IsRelevantName(name) && !names.Add(name) && names.Count > ModelSelectionLimits.MaximumRetainedNames)
            {
                return Failure(operationId, input.DisplayName, "selection-enumeration-limit", "This folder has too many model entries to check safely. Choose a more specific folder.");
            }

            if (IsRelevantName(name) && names.Count > ModelSelectionLimits.MaximumRetainedNames)
            {
                return Failure(operationId, input.DisplayName, "selection-enumeration-limit", "This folder has too many model entries to check safely. Choose a more specific folder.");
            }

            if (directoryExists(child))
            {
                continue;
            }

            ModelSelectionDiagnostic? childUnsafe = GetUnsafeLocationDiagnostic(child, isFolder: false);
            if (childUnsafe is not null)
            {
                return ModelSelectionResult.Failure(operationId, input.DisplayName, childUnsafe);
            }

            string extension = Path.GetExtension(name);
            if (string.Equals(extension, ".xml", StringComparison.OrdinalIgnoreCase)) xmlStems.Add(Path.GetFileNameWithoutExtension(name));
            if (string.Equals(extension, ".bin", StringComparison.OrdinalIgnoreCase)) binStems.Add(Path.GetFileNameWithoutExtension(name));
            if (string.Equals(name, "config.json", StringComparison.OrdinalIgnoreCase)) configPath = child;
            if (string.Equals(name, "model.safetensors.index.json", StringComparison.OrdinalIgnoreCase)) indexPath = child;
            if (string.Equals(extension, ".safetensors", StringComparison.OrdinalIgnoreCase)) safetensorsPaths.Add(child);
        }

        var pairs = xmlStems.Intersect(binStems, StringComparer.OrdinalIgnoreCase).ToArray();
        if (pairs.Length > 1)
        {
            return Failure(operationId, input.DisplayName, "selection-ambiguous-folder", "This folder contains more than one possible model. Choose the folder for one model only.");
        }

        if (pairs.Length == 1 && pairs.Length == xmlStems.Count && pairs.Length == binStems.Count)
        {
            EnsureUnchanged(snapshot, input.LocalPath, token, stopwatch);
            return ModelSelectionResult.Accepted(operationId, ModelSelectionRoute.OpenVinoDirectory, input.DisplayName);
        }

        if (xmlStems.Count != 0 || binStems.Count != 0)
        {
            return Failure(operationId, input.DisplayName, "selection-incomplete-openvino", "This OpenVINO folder is incomplete. Restore its matching XML and BIN files, then try again.");
        }

        if (configPath is null && indexPath is null && safetensorsPaths.Count == 0)
        {
            return Failure(operationId, input.DisplayName, "selection-unsupported-folder", UnsupportedFolderMessage);
        }

        return await ClassifySourceFolderAsync(operationId, input, configPath, indexPath, safetensorsPaths, snapshot, token, stopwatch).ConfigureAwait(false);
    }

    private async Task<ModelSelectionResult> ClassifySourceFolderAsync(
        ModelSelectionOperationId operationId,
        ModelSelectionInput input,
        string? configPath,
        string? indexPath,
        List<string> safetensorsPaths,
        DirectorySnapshot snapshot,
        CancellationToken token,
        Stopwatch stopwatch)
    {
        if (configPath is null || (indexPath is null && safetensorsPaths.Count == 0))
        {
            return Failure(operationId, input.DisplayName, "selection-incomplete-source-model", "This source-model folder is incomplete. Choose a folder with config and model weights.");
        }

        BoundedJson config;
        try { config = await ReadBoundedJsonAsync(configPath, ModelSelectionLimits.MaximumMetadataBytes, token, stopwatch).ConfigureAwait(false); }
        catch (JsonException) { return Failure(operationId, input.DisplayName, "selection-invalid-source-model", "This source-model configuration is not valid. Choose another folder."); }

        using (config.Document)
        {
            JsonElement root = config.Document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                return Failure(operationId, input.DisplayName, "selection-invalid-source-model", "This source-model configuration is not valid. Choose another folder.");
            }

            if (root.TryGetProperty("auto_map", out _) || root.TryGetProperty("trust_remote_code", out _))
            {
                return Failure(operationId, input.DisplayName, "selection-custom-code", "This source model requires custom code and cannot be imported here.");
            }

            if (!IsSupportedSourceConfiguration(root))
            {
                return Failure(operationId, input.DisplayName, "selection-unsupported-source-model", "This source-model architecture is not supported here. Choose another model.");
            }
        }

        if (indexPath is not null)
        {
            BoundedJson index;
            try { index = await ReadBoundedJsonAsync(indexPath, ModelSelectionLimits.MaximumMetadataBytes - config.ByteCount, token, stopwatch).ConfigureAwait(false); }
            catch (JsonException) { return Failure(operationId, input.DisplayName, "selection-invalid-source-model", "This source-model index is not valid. Choose another folder."); }

            using (index.Document)
            {
                if (!IndexReferencesOnlyExistingRootShards(index.Document.RootElement, input.LocalPath))
                {
                    return Failure(operationId, input.DisplayName, "selection-incomplete-source-model", "This source-model folder is missing a required weight shard. Choose another folder.");
                }
            }
        }

        EnsureUnchanged(snapshot, input.LocalPath, token, stopwatch);
        return ModelSelectionResult.Accepted(operationId, ModelSelectionRoute.SourceModelDirectory, input.DisplayName);
    }

    private string[] EnumerateDirectChildren(string path, CancellationToken token, Stopwatch stopwatch)
    {
        var children = new List<string>();
        foreach (string child in Directory.EnumerateFileSystemEntries(path, "*", SearchOption.TopDirectoryOnly))
        {
            token.ThrowIfCancellationRequested();
            ThrowIfTimedOut(stopwatch, token);
            children.Add(child);
            if (children.Count > ModelSelectionLimits.MaximumDirectChildren)
            {
                throw new DirectoryTooLargeException();
            }
        }

        return children.ToArray();
    }

    private async Task<BoundedJson> ReadBoundedJsonAsync(string path, int remainingBudget, CancellationToken token, Stopwatch stopwatch)
    {
        if (remainingBudget < 0) throw new JsonException();
        await using FileStream stream = new(path, FileMode.Open, FileAccess.Read, FileShare.Read, 8192, useAsync: true);
        using var content = new MemoryStream(Math.Min(remainingBudget + 1, 8192));
        byte[] buffer = new byte[8192];
        int total = 0;
        while (true)
        {
            token.ThrowIfCancellationRequested();
            ThrowIfTimedOut(stopwatch, token);
            int count = Math.Min(buffer.Length, remainingBudget - total + 1);
            int read = await stream.ReadAsync(buffer.AsMemory(0, count), token).ConfigureAwait(false);
            afterMetadataRead?.Invoke();
            token.ThrowIfCancellationRequested();
            ThrowIfTimedOut(stopwatch, token);
            if (read == 0) break;
            total += read;
            if (total > remainingBudget) throw new JsonException();
            content.Write(buffer, 0, read);
        }
        return new BoundedJson(JsonDocument.Parse(content.ToArray()), total);
    }

    private static bool IsSupportedSourceConfiguration(JsonElement root)
    {
        if (!root.TryGetProperty("model_type", out JsonElement modelType) ||
            !string.Equals(modelType.GetString(), "granite", StringComparison.OrdinalIgnoreCase)) return false;
        if (root.TryGetProperty("task", out JsonElement task) && task.ValueKind == JsonValueKind.String &&
            !string.Equals(task.GetString(), "text-generation", StringComparison.OrdinalIgnoreCase)) return false;
        if (!root.TryGetProperty("architectures", out JsonElement architectures) || architectures.ValueKind != JsonValueKind.Array) return false;
        return architectures.EnumerateArray().Any(item => item.ValueKind == JsonValueKind.String && item.GetString()!.StartsWith("Granite", StringComparison.OrdinalIgnoreCase));
    }

    private static bool IndexReferencesOnlyExistingRootShards(JsonElement root, string folder)
    {
        if (root.ValueKind != JsonValueKind.Object || !root.TryGetProperty("weight_map", out JsonElement map) || map.ValueKind != JsonValueKind.Object) return false;
        var shards = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (JsonProperty property in map.EnumerateObject())
        {
            if (property.Value.ValueKind != JsonValueKind.String) return false;
            string? shard = property.Value.GetString();
            if (string.IsNullOrWhiteSpace(shard) || Path.GetFileName(shard) != shard || !shard.EndsWith(".safetensors", StringComparison.OrdinalIgnoreCase)) return false;
            shards.Add(shard);
            if (shards.Count > ModelSelectionLimits.MaximumRetainedNames) return false;
        }
        return shards.Count != 0 && shards.All(shard => File.Exists(Path.Combine(folder, shard)));
    }

    private ModelSelectionDiagnostic? GetUnsafeLocationDiagnostic(string path, bool isFolder)
    {
        if (path.StartsWith("\\\\?\\", StringComparison.Ordinal) || path.StartsWith("\\\\.\\", StringComparison.Ordinal)) return new("selection-network-location", "Choose an ordinary local item.");
        if (path.StartsWith("\\\\", StringComparison.Ordinal) || path.StartsWith("//", StringComparison.Ordinal)) return new("selection-network-location", "Choose an ordinary local item.");
        try
        {
            FileAttributes attributes = attributesReader(path);
            if ((attributes & FileAttributes.ReparsePoint) != 0) return new("selection-reparse-point", "Choose an ordinary local item.");
            if ((attributes & FileAttributes.Offline) != 0) return new("selection-not-local", AccessMessage);
            string root = Path.GetPathRoot(Path.GetFullPath(path))!;
            if (new DriveInfo(root).DriveType == DriveType.Network) return new("selection-network-location", "Choose an ordinary local item.");
        }
        catch (FileNotFoundException) when (!isFolder) { }
        catch (DirectoryNotFoundException) when (isFolder) { }
        return null;
    }

    private void EnsureUnchanged(DirectorySnapshot snapshot, string path, CancellationToken token, Stopwatch stopwatch)
    {
        beforeContinuityCheck?.Invoke();
        string[] children = EnumerateDirectChildren(path, token, stopwatch);
        if (!snapshot.Equals(DirectorySnapshot.Capture(path, children, attributesReader, directoryExists, token, stopwatch, timeoutReached, maximumElapsed))) throw new CandidateChangedException();
    }

    private static bool IsRelevantName(string name) =>
        name.EndsWith(".xml", StringComparison.OrdinalIgnoreCase) || name.EndsWith(".bin", StringComparison.OrdinalIgnoreCase) ||
        name.EndsWith(".safetensors", StringComparison.OrdinalIgnoreCase) || string.Equals(name, "config.json", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(name, "model.safetensors.index.json", StringComparison.OrdinalIgnoreCase);

    private void ThrowIfTimedOut(Stopwatch stopwatch, CancellationToken token)
    {
        if (timeoutReached?.Invoke() == true || stopwatch.Elapsed > maximumElapsed) throw new OperationCanceledException(token);
    }

    private static ModelSelectionResult Failure(ModelSelectionOperationId id, string displayName, string code, string message) =>
        ModelSelectionResult.Failure(id, displayName, new ModelSelectionDiagnostic(code, message));

    private static void ObserveLateFailure(Task worker)
    {
        // A synchronous file-system call may continue read-only after the caller receives its deadline result.
        // Observe any late fault without allowing that abandoned worker to publish a selection result.
        _ = worker.ContinueWith(
            completed => _ = completed.Exception,
            CancellationToken.None,
            TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously,
            TaskScheduler.Default);
    }

    private static async Task AwaitCooperativeCancellationOrObserveLateFailureAsync(Task worker)
    {
        if (await Task.WhenAny(worker, Task.Delay(TimeSpan.FromMilliseconds(100))).ConfigureAwait(false) != worker)
        {
            ObserveLateFailure(worker);
            return;
        }

        try
        {
            await worker.ConfigureAwait(false);
        }
        catch (OperationCanceledException) { }
        catch (Exception) { }
    }

    private readonly record struct DirectorySnapshot(FileAttributes Attributes, DateTime LastWriteTimeUtc, long ChildState)
    {
        internal static DirectorySnapshot Capture(
            string path,
            IEnumerable<string> children,
            Func<string, FileAttributes> attributesReader,
            Func<string, bool> directoryExists,
            CancellationToken token,
            Stopwatch stopwatch,
            Func<bool>? timeoutReached,
            TimeSpan maximumElapsed)
        {
            var validatedChildren = new List<(string Path, FileAttributes Attributes)>();
            foreach (string child in children.OrderBy(value => value, StringComparer.OrdinalIgnoreCase))
            {
                ThrowIfStopped(token, stopwatch, timeoutReached, maximumElapsed);
                FileAttributes attributes = attributesReader(child);
                if ((attributes & FileAttributes.ReparsePoint) != 0)
                {
                    throw new UnsafeCandidateException(new ModelSelectionDiagnostic("selection-reparse-point", "Choose an ordinary local item."));
                }
                validatedChildren.Add((child, attributes));
            }

            DirectoryInfo info = new(path);
            long childState = 17;
            foreach ((string child, FileAttributes attributes) in validatedChildren)
            {
                ThrowIfStopped(token, stopwatch, timeoutReached, maximumElapsed);
                if (directoryExists(child))
                {
                    childState = HashCode.Combine(childState, Path.GetFileName(child), attributes, new DirectoryInfo(child).LastWriteTimeUtc.Ticks);
                }
                else
                {
                    FileInfo childInfo = new(child);
                    childState = HashCode.Combine(childState, Path.GetFileName(child), attributes, childInfo.Length, childInfo.LastWriteTimeUtc.Ticks);
                }
                ThrowIfStopped(token, stopwatch, timeoutReached, maximumElapsed);
            }
            return new DirectorySnapshot(attributesReader(path), info.LastWriteTimeUtc, childState);
        }

        private static void ThrowIfStopped(CancellationToken token, Stopwatch stopwatch, Func<bool>? timeoutReached, TimeSpan maximumElapsed)
        {
            token.ThrowIfCancellationRequested();
            if (timeoutReached?.Invoke() == true || stopwatch.Elapsed > maximumElapsed) throw new OperationCanceledException(token);
        }
    }

    private readonly record struct BoundedJson(JsonDocument Document, int ByteCount);

    private sealed class DirectoryTooLargeException : IOException { }
    private sealed class CandidateChangedException : IOException { }
    private sealed class UnsafeCandidateException : IOException
    {
        internal UnsafeCandidateException(ModelSelectionDiagnostic diagnostic) => Diagnostic = diagnostic;
        internal ModelSelectionDiagnostic Diagnostic { get; }
    }
}
