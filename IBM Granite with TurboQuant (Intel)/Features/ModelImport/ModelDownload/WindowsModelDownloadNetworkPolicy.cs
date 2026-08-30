using Windows.Networking.Connectivity;

namespace GraniteEdgeAI.Features.ModelImport.ModelDownload;

internal sealed class WindowsModelDownloadNetworkPolicy : IModelDownloadNetworkPolicy
{
    public ModelDownloadConnectionKind GetCurrentConnectionKind()
    {
        ConnectionProfile? profile = NetworkInformation.GetInternetConnectionProfile();
        if (profile is null || profile.GetNetworkConnectivityLevel() != NetworkConnectivityLevel.InternetAccess)
        {
            return ModelDownloadConnectionKind.Offline;
        }

        ConnectionCost cost = profile.GetConnectionCost();
        return cost.NetworkCostType == NetworkCostType.Unrestricted &&
               !cost.Roaming &&
               !cost.OverDataLimit
            ? ModelDownloadConnectionKind.Unrestricted
            : ModelDownloadConnectionKind.ConfirmationRequired;
    }
}
