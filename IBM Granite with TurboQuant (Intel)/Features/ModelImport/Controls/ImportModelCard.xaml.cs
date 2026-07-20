using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace GraniteEdgeAI.Features.ModelImport.Controls
{
    public sealed partial class ImportModelCard : UserControl
    {
        public ImportModelCard()
        {
            InitializeComponent();
        }

        public event RoutedEventHandler? BrowseFilesRequested;


        /// Handles the internal Browse button click.
        private void BrowseFilesButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            BrowseFilesRequested?.Invoke(this,e);
        }
    }
}
