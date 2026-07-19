using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.Windows.Storage.Pickers;


// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace GraniteEdgeAI.Features.ModelImport.FileImport
{
    public enum ModelFormatSelected
    {
        None,
        GGUF,
        OpenVINO
    }
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class ModelFormatSelectionCard : ContentDialog
    {
        public ModelFormatSelectionCard()
        {
            InitializeComponent();
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            // Close the current ContentDialog and reveal ModelImportPage.

            Hide();
        }

        private async void GgufFormatButton_ClickAsync(object sender, RoutedEventArgs e)
        {
            GgufModelFilePicker modelFilePicker = new GgufModelFilePicker();

            ModelFormatSelected formatSelected = ModelFormatSelected.GGUF;
            Hide();
        }

        private async void OpenVINOFormatButton_ClickAsync(object sender, RoutedEventArgs e)
        {
            OpenVINOFolderPicker modelFolderPicker = new OpenVINOFolderPicker();

            PickFolderResult? folder = await modelFolderPicker.PickOpenVINOAsync();

            if (folder is null)
            {
                return;
            }
        }
    }
}
