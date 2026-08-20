using System;
using System.IO;

namespace GraniteEdgeAI.Features.ModelImport.Selection;

/// <summary>
/// Converts a native picker path into the single internal selection shape.
/// </summary>
internal sealed class ModelSelectionInputNormalizer
{
    internal ModelSelectionInput FromPickerPath(string localPath, bool isFolder)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(localPath);

        string displayName = Path.GetFileName(
            Path.TrimEndingDirectorySeparator(localPath));
        if (string.IsNullOrWhiteSpace(displayName))
        {
            throw new ArgumentException(
                "Picker paths must identify a file or folder.",
                nameof(localPath));
        }

        return new ModelSelectionInput(localPath, displayName, isFolder);
    }
}
