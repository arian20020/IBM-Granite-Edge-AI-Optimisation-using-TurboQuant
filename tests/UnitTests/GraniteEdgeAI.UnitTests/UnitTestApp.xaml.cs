using Microsoft.UI.Xaml;
using Microsoft.VisualStudio.TestTools.UnitTesting.AppContainer;
using GraniteEdgeAI.UnitTests.Features.HardwareInspection.Acceptance;

namespace GraniteEdgeAI.UnitTests;

public partial class UnitTestApp : Application
{
    private UnitTestAppWindow? _window;

    public UnitTestApp()
    {
        UnhandledException += (_, args) =>
        {
            // Keep native UI failures diagnosable without treating them as handled.
            try
            {
                string details = args.Exception.ToString();
                string path = System.IO.Path.Combine(
                    Windows.Storage.ApplicationData.Current.LocalFolder.Path,
                    $"test-host-unhandled-{Environment.ProcessId}.log");
                System.IO.File.WriteAllText(path, details[..Math.Min(details.Length, 65536)]);
            }
            catch
            {
                // Logging must not replace or suppress the original test-host failure.
            }
        };
        InitializeComponent();
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        if (HardwareInspectionGate9AcceptanceHost.TryParseActivation(
                Environment.GetCommandLineArgs(),
                out string gate9ResultToken))
        {
            StartHardwareInspectionAcceptance(() =>
                HardwareInspectionGate9AcceptanceHost.RunAsync(gate9ResultToken));
            return;
        }

        if (HardwareInspectionProcessAcceptanceHost.TryParseActivation(
                Environment.GetCommandLineArgs(),
                out string processResultToken))
        {
            StartHardwareInspectionAcceptance(() =>
                HardwareInspectionProcessAcceptanceHost.RunAsync(processResultToken));
            return;
        }

        Microsoft.VisualStudio.TestPlatform.TestExecutor.UnitTestClient.CreateDefaultUI();

        _window = new UnitTestAppWindow();
        _window.Activate();
        UITestMethodAttribute.DispatcherQueue = _window.DispatcherQueue;

        Microsoft.VisualStudio.TestPlatform.TestExecutor.UnitTestClient.Run(
            Environment.CommandLine);
    }

    private void StartHardwareInspectionAcceptance(Func<Task<int>> run)
    {
        _window = new UnitTestAppWindow();
        _window.Activate();
        UITestMethodAttribute.DispatcherQueue = _window.DispatcherQueue;
        _ = RunHardwareInspectionAcceptanceAsync(run);
    }

    private async Task RunHardwareInspectionAcceptanceAsync(Func<Task<int>> run)
    {
        int exitCode = 70;
        try
        {
            exitCode = await run();
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
