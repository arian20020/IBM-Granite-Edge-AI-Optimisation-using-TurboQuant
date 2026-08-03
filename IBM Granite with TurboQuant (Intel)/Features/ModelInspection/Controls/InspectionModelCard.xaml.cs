using Microsoft.UI.Xaml.Controls;

namespace GraniteEdgeAI.Features.ModelInspection.Controls
{
    /// <summary>
    /// Displays the selected model and its inspected metadata.
    /// </summary>
    public sealed partial class InspectionModelCard : UserControl
    {
        /// <summary>
        /// Creates the model card and loads its XAML layout.
        /// </summary>
        public InspectionModelCard()
        {
            InitializeComponent();
        }
    }
}