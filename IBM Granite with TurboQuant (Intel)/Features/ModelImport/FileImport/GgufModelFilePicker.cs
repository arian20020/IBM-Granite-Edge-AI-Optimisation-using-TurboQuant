using Microsoft.Windows.Storage.Pickers;
using System;
using System.Threading.Tasks;


namespace GraniteEdgeAI.Features.ModelImport.FileImport
{
    class GgufModelFilePicker
    {
        public GgufModelFilePicker()
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
    }
}
