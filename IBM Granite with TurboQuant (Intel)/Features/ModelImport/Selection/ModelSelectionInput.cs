namespace GraniteEdgeAI.Features.ModelImport.Selection;

internal sealed class ModelSelectionInput
{
    internal ModelSelectionInput(string localPath, string displayName, bool isFolder)
    {
        LocalPath = localPath;
        DisplayName = displayName;
        IsFolder = isFolder;
    }

    internal string LocalPath { get; }

    internal string DisplayName { get; }

    internal bool IsFolder { get; }
}
