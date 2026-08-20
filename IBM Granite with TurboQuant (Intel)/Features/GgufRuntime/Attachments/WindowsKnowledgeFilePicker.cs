using Microsoft.Windows.Storage.Pickers;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace GraniteEdgeAI.Features.GgufRuntime.Attachments;

internal sealed class WindowsKnowledgeFilePicker : IKnowledgeFilePicker
{
    private static readonly IReadOnlyList<string> AllowedFileTypes =
        Array.AsReadOnly(new[] { ".txt", ".md" });

    public async Task<IReadOnlyList<KnowledgeFileCandidate>> PickAsync()
    {
        FileOpenPicker picker = new FileOpenPicker(App.MainWindow.AppWindow.Id)
        {
            Title = "Add knowledge files",
            CommitButtonText = "Attach",
        };
        foreach (string fileType in AllowedFileTypes)
        {
            picker.FileTypeFilter.Add(fileType);
        }

        IReadOnlyList<PickFileResult>? results = await picker.PickMultipleFilesAsync();
        if (results is null || results.Count == 0)
        {
            return Array.Empty<KnowledgeFileCandidate>();
        }

        var candidates = new List<KnowledgeFileCandidate>(results.Count);
        foreach (PickFileResult result in results)
        {
            candidates.Add(ToCandidate(result?.Path));
        }

        return candidates.AsReadOnly();
    }

    private static KnowledgeFileCandidate ToCandidate(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return new KnowledgeFileCandidate(path, 0, false);
        }

        try
        {
            long sizeInBytes = new FileInfo(path).Length;
            return new KnowledgeFileCandidate(path, sizeInBytes, true);
        }
        // PathTooLongException is covered by IOException.
        catch (IOException)
        {
            return new KnowledgeFileCandidate(path, 0, false);
        }
        catch (UnauthorizedAccessException)
        {
            return new KnowledgeFileCandidate(path, 0, false);
        }
        catch (System.Security.SecurityException)
        {
            return new KnowledgeFileCandidate(path, 0, false);
        }
        catch (ArgumentException)
        {
            return new KnowledgeFileCandidate(path, 0, false);
        }
        catch (NotSupportedException)
        {
            return new KnowledgeFileCandidate(path, 0, false);
        }
    }
}
