using System;
using GraniteEdgeAI.Features.ModelHardwareCompatibility.Journey;
using GraniteEdgeAI.Features.ModelOptimization.Export;
using GraniteEdgeAI.Features.ModelOptimization.Presentation;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace GraniteEdgeAI.Features.ModelOptimization;

public sealed partial class OptimizationPage : Page
{
    private OptimizationPresentationState? _presentation;
    private OptimizationJourneyEntryContext? _entryContext;

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

    internal bool BindVerifiedExport(
        VerifiedPersistentExportTarget target,
        IOptimizationExportService service) =>
        DestinationCard.BindVerifiedExport(target, service);

    internal void ApplyPresentation(OptimizationPresentationState presentation)
    {
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
        if (_entryContext is null)
        {
            return;
        }
        IntentRequested?.Invoke(this, new OptimizationIntentEventArgs(command));
    }

    private void OnOutcomeActionRequested(OptimizationCommand command) =>
        RaiseIntent(command);
}
