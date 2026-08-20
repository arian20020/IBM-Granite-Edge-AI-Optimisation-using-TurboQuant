using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;

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
        Directory.CreateDirectory(this.root);
    }

    public async Task<IReadOnlyList<ChatConversation>> LoadAsync(
        CancellationToken cancellationToken)
    {
        var conversations = new List<ChatConversation>();
        foreach (string file in Directory.EnumerateFiles(root, "*.json"))
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
            }
            catch (Exception exception) when (
                exception is JsonException or IOException or UnauthorizedAccessException or
                    ArgumentException)
            {
                // One damaged record cannot hide the remaining local history.
            }
        }

        return conversations.OrderByDescending(item => item.UpdatedUtc).ToArray();
    }

    public async Task SaveAsync(
        ChatConversation conversation,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(conversation);
        string destination = GetFile(conversation.Id);
        string temporary = Path.Combine(root, $".{Guid.NewGuid():N}.tmp");
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
        finally
        {
            if (File.Exists(temporary))
            {
                File.Delete(temporary);
            }
        }
    }

    public Task DeleteAsync(Guid conversationId, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        File.Delete(GetFile(conversationId));
        return Task.CompletedTask;
    }

    public Task ClearAsync(CancellationToken cancellationToken)
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

    private string GetFile(Guid id)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException("A conversation identifier is required.", nameof(id));
        }

        return Path.Combine(root, $"{id:N}.json");
    }
}
