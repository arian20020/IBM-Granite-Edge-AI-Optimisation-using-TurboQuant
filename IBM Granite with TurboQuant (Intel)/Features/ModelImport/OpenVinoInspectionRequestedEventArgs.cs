using GraniteEdgeAI.Features.ModelImport.Selection;
using System;

namespace GraniteEdgeAI.Features.ModelImport;

internal sealed class OpenVinoInspectionRequestedEventArgs : EventArgs
{
    internal OpenVinoInspectionRequestedEventArgs(
        ModelSelectionOperationId operationId,
        string displayName)
        : this(operationId, string.Empty, displayName)
    {
    }

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

    // The shell marks this only after its destination page exists and has
    // accepted the corresponding app-private folder selection.
    internal bool NavigationAccepted { get; private set; }

    internal void AcceptNavigation() => NavigationAccepted = true;
}
