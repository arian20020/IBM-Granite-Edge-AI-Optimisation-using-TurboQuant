using Microsoft.UI.Xaml;
using System.Threading.Tasks;

namespace GraniteEdgeAI.Features.ModelImport.FileImport
{
    /// <summary>
    /// Displays the WinUI model-format dialog for the pure import workflow.
    /// </summary>
    internal sealed class ModelFormatSelectionDialogService
        : IModelFormatSelectionService
    {
        // Identifies the visual tree that must host the ContentDialog.
        private readonly XamlRoot _xamlRoot;

        /// <summary>
        /// Creates the service for the page that is opening the dialog.
        /// </summary>
        public ModelFormatSelectionDialogService(XamlRoot xamlRoot)
        {
            // A ContentDialog cannot be displayed without a valid XamlRoot.
            _xamlRoot = xamlRoot
                ?? throw new System.ArgumentNullException(nameof(xamlRoot));
        }

        /// <summary>
        /// Displays the format dialog and returns the format recorded by it.
        /// </summary>
        public async Task<ModelFormatSelection> SelectFormatAsync()
        {
            // Create a new dialog for this single interaction.
            ModelFormatSelectionCard formatSelectionDialog =
                new ModelFormatSelectionCard();

            // Attach the dialog to the page's current WinUI visual tree.
            formatSelectionDialog.XamlRoot = _xamlRoot;

            // Wait without blocking the UI thread until the dialog closes.
            await formatSelectionDialog.ShowAsync();

            // Return the custom choice recorded before the dialog was hidden.
            return formatSelectionDialog.SelectedFormat;
        }
    }
}
