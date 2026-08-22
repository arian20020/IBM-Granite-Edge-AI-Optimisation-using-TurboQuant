using GraniteEdgeAI.Features.GgufRuntime;
using GraniteEdgeAI.Features.ModelInspection.Presentation;
using GraniteEdgeAI.Features.Onboarding;
using GraniteEdgeAI.GgufRuntime.Capabilities.Manifest;
using GraniteEdgeAI.GgufRuntime.Transport;
using GraniteEdgeAI.GgufRuntime.WorkerClient;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.ComponentModel;
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
        private bool _productionChatOpenInProgress;

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
                onboarding.ProductionChatRequested +=
                    Onboarding_ProductionChatRequested;
            }
        }

        private async void Onboarding_ProductionChatRequested(
            object? sender,
            ModelInspectionChatRequestedEventArgs eventArguments)
        {
            if (_productionChatOpenInProgress)
            {
                eventArguments.ReportLaunchFailed();
                return;
            }

            _productionChatOpenInProgress = true;
            try
            {
                GgufChatLaunchRequest request =
                    GgufInspectedModelLaunchFactory.Create(
                        eventArguments.Request,
                        eventArguments.Execution,
                        GetPackagedGgufRuntimeRoot(AppContext.BaseDirectory));
                if (sender is OnboardingShellPage onboarding)
                {
                    onboarding.ChatPreviewRequested -=
                        Onboarding_ChatPreviewRequested;
                    onboarding.ProductionChatRequested -=
                        Onboarding_ProductionChatRequested;
                }

                await OpenProductionChatAsync(request);
            }
            catch (Exception exception) when (
                IsExpectedProductionChatFailure(exception))
            {
                eventArguments.ReportLaunchFailed();
                await ShowProductionChatFailureAsync();
            }
            catch
            {
                eventArguments.ReportLaunchFailed();
                throw;
            }
            finally
            {
                _productionChatOpenInProgress = false;
            }
        }

        internal static string GetPackagedGgufRuntimeRoot(
            string appBaseDirectory)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(appBaseDirectory);
            return Path.Combine(appBaseDirectory, "GgufRuntime");
        }

        private static bool IsExpectedProductionChatFailure(
            Exception exception) => exception is
                GgufChatLaunchException or
                GgufRuntimeTrustException or
                GgufTransportException or
                GgufWorkerPolicyException or
                IOException or
                UnauthorizedAccessException or
                Win32Exception or
                TimeoutException or
                OperationCanceledException;

        private async Task ShowProductionChatFailureAsync()
        {
            if (rootFrame.Content is not FrameworkElement content ||
                content.XamlRoot is null)
            {
                return;
            }

            var dialog = new ContentDialog
            {
                Title = "Could not open this model",
                Content =
                    "Granite Edge AI could not start local generation. " +
                    "The model was not opened and the preview response was not used.",
                CloseButtonText = "Close",
                XamlRoot = content.XamlRoot
            };
            await dialog.ShowAsync();
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
                throw new GgufChatLaunchException(
                    "chat-page-navigation-failed");
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
