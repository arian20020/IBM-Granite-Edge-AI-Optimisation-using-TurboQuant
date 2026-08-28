using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using GraniteEdgeAI.Features.ModelOptimization.Export;
using GraniteEdgeAI.Features.ModelOptimization.Presentation;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace GraniteEdgeAI.Features.ModelOptimization.Controls;

public sealed partial class OptimizationDestinationCard : UserControl
{
    private OptimizationPresentationState? _presentation;
    private OptimizationExportController? _exportController;

    public OptimizationDestinationCard()
    {
        InitializeComponent();
        DestinationCore.ActionRequested += OnActionRequested;
    }

    internal event Action<OptimizationCommand>? ActionRequested;

    internal string PrimaryActionText => DestinationCore.VisibleActionTexts[0];

    internal string SecondaryActionText => DestinationCore.VisibleActionTexts[1];

    internal OptimizationExportViewState ExportState =>
        _exportController?.State ?? OptimizationExportViewState.Unbound();

    internal bool IsActionEnabled(OptimizationCommand command) =>
        DestinationCore.IsActionEnabled(command);

    internal bool TryRequestAction(OptimizationCommand command) =>
        DestinationCore.TryRequestAction(command);

    internal void Apply(OptimizationPresentationState presentation)
    {
        ArgumentNullException.ThrowIfNull(presentation);
        if (presentation.Kind is not OptimizationPageStateKind.SucceededPersistent
            and not OptimizationPageStateKind.SucceededRuntimeProfile)
        {
            throw new ArgumentException("Destination cards require a successful result.", nameof(presentation));
        }

        ResetExportBinding();
        _presentation = presentation;
        DestinationCore.Apply(presentation);
        DestinationCore.SetActionEnabled(OptimizationCommand.Save, false);
    }

    internal bool BindVerifiedExport(
        VerifiedPersistentExportTarget target,
        IOptimizationExportService service)
    {
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(service);
        if (_presentation is not
            { Kind: OptimizationPageStateKind.SucceededPersistent } presentation
            || presentation.OptimizationPlanId == Guid.Empty
            || !presentation.Configuration.ProducesPersistentArtifact
            || target.OptimizationPlanId != presentation.OptimizationPlanId
            || !string.Equals(
                target.ConfigurationSha256,
                presentation.ConfigurationSha256,
                StringComparison.Ordinal))
        {
            DestinationCore.SetActionEnabled(OptimizationCommand.Save, false);
            ResetExportBinding();
            return false;
        }

        ResetExportBinding();
        _exportController = new OptimizationExportController(service);
        _exportController.StateChanged += ExportController_StateChanged;
        _exportController.Bind(target);
        return true;
    }

    internal Task<bool> TryStartExportAsync()
    {
        if (!DestinationCore.IsActionEnabled(OptimizationCommand.Save))
        {
            return Task.FromResult(false);
        }
        return _exportController?.TryStartAsync() ?? Task.FromResult(false);
    }

    internal bool TryCancelExport() =>
        _exportController?.TryCancel() ?? false;

    internal Task<bool> TryRetryExportAsync() =>
        _exportController?.TryRetryAsync() ?? Task.FromResult(false);

    internal void ClearExportBinding()
    {
        ResetExportBinding();
        _presentation = null;
        DestinationCore.SetActionEnabled(OptimizationCommand.Save, false);
    }

    private void OnActionRequested(OptimizationCommand command)
    {
        if (command == OptimizationCommand.Save)
        {
            _ = TryStartExportAsync();
            return;
        }
        ActionRequested?.Invoke(command);
    }

    private void ExportController_StateChanged(
        object? sender,
        OptimizationExportViewState state)
    {
        if (sender is not OptimizationExportController controller
            || !ReferenceEquals(controller, _exportController))
        {
            return;
        }
        DispatcherQueue dispatcher = DispatcherQueue;
        if (!dispatcher.HasThreadAccess)
        {
            dispatcher.TryEnqueue(() =>
            {
                if (ReferenceEquals(controller, _exportController)
                    && Equals(controller.State, state))
                {
                    ApplyExportState(state);
                }
            });
            return;
        }
        if (Equals(controller.State, state))
        {
            ApplyExportState(state);
        }
    }

    private void ApplyExportState(OptimizationExportViewState state)
    {
        bool running = state.Kind is OptimizationExportStateKind.Running
            or OptimizationExportStateKind.Cancelling;
        DestinationCore.SetAllActionsEnabled(!running);
        DestinationCore.SetActionEnabled(
            OptimizationCommand.Save,
            state.Kind == OptimizationExportStateKind.Ready);
        ExportStatusText.Text = state.StatusText;
        ExportProgressBar.Visibility = running
            ? Visibility.Visible
            : Visibility.Collapsed;
        ExportProgressBar.IsIndeterminate = state.Fraction is null;
        if (state.Fraction is double fraction)
        {
            ExportProgressBar.Value = fraction * 100;
        }
        CancelExportButton.Visibility = state.Kind == OptimizationExportStateKind.Running
            ? Visibility.Visible
            : Visibility.Collapsed;
        RetryExportButton.Visibility = state.Kind is OptimizationExportStateKind.Cancelled
            or OptimizationExportStateKind.Failed
                ? Visibility.Visible
                : Visibility.Collapsed;

        if (state.Kind is OptimizationExportStateKind.Cancelled
            or OptimizationExportStateKind.Failed)
        {
            RetryExportButton.Focus(FocusState.Programmatic);
        }
        else if (state.Kind == OptimizationExportStateKind.Succeeded)
        {
            DestinationCore.FocusAction(
                OptimizationCommand.Chat,
                FocusState.Programmatic);
        }
    }

    private void ResetExportBinding()
    {
        if (_exportController is not null)
        {
            _exportController.StateChanged -= ExportController_StateChanged;
            _exportController.Unbind();
            _exportController = null;
        }
        ExportStatusText.Text = OptimizationExportViewState.Unbound().StatusText;
        ExportProgressBar.Visibility = Visibility.Collapsed;
        CancelExportButton.Visibility = Visibility.Collapsed;
        RetryExportButton.Visibility = Visibility.Collapsed;
    }

    private void CancelExportButton_Click(
        object sender,
        RoutedEventArgs eventArguments) =>
        TryCancelExport();

    private async void RetryExportButton_Click(
        object sender,
        RoutedEventArgs eventArguments) =>
        await TryRetryExportAsync();
}
