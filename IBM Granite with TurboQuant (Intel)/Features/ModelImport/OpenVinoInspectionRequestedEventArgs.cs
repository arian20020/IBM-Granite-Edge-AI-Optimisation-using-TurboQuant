using GraniteEdgeAI.Features.ModelImport.Selection;
using System;

namespace GraniteEdgeAI.Features.ModelImport;

internal sealed class OpenVinoInspectionRequestedEventArgs : EventArgs
{
    internal OpenVinoInspectionRequestedEventArgs(
        ModelSelectionOperationId operationId,
        string directoryPath,
        string displayName)
    {
        OperationId = operationId;
        DirectoryPath = directoryPath ??
            throw new ArgumentNullException(nameof(directoryPath));
        DisplayName = displayName ?? throw new ArgumentNullException(nameof(displayName));
    }

    internal ModelSelectionOperationId OperationId { get; }
    internal string DirectoryPath { get; }
    internal string DisplayName { get; }
}
