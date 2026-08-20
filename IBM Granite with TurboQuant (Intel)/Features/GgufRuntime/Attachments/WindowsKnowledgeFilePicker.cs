using Microsoft.Windows.Storage.Pickers;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace GraniteEdgeAI.Features.GgufRuntime.Attachments;

internal sealed class WindowsKnowledgeFilePicker : IKnowledgeFilePicker
{
    public async Task<IReadOnlyList<KnowledgeFileCandidate>> PickAsync()
    {
        FileOpenPicker picker = new FileOpenPicker(App.MainWindow.AppWindow.Id)
        {
            Title = "Add knowledge files",
            CommitButtonText = "Attach",
        };
        picker.FileTypeFilter.Add(".txt");
        picker.FileTypeFilter.Add(".md");

        IReadOnlyList<PickFileResult>? selected = await picker.PickMultipleFilesAsync();
        if (selected is null || selected.Count == 0)
        {
            return Array.Empty<KnowledgeFileCandidate>();
        }

        var candidates = new List<KnowledgeFileCandidate>(selected.Count);
        foreach (PickFileResult result in selected)
        {
            candidates.Add(ToCandidate(result?.Path));
        }

        return candidates;
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
