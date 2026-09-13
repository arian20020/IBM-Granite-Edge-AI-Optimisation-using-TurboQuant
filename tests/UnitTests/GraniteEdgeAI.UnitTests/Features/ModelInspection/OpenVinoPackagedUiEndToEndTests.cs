using System.Diagnostics;
using System.Reflection;
using System.Security.Cryptography;
using GraniteEdgeAI.Features.ModelImport;
using GraniteEdgeAI.Features.ModelImport.FileImport;
using GraniteEdgeAI.Features.ModelImport.Selection;
using GraniteEdgeAI.Features.ModelInspection;
using GraniteEdgeAI.Features.ModelInspection.Controls;
using GraniteEdgeAI.Features.ModelInspection.Services;
using GraniteEdgeAI.Features.ModelInspection.Views;
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

    private static object PromptControl(ModelInspectionPage page, string name)
    {
        var chat = page.OpenVinoChatView;
        var composer = (GraniteEdgeAI.Features.GgufRuntime.Controls.ChatComposer)chat.FindName("Composer");
        return name switch
        {
            "PromptInput" => composer.FindName("PromptTextBox"),
            "PromptSendButton" => composer.FindName("SendButton"),
            "PromptStopButton" => composer.FindName("StopButton"),
            "PromptCancelButton" => chat.FindName("RouteCloseButton"),
            "PromptResponseText" => chat.FindName("RouteStatusText"),
            "PromptCapabilitySummary" => chat.FindName("RouteCapabilityText"),
            "PromptExecutionEvidenceText" => chat.FindName("RouteExecutionText"),
            "PromptBuildEvidenceText" => chat.FindName("RouteBuildText"),
            _ => throw new ArgumentOutOfRangeException(nameof(name))
        };
    }

    [UITestMethod]
    [TestCategory("WinUI")]
    [TestCategory("OfficialNative")]
    public Task CanonicalDirectoryPromptsStopsCancelsAndNavigatesWithoutResidue() =>
        WithJourneyActionPaletteAsync(
            CanonicalDirectoryPromptsStopsCancelsAndNavigatesWithoutResidueCoreAsync);

    private static async Task
        CanonicalDirectoryPromptsStopsCancelsAndNavigatesWithoutResidueCoreAsync()
    {
        await AssertNoOfficialWorkerProcessAsync();
        string package = PackagedCanonicalFixture();
        PageJourney failedJourney = await NavigateCanonicalAsync(package);
        Assert.AreEqual(128, failedJourney.Page.RequestedOpenVinoNewTokens);
        TextBox failedInput =
            (TextBox)PromptControl(failedJourney.Page, "PromptInput");
        Button failedSend =
            (Button)PromptControl(failedJourney.Page, "PromptSendButton");
        failedInput.Text = "hello";
        InvokeButton(failedSend, "default-limit prompt Send");
        await Require(failedJourney.Page.CurrentOpenVinoPromptTask)
            .WaitAsync(TimeSpan.FromSeconds(30));
        Assert.AreEqual(
            PromptTurnStatus.Failed,
            Require(failedJourney.Page.LastOpenVinoTurnResult).Status);
        await AssertNoOfficialWorkerProcessAsync();
        Assert.IsTrue(await failedJourney.Shell.ReturnToModelImportAsync());
        Assert.IsInstanceOfType<ModelImportPage>(failedJourney.Frame.Content);

        PageJourney journey = await NavigateCanonicalAsync(package);
        ModelInspectionPage page = journey.Page;
        page.RequestedOpenVinoNewTokens = 2;
        TextBox input = (TextBox)PromptControl(page, "PromptInput");
        Button send = (Button)PromptControl(page, "PromptSendButton");
        Button stop = (Button)PromptControl(page, "PromptStopButton");
        Button cancel = (Button)PromptControl(page, "PromptCancelButton");
        TextBlock response = (TextBlock)PromptControl(page, "PromptResponseText");

        input.Text = "hello";
        InvokeButton(send, "first prompt Send");
        Task firstTurn = Require(page.CurrentOpenVinoPromptTask);
        input.Text = "duplicate must not be submitted";
        object? duplicate = typeof(ModelInspectionPage).GetMethod("StartOpenVinoPrompt",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.Invoke(page, null);
        Assert.IsNull(duplicate, "A second send must not start while the first turn is pending.");
        Assert.AreSame(firstTurn, page.CurrentOpenVinoPromptTask);
        await Require(page.CurrentOpenVinoPromptTask)
            .WaitAsync(TimeSpan.FromSeconds(30));
        Assert.AreEqual("fixture", ((GraniteEdgeAI.Features.GgufRuntime.Controls.ChatMessageBubble)((ListView)page.OpenVinoChatView.FindName("TranscriptList")).Items.Last()).MessageContent);
        Assert.AreEqual(
            PromptTurnStatus.Completed,
            Require(page.LastOpenVinoTurnResult).Status);

        input.Text = "hello";
        InvokeButton(send, "STOP prompt Send");
        await WaitForPromptStateAsync(
            page,
            state => state.ActiveTurnId is not null,
            "worker-confirmed STOP ownership");
        InvokeButton(stop, "STOP");
        await Require(page.CurrentOpenVinoPromptTask)
            .WaitAsync(TimeSpan.FromSeconds(30));
        Assert.AreEqual(
            PromptTurnStatus.Stopped,
            Require(page.LastOpenVinoTurnResult).Status);
        Assert.IsTrue(send.IsEnabled, "STOP must return the same session to ready.");

        input.Text = "hello";
        InvokeButton(send, "post-STOP prompt Send");
        await Require(page.CurrentOpenVinoPromptTask)
            .WaitAsync(TimeSpan.FromSeconds(30));
        Assert.AreEqual("fixture", ((GraniteEdgeAI.Features.GgufRuntime.Controls.ChatMessageBubble)((ListView)page.OpenVinoChatView.FindName("TranscriptList")).Items.Last()).MessageContent);
        Assert.AreEqual(
            PromptTurnStatus.Completed,
            Require(page.LastOpenVinoTurnResult).Status);

        page.RequestedOpenVinoNewTokens = 32;
        input.Text = "hello";
        int completedTurnsBeforeCancellation =
            Require(page.CurrentPromptSurfaceState).CompletedTurnCount;
        InvokeButton(send, "active CANCEL prompt Send");
        PromptSurfaceState generating = await WaitForPromptStateAsync(
            page,
            state => state.ActiveTurnId is not null &&
                state.LastEventKind is PromptEventKind.GenerationConfirmed or
                    PromptEventKind.TextDelta,
            "worker-confirmed native generation ownership");
        Guid cancelledTurnId = generating.ActiveTurnId!.Value;
        InvokeButton(cancel, "Cancel session");
        await Require(page.CurrentOpenVinoCancelTask)
            .WaitAsync(TimeSpan.FromSeconds(30));
        await Require(page.CurrentOpenVinoPromptTask)
            .WaitAsync(TimeSpan.FromSeconds(30));
        PromptSurfaceState cancelled = await WaitForPromptStateAsync(
            page,
            state => state.LastEventKind == PromptEventKind.Cancelled &&
                state.ActiveTurnId is null,
            "cancelled session terminal");
        Assert.AreNotEqual(Guid.Empty, cancelledTurnId);
        Assert.AreEqual(
            completedTurnsBeforeCancellation,
            cancelled.CompletedTurnCount,
            "An actively cancelled native turn cannot publish TurnCompleted.");
        Assert.IsNull(page.LastOpenVinoTurnResult,
            "An actively cancelled turn has no successful turn result.");
        string responseAtCancellation = response.Text;
        long revisionAtCancellation = cancelled.EventRevision;
        await Task.Delay(250);
        Assert.AreEqual(responseAtCancellation, response.Text,
            "A cancelled native turn must not publish late output.");
        Assert.AreEqual(
            revisionAtCancellation,
            Require(page.CurrentPromptSurfaceState).EventRevision,
            "No token, text, or completion event may arrive after cancellation ownership.");
        Assert.IsFalse(send.IsEnabled);
        Assert.IsFalse(stop.IsEnabled);
        Assert.IsFalse(cancel.IsEnabled);

        Assert.IsTrue(await journey.Shell.ReturnToModelImportAsync());
        Assert.IsInstanceOfType<ModelImportPage>(journey.Frame.Content);
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
            (Button)import.FindName("BtnContinueToInspection"),
            "OpenVINO Continue");

        ModelInspectionPage page =
            Assert.IsInstanceOfType<ModelInspectionPage>(frame.Content);
        Assert.AreEqual(OnboardingStage.InspectModel, shell.CurrentStage);
        await Require(page.CurrentOpenVinoInspectionTask)
            .WaitAsync(TimeSpan.FromSeconds(30));
        Assert.AreEqual(
            Visibility.Collapsed,
            ((Border)page.FindName("PromptSurface")).Visibility);
        ModelInspectionPreviewProjection preview = Preview(page);
        Button renderedPrimary = Assert.IsInstanceOfType<Button>(
            preview.FindElement("BtnCheckHardwareFit"));
        Assert.AreEqual("Check hardware fit",
            preview.ActionPresentation.PrimaryAction.Text);
        Assert.IsTrue(preview.ActionPresentation.PrimaryAction.IsEnabled);
        Assert.AreEqual("Check hardware fit", renderedPrimary.Content);
        Assert.AreEqual(Visibility.Visible, renderedPrimary.Visibility);
        Assert.IsTrue(renderedPrimary.IsEnabled);
        Assert.IsNull(page.CurrentPromptSurfaceState);

        Assert.IsTrue(await page.ActivateOpenVinoChatAsync(
            CancellationToken.None));
        Assert.AreEqual(
            Visibility.Visible,
            ((Border)page.FindName("PromptSurface")).Visibility);
        Assert.AreEqual(
            "Requested CPU · Running CPU",
            ((TextBlock)PromptControl(page, "PromptExecutionEvidenceText")).Text);
        Assert.AreEqual(
            "Runtime 2026.3.0-22451-8a17657b995-releases/2026/3 · " +
            "GenAI 2026.3.0.0-3277-bd8d6542e3c · " +
            "Tokenizers 2026.3.0.0-703-183c6f25cda · " +
            $"Worker manifest {PackagedWorkerManifestSha256()}",
            ((TextBlock)PromptControl(page, "PromptBuildEvidenceText")).Text);
        return new PageJourney(shell, frame, page);
    }

    [UITestMethod]
    [TestCategory("WinUI")]
    [TestCategory("OfficialNative")]
    public Task MissingOrTamperedPackagedWorkerRecoversWithoutLaunching() =>
        WithJourneyActionPaletteAsync(
            MissingOrTamperedPackagedWorkerRecoversWithoutLaunchingCoreAsync);

    private static async Task
        MissingOrTamperedPackagedWorkerRecoversWithoutLaunchingCoreAsync()
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
        TextBlock response = (TextBlock)PromptControl(page, "PromptResponseText");
        Assert.AreEqual(
            Visibility.Collapsed,
            ((Border)page.FindName("PromptSurface")).Visibility);
        Assert.IsFalse(((Button)PromptControl(page, "PromptSendButton")).IsEnabled);
        Assert.IsFalse(response.Text.Contains(package, StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(response.Text.Contains(workerRoot, StringComparison.OrdinalIgnoreCase));
        ModelInspectionPreviewProjection failurePreview = Preview(page);
        Assert.AreEqual(
            Visibility.Visible,
            Assert.IsInstanceOfType<FrameworkElement>(
                failurePreview.FindElement("InspectionFailurePanel")).Visibility);
        string recoveryEvidence = response.Text + " " +
            failurePreview.ContentPresentation.DiagnosticCode;
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
            (Button)import.FindName("BtnContinueToInspection"),
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
        Assert.IsTrue(File.Exists(Path.Combine(path, "worker-manifest.json")));
        return path;
    }

    private static string PackagedWorkerManifestSha256() =>
        Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(Path.Combine(
            PackagedWorkerRoot(),
            "worker-manifest.json")))).ToLowerInvariant();

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

    private static async Task<PromptSurfaceState> WaitForPromptStateAsync(
        ModelInspectionPage page,
        Func<PromptSurfaceState, bool> predicate,
        string operation)
    {
        DateTimeOffset deadline = DateTimeOffset.UtcNow.AddSeconds(10);
        while (DateTimeOffset.UtcNow < deadline)
        {
            PromptSurfaceState? state = page.CurrentPromptSurfaceState;
            if (state is not null && predicate(state))
            {
                return state;
            }
            await Task.Delay(10);
        }

        PromptSurfaceState? last = page.CurrentPromptSurfaceState;
        Assert.Fail(
            $"Timed out waiting for {operation}; last event was " +
            $"{last?.LastEventKind}, active turn {last?.ActiveTurnId}, " +
            $"response '{last?.ResponseText}'.");
        throw new InvalidOperationException("Unreachable assertion path.");
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

    private static async Task WithJourneyActionPaletteAsync(
        Func<Task> operation)
    {
        var palette = new ResourceDictionary
        {
            Source = new Uri(
                "ms-appx:///Features/Onboarding/Presentation/GraniteJourneyActionPalette.xaml")
        };
        var mergedDictionaries =
            Application.Current.Resources.MergedDictionaries;
        mergedDictionaries.Add(palette);
        try
        {
            await operation();
        }
        finally
        {
            Assert.IsTrue(
                mergedDictionaries.Remove(palette),
                "The test must remove the exact action-palette instance it owns.");
        }
    }

    private static ModelInspectionPreviewProjection Preview(
        ModelInspectionPage page)
    {
        FieldInfo? field = typeof(ModelInspectionPage).GetField(
            "_activePreview",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.IsNotNull(field);
        return Assert.IsInstanceOfType<ModelInspectionPreviewProjection>(
            field.GetValue(page));
    }

    private sealed record PageJourney(
        OnboardingShellPage Shell,
        Frame Frame,
        ModelInspectionPage Page);
}
