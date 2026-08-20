using Microsoft.Windows.Storage.Pickers;
using System;
using System.Collections.Generic;
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

        var paths = new List<string?>(results.Count);
        foreach (PickFileResult result in results)
        {
            paths.Add(result?.Path);
        }

        return await Task.Run(() => KnowledgeFileCandidateMapper.Map(paths));
    }
}
