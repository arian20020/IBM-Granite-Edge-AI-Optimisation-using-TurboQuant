using System;
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
    private readonly object _retirementGate = new();
    private Task _detachedOperations = Task.CompletedTask;
    private Task _observedExportOperation = Task.CompletedTask;
    private Task? _retirementTask;
    private bool _retired;

    public OptimizationDestinationCard()
    {
        InitializeComponent();
        DestinationCore.ActionRequested += OnActionRequested;
    }

    internal event Action<OptimizationCommand>? ActionRequested;
    internal string PrimaryActionText => DestinationCore.VisibleActionTexts[0];
    internal string SecondaryActionText => DestinationCore.VisibleActionTexts[1];
    internal OptimizationExportViewState ExportState => _exportController?.State ?? OptimizationExportViewState.Unbound();
    internal bool IsActionEnabled(OptimizationCommand command) => DestinationCore.IsActionEnabled(command);
    internal bool TryRequestAction(OptimizationCommand command) => DestinationCore.TryRequestAction(command);
    internal Task ObservedExportOperation => _observedExportOperation;

    internal void Apply(OptimizationPresentationState presentation)
    {
        ArgumentNullException.ThrowIfNull(presentation);
        if (_retired) return;
        if (presentation.Kind is not OptimizationPageStateKind.SucceededPersistent and not OptimizationPageStateKind.SucceededRuntimeProfile)
            throw new ArgumentException("Destination cards require a successful result.", nameof(presentation));
        ResetExportBinding();
        _presentation = presentation;
        DestinationCore.Apply(presentation);
        DestinationCore.SetActionEnabled(OptimizationCommand.Save, false);
    }

    internal bool BindVerifiedExport(VerifiedPersistentExportTarget target, IOptimizationExportService service)
    {
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(service);
        if (_retired || !_detachedOperations.IsCompletedSuccessfully
            || _exportController?.State.Kind is OptimizationExportStateKind.Running or OptimizationExportStateKind.Cancelling
            || _presentation is not { Kind: OptimizationPageStateKind.SucceededPersistent } presentation
            || presentation.OptimizationPlanId == Guid.Empty || !presentation.Configuration.ProducesPersistentArtifact
            || target.OptimizationPlanId != presentation.OptimizationPlanId
            || !string.Equals(target.ConfigurationSha256, presentation.ConfigurationSha256, StringComparison.Ordinal))
        {
            DestinationCore.SetActionEnabled(OptimizationCommand.Save, false);
            ResetExportBinding();
            return false;
        }
        ResetExportBinding();
        _exportController = new(service);
        _exportController.StateChanged += ExportController_StateChanged;
        _exportController.Bind(target);
        return true;
    }

    internal Task<bool> TryStartExportAsync() =>
        _retired || !DestinationCore.IsActionEnabled(OptimizationCommand.Save)
            ? Task.FromResult(false)
            : _exportController?.TryStartAsync() ?? Task.FromResult(false);
    internal bool TryCancelExport() => _exportController?.TryCancel() ?? false;
    internal Task<bool> TryRetryExportAsync() => _exportController?.TryRetryAsync() ?? Task.FromResult(false);

    internal void ClearExportBinding()
    {
        ResetExportBinding();
        _presentation = null;
        DestinationCore.SetActionEnabled(OptimizationCommand.Save, false);
    }

    internal Task RetireAsync()
    {
        lock (_retirementGate)
        {
            if (_retirementTask is not null) return _retirementTask;
            _retired = true;
            ResetExportBinding();
            _presentation = null;
            DestinationCore.SetAllActionsEnabled(false);
            _retirementTask = _detachedOperations;
            return _retirementTask;
        }
    }

    private void OnActionRequested(OptimizationCommand command)
    {
        if (command == OptimizationCommand.Save) { _observedExportOperation = ObserveExportOperationAsync(TryStartExportAsync()); return; }
        ActionRequested?.Invoke(command);
    }

    private void ExportController_StateChanged(object? sender, OptimizationExportViewState state)
    {
        if (sender is not OptimizationExportController controller || !ReferenceEquals(controller, _exportController)) return;
        DispatcherQueue dispatcher = DispatcherQueue;
        if (!dispatcher.HasThreadAccess)
        {
            dispatcher.TryEnqueue(() =>
            {
                if (ReferenceEquals(controller, _exportController) && Equals(controller.State, state)) ApplyExportState(state);
            });
            return;
        }
        if (Equals(controller.State, state)) ApplyExportState(state);
    }

    private void ApplyExportState(OptimizationExportViewState state)
    {
        bool running = state.Kind is OptimizationExportStateKind.Running or OptimizationExportStateKind.Cancelling;
        DestinationCore.SetAllActionsEnabled(!running);
        DestinationCore.SetActionEnabled(OptimizationCommand.Save, state.Kind == OptimizationExportStateKind.Ready);
        ExportStatusText.Text = state.StatusText;
        ExportProgressBar.Visibility = running ? Visibility.Visible : Visibility.Collapsed;
        ExportProgressBar.IsIndeterminate = state.Fraction is null;
        if (state.Fraction is double fraction) ExportProgressBar.Value = fraction * 100;
        CancelExportButton.Visibility = state.Kind == OptimizationExportStateKind.Running ? Visibility.Visible : Visibility.Collapsed;
        RetryExportButton.Visibility = state.Kind is OptimizationExportStateKind.Cancelled or OptimizationExportStateKind.Failed ? Visibility.Visible : Visibility.Collapsed;
        if (state.Kind is OptimizationExportStateKind.Cancelled or OptimizationExportStateKind.Failed)
            RetryExportButton.Focus(FocusState.Programmatic);
        else if (state.Kind == OptimizationExportStateKind.Succeeded)
            DestinationCore.FocusAction(OptimizationCommand.Chat, FocusState.Programmatic);
    }

    private void ResetExportBinding()
    {
        if (_exportController is not null)
        {
            _exportController.StateChanged -= ExportController_StateChanged;
            _detachedOperations = Task.WhenAll(_detachedOperations, _exportController.RetireAsync());
            _exportController = null;
        }
        ExportStatusText.Text = OptimizationExportViewState.Unbound().StatusText;
        ExportProgressBar.Visibility = Visibility.Collapsed;
        CancelExportButton.Visibility = Visibility.Collapsed;
        RetryExportButton.Visibility = Visibility.Collapsed;
    }

    private void CancelExportButton_Click(object sender, RoutedEventArgs eventArguments) => TryCancelExport();
    private void RetryExportButton_Click(object sender, RoutedEventArgs eventArguments) =>
        _observedExportOperation = ObserveExportOperationAsync(TryRetryExportAsync());

    internal Type? ObservedExportFaultType { get; private set; }

    private async Task ObserveExportOperationAsync(Task<bool> operation)
    {
        try { await operation.ConfigureAwait(false); }
        catch (Exception exception)
        {
            ObservedExportFaultType = exception.GetType();
            System.Diagnostics.Trace.TraceError(
                "The optimization-export UI boundary observed {0}.",
                exception.GetType().Name);
        }
    }
}
