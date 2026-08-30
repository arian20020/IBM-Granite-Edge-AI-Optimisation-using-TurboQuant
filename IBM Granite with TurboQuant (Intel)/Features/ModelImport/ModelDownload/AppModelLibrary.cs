using System.Text.Json;
using System.Text.Json.Serialization;
using System.Security.Cryptography;
using Windows.Storage;

namespace GraniteEdgeAI.Features.ModelImport.ModelDownload;

internal sealed class AppModelLibrary
{
    private static readonly JsonSerializerOptions CheckpointJsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = false,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        WriteIndented = false
    };

    private readonly string _root;
    private readonly string _modelsRoot;
    private readonly string _partialRoot;
    private readonly string _stateRoot;
    private readonly string _locksRoot;
    private readonly Func<string, long> _availableFreeSpace;
    private readonly Func<ModelDownloadCancellationBoundary, CancellationToken, ValueTask>? _boundaryObserver;

    internal AppModelLibrary(
        string root,
        Func<string, long> availableFreeSpace,
        Func<ModelDownloadCancellationBoundary, CancellationToken, ValueTask>? boundaryObserver = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(root);
        _availableFreeSpace = availableFreeSpace ?? throw new ArgumentNullException(nameof(availableFreeSpace));
        _boundaryObserver = boundaryObserver;
        _root = Path.GetFullPath(root);
        _modelsRoot = ContainedDirectory("Models");
        _partialRoot = ContainedDirectory("Partial");
        _stateRoot = ContainedDirectory("State");
        _locksRoot = ContainedDirectory("Locks");

        RejectReparseAncestry(_root);
        Directory.CreateDirectory(_root);
        RejectReparseAncestry(_root);
        Directory.CreateDirectory(_modelsRoot);
        Directory.CreateDirectory(_partialRoot);
        Directory.CreateDirectory(_stateRoot);
        Directory.CreateDirectory(_locksRoot);
        RejectReparsePoint(_modelsRoot);
        RejectReparsePoint(_partialRoot);
        RejectReparsePoint(_stateRoot);
        RejectReparsePoint(_locksRoot);
    }

    internal static AppModelLibrary CreateDefault()
    {
        string root = Path.Combine(
            ApplicationData.Current.LocalFolder.Path,
            "GraniteEdgeAI",
            "Models");
        return new AppModelLibrary(root, path => new DriveInfo(Path.GetPathRoot(path)!).AvailableFreeSpace);
    }

    internal ValueTask<ModelDownloadLibraryLease> AcquireLeaseAsync(
        ModelDownloadCatalogEntry entry,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        string lockPath = ArtifactPath(_locksRoot, entry.Id, ".lock");
        var stream = new FileStream(
            lockPath,
            FileMode.OpenOrCreate,
            FileAccess.ReadWrite,
            FileShare.None,
            bufferSize: 1,
            FileOptions.Asynchronous);
        return ValueTask.FromResult(new ModelDownloadLibraryLease(stream, lockPath));
    }

    internal async Task<Stream> OpenPartialWriteAsync(
        ModelDownloadCatalogEntry entry,
        long truncateToLength,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (truncateToLength < 0 || truncateToLength > entry.ExpectedByteLength)
        {
            throw new ArgumentOutOfRangeException(nameof(truncateToLength));
        }

        var stream = new FileStream(
            PartialPath(entry),
            FileMode.OpenOrCreate,
            FileAccess.ReadWrite,
            FileShare.Read,
            bufferSize: 128 * 1024,
            FileOptions.Asynchronous | FileOptions.SequentialScan);
        try
        {
            stream.SetLength(truncateToLength);
            stream.Position = truncateToLength;
            await ObserveBoundaryAsync(ModelDownloadCancellationBoundary.PartialWrite, cancellationToken);
            return stream;
        }
        catch
        {
            await stream.DisposeAsync();
            throw;
        }
    }

    internal async Task WriteCheckpointAsync(
        ModelDownloadCatalogEntry entry,
        ModelDownloadPartialState state,
        CancellationToken cancellationToken)
    {
        ValidateState(entry, state);
        string partialPath = PartialPath(entry);
        if (!File.Exists(partialPath) || new FileInfo(partialPath).Length < state.DurableByteLength)
        {
            throw new InvalidDataException("The durable checkpoint exceeds the partial file.");
        }

        string statePath = StatePath(entry);
        string temporaryPath = statePath + ".new";
        EnsureContained(temporaryPath);
        RejectReparsePointIfExists(temporaryPath);
        byte[] json = JsonSerializer.SerializeToUtf8Bytes(state, CheckpointJsonOptions);
        try
        {
            await using (var stream = new FileStream(
                temporaryPath,
                FileMode.Create,
                FileAccess.Write,
                FileShare.None,
                bufferSize: 16 * 1024,
                FileOptions.Asynchronous | FileOptions.WriteThrough))
            {
                await ObserveBoundaryAsync(ModelDownloadCancellationBoundary.CheckpointWrite, cancellationToken);
                await stream.WriteAsync(json, cancellationToken);
                await stream.FlushAsync(cancellationToken);
            }

            File.Move(temporaryPath, statePath, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporaryPath))
            {
                File.Delete(temporaryPath);
            }
        }
    }

    internal async Task<ModelDownloadResumeInfo?> RecoverAsync(
        ModelDownloadCatalogEntry entry,
        CancellationToken cancellationToken)
    {
        string statePath = StatePath(entry);
        string partialPath = PartialPath(entry);
        if (!File.Exists(statePath) || !File.Exists(partialPath))
        {
            return null;
        }

        ModelDownloadPartialState? state;
        try
        {
            await using FileStream stream = File.OpenRead(statePath);
            state = await JsonSerializer.DeserializeAsync<ModelDownloadPartialState>(
                stream,
                CheckpointJsonOptions,
                cancellationToken);
            if (state is null)
            {
                throw new InvalidDataException("The checkpoint is empty.");
            }

            ValidateState(entry, state);
        }
        catch (JsonException)
        {
            await DiscardPartialAsync(entry, CancellationToken.None);
            return null;
        }
        catch (InvalidDataException)
        {
            await DiscardPartialAsync(entry, CancellationToken.None);
            return null;
        }

        long partialLength = new FileInfo(partialPath).Length;
        if (partialLength < state.DurableByteLength)
        {
            await DiscardPartialAsync(entry, CancellationToken.None);
            return null;
        }

        if (partialLength > state.DurableByteLength)
        {
            await using var stream = new FileStream(
                partialPath,
                FileMode.Open,
                FileAccess.Write,
                FileShare.None,
                bufferSize: 1,
                FileOptions.Asynchronous | FileOptions.WriteThrough);
            stream.SetLength(state.DurableByteLength);
            await stream.FlushAsync(cancellationToken);
        }

        return new ModelDownloadResumeInfo(
            entry.Id,
            state.DurableByteLength,
            entry.ExpectedByteLength,
            state.EntityTag);
    }

    internal Task<long> GetPartialLengthAsync(
        ModelDownloadCatalogEntry entry,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        string path = PartialPath(entry);
        return Task.FromResult(File.Exists(path) ? new FileInfo(path).Length : 0L);
    }

    internal async Task<bool> HasSufficientSpaceAsync(
        ModelDownloadCatalogEntry entry,
        CancellationToken cancellationToken)
    {
        long partialLength = await GetPartialLengthAsync(entry, cancellationToken);
        long remaining = Math.Max(0, entry.ExpectedByteLength - partialLength);
        return _availableFreeSpace(_root) >= remaining;
    }

    internal Task<bool> FinalExistsAsync(
        ModelDownloadCatalogEntry entry,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(File.Exists(FinalPath(entry)));
    }

    internal async Task<VerifiedDownloadedModel?> GetVerifiedExistingAsync(
        ModelDownloadCatalogEntry entry,
        CancellationToken cancellationToken)
    {
        string finalPath = FinalPath(entry);
        if (!File.Exists(finalPath))
        {
            return null;
        }

        if (new FileInfo(finalPath).Length != entry.ExpectedByteLength)
        {
            File.Delete(finalPath);
            return null;
        }

        byte[] actual;
        await using (var stream = new FileStream(
            finalPath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            bufferSize: 128 * 1024,
            FileOptions.Asynchronous | FileOptions.SequentialScan))
        {
            actual = await SHA256.HashDataAsync(stream, cancellationToken);
        }

        byte[] expected = Convert.FromHexString(entry.ExpectedSha256);
        if (!CryptographicOperations.FixedTimeEquals(actual, expected))
        {
            File.Delete(finalPath);
            return null;
        }

        return new VerifiedDownloadedModel(
            finalPath,
            entry.FileName,
            entry.Id,
            entry.ExpectedByteLength,
            entry.ExpectedSha256);
    }

    internal async Task<byte[]> ComputePartialSha256Async(
        ModelDownloadCatalogEntry entry,
        CancellationToken cancellationToken)
    {
        await using var stream = new FileStream(
            PartialPath(entry),
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            bufferSize: 128 * 1024,
            FileOptions.Asynchronous | FileOptions.SequentialScan);
        await ObserveBoundaryAsync(ModelDownloadCancellationBoundary.IntegrityHash, cancellationToken);
        return await SHA256.HashDataAsync(stream, cancellationToken);
    }

    internal async Task<VerifiedDownloadedModel> PublishAsync(
        ModelDownloadCatalogEntry entry,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        string partialPath = PartialPath(entry);
        if (!File.Exists(partialPath) || new FileInfo(partialPath).Length != entry.ExpectedByteLength)
        {
            throw new InvalidDataException("The partial artifact does not have the expected length.");
        }

        string finalPath = FinalPath(entry);
        await ObserveBoundaryAsync(ModelDownloadCancellationBoundary.FinalPublish, cancellationToken);
        File.Move(partialPath, finalPath, overwrite: false);
        DeleteIfExists(StatePath(entry));
        return new VerifiedDownloadedModel(
                finalPath,
                entry.FileName,
                entry.Id,
                entry.ExpectedByteLength,
                entry.ExpectedSha256);
    }

    internal Task DiscardPartialAsync(
        ModelDownloadCatalogEntry entry,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        DeleteIfExists(PartialPath(entry));
        DeleteIfExists(StatePath(entry));
        DeleteIfExists(StatePath(entry) + ".new");
        return Task.CompletedTask;
    }

    private string ContainedDirectory(string name)
    {
        string path = Path.GetFullPath(Path.Combine(_root, name));
        EnsureContained(path);
        return path;
    }

    private async ValueTask ObserveBoundaryAsync(
        ModelDownloadCancellationBoundary boundary,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (_boundaryObserver is not null)
            await _boundaryObserver(boundary, cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
    }

    private string PartialPath(ModelDownloadCatalogEntry entry) =>
        ArtifactPath(_partialRoot, entry.Id, ".partial");

    private string StatePath(ModelDownloadCatalogEntry entry) =>
        ArtifactPath(_stateRoot, entry.Id, ".json");

    private string FinalPath(ModelDownloadCatalogEntry entry) =>
        ArtifactPath(_modelsRoot, entry.FileName, string.Empty);

    private string ArtifactPath(string parent, string stem, string suffix)
    {
        RejectReparseAncestry(_root);
        RejectReparsePoint(parent);
        if (string.IsNullOrWhiteSpace(stem) ||
            stem.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 ||
            !string.Equals(Path.GetFileName(stem), stem, StringComparison.Ordinal))
        {
            throw new InvalidDataException("The catalogue contains an unsafe storage name.");
        }

        string path = Path.GetFullPath(Path.Combine(parent, stem + suffix));
        EnsureContained(path);
        RejectReparsePointIfExists(path);
        return path;
    }

    private void EnsureContained(string path)
    {
        string boundary = _root.EndsWith(Path.DirectorySeparatorChar)
            ? _root
            : _root + Path.DirectorySeparatorChar;
        if (!path.StartsWith(boundary, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException("The model library path escaped its fixed root.");
        }
    }

    private static void RejectReparsePoint(string path)
    {
        if ((File.GetAttributes(path) & System.IO.FileAttributes.ReparsePoint) != 0)
        {
            throw new IOException("The model library root cannot be a reparse point.");
        }
    }

    private static void RejectReparsePointIfExists(string path)
    {
        if ((File.Exists(path) || Directory.Exists(path)) &&
            (File.GetAttributes(path) & System.IO.FileAttributes.ReparsePoint) != 0)
        {
            throw new IOException("A model library artifact cannot be a reparse point.");
        }
    }

    private static void RejectReparseAncestry(string path)
    {
        var cursor = new DirectoryInfo(Path.GetFullPath(path));
        while (cursor is not null)
        {
            if (cursor.Exists && (cursor.Attributes & System.IO.FileAttributes.ReparsePoint) != 0)
            {
                throw new IOException("The model library path cannot traverse a reparse point.");
            }
            cursor = cursor.Parent;
        }
    }

    private static void ValidateState(
        ModelDownloadCatalogEntry entry,
        ModelDownloadPartialState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        if (state.SchemaVersion != 1 ||
            !string.Equals(state.CatalogId, entry.Id, StringComparison.Ordinal) ||
            !string.Equals(state.Revision, entry.Revision, StringComparison.Ordinal) ||
            !string.Equals(state.FileName, entry.FileName, StringComparison.Ordinal) ||
            state.ExpectedByteLength != entry.ExpectedByteLength ||
            !string.Equals(state.ExpectedSha256, entry.ExpectedSha256, StringComparison.Ordinal) ||
            state.DurableByteLength < 0 ||
            state.DurableByteLength > entry.ExpectedByteLength)
        {
            throw new InvalidDataException("The checkpoint does not match the pinned catalogue artifact.");
        }
    }

    private static void DeleteIfExists(string path)
    {
        if (File.Exists(path))
        {
            File.Delete(path);
        }
    }
}

internal sealed class ModelDownloadLibraryLease : IAsyncDisposable
{
    private FileStream? _stream;
    private readonly string _lockPath;

    internal ModelDownloadLibraryLease(FileStream stream, string lockPath)
    {
        _stream = stream;
        _lockPath = lockPath;
    }

    public ValueTask DisposeAsync()
    {
        FileStream? stream = Interlocked.Exchange(ref _stream, null);
        if (stream is null)
        {
            return ValueTask.CompletedTask;
        }

        stream.Dispose();
        if (File.Exists(_lockPath))
        {
            File.Delete(_lockPath);
        }

        return ValueTask.CompletedTask;
    }
}
