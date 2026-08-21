using System.Diagnostics;
using GraniteEdgeAI.Features.ModelImport;
using GraniteEdgeAI.Features.ModelImport.Selection;
using GraniteEdgeAI.Features.ModelInspection;
using GraniteEdgeAI.Features.ModelInspection.Services;
using GraniteEdgeAI.Features.Onboarding;
using GraniteEdgeAI.Features.Prompting;
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
    public async Task CanonicalDirectoryPromptsStopsCancelsAndNavigatesWithoutResidue()
    {
        await AssertNoOfficialWorkerProcessAsync();
        string package = PackagedCanonicalFixture();
        PageJourney failedJourney = await NavigateCanonicalAsync(package);
        Assert.AreEqual(128, failedJourney.Page.RequestedOpenVinoNewTokens);
        TextBox failedInput =
            (TextBox)failedJourney.Page.FindName("PromptInput");
        Button failedSend =
            (Button)failedJourney.Page.FindName("PromptSendButton");
        failedInput.Text = "hello";
        InvokeButton(failedSend, "default-limit prompt Send");
        await Require(failedJourney.Page.CurrentOpenVinoPromptTask)
            .WaitAsync(TimeSpan.FromSeconds(30));
        Assert.AreEqual(
            PromptTurnStatus.Failed,
            Require(failedJourney.Page.LastOpenVinoTurnResult).Status);
        await AssertNoOfficialWorkerProcessAsync();
        Assert.IsTrue(failedJourney.Frame.Navigate(typeof(ModelImportPage)));
        await Require(failedJourney.Page.CurrentOpenVinoCleanupTask)
            .WaitAsync(TimeSpan.FromSeconds(30));

        PageJourney journey = await NavigateCanonicalAsync(package);
        ModelInspectionPage page = journey.Page;
        page.RequestedOpenVinoNewTokens = 2;
        TextBox input = (TextBox)page.FindName("PromptInput");
        Button send = (Button)page.FindName("PromptSendButton");
        Button stop = (Button)page.FindName("PromptStopButton");
        Button cancel = (Button)page.FindName("PromptCancelButton");
        TextBlock response = (TextBlock)page.FindName("PromptResponseText");

        input.Text = "hello";
        InvokeButton(send, "first prompt Send");
        await Require(page.CurrentOpenVinoPromptTask)
            .WaitAsync(TimeSpan.FromSeconds(30));
        Assert.AreEqual("fixture", response.Text);
        Assert.AreEqual(
            PromptTurnStatus.Completed,
            Require(page.LastOpenVinoTurnResult).Status);

        input.Text = "hello";
        InvokeButton(send, "STOP prompt Send");
        InvokeButton(stop, "STOP");
        await Require(page.CurrentOpenVinoPromptTask)
            .WaitAsync(TimeSpan.FromSeconds(30));
        Assert.AreEqual(
            PromptTurnStatus.Stopped,
            Require(page.LastOpenVinoTurnResult).Status);
        Assert.IsTrue(send.IsEnabled, "STOP must return the same session to ready.");

        InvokeButton(cancel, "Cancel session");
        await Require(page.CurrentOpenVinoCancelTask)
            .WaitAsync(TimeSpan.FromSeconds(30));
        Assert.IsFalse(send.IsEnabled);
        Assert.IsFalse(stop.IsEnabled);

        Assert.IsTrue(journey.Frame.Navigate(typeof(ModelImportPage)));
        await Require(page.CurrentOpenVinoCleanupTask)
            .WaitAsync(TimeSpan.FromSeconds(30));
        await AssertNoOfficialWorkerProcessAsync();
    }

    private static async Task<PageJourney> NavigateCanonicalAsync(
        string package)
    {
        OnboardingShellPage shell = new();
        Frame frame = (Frame)shell.FindName("StageFrame");
        ModelImportPage import =
            Assert.IsInstanceOfType<ModelImportPage>(frame.Content);
        await import.SubmitInputAsync(new ModelSelectionInput(
            package,
            "TinySyntheticV1",
            isFolder: true));
        InvokeButton(
            (Button)import.FindName("ContinueToModelInspectionButton"),
            "OpenVINO Continue");

        ModelInspectionPage page =
            Assert.IsInstanceOfType<ModelInspectionPage>(frame.Content);
        Assert.AreEqual(OnboardingStage.InspectModel, shell.CurrentStage);
        await Require(page.CurrentOpenVinoInspectionTask)
            .WaitAsync(TimeSpan.FromSeconds(30));
        Assert.AreEqual(
            Visibility.Visible,
            ((Border)page.FindName("PromptSurface")).Visibility);
        Assert.AreEqual(
            "Requested CPU · Running CPU",
            ((TextBlock)page.FindName("OpenVinoExecutionEvidenceText")).Text);
        Assert.AreEqual(
            "Runtime 2026.3.0-22451-8a17657b995-releases/2026/3 · " +
            "GenAI 2026.3.0.0-3277-bd8d6542e3c · " +
            "Tokenizers 2026.3.0.0-703-183c6f25cda · " +
            "Worker manifest 0f656f6f2afe0b7246d0746f458ad6ed02e23be943145779b0160dd69aff2ebe",
            ((TextBlock)page.FindName("OpenVinoBuildEvidenceText")).Text);
        return new PageJourney(frame, page);
    }

    [UITestMethod]
    [TestCategory("WinUI")]
    [TestCategory("OfficialNative")]
    public async Task MissingOrTamperedPackagedWorkerRecoversWithoutLaunching()
    {
        await AssertNoOfficialWorkerProcessAsync();
        string package = PackagedCanonicalFixture();
        string sourceWorker = PackagedWorkerRoot();
        string operationRoot = Path.Combine(
            Path.GetTempPath(),
            $"granite-o1-task8-worker-negative-{Guid.NewGuid():N}");
        try
        {
            CopyDirectory(sourceWorker, operationRoot);
            File.Delete(Path.Combine(operationRoot, "worker-manifest.json"));
            await AssertControlledRecoveryAsync(operationRoot, package);

            CopyDirectory(sourceWorker, operationRoot, overwrite: true);
            await File.AppendAllTextAsync(
                Path.Combine(operationRoot, "licenses", "Apache_license.txt"),
                "tampered");
            await AssertControlledRecoveryAsync(operationRoot, package);
        }
        finally
        {
            if (Directory.Exists(operationRoot))
            {
                Directory.Delete(operationRoot, recursive: true);
            }
        }

        await AssertNoOfficialWorkerProcessAsync();
        Assert.IsFalse(Directory.Exists(operationRoot));
    }

    private static async Task AssertControlledRecoveryAsync(
        string workerRoot,
        string package)
    {
        ModelInspectionPage page = new();
        page.OpenVinoRouteServiceFactory = () =>
            ModelInspectionServiceComposition.CreateOpenVinoRouteService(
                workerRoot);
        page.ActivateOpenVinoInspection(new OpenVinoInspectionRequestedEventArgs(
            ModelSelectionOperationId.CreateNew(),
            package,
            "TinySyntheticV1"));

        await Require(page.CurrentOpenVinoInspectionTask)
            .WaitAsync(TimeSpan.FromSeconds(30));
        TextBlock response = (TextBlock)page.FindName("PromptResponseText");
        Assert.IsFalse(((Button)page.FindName("PromptSendButton")).IsEnabled);
        Assert.IsFalse(response.Text.Contains(package, StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(response.Text.Contains(workerRoot, StringComparison.OrdinalIgnoreCase));
        await page.RetireOpenVinoInspectionAsync();
        await AssertNoOfficialWorkerProcessAsync();
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
        Assert.IsTrue(File.Exists(Path.Combine(path, "worker-manifest.json")));
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

    private static void CopyDirectory(
        string source,
        string destination,
        bool overwrite = false)
    {
        Directory.CreateDirectory(destination);
        foreach (string directory in Directory.EnumerateDirectories(
                     source, "*", SearchOption.AllDirectories))
        {
            Directory.CreateDirectory(Path.Combine(
                destination,
                Path.GetRelativePath(source, directory)));
        }
        foreach (string file in Directory.EnumerateFiles(
                     source, "*", SearchOption.AllDirectories))
        {
            string target = Path.Combine(
                destination,
                Path.GetRelativePath(source, file));
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.Copy(file, target, overwrite);
        }
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

    private sealed record PageJourney(Frame Frame, ModelInspectionPage Page);
}
