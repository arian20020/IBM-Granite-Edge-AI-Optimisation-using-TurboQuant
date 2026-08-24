using Microsoft.UI.Xaml;
using Microsoft.VisualStudio.TestTools.UnitTesting.AppContainer;
using GraniteEdgeAI.UnitTests.Features.HardwareInspection.Acceptance;

namespace GraniteEdgeAI.UnitTests;

public partial class UnitTestApp : Application
{
    private UnitTestAppWindow? _window;

    public UnitTestApp()
    {
        InitializeComponent();
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        if (HardwareInspectionProcessAcceptanceHost.TryParseActivation(
                Environment.GetCommandLineArgs(),
                out string resultToken))
        {
            _window = new UnitTestAppWindow();
            _window.Activate();
            UITestMethodAttribute.DispatcherQueue = _window.DispatcherQueue;
            _ = RunHardwareInspectionProcessAcceptanceAsync(resultToken);
            return;
        }

        Microsoft.VisualStudio.TestPlatform.TestExecutor.UnitTestClient.CreateDefaultUI();

        _window = new UnitTestAppWindow();
        _window.Activate();
        UITestMethodAttribute.DispatcherQueue = _window.DispatcherQueue;

        Microsoft.VisualStudio.TestPlatform.TestExecutor.UnitTestClient.Run(
            Environment.CommandLine);
    }

    private async Task RunHardwareInspectionProcessAcceptanceAsync(string resultToken)
    {
        int exitCode = 70;
        try
        {
            exitCode = await HardwareInspectionProcessAcceptanceHost.RunAsync(resultToken);
        }
        finally
        {
            try
            {
                _window?.Close();
            }
            finally
            {
                _window = null;
                Environment.Exit(exitCode);
            }
        }
    }
}
