using Microsoft.Windows.Storage.Pickers;
using System;
using System.Threading.Tasks;

namespace GraniteEdgeAI.Features.ModelImport.FileImport.PickerRoute
{
    class OpenVINOFolderPicker
    {
        public OpenVINOFolderPicker()
        { }
    
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