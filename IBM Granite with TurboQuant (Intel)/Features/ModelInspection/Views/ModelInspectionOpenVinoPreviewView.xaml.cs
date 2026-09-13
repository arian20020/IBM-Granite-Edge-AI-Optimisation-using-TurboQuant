using Microsoft.UI.Xaml.Controls;

namespace GraniteEdgeAI.Features.ModelInspection.Views;

internal sealed partial class ModelInspectionOpenVinoPreviewView : UserControl
{
    internal ModelInspectionOpenVinoPreviewView()
    {
        InitializeComponent();
        PreviewProjection = new ModelInspectionPreviewProjection(this, isOpenVino: true);
    }

    internal ModelInspectionPreviewProjection PreviewProjection { get; }
}
