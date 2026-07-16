using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace GraniteEdgeAI.Features.ModelImport
{
    /// <summary>
    /// Displays the recommended models available for download.
    /// </summary>
    public sealed partial class RecommendedModelDownloadPage : Page
    {
        public RecommendedModelDownloadPage()
        {
            // Loads and connects RecommendedModelDownloadPage.xaml.
            InitializeComponent();
        }

        /// <summary>
        /// Returns to the previous page when navigation history is available.
        /// </summary>
        private void BackButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            if (Frame is not null && Frame.CanGoBack)
            {
                Frame.GoBack();
            }
        }
    }
}