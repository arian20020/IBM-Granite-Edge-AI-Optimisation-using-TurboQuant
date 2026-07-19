using Microsoft.Windows.Storage.Pickers;
using System;
using System.Threading.Tasks;

namespace GraniteEdgeAI.Features.ModelImport.FileImport
{
    /// <summary>
    /// Opens the Windows file picker for one GGUF model file.
    /// </summary>
    internal sealed class GgufModelFilePicker
    {
        /// <summary>
        /// Creates the GGUF file-picker helper.
        /// </summary>
        public GgufModelFilePicker()
        {
        }

        /// <summary>
        /// Opens the Windows picker and returns the selected file.
        ///
        /// Returns null when the user cancels the picker.
        /// </summary>
        public async Task<PickFileResult?> PickGGUFAsync()
        {
            // Create the picker and associate it with the main app window.
            FileOpenPicker openGGUFPicker =
                new FileOpenPicker(App.MainWindow.AppWindow.Id);

            // Restrict selection to files that use the .gguf extension.
            openGGUFPicker.FileTypeFilter.Add(".gguf");

            // Wait for the user to choose one file or cancel.
            PickFileResult? selectedFile =
                await openGGUFPicker.PickSingleFileAsync();

            // Stop safely when the user cancels.
            if (selectedFile is null)
            {
                return null;
            }

            // Return the complete picker result to ModelImportPage.
            return selectedFile;
        }
    }
}