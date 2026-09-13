using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using System.Runtime.ExceptionServices;

namespace GraniteEdgeAI.Features.GgufRuntime.History;

internal sealed class AtomicJsonChatHistoryStore : IChatHistoryStore
{
    private readonly string root;
    private readonly JsonSerializerOptions options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = false,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        WriteIndented = false,
    };

    internal AtomicJsonChatHistoryStore(string root)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(root);
        if (!Path.IsPathFullyQualified(root))
        {
            throw new ArgumentException("The history root must be absolute.", nameof(root));
        }

        this.root = Path.GetFullPath(root);
        try
        {
            Directory.CreateDirectory(this.root);
        }
        catch (Exception exception) when (IsExpectedStorageFailure(exception))
        {
            throw new ChatHistoryUnavailableException();
        }
    }

    public async Task<ChatHistoryLoadResult> LoadAsync(
        CancellationToken cancellationToken)
    {
        var conversations = new List<ChatConversation>();
        bool hasUnavailableRecords = false;
        IEnumerable<string> files;
        try
        {
            files = Directory.EnumerateFiles(root, "*.json").ToArray();
        }
        catch (Exception exception) when (IsExpectedStorageFailure(exception))
        {
            throw new ChatHistoryUnavailableException();
        }

        foreach (string file in files)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!Guid.TryParseExact(Path.GetFileNameWithoutExtension(file), "N", out Guid id))
            {
                continue;
            }

            try
            {
                var info = new FileInfo(file);
                if (info.Length is <= 0 or > ChatHistoryPolicy.MaximumRecordBytes)
                {
                    hasUnavailableRecords = true;
                    continue;
                }

                await using FileStream stream = new(
                    file,
                    FileMode.Open,
                    FileAccess.Read,
                    FileShare.Read,
                    4096,
                    FileOptions.Asynchronous | FileOptions.SequentialScan);
                ChatConversation? conversation = await JsonSerializer
                    .DeserializeAsync<ChatConversation>(stream, options, cancellationToken)
                    .ConfigureAwait(false);
                if (conversation?.Id == id)
                {
                    conversations.Add(conversation);
                }
                else
                {
                    hasUnavailableRecords = true;
                }
            }
            catch (Exception exception) when (IsExpectedRecordFailure(exception))
            {
                hasUnavailableRecords = true;
            }
        }

        return new ChatHistoryLoadResult(
            conversations.OrderByDescending(item => item.UpdatedUtc).ToArray(),
            hasUnavailableRecords);
    }

    public async Task SaveAsync(
        ChatConversation conversation,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(conversation);
        string destination = GetFile(conversation.Id);
        string temporary = Path.Combine(root, $".{Guid.NewGuid():N}.tmp");
        Exception? primaryFailure = null;
        try
        {
            await using (var stream = new FileStream(
                temporary,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                4096,
                FileOptions.Asynchronous | FileOptions.WriteThrough))
            {
                await JsonSerializer.SerializeAsync(
                    stream,
                    conversation,
                    options,
                    cancellationToken).ConfigureAwait(false);
                await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
                stream.Flush(flushToDisk: true);
                if (stream.Length > ChatHistoryPolicy.MaximumRecordBytes)
                {
                    throw new InvalidOperationException("The conversation record is too large.");
                }
            }

            File.Move(temporary, destination, overwrite: true);
        }
        catch (Exception exception) when (IsExpectedStorageFailure(exception))
        {
            primaryFailure = new ChatHistoryUnavailableException();
        }
        catch (Exception exception)
        {
            primaryFailure = exception;
        }

        Exception? cleanupFailure = null;
        try
        {
            if (File.Exists(temporary))
            {
                File.Delete(temporary);
            }
        }
        catch (Exception exception)
        {
            cleanupFailure = exception;
        }

        CompleteSave(primaryFailure, cleanupFailure);
    }

    public Task DeleteAsync(Guid conversationId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        try
        {
            File.Delete(GetFile(conversationId));
            return Task.CompletedTask;
        }
        catch (Exception exception) when (IsExpectedStorageFailure(exception))
        {
            throw new ChatHistoryUnavailableException();
        }
    }

    public Task ClearAsync(CancellationToken cancellationToken)
    {
        try
        {
            foreach (string file in Directory.EnumerateFiles(root, "*.json"))
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (Guid.TryParseExact(Path.GetFileNameWithoutExtension(file), "N", out _))
                {
                    File.Delete(file);
                }
            }

            return Task.CompletedTask;
        }
        catch (Exception exception) when (IsExpectedStorageFailure(exception))
        {
            throw new ChatHistoryUnavailableException();
        }
    }

    private string GetFile(Guid id)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException("A conversation identifier is required.", nameof(id));
        }

        return Path.Combine(root, $"{id:N}.json");
    }

    private static bool IsExpectedRecordFailure(Exception exception) =>
        exception is JsonException || IsExpectedStorageFailure(exception);

    private static bool IsExpectedStorageFailure(Exception exception) =>
        exception is IOException or UnauthorizedAccessException;

    internal static void CompleteSave(
        Exception? primaryFailure,
        Exception? cleanupFailure)
    {
        if (primaryFailure is not null)
        {
            ExceptionDispatchInfo.Capture(primaryFailure).Throw();
        }

        if (cleanupFailure is not null)
        {
            if (IsExpectedStorageFailure(cleanupFailure))
            {
                throw new ChatHistoryUnavailableException();
            }

            ExceptionDispatchInfo.Capture(cleanupFailure).Throw();
        }
    }
}
