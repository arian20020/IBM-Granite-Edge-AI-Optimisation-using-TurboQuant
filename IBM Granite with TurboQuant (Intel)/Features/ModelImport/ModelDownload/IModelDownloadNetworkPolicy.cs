namespace GraniteEdgeAI.Features.ModelImport.ModelDownload;

internal enum ModelDownloadConnectionKind
{
    Offline,
    Unrestricted,
    ConfirmationRequired
}

internal interface IModelDownloadNetworkPolicy
{
    ModelDownloadConnectionKind GetCurrentConnectionKind();
}
