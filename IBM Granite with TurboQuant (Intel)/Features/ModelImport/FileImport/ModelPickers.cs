using Microsoft.Windows.Storage.Pickers;
using System;
using System.Threading.Tasks;


namespace GraniteEdgeAI.Features.ModelImport.FileImport
{
    class ModelPickers
    {
        public ModelPickers()
        {
        }


        public async Task<PickFileResult?> PickGGUFAsync()
        {

            FileOpenPicker openGGUFPicker = new FileOpenPicker(App.MainWindow.AppWindow.Id);

            openGGUFPicker.FileTypeFilter.Add(".gguf");

            PickFileResult? file = await openGGUFPicker.PickSingleFileAsync();

            if (file == null)
            {
                return null;
            }

            return file;
        }
        public async Task<PickFolderResult?> PickOpenVINOAsync()
        {
            FolderPicker openOpenVINOPicker = new FolderPicker(App.MainWindow.AppWindow.Id);

            PickFolderResult? folder = await openOpenVINOPicker.PickSingleFolderAsync();

            if (folder == null)
            {
                return null;
            }

            return folder;
        }
    }
}
