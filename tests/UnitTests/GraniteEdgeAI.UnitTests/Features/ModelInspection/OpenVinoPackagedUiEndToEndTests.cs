using System.Diagnostics;
using GraniteEdgeAI.Features.HardwareInspection;
using GraniteEdgeAI.Features.ModelImport;
using GraniteEdgeAI.Features.ModelImport.FileImport;
using GraniteEdgeAI.Features.ModelImport.Selection;
using GraniteEdgeAI.Features.ModelInspection;
using GraniteEdgeAI.Features.ModelInspection.Controls;
using GraniteEdgeAI.Features.ModelInspection.Handoff;
using GraniteEdgeAI.Features.ModelInspection.Services;
using GraniteEdgeAI.Features.Onboarding;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation.Peers;
using Microsoft.UI.Xaml.Automation.Provider;
using Microsoft.UI.Xaml.Controls;
using Microsoft.VisualStudio.TestTools.UnitTesting.AppContainer;

namespace GraniteEdgeAI.UnitTests.Features.ModelInspection;

[TestClass]
[DoNotParallelize]
public sealed class OpenVinoPackagedUiEndToEndTests
{
    [UITestMethod]
    [TestCategory("WinUI")]
    [TestCategory("OfficialNative")]
    public async Task CanonicalDirectoryContinuesToSharedHardwareFlowWithoutResidue()
    {
        await AssertNoOfficialWorkerProcessAsync();
        _ = PackagedWorkerRoot();
        string package = PackagedCanonicalFixture();
        PageJourney journey = await NavigateCanonicalAsync(package);
        ModelInspectionPage page = journey.Page;
        var actions = (InspectionActionCard)page.FindName(
            "InspectionActionCardControl");
        Button checkHardware = (Button)actions.FindName("PrimaryActionButton");
        Assert.AreEqual("Check hardware", checkHardware.Content);
        Assert.IsTrue(checkHardware.IsEnabled);
        InvokeButton(checkHardware, "Check hardware");

        HardwareInspectionPage hardware =
            Assert.IsInstanceOfType<HardwareInspectionPage>(journey.Frame.Content);
        Assert.AreEqual(OnboardingStage.CheckHardwareFit, journey.Shell.CurrentStage);
        Assert.AreEqual(
            ModelInspectionRouteKind.OpenVino,
            hardware.OpaqueModelHandoff!.Route);
        await AssertNoOfficialWorkerProcessAsync();
    }

    private static async Task<PageJourney> NavigateCanonicalAsync(
        string package)
    {
        OnboardingShellPage shell = new();
        Frame frame = (Frame)shell.FindName("StageFrame");
        ModelImportPage import = new(
            () => Task.FromResult(ModelFormatSelection.OpenVino),
            () => throw new AssertFailedException(
                "OpenVINO selection must not invoke the GGUF picker."),
            pickOpenVinoPathAsync: () => Task.FromResult<string?>(package));
        frame.Content = import;
        shell.AttachModelImportPage(import);
        await import.BrowseFilesAsync();
        InvokeButton(
            (Button)import.FindName("ContinueToModelInspectionButton"),
            "OpenVINO Continue");

        ModelInspectionPage page =
            Assert.IsInstanceOfType<ModelInspectionPage>(frame.Content);
        Assert.AreEqual(OnboardingStage.InspectModel, shell.CurrentStage);
        await Require(page.CurrentOpenVinoInspectionTask)
            .WaitAsync(TimeSpan.FromSeconds(30));
        Assert.AreEqual(
            Visibility.Collapsed,
            ((Border)page.FindName("PromptSurface")).Visibility);
        return new PageJourney(shell, frame, page);
    }

    [UITestMethod]
    [TestCategory("WinUI")]
    [TestCategory("OfficialNative")]
    public async Task MissingOrTamperedPackagedWorkerRecoversWithoutLaunching()
    {
        await AssertNoOfficialWorkerProcessAsync();
        string package = PackagedCanonicalFixture();
        string workerRoot = PackagedWorkerRoot();
        string manifest = Path.Combine(workerRoot, "worker-manifest.json");
        string manifestBackup = manifest + $".task8-backup-{Guid.NewGuid():N}";
        string license = Path.Combine(workerRoot, "licenses", "Apache_license.txt");
        byte[] originalLicense = await File.ReadAllBytesAsync(license);
        try
        {
            File.Move(manifest, manifestBackup);
            await AssertDefaultControlledRecoveryAsync(package, workerRoot);
            File.Move(manifestBackup, manifest);

            await File.AppendAllTextAsync(license, "tampered");
            await AssertDefaultControlledRecoveryAsync(package, workerRoot);
        }
        finally
        {
            await File.WriteAllBytesAsync(license, originalLicense);
            if (File.Exists(manifestBackup))
            {
                if (File.Exists(manifest))
                {
                    File.Delete(manifest);
                }
                File.Move(manifestBackup, manifest);
            }
        }

        await AssertNoOfficialWorkerProcessAsync();
        Assert.IsFalse(File.Exists(manifestBackup));
    }

    private static async Task AssertDefaultControlledRecoveryAsync(
        string package,
        string workerRoot)
    {
        PageJourney journey = await StartJourneyAsync(package);
        ModelInspectionPage page = journey.Page;

        await Require(page.CurrentOpenVinoInspectionTask)
            .WaitAsync(TimeSpan.FromSeconds(30));
        TextBlock response = (TextBlock)page.FindName("PromptResponseText");
        Assert.IsFalse(((Button)page.FindName("PromptSendButton")).IsEnabled);
        Assert.IsFalse(response.Text.Contains(package, StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(response.Text.Contains(workerRoot, StringComparison.OrdinalIgnoreCase));
        InspectionContentCard content = (InspectionContentCard)page.FindName(
            "InspectionContentCardControl");
        string recoveryEvidence = response.Text + " " +
            content.Presentation.DiagnosticCode;
        Assert.IsTrue(
            recoveryEvidence.Contains("runtime_load_failed", StringComparison.Ordinal) ||
            recoveryEvidence.Contains("runtime_manifest_invalid", StringComparison.Ordinal) ||
            recoveryEvidence.Contains("runtime_integrity_failed", StringComparison.Ordinal),
            $"Expected a fixed path-free recovery code, actual: {recoveryEvidence}");
        Assert.IsFalse(recoveryEvidence.Contains('\\'));
        Assert.IsFalse(recoveryEvidence.Contains(":/", StringComparison.Ordinal));
        await journey.Shell.ShutdownAsync();
        await AssertNoOfficialWorkerProcessAsync();
    }

    private static async Task<PageJourney> StartJourneyAsync(string package)
    {
        OnboardingShellPage shell = new();
        Frame frame = (Frame)shell.FindName("StageFrame");
        ModelImportPage import = new(
            () => Task.FromResult(ModelFormatSelection.OpenVino),
            () => throw new AssertFailedException(
                "OpenVINO selection must not invoke the GGUF picker."),
            pickOpenVinoPathAsync: () => Task.FromResult<string?>(package));
        frame.Content = import;
        shell.AttachModelImportPage(import);
        await import.BrowseFilesAsync();
        InvokeButton(
            (Button)import.FindName("ContinueToModelInspectionButton"),
            "OpenVINO Continue");
        return new PageJourney(
            shell,
            frame,
            Assert.IsInstanceOfType<ModelInspectionPage>(frame.Content));
    }

    private static string PackagedCanonicalFixture()
    {
        string path = Path.Combine(
            AppContext.BaseDirectory,
            "TestFixtures",
            "OpenVINO",
            "GenAI",
            "TinySyntheticV1",
            "package");
        Assert.IsTrue(Directory.Exists(path),
            "The canonical Task 5 fixture must be present in the packaged UI recipe.");
        return path;
    }

    private static string PackagedWorkerRoot()
    {
        string path = Path.Combine(
            AppContext.BaseDirectory,
            "OpenVino",
            "Official",
            "Worker");
        if (!File.Exists(Path.Combine(path, "worker-manifest.json")))
        {
            Assert.Inconclusive(
                "The official OpenVINO worker package is unavailable in this merge-only Debug build.");
        }
        return path;
    }

    private static void InvokeButton(Button button, string operation)
    {
        Assert.IsTrue(
            button.IsEnabled,
            $"{operation} button must be enabled before invocation.");
        ButtonAutomationPeer peer = new(button);
        try
        {
            ((IInvokeProvider)peer.GetPattern(PatternInterface.Invoke)).Invoke();
        }
        catch (Exception error)
        {
            Assert.Fail(
                $"{operation} invocation failed while enabled: " +
                $"{error.GetType().Name} {error.Message}");
        }
    }

    private static T Require<T>(T? value) where T : class
    {
        Assert.IsNotNull(value);
        return value!;
    }

    private static async Task AssertNoOfficialWorkerProcessAsync()
    {
        DateTimeOffset deadline = DateTimeOffset.UtcNow.AddSeconds(3);
        while (DateTimeOffset.UtcNow < deadline)
        {
            Process[] processes = Process.GetProcessesByName(
                "OpenVinoOfficial.Worker");
            try
            {
                if (processes.Length == 0)
                {
                    return;
                }
            }
            finally
            {
                foreach (Process process in processes)
                {
                    process.Dispose();
                }
            }
            await Task.Delay(25);
        }

        Assert.Fail("An official OpenVINO worker process remained after UI cleanup.");
    }

    private sealed record PageJourney(
        OnboardingShellPage Shell,
        Frame Frame,
        ModelInspectionPage Page);
}
