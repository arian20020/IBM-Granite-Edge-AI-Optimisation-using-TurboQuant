using GraniteEdgeAI.Features.GgufRuntime;
using GraniteEdgeAI.Features.Onboarding;
using Microsoft.UI.Xaml;
using System;
using System.IO;
using System.Threading.Tasks;

// To learn more about WinUI, the WinUI project structure,
// and more about our project templates, see: http://aka.ms/winui-project-info.

namespace GraniteEdgeAI
{
    /// <summary>
    /// An empty window that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainWindow : Window
    {
        private ChatDemoController? _chatDemoController;

        public MainWindow()
        {
            // Loads MainWindow.xaml and creates its named controls, including rootFrame.
            InitializeComponent();
            Title = "Granite Edge AI";
            string iconPath = Path.Combine(
                AppContext.BaseDirectory,
                "Assets",
                "Branding",
                "granite-edge-ai.ico");
            AppWindow.SetIcon(iconPath);

            // Loads OnboardingShellPage inside rootFrame when the window is created.
            ShowOnboarding();
        }

        private void ShowOnboarding()
        {
            rootFrame.Navigate(typeof(OnboardingShellPage));
            if (rootFrame.Content is OnboardingShellPage onboarding)
            {
                onboarding.ChatPreviewRequested += Onboarding_ChatPreviewRequested;
            }
        }

        private async void Onboarding_ChatPreviewRequested(
            object? sender,
            EventArgs eventArguments)
        {
            if (sender is OnboardingShellPage onboarding)
            {
                onboarding.ChatPreviewRequested -= Onboarding_ChatPreviewRequested;
            }

            rootFrame.Navigate(typeof(ChatPage));
            if (rootFrame.Content is not ChatPage chatPage)
            {
                return;
            }

            chatPage.ImportModelRequested += ChatPage_ImportModelRequested;
            _chatDemoController = new ChatDemoController(chatPage);
            await _chatDemoController.InitializeAsync();
        }

        internal async Task OpenProductionChatAsync(
            GgufChatLaunchRequest request)
        {
            ArgumentNullException.ThrowIfNull(request);
            if (_chatDemoController is not null)
            {
                await _chatDemoController.DisposeAsync();
                _chatDemoController = null;
            }

            rootFrame.Navigate(typeof(ChatPage));
            if (rootFrame.Content is not ChatPage chatPage)
            {
                ShowOnboarding();
                throw new InvalidOperationException(
                    "The application could not display the local chat page.");
            }

            chatPage.ImportModelRequested += ChatPage_ImportModelRequested;
            try
            {
                _chatDemoController = await ChatDemoController.CreateProductionAsync(
                    chatPage,
                    request,
                    System.Threading.CancellationToken.None);
                await _chatDemoController.InitializeAsync();
            }
            catch
            {
                chatPage.ImportModelRequested -= ChatPage_ImportModelRequested;
                if (_chatDemoController is not null)
                {
                    await _chatDemoController.DisposeAsync();
                    _chatDemoController = null;
                }

                ShowOnboarding();
                throw;
            }
        }

        private async void ChatPage_ImportModelRequested(
            object? sender,
            EventArgs eventArguments)
        {
            if (sender is ChatPage chatPage)
            {
                chatPage.ImportModelRequested -= ChatPage_ImportModelRequested;
            }

            if (_chatDemoController is not null)
            {
                await _chatDemoController.DisposeAsync();
                _chatDemoController = null;
            }

            ShowOnboarding();
        }
    }
}
