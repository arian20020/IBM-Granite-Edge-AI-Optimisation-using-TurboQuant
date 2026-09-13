using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;

namespace GraniteEdgeAI.Features.ModelOptimization.Storage;

internal sealed class OptimizationCommitJournal
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false,
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly string _root;
    private readonly string _path;

    internal OptimizationCommitJournal(string committedRoot)
    {
        _root = StoragePathGuard.RequireRoot(committedRoot, create: true);
        _path = Path.Combine(_root, ".optimization-receipts.v1.json");
    }

    internal IReadOnlyList<OptimizationCommitReceipt> Load()
    {
        if (!File.Exists(_path))
        {
            return [];
        }
        StoragePathGuard.RequireChild(_root, _path, mustExist: true);
        StoragePathGuard.RequireRegularFile(_path);
        byte[] bytes = File.ReadAllBytes(_path);
        return JsonSerializer.Deserialize<List<OptimizationCommitReceipt>>(bytes, JsonOptions)
            ?? throw new InvalidDataException("The optimization receipt journal is invalid.");
    }

    internal async Task ReplaceAsync(
        IReadOnlyCollection<OptimizationCommitReceipt> receipts,
        CancellationToken cancellationToken)
    {
        string temporary = Path.Combine(_root, $".receipts-{Guid.NewGuid():N}.tmp");
        byte[] bytes = JsonSerializer.SerializeToUtf8Bytes(receipts, JsonOptions);
        try
        {
            await using (var stream = new FileStream(
                temporary,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                16 * 1024,
                FileOptions.Asynchronous | FileOptions.WriteThrough))
            {
                await stream.WriteAsync(bytes, cancellationToken).ConfigureAwait(false);
                await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
                stream.Flush(flushToDisk: true);
            }
            File.Move(temporary, _path, overwrite: true);
            using var durable = new FileStream(
                _path, FileMode.Open, FileAccess.Read, FileShare.Read, 1, FileOptions.WriteThrough);
            durable.Flush(flushToDisk: true);
        }
        finally
        {
            if (File.Exists(temporary))
            {
                File.Delete(temporary);
            }
        }
    }
}
