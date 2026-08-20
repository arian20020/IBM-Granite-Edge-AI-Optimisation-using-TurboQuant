using GraniteEdgeAI.Features.ModelImport.Selection;
using System;

namespace GraniteEdgeAI.Features.Onboarding;

/// <summary>
/// A path-private request to start the source-model conversion journey. The
/// shell raises it only for its active Model Import page; a converter route
/// must accept it only after it has a real destination.
/// </summary>
internal sealed class SourceModelConversionRequestedEventArgs : EventArgs
{
    internal SourceModelConversionRequestedEventArgs(
        ModelSelectionOperationId operationId,
        string displayName)
    {
        OperationId = operationId;
        DisplayName = string.IsNullOrWhiteSpace(displayName)
            ? throw new ArgumentException("A display name is required.", nameof(displayName))
            : displayName;
    }

    internal ModelSelectionOperationId OperationId { get; }
    internal string DisplayName { get; }
    internal bool NavigationAccepted { get; private set; }
    internal void AcceptNavigation() => NavigationAccepted = true;
}
