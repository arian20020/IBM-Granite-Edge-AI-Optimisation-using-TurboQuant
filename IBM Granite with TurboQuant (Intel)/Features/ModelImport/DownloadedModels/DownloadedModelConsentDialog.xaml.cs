using Microsoft.UI.Xaml.Controls;
using System;
using System.Threading.Tasks;

namespace GraniteEdgeAI.Features.ModelImport.DownloadedModels;

public sealed partial class DownloadedModelConsentDialog : ContentDialog
{
    public DownloadedModelConsentDialog()
    {
        InitializeComponent();
    }

    internal async Task<bool> RequestConsentAsync() =>
        await ShowAsync() == ContentDialogResult.Primary;
}
