using GraniteEdgeAI.Features.ModelImport.ModelDownload;

namespace GraniteEdgeAI.Features.ModelImport;

internal sealed class VerifiedDownloadInspectionReadyEventArgs(
    ModelDownloadOperationId operationId,
    string displayName) : EventArgs
{
    internal ModelDownloadOperationId OperationId { get; } = operationId;
    internal string DisplayName { get; } = displayName;
}
