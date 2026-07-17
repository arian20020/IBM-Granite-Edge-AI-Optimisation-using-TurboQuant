using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.Windows.Storage.Pickers;
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
            // Create the modern Windows App SDK picker.

            // The picker needs the ID of the real application window so that
            // Windows knows which window owns the file-selection dialog.
            FileOpenPicker openPicker =
                new FileOpenPicker(App.MainWindow.AppWindow.Id);
            
            
            // Allow IBM Granite models stored in GGUF, .xml,.bin format.
            openPicker.FileTypeFilter.Add(".xml");
            openPicker.FileTypeFilter.Add(".bin");
            openPicker.FileTypeFilter.Add(".gguf");

            // Display the picker and wait for the user to select one file
            // or close the picker without selecting anything.
            PickFileResult result =
                await openPicker.PickSingleFileAsync();

            // a null result means the user pressed Cancel or closed the picker.
            if (result is null)
            {
                // Leave ModelImportPage unchanged.
                return;
            }
        }
        
    }
}