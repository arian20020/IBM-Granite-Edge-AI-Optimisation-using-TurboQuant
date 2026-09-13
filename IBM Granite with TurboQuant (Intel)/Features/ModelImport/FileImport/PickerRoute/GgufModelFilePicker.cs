using GraniteEdgeAI.Features.ModelImport.Selection;
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
        private readonly Func<Task<PickFileResult?>>? _pickFileAsync;

        internal static IReadOnlyList<string> AllowedFileTypes { get; } =
            Array.AsReadOnly(new[] { ".gguf" });

        public GgufModelFilePicker()
        { }

        internal GgufModelFilePicker(Func<Task<PickFileResult?>> pickFileAsync)
        {
            _pickFileAsync = pickFileAsync ?? throw new ArgumentNullException(nameof(pickFileAsync));
        }

        public async Task<PickFileResult?> PickGGUFAsync()
        {
            if (_pickFileAsync is not null)
            {
                return await _pickFileAsync();
            }

            FileOpenPicker openGGUFPicker =
                new FileOpenPicker(App.MainWindow.AppWindow.Id);

            foreach (string fileType in AllowedFileTypes)
            {
                openGGUFPicker.FileTypeFilter.Add(fileType);
            }

            return await openGGUFPicker.PickSingleFileAsync();
        }

        internal async Task<ModelSelectionInput?> PickInputAsync(
            ModelSelectionInputNormalizer normalizer)
        {
            ArgumentNullException.ThrowIfNull(normalizer);

            PickFileResult? selectedFile = await PickGGUFAsync();
            return selectedFile is null
                ? null
                : normalizer.FromPickerPath(selectedFile.Path, isFolder: false);
        }
    }
}
