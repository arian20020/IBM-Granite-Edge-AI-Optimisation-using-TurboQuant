using Microsoft.UI.Xaml.Controls;

namespace GraniteEdgeAI.Features.ModelInspection.Views;

internal sealed partial class ModelInspectionGgufPreviewView : UserControl
{
    internal ModelInspectionGgufPreviewView()
    {
        InitializeComponent();
        PreviewProjection = new ModelInspectionPreviewProjection(this, isOpenVino: false);
    }

    internal ModelInspectionPreviewProjection PreviewProjection { get; }
}
