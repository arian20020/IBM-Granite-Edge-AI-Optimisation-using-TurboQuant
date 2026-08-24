using Microsoft.Windows.Storage.Pickers;
using System;
using GraniteEdgeAI.Features.ModelImport.Selection;
using System.Threading.Tasks;

namespace GraniteEdgeAI.Features.ModelImport.FileImport.PickerRoute
{
    internal sealed class OpenVINOFolderPicker
    {
        private readonly Func<Task<PickFolderResult?>>? _pickFolderAsync;

        public OpenVINOFolderPicker()
        { }

        internal OpenVINOFolderPicker(Func<Task<PickFolderResult?>> pickFolderAsync)
        {
            _pickFolderAsync = pickFolderAsync ?? throw new ArgumentNullException(nameof(pickFolderAsync));
        }
    
        public async Task<PickFolderResult?> PickOpenVINOAsync()
        {
            if (_pickFolderAsync is not null)
            {
                return await _pickFolderAsync();
            }

            FolderPicker openOpenVINOPicker = new FolderPicker(App.MainWindow.AppWindow.Id);

            PickFolderResult? folder = await openOpenVINOPicker.PickSingleFolderAsync();

            if (folder == null)
            {
                return null;
            }

            return folder;
        }

        internal async Task<ModelSelectionInput?> PickInputAsync(
            ModelSelectionInputNormalizer normalizer)
        {
            ArgumentNullException.ThrowIfNull(normalizer);

            PickFolderResult? selectedFolder = await PickOpenVINOAsync();
            return selectedFolder is null
                ? null
                : normalizer.FromPickerPath(selectedFolder.Path, isFolder: true);
        }
    }

}
