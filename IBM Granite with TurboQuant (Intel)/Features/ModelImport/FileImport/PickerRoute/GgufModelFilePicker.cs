using Microsoft.Windows.Storage.Pickers;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace GraniteEdgeAI.Features.ModelImport.FileImport.PickerRoute
{
    /// <summary>
    /// Opens the Windows file picker for one GGUF model file.
    /// </summary>
    internal sealed class GgufModelFilePicker
    {
        internal static IReadOnlyList<string> AllowedFileTypes { get; } =
            Array.AsReadOnly(new[] { ".gguf" });

        public async Task<PickFileResult?> PickGGUFAsync()
        {
            FileOpenPicker openGGUFPicker =
                new FileOpenPicker(App.MainWindow.AppWindow.Id);

            foreach (string fileType in AllowedFileTypes)
            {
                openGGUFPicker.FileTypeFilter.Add(fileType);
            }

            return await openGGUFPicker.PickSingleFileAsync();
        }
    }
}
