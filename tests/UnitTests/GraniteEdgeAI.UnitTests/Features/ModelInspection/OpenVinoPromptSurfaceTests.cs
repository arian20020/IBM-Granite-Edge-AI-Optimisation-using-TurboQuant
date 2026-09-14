using GraniteEdgeAI.Features.ModelInspection;
using GraniteEdgeAI.Features.ModelInspection.Controls;
using GraniteEdgeAI.Features.ModelInspection.Contracts;
using GraniteEdgeAI.Features.ModelInspection.Models;
using GraniteEdgeAI.Features.ModelInspection.Views;
using GraniteEdgeAI.Features.OpenVinoRoute;
using GraniteEdgeAI.Features.Prompting;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Automation.Peers;
using Microsoft.UI.Xaml.Automation.Provider;
using Microsoft.UI.Xaml.Controls;
using Microsoft.VisualStudio.TestTools.UnitTesting.AppContainer;
using System.Reflection;
using System.Threading;
using System.Windows.Input;
using Windows.System;

namespace GraniteEdgeAI.UnitTests.Features.ModelInspection;

[TestClass]
public sealed class OpenVinoPromptSurfaceTests
{
    private sealed class ReducedMotionSettings : GraniteEdgeAI.Features.ModelInspection.Presentation.IModelInspectionMotionSettings
    {
        public bool AnimationsEnabled => false;
        public event EventHandler? AnimationsEnabledChanged { add { } remove { } }
        public void Dispose() { }
    }

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
    public void SharedPageOwnsOneAccessibleRouteNeutralPromptSurface()
    {
        ModelInspectionPage page = new();
        Border surface = Assert.IsInstanceOfType<Border>(
            page.FindName("PromptSurface"));
        TextBox prompt = Assert.IsInstanceOfType<TextBox>(
            PromptControl(page, "PromptInput"));
        Button send = Assert.IsInstanceOfType<Button>(PromptControl(page, "PromptSendButton"));
        Button stop = Assert.IsInstanceOfType<Button>(PromptControl(page, "PromptStopButton"));
        Button cancel = Assert.IsInstanceOfType<Button>(PromptControl(page, "PromptCancelButton"));
        TextBlock response = Assert.IsInstanceOfType<TextBlock>(
            PromptControl(page, "PromptResponseText"));
        TextBlock capability = Assert.IsInstanceOfType<TextBlock>(
            PromptControl(page, "PromptCapabilitySummary"));
        TextBlock executionEvidence = Assert.IsInstanceOfType<TextBlock>(
            PromptControl(page, "PromptExecutionEvidenceText"));
        TextBlock buildEvidence = Assert.IsInstanceOfType<TextBlock>(
            PromptControl(page, "PromptBuildEvidenceText"));

        Assert.AreEqual(Visibility.Collapsed, surface.Visibility);
        Assert.AreEqual("Message", AutomationProperties.GetName(prompt));
        Assert.AreEqual("Send message", AutomationProperties.GetName(send));
        Assert.AreEqual("Stop generation", AutomationProperties.GetName(stop));
        Assert.AreEqual("Close local session", AutomationProperties.GetName(cancel));
        Assert.AreEqual(AutomationLiveSetting.Polite,
            AutomationProperties.GetLiveSetting(response));
        Assert.AreEqual(string.Empty, capability.Text);
        Assert.AreEqual(
            "Requested and actual local execution device",
            AutomationProperties.GetName(executionEvidence));
        Assert.AreEqual(
            "Verified local runtime build evidence",
            AutomationProperties.GetName(buildEvidence));
        Assert.IsTrue(prompt.AcceptsReturn);
        Assert.AreEqual(TextWrapping.Wrap, prompt.TextWrapping);
        Assert.IsFalse(prompt.UseSystemFocusVisuals, "The shared composer supplies its outer focus indicator.");
        Assert.IsInstanceOfType<Border>(((GraniteEdgeAI.Features.GgufRuntime.Controls.ChatComposer)page.OpenVinoChatView.FindName("Composer")).FindName("ComposerFocusVisual"));
        Assert.IsTrue(send.UseSystemFocusVisuals);
        Assert.IsTrue(stop.UseSystemFocusVisuals);
        Assert.IsTrue(cancel.UseSystemFocusVisuals);
    }

    [UITestMethod]
    [TestCategory("WinUI")]
    public void PromptSurfaceUsesFlatSharedChatAndScalesNaturally()
    {
        ModelInspectionPage page = new();
        Border surface = Assert.IsInstanceOfType<Border>(page.FindName("PromptSurface"));
        TextBox prompt = Assert.IsInstanceOfType<TextBox>(PromptControl(page, "PromptInput"));

        Assert.AreEqual(
            new CornerRadius(0),
            surface.CornerRadius);
        Assert.AreEqual(
            new Thickness(0),
            surface.Padding);
        Assert.IsTrue(prompt.IsTextScaleFactorEnabled);
        Assert.IsTrue(double.IsNaN(prompt.Height));
    }

    [UITestMethod]
    [TestCategory("WinUI")]
    public void PromptKeyboardGestureSendsOnEnterAndKeepsShiftEnterForNewlines()
    {
        MethodInfo? method = typeof(ModelInspectionPage).GetMethod(
            "IsPromptSendKey",
            BindingFlags.Static | BindingFlags.NonPublic);
        Assert.IsNotNull(method);

        Assert.IsTrue(InvokePromptSendKey(method, VirtualKey.Enter, false));
        Assert.IsFalse(InvokePromptSendKey(method, VirtualKey.Enter, true));
        Assert.IsFalse(InvokePromptSendKey(method, VirtualKey.Space, false));
    }

    [UITestMethod]
    [TestCategory("WinUI")]
    public void InspectionStartupFailureUsesInspectionCardsAndNeverPromptSurface()
    {
        ModelInspectionPage page = new();
        SelectOpenVinoPreview(page);
        MethodInfo? method = typeof(ModelInspectionPage).GetMethod(
            "ApplyOpenVinoInspectionFailurePresentation",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.IsNotNull(method);

        method.Invoke(page, new object[]
        {
            "runtime_load_failed",
            "The verified OpenVINO worker is unavailable.",
            "Repair or reinstall the app, then retry."
        });

        Border promptSurface = Assert.IsInstanceOfType<Border>(
            page.FindName("PromptSurface"));
        ModelInspectionPreviewProjection preview = Preview(page);
        FrameworkElement failurePanel = PreviewElement<FrameworkElement>(
            preview,
            "InspectionFailurePanel");
        Button retry = PreviewElement<Button>(preview, "BtnRetryOpenVinoDependency");

        Assert.AreEqual(Visibility.Collapsed, promptSurface.Visibility);
        Assert.AreEqual(Visibility.Visible, failurePanel.Visibility);
        Assert.AreEqual("Retry inspection", retry.Content);
        Assert.IsFalse(retry.IsEnabled, "This isolated failure has no retry request to execute.");
        Assert.AreEqual(InspectionContentCardMode.OperationalFailure,
            preview.ContentPresentation.Mode);
        Assert.AreEqual("runtime_load_failed", preview.ContentPresentation.DiagnosticCode);
        Assert.AreEqual(Visibility.Visible,
            preview.ContentPresentation.DiagnosticCodeVisibility);
        Assert.AreEqual(InspectionActionCardMode.Result, preview.ActionPresentation.Mode);
    }

    [UITestMethod]
    [TestCategory("WinUI")]
    public void ConversionProgressBindsTheVisibleCancelWithoutAccumulatingHandlers()
    {
        ModelInspectionPreviewProjection preview =
            new ModelInspectionOpenVinoPreviewView().PreviewProjection;
        CountingCommand command = new();
        object parameter = new();
        InspectionActionCardPresentation actions = new()
        {
            Mode = InspectionActionCardMode.Inspecting,
            CancelAction = new InspectionActionPresentation
            {
                Text = "Stop conversion",
                AutomationName = "Stop the active OpenVINO conversion",
                ActionId = "cancel-openvino-conversion",
                Visibility = Visibility.Visible,
                Command = command,
                CommandParameter = parameter
            }
        };

        preview.ShowOpenVinoSpecialProgress(
            "Preparing OpenVINO package",
            "Converting",
            "Step 2 of 6",
            Array.Empty<InspectionContentItemPresentation>());
        preview.ApplyActions(actions);

        Button conversionCancel = PreviewElement<Button>(
            preview,
            "BtnCancelOpenVinoConversion");
        Assert.AreSame(conversionCancel, preview.CancelActionButton);
        Assert.AreSame(conversionCancel, preview.FindElement("CancelActionButton"));
        Assert.AreEqual(Visibility.Visible, conversionCancel.Visibility);
        Assert.IsTrue(conversionCancel.IsEnabled);
        InvokeButton(conversionCancel);
        Assert.AreEqual(1, command.ExecuteCount);
        Assert.AreSame(parameter, command.LastParameter);

        preview.ApplyActions(actions);
        InvokeButton(conversionCancel);
        Assert.AreEqual(2, command.ExecuteCount,
            "Reapplying the presentation must replace, not accumulate, the click handler.");

        preview.ApplyContent(new InspectionContentCardPresentation
        {
            Mode = InspectionContentCardMode.Progress
        });
        Assert.AreSame(
            PreviewElement<Button>(preview, "BtnCancelInspection"),
            preview.CancelActionButton);
    }

    [UITestMethod]
    [TestCategory("WinUI")]
    public void DirectOpenVinoFailureSettersProjectAndClearActualTerminalEvidence()
    {
        ModelInspectionPreviewProjection preview =
            new ModelInspectionOpenVinoPreviewView().PreviewProjection;
        InspectionOutcomePresentation firstOutcome = new()
        {
            Kind = InspectionOutcomePresentationKind.OperationalFailure,
            Tone = InspectionOutcomeTone.Error,
            Title = "First failure",
            Message = "First visible failure message",
            AutomationName = "First failure. First visible failure message."
        };

        preview.ApplyOutcome(firstOutcome);
        preview.ApplyContent(new InspectionContentCardPresentation
        {
            Mode = InspectionContentCardMode.OperationalFailure,
            DiagnosticCode = "first_runtime_code",
            DiagnosticCodeVisibility = Visibility.Visible,
            DiagnosticStatus = InspectionContentStatus.Error,
            SupportingText = "First recovery instruction",
            SupportingTextVisibility = Visibility.Visible
        });

        AssertTerminalEvidence(
            preview,
            "First failure",
            "First visible failure message",
            "first_runtime_code",
            "First recovery instruction");
        Assert.AreSame(
            PreviewElement<TextBlock>(preview, "InspectionFailureHeading"),
            preview.OutcomeFocusTarget);

        preview.ApplyOutcome(new InspectionOutcomePresentation
        {
            Kind = InspectionOutcomePresentationKind.OperationalFailure,
            Tone = InspectionOutcomeTone.Error,
            Title = "Second failure",
            Message = "Second visible failure message",
            AutomationName = "Second failure. Second visible failure message."
        });
        preview.ApplyContent(new InspectionContentCardPresentation
        {
            Mode = InspectionContentCardMode.OperationalFailure,
            DiagnosticCode = "second_runtime_code",
            DiagnosticCodeVisibility = Visibility.Visible,
            DiagnosticStatus = InspectionContentStatus.Error,
            SupportingText = "Second recovery instruction",
            SupportingTextVisibility = Visibility.Visible
        });
        AssertTerminalEvidence(
            preview,
            "Second failure",
            "Second visible failure message",
            "second_runtime_code",
            "Second recovery instruction");

        preview.ApplyContent(new InspectionContentCardPresentation
        {
            Mode = InspectionContentCardMode.OperationalFailure,
            DiagnosticCodeVisibility = Visibility.Collapsed,
            SupportingTextVisibility = Visibility.Collapsed
        });
        TextBlock diagnostic = PreviewElement<TextBlock>(
            preview,
            "InspectionFailureDiagnostic");
        Assert.AreEqual(string.Empty, diagnostic.Text);
        Assert.AreEqual(string.Empty, AutomationProperties.GetName(diagnostic));
        Assert.AreEqual(
            Visibility.Collapsed,
            PreviewElement<FrameworkElement>(
                preview,
                "InspectionFailureDiagnosticContainer").Visibility);
        Assert.AreEqual(
            Visibility.Collapsed,
            PreviewElement<TextBlock>(preview, "InspectionFailureRecovery").Visibility);
    }

    [UITestMethod]
    [TestCategory("WinUI")]
    public void ConversionRecoveryUsesItsTypedActionToKeepBothTerminalActions()
    {
        ModelInspectionPreviewProjection preview =
            new ModelInspectionOpenVinoPreviewView().PreviewProjection;
        preview.ApplyOutcome(new InspectionOutcomePresentation
        {
            Kind = InspectionOutcomePresentationKind.Invalid,
            Tone = InspectionOutcomeTone.Error,
            Title = "Conversion failed",
            Message = "The verified conversion did not publish a package."
        });
        preview.ApplyContent(new InspectionContentCardPresentation
        {
            Mode = InspectionContentCardMode.Invalid,
            DiagnosticCode = "conversion_output_invalid",
            DiagnosticCodeVisibility = Visibility.Visible,
            SupportingText = "Retry conversion or choose another model.",
            SupportingTextVisibility = Visibility.Visible
        });
        preview.ApplyActions(new InspectionActionCardPresentation
        {
            Mode = InspectionActionCardMode.Result,
            SecondaryActionOne = VisibleAction(
                "Choose another model",
                "choose-another-model"),
            PrimaryAction = VisibleAction(
                "Retry conversion",
                "retry-openvino-conversion")
        });

        Assert.AreEqual(
            Visibility.Visible,
            PreviewElement<FrameworkElement>(
                preview,
                "OpenVinoConversionFailedPanel").Visibility);
        Assert.AreEqual(
            "conversion_output_invalid",
            PreviewElement<TextBlock>(
                preview,
                "OpenVinoConversionFailedDiagnostic").Text);
        Assert.AreEqual(
            "Retry conversion or choose another model.",
            PreviewElement<TextBlock>(
                preview,
                "OpenVinoConversionFailedRecovery").Text);
        Assert.AreEqual(
            Visibility.Visible,
            PreviewElement<Button>(
                preview,
                "BtnChooseAnotherModelConversionFailed").Visibility);
        Assert.AreEqual(
            Visibility.Visible,
            PreviewElement<Button>(
                preview,
                "BtnRetryOpenVinoConversionFailed").Visibility);
    }

    [UITestMethod]
    [TestCategory("WinUI")]
    public void OpenVinoPresentationLifecyclePublishesTruthfulFooterStatus()
    {
        ModelInspectionPage page = new();
        SelectOpenVinoPreview(page);
        MethodInfo inspecting = RequirePrivateMethod(
            "ApplyOpenVinoInspectingPresentation");
        MethodInfo ready = RequirePrivateMethod(
            "ApplyOpenVinoReadyPresentation");
        MethodInfo cancelled = RequirePrivateMethod(
            "ApplyOpenVinoCancelledPresentation");
        MethodInfo failed = RequirePrivateMethod(
            "ApplyOpenVinoInspectionFailurePresentation");
        List<InspectionFooterStatus> observed = [];
        page.FooterStatusChanged += (_, eventArguments) =>
            observed.Add(eventArguments.Status);

        ready.Invoke(page, new object[]
        {
            new OpenVinoRouteInspectionResult(
                OpenVinoRouteInspectionOutcome.Ready,
                HandoffLease: null,
                Failure: null,
                Configuration: null)
        });
        Assert.AreEqual(
            InspectionFooterStatus.Complete,
            page.CurrentFooterStatus,
            "A completed OpenVINO inspection must not leave the onboarding footer in progress.");

        inspecting.Invoke(page, new object[] { "granite-openvino" });
        Assert.AreEqual(
            InspectionFooterStatus.InProgress,
            page.CurrentFooterStatus,
            "A fresh OpenVINO attempt must restore the active footer state.");

        cancelled.Invoke(page, null);
        Assert.AreEqual(
            InspectionFooterStatus.NotComplete,
            page.CurrentFooterStatus);

        inspecting.Invoke(page, new object[] { "granite-openvino" });
        failed.Invoke(page, new object[]
        {
            "runtime_load_failed",
            "The verified OpenVINO worker is unavailable.",
            "Repair or reinstall the app, then retry."
        });
        Assert.AreEqual(
            InspectionFooterStatus.Interrupted,
            page.CurrentFooterStatus);
        CollectionAssert.AreEqual(
            new[]
            {
                InspectionFooterStatus.Complete,
                InspectionFooterStatus.InProgress,
                InspectionFooterStatus.NotComplete,
                InspectionFooterStatus.InProgress,
                InspectionFooterStatus.Interrupted
            },
            observed,
            "Every semantic OpenVINO lifecycle transition must reach the shared shell footer exactly once.");
    }

    [UITestMethod]
    [TestCategory("WinUI")]
    public async Task RetiredOpenVinoInspectionCannotPublishTerminalResult()
    {
        ModelInspectionPage page = new();
        SelectOpenVinoPreview(page);
        MethodInfo inspecting = RequirePrivateMethod(
            "ApplyOpenVinoInspectingPresentation");
        MethodInfo tryPublish = RequirePrivateMethod(
            "TryApplyOpenVinoInspectionResult");
        FieldInfo lifetime = RequirePrivateField("_openVinoLifetime");
        FieldInfo cancellation = RequirePrivateField("_openVinoCancellation");
        using CancellationTokenSource cancellationSource = new();

        inspecting.Invoke(page, new object[] { "granite-openvino" });
        lifetime.SetValue(page, 7L);
        cancellation.SetValue(page, cancellationSource);
        await page.RetireOpenVinoInspectionAsync();

        OpenVinoRouteInspectionResult staleResult = new(
            OpenVinoRouteInspectionOutcome.DependencyUnavailable,
            HandoffLease: null,
            Failure: new PromptFailure(
                "runtime_dependency_missing",
                "The local OpenVINO operation could not continue.",
                "Retry model inspection."),
            Configuration: null);
        bool published = Assert.IsInstanceOfType<bool>(tryPublish.Invoke(
            page,
            new object[] { 7L, staleResult }));

        Assert.IsFalse(published);
        Assert.AreEqual(InspectionContentCardMode.Progress,
            Preview(page).ContentPresentation.Mode);
        Assert.AreEqual(
            Visibility.Visible,
            PreviewElement<FrameworkElement>(
                Preview(page),
                "InspectionProgressPanel").Visibility);
        Assert.AreEqual(Visibility.Collapsed,
            ((Border)page.FindName("PromptSurface")).Visibility);
    }

    [UITestMethod]
    [TestCategory("WinUI")]
    public void OpenVinoChangedDetailAndFractionAreAcceptedWithinActiveStage()
    {
        ModelInspectionPage page = new();
        SelectOpenVinoPreview(page);
        using CancellationTokenSource cancellation = new();
        RequirePrivateField("_openVinoLifetime").SetValue(page, 4L);
        RequirePrivateField("_openVinoCancellation").SetValue(page, cancellation);
        RequirePrivateMethod("ApplyOpenVinoInspectingPresentation").Invoke(page, new object[] { "granite-openvino" });
        var previous = new ModelInspectionProgress(ModelInspectionStage.CheckModelPackage,
            ModelInspectionStageStatus.Active, 0, 5, .24, "Reading the large file.");
        RequirePrivateField("_openVinoPresentedActiveProgress").SetValue(page, previous);
        var current = new ModelInspectionProgress(ModelInspectionStage.CheckModelPackage,
            ModelInspectionStageStatus.Active, 0, 5, .25, "Continuing the secure read.");
        RequirePrivateMethod("ApplyOpenVinoProgress").Invoke(page, new object[] { 4L, current });
        var row = Preview(page).ContentPresentation.ProgressRows.Items[0];
        Assert.AreEqual(.25, row.StageFraction);
        Assert.AreEqual(current.UserMessage, row.Detail);
    }

    [UITestMethod]
    [TestCategory("WinUI")]
    public void OpenVinoProgressUpdatesTheSharedRowsAndIgnoresStaleCallbacks()
    {
        ModelInspectionPage page = new();
        RequirePrivateField("_motionSettings").SetValue(page, new ReducedMotionSettings());
        SelectOpenVinoPreview(page);
        MethodInfo inspecting = RequirePrivateMethod(
            "ApplyOpenVinoInspectingPresentation");
        MethodInfo applyProgress = RequirePrivateMethod(
            "ApplyOpenVinoProgress");
        FieldInfo lifetime = RequirePrivateField("_openVinoLifetime");
        FieldInfo cancellation = RequirePrivateField("_openVinoCancellation");
        using CancellationTokenSource cancellationSource = new();
        lifetime.SetValue(page, 4L);
        cancellation.SetValue(page, cancellationSource);
        inspecting.Invoke(page, new object[] { "granite-openvino" });

        applyProgress.Invoke(page, new object[]
        {
            4L,
            new ModelInspectionProgress(
                ModelInspectionStage.CheckModelPackage,
                ModelInspectionStageStatus.Completed,
                completedStageCount: 1,
                totalStageCount: 5,
                stageFraction: 1d,
                "Model package checked.")
        });
        applyProgress.Invoke(page, new object[]
        {
            4L,
            new ModelInspectionProgress(
                ModelInspectionStage.ReadModelConfiguration,
                ModelInspectionStageStatus.Completed,
                completedStageCount: 2,
                totalStageCount: 5,
                stageFraction: 1d,
                "Model configuration read.")
        });
        applyProgress.Invoke(page, new object[]
        {
            4L,
            new ModelInspectionProgress(
                ModelInspectionStage.ValidateTokenizerAndChatSetup,
                ModelInspectionStageStatus.Active,
                completedStageCount: 2,
                totalStageCount: 5,
                stageFraction: 0.5d,
                "Validating tokenizer resources.")
        });

        ModelInspectionPreviewProjection preview = Preview(page);
        Assert.AreEqual(
            Visibility.Visible,
            PreviewElement<FrameworkElement>(
                preview,
                "InspectionProgressPanel").Visibility);
        Assert.AreEqual(
            "Checking\n0%",
            PreviewElement<TextBlock>(preview, "InspectionStage1Status").Text);
        Assert.AreEqual(
            "Waiting",
            PreviewElement<TextBlock>(preview, "InspectionStage2Status").Text);
        Assert.AreEqual(
            "Waiting",
            PreviewElement<TextBlock>(preview, "InspectionStage3Status").Text);
        Assert.AreEqual("2 of 5 checks complete",
            preview.ContentPresentation.ProgressRows.ProgressSummary);
        Assert.AreEqual(InspectionContentStatus.Passed,
            preview.ContentPresentation.ProgressRows.Items[0].Status);
        Assert.AreEqual(InspectionContentStatus.Passed,
            preview.ContentPresentation.ProgressRows.Items[1].Status);
        Assert.AreEqual(InspectionContentStatus.Active,
            preview.ContentPresentation.ProgressRows.Items[2].Status);
        Assert.AreEqual(0.5d,
            preview.ContentPresentation.ProgressRows.Items[2].StageFraction);

        applyProgress.Invoke(page, new object[]
        {
            3L,
            new ModelInspectionProgress(
                ModelInspectionStage.ConfirmCoreRuntimeCompatibility,
                ModelInspectionStageStatus.Completed,
                completedStageCount: 5,
                totalStageCount: 5,
                stageFraction: 1d,
                "Stale completion.")
        });

        Assert.AreEqual("2 of 5 checks complete",
            preview.ContentPresentation.ProgressRows.ProgressSummary);
        Assert.AreEqual(InspectionContentStatus.Waiting,
            preview.ContentPresentation.ProgressRows.Items[4].Status);

        MethodInfo applyResult = RequirePrivateMethod(
            "TryApplyOpenVinoInspectionResult");
        Assert.AreEqual(true, applyResult.Invoke(page, new object[]
        {
            4L,
            new OpenVinoRouteInspectionResult(
                OpenVinoRouteInspectionOutcome.Cancelled,
                HandoffLease: null,
                Failure: null,
                Configuration: null)
        }));
        Assert.IsNull(RequirePrivateField("_openVinoProgressRows").GetValue(page),
            "A terminal result must retire the progress owner so late callbacks cannot replace the result UI.");

        applyProgress.Invoke(page, new object[]
        {
            4L,
            new ModelInspectionProgress(
                ModelInspectionStage.ConfirmCoreRuntimeCompatibility,
                ModelInspectionStageStatus.Completed,
                completedStageCount: 5,
                totalStageCount: 5,
                stageFraction: 1d,
                "Late completion.")
        });
        Assert.AreEqual(
            InspectionContentCardMode.Cancelled,
            preview.ContentPresentation.Mode);
        Assert.AreEqual(
            Visibility.Visible,
            PreviewElement<FrameworkElement>(
                preview,
                "InspectionCancelledPanel").Visibility);
    }

    private static bool InvokePromptSendKey(
        MethodInfo method,
        VirtualKey key,
        bool isShiftPressed) =>
        Assert.IsInstanceOfType<bool>(method.Invoke(
            null,
            new object[] { key, isShiftPressed }));

    private static void InvokeButton(Button button)
    {
        ButtonAutomationPeer peer = new(button);
        Assert.IsInstanceOfType<IInvokeProvider>(
            peer.GetPattern(PatternInterface.Invoke)).Invoke();
    }

    private static void AssertTerminalEvidence(
        ModelInspectionPreviewProjection preview,
        string title,
        string message,
        string diagnostic,
        string recovery)
    {
        Assert.AreEqual(
            Visibility.Visible,
            PreviewElement<FrameworkElement>(preview, "InspectionFailurePanel").Visibility);
        Assert.AreEqual(title,
            PreviewElement<TextBlock>(preview, "InspectionFailureHeading").Text);
        Assert.AreEqual(message,
            PreviewElement<TextBlock>(preview, "InspectionFailureMessage").Text);
        Assert.AreEqual(diagnostic,
            PreviewElement<TextBlock>(preview, "InspectionFailureDiagnostic").Text);
        Assert.AreEqual(recovery,
            PreviewElement<TextBlock>(preview, "InspectionFailureRecovery").Text);
    }

    private static InspectionActionPresentation VisibleAction(
        string text,
        string actionId) => new()
        {
            Text = text,
            AutomationName = text,
            ActionId = actionId,
            Visibility = Visibility.Visible,
            Command = new CountingCommand()
        };

    private static MethodInfo RequirePrivateMethod(string name)
    {
        MethodInfo? method = typeof(ModelInspectionPage).GetMethod(
            name,
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.IsNotNull(method);
        return method;
    }

    private static void SelectOpenVinoPreview(ModelInspectionPage page) =>
        RequirePrivateMethod("SelectOpenVinoPreview").Invoke(page, null);

    private static FieldInfo RequirePrivateField(string name)
    {
        FieldInfo? field = typeof(ModelInspectionPage).GetField(
            name,
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.IsNotNull(field);
        return field;
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

    private static T PreviewElement<T>(
        ModelInspectionPreviewProjection preview,
        string name)
        where T : class
    {
        return Assert.IsInstanceOfType<T>(preview.FindElement(name));
    }

    private sealed class CountingCommand : ICommand
    {
        internal int ExecuteCount { get; private set; }
        internal object? LastParameter { get; private set; }

        public event EventHandler? CanExecuteChanged;

        public bool CanExecute(object? parameter) => true;

        public void Execute(object? parameter)
        {
            ExecuteCount++;
            LastParameter = parameter;
        }
    }
}
