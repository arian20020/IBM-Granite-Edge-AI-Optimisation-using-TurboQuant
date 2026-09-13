using System;
using System.IO;

namespace GraniteEdgeAI.Features.ModelImport.Selection;

internal sealed class ModelSelectionResult
{
    private ModelSelectionResult(
        ModelSelectionOperationId operationId,
        bool isAccepted,
        ModelSelectionRoute? route,
        string displayName,
        ModelSelectionDiagnostic? diagnostic)
    {
        OperationId = operationId;
        IsAccepted = isAccepted;
        Route = route;
        DisplayName = NormalizeDisplayName(displayName);
        Diagnostic = diagnostic;
    }

    internal ModelSelectionOperationId OperationId { get; }

    internal bool IsAccepted { get; }

    internal ModelSelectionRoute? Route { get; }

    internal string DisplayName { get; }

    internal ModelSelectionDiagnostic? Diagnostic { get; }

    internal static ModelSelectionResult Accepted(
        ModelSelectionOperationId operationId,
        ModelSelectionRoute route,
        string displayName)
    {
        return new ModelSelectionResult(
            operationId,
            isAccepted: true,
            route,
            displayName,
            diagnostic: null);
    }

    internal static ModelSelectionResult Failure(
        ModelSelectionOperationId operationId,
        string displayName,
        ModelSelectionDiagnostic diagnostic)
    {
        ArgumentNullException.ThrowIfNull(diagnostic);

        return new ModelSelectionResult(
            operationId,
            isAccepted: false,
            route: null,
            displayName,
            diagnostic);
    }

    private static string NormalizeDisplayName(string displayName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(displayName);

        string safeDisplayName = Path.GetFileName(displayName);
        if (string.IsNullOrWhiteSpace(safeDisplayName))
        {
            throw new ArgumentException(
                "Display name must include a file or folder name.",
                nameof(displayName));
        }

        return safeDisplayName;
    }
}
