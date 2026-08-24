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

#if COMPATIBILITY_FIXTURE_GALLERY
            // Opens straight onto the compatibility screen gallery so every state
            // can be reviewed. Nine of the ten cannot be reached by running the
            // real flow, because the checks that would feed them do not exist
            // yet. Inert unless COMPATIBILITY_FIXTURE_GALLERY is defined, so a
            // normal build is untouched by this.
            rootFrame.Navigate(
                typeof(Features.ModelHardwareCompatibility.DebugFixtures
                    .CompatibilityFixtureGalleryPage));
#else
            // Loads OnboardingShellPage inside rootFrame when the window is created.
            rootFrame.Navigate(typeof(OnboardingShellPage));
#endif
            AppWindow.Closing += AppWindow_Closing;
        }

        private bool _shutdownStarted;
        private bool _shutdownComplete;

        private async void AppWindow_Closing(
            Microsoft.UI.Windowing.AppWindow sender,
            Microsoft.UI.Windowing.AppWindowClosingEventArgs eventArguments)
        {
            if (_shutdownComplete)
            {
                return;
            }

            eventArguments.Cancel = true;
            if (_shutdownStarted)
            {
                return;
            }

            _shutdownStarted = true;
            try
            {
                if (rootFrame.Content is OnboardingShellPage shell)
                {
                    await shell.ShutdownAsync();
                }
            }
            finally
            {
                _shutdownComplete = true;
                Close();
            }
        }
    }
}
