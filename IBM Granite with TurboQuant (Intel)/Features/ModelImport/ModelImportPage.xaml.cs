using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using GraniteEdgeAI.Features.ModelImport.ModelDownload;
using Microsoft.Windows.Storage.Pickers;
using GraniteEdgeAI.Features.ModelImport.FileImport;
using System;

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

        // Opens the separate recommended-model selection page.
        private void RecommendedModelDownloadButton_Click(
            object sender,
            RoutedEventArgs e)
        {
            Frame.Navigate(typeof(RecommendedModelDownloadPage));
        }

        private async void BrowseFilesButton_ClickAsync(object sender, RoutedEventArgs e)
        {
            ModelFormatSelectionCard SelectFormat = new ModelFormatSelectionCard();

            // tell WinUI which window should display and host the dialog.
            SelectFormat.XamlRoot = Content.XamlRoot;


            //Wait for the user to click a button
            ContentDialogResult result = await SelectFormat.ShowAsync();

            // Check if they clicked the Primary button(e.g., "Yes")
            if (result == ContentDialogResult.Primary)
            {
                
            }
        }
    }
}
