using GraniteEdgeAI.Features.Onboarding;
using Microsoft.UI.Xaml;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace GraniteEdgeAI
{
    /// <summary>
    /// An empty window that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainWindow : Window
    {
        public MainWindow()
        {
            // Loads MainWindow.xaml and creates its named controls, including rootFrame.
            InitializeComponent();

            // Loads OnboardingShellPage inside rootFrame when the window is created.
            rootFrame.Navigate(typeof(OnboardingShellPage));
        }
    }
}
