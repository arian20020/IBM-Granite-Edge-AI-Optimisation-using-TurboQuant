using System;
using GraniteEdgeAI.Features.ModelHardwareCompatibility.Journey;
using GraniteEdgeAI.Features.ModelOptimization.Export;
using GraniteEdgeAI.Features.ModelOptimization.Presentation;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using System.Threading;
using System.Threading.Tasks;

namespace GraniteEdgeAI.Features.ModelOptimization;

public sealed partial class OptimizationPage : Page
{
    private OptimizationPresentationState? _presentation;
    private OptimizationJourneyEntryContext? _entryContext;
    private readonly object _navigationRetirementLock = new();
    private Task? _navigationRetirementTask;
    private int _isRetired;

    public OptimizationPage()
    {
        InitializeComponent();
        ConfirmationCard.BackRequested += OnBackRequested;
        ConfirmationCard.ConfirmRequested += OnConfirmRequested;
        ProgressCard.CancelRequested += OnCancelRequested;
        RecoveryCard.ActionRequested += OnOutcomeActionRequested;
        DestinationCard.ActionRequested += OnOutcomeActionRequested;
    }

    internal OptimizationPage(OptimizationJourneyEntryContext entryContext)
        : this()
    {
        _entryContext = entryContext
            ?? throw new ArgumentNullException(nameof(entryContext));
        ApplyPresentation(OptimizationPresentationFactory.Confirmation(
            entryContext.OptimizationHandoff.Plan.Preference,
            OptimizationConfigurationProjection.From(
                entryContext.OptimizationHandoff.Plan),
            entryContext.OptimizationHandoff.OptimizationPlanId,
            entryContext.OptimizationHandoff.ConfigurationSha256));
    }

    internal event EventHandler<OptimizationIntentEventArgs>? IntentRequested;

    internal OptimizationPresentationState? Presentation => _presentation;

    internal bool BindVerifiedExport(VerifiedPersistentExportTarget target, IOptimizationExportService service) =>
        Volatile.Read(ref _isRetired) == 0 && DestinationCard.BindVerifiedExport(target, service);

    internal void ApplyPresentation(OptimizationPresentationState presentation)
    {
        if (Volatile.Read(ref _isRetired) != 0) return;
        _presentation = presentation ?? throw new ArgumentNullException(nameof(presentation));

        PageTitle.Text = presentation.Title;
        PageSummary.Text = presentation.Summary;
        ConfigurationCard.Apply(presentation.Configuration);
        ConfirmationCard.Apply(presentation);
        ProgressCard.Apply(presentation);
        DestinationCard.ClearExportBinding();
        bool succeeded = presentation.Kind is OptimizationPageStateKind.SucceededPersistent
            or OptimizationPageStateKind.SucceededRuntimeProfile;
        bool recovering = presentation.Kind is OptimizationPageStateKind.Cancelled
            or OptimizationPageStateKind.ReplanRequired
            or OptimizationPageStateKind.Failed;
        if (succeeded)
        {
            DestinationCard.Apply(presentation);
        }
        else if (recovering)
        {
            RecoveryCard.Apply(presentation);
        }

        bool confirming = presentation.Kind == OptimizationPageStateKind.Confirming;
        bool running = presentation.Kind == OptimizationPageStateKind.Running;
        ConfigurationCard.Visibility = confirming ? Visibility.Visible : Visibility.Collapsed;
        ConfirmationCard.Visibility = confirming ? Visibility.Visible : Visibility.Collapsed;
        ProgressCard.Visibility = running ? Visibility.Visible : Visibility.Collapsed;
        RecoveryCard.Visibility = recovering ? Visibility.Visible : Visibility.Collapsed;
        DestinationCard.Visibility = succeeded ? Visibility.Visible : Visibility.Collapsed;
    }

    private void OnBackRequested(object? sender, EventArgs args) =>
        RaiseIntent(OptimizationCommand.BackToCompatibility);

    private void OnConfirmRequested(object? sender, EventArgs args) =>
        RaiseIntent(OptimizationCommand.Confirm);

    private void OnCancelRequested(object? sender, EventArgs args) =>
        RaiseIntent(OptimizationCommand.Cancel);

    private void RaiseIntent(OptimizationCommand command)
    {
        if (Volatile.Read(ref _isRetired) != 0 || _entryContext is null)
        {
            return;
        }
        IntentRequested?.Invoke(this, new OptimizationIntentEventArgs(command));
    }

    private void OnOutcomeActionRequested(OptimizationCommand command) =>
        RaiseIntent(command);

    internal Task RetireForNavigationAsync()
    {
        lock (_navigationRetirementLock)
        {
            if (_navigationRetirementTask is not null) return _navigationRetirementTask;
            Interlocked.Exchange(ref _isRetired, 1);
            DestinationCard.ActionRequested -= OnOutcomeActionRequested;
            _navigationRetirementTask = DestinationCard.RetireAsync();
            return _navigationRetirementTask;
        }
    }

    protected override async void OnNavigatedFrom(NavigationEventArgs e)
    {
        base.OnNavigatedFrom(e);
        await RetireForNavigationAsync();
    }
}
