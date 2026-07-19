using Microsoft.Windows.Storage.Pickers;
using System.Threading.Tasks;

namespace GraniteEdgeAI.Features.ModelImport.FileImport
{
    /// <summary>
    /// Opens the Windows file picker for one GGUF model file.
    /// </summary>
    internal sealed class GgufModelFilePicker : IGgufModelFilePicker
    {
        /// <summary>
        /// Returns the selected GGUF path, or null when the user cancels.
        /// </summary>
        public async Task<string?> PickGgufAsync()
        {
            // Create the picker and attach it to the application's main window.
            FileOpenPicker openGgufPicker =
                new FileOpenPicker(App.MainWindow.AppWindow.Id);

            // Apply the centrally defined GGUF-only extension list.
            foreach (string extension in GgufPickerConfiguration.AllowedFileExtensions)
            {
                openGgufPicker.FileTypeFilter.Add(extension);
            }

            // Wait for the user to select one file or cancel the picker.
            PickFileResult? selectedFile =
                await openGgufPicker.PickSingleFileAsync();

            // Return only the path needed by the later import stages.
            return selectedFile?.Path;
        }
    }
}
