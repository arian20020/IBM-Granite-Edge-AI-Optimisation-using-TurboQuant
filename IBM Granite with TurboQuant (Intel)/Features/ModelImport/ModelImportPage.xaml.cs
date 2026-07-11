// Provides the WinUI Page class inherited by ModelImportPage.
using Microsoft.UI.Xaml.Controls;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace GraniteEdgeAI.Features.ModelImport
{
    /// <summary>
    /// Represents the page where the user begins the model-import workflow.
    /// </summary>
    public sealed partial class ModelImportPage : Page
    {
        public ModelImportPage()
        {
            // Loads and connects the controls declared in ModelImportPage.xaml.
            InitializeComponent();
        }
    }
}