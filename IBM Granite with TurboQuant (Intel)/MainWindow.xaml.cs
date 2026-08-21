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
