using GraniteEdgeAI.Features.ModelImport.Selection;
using System;

namespace GraniteEdgeAI.Features.ModelImport;

internal sealed class OpenVinoInspectionRequestedEventArgs : EventArgs
{
    internal OpenVinoInspectionRequestedEventArgs(ModelSelectionOperationId operationId, string displayName)
    {
        OperationId = operationId;
        DisplayName = displayName ?? throw new ArgumentNullException(nameof(displayName));
    }

    internal ModelSelectionOperationId OperationId { get; }
    internal string DisplayName { get; }
}
