using GraniteEdgeAI.Features.ModelImport.Selection;
using System;

namespace GraniteEdgeAI.Features.ModelImport;

internal sealed class SourceModelInspectionRequestedEventArgs : EventArgs
{
    internal SourceModelInspectionRequestedEventArgs(ModelSelectionOperationId operationId, string displayName)
    {
        OperationId = operationId;
        DisplayName = displayName ?? throw new ArgumentNullException(nameof(displayName));
    }

    internal ModelSelectionOperationId OperationId { get; }
    internal string DisplayName { get; }

    internal bool NavigationAccepted { get; private set; }

    internal void AcceptNavigation() => NavigationAccepted = true;
}
