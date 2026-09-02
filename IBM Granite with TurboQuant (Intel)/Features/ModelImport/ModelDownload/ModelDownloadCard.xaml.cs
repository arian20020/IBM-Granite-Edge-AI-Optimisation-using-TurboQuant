using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;

namespace GraniteEdgeAI.Features.ModelImport.ModelDownload
{
    /// <summary>
    /// Displays the currently selected recommended-model profile.
    /// </summary>
    public sealed partial class ModelDownloadCard : UserControl
    {
        public ModelDownloadCard()
        {
            // Loads and connects the controls declared in ModelCard.xaml.
            InitializeComponent();

            // Ensures the initial label matches the slider's starting value.
            UpdateModelScaleLabel(ModelScaleSlider.Value);
        }

        /*
            Runs every time the slider value changes.

            This is currently view-only behaviour because it changes visible text.
            The final model-selection logic will later move into
            ModelImportViewModel.
        */
        private void ModelScaleSlider_ValueChanged(
            object sender,
            RangeBaseValueChangedEventArgs e)
        {
            UpdateModelScaleLabel(e.NewValue);
        }

        // Updates the visible preference label above the slider.
        private void UpdateModelScaleLabel(double sliderValue)
        {
            /*
                The event can potentially run while XAML is still being loaded.

                This check prevents us from accessing the TextBlock before
                WinUI has created it.
            */
            if (ModelScaleValueText is null)
            {
                return;
            }

            ModelScaleValueText.Text =
                GetModelScaleLabel(sliderValue);
        }

        /*
            Converts the continuous 0–100 slider value into five
            user-friendly preference descriptions.

            The slider itself remains continuous—it does not snap
            to these five categories.
        */
        private static string GetModelScaleLabel(double sliderValue)
        {
            return sliderValue switch
            {
                < 20 => "Maximum efficiency",
                < 40 => "Efficient",
                < 60 => "Balanced",
                < 80 => "High capability",
                _ => "Maximum capability"
            };
        }
    }
}