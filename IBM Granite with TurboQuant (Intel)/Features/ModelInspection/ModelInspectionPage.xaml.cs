using GraniteEdgeAI.Features.ModelInspection.Contracts;
using GraniteEdgeAI.Features.ModelInspection.Presentation;
using GraniteEdgeAI.Features.ModelInspection.Services;
using GraniteEdgeAI.Features.ModelInspection.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using System;
using System.ComponentModel;
using System.Threading.Tasks;

namespace GraniteEdgeAI.Features.ModelInspection;

/// <summary>
/// Displays and owns the UI lifecycle of one GGUF Model Inspection request.
/// </summary>
public sealed partial class ModelInspectionPage : Page
{
    private readonly IModelInspectionService service;
    private ModelInspectionViewModel? startedViewModel;

    /// <summary>
    /// Creates the production page with the approved x64 Model Inspection
    /// service composition.
    /// </summary>
    public ModelInspectionPage()
        : this(ModelInspectionServiceComposition.CreateDefault())
    {
    }

    /// <summary>
    /// Creates a page with an explicit application service.
    /// </summary>
    internal ModelInspectionPage(IModelInspectionService service)
    {
        this.service = service ?? throw new ArgumentNullException(nameof(service));
        InitializeComponent();
        Loaded += ModelInspectionPage_Loaded;
    }

    /// <summary>
    /// Requests that the onboarding owner return to model selection.
    /// </summary>
    internal event EventHandler? ChooseAnotherModelRequested;

    /// <summary>
    /// Gets the exact immutable request supplied by onboarding navigation.
    /// </summary>
    internal ModelInspectionRequest? Request { get; private set; }

    /// <summary>
    /// Gets the current navigation-owned ViewModel.
    /// </summary>
    internal ModelInspectionViewModel? ViewModel { get; private set; }

    /// <summary>
    /// Gets the initial inspection task started for this navigation.
    /// </summary>
    internal Task? CurrentInspectionTask { get; private set; }

    /// <summary>
    /// Gets the complete snapshot most recently assigned to the four cards.
    /// </summary>
    internal ModelInspectionPagePresentation? CurrentPresentation
        { get; private set; }

    /// <summary>
    /// Creates a fresh ViewModel for the exact validated navigation request.
    /// </summary>
    protected override void OnNavigatedTo(NavigationEventArgs eventArguments)
    {
        base.OnNavigatedTo(eventArguments);

        if (eventArguments.Parameter is not ModelInspectionRequest request)
        {
            throw new ArgumentException(
                "ModelInspectionPage requires a validated ModelInspectionRequest.",
                nameof(eventArguments));
        }

        RetireCurrentViewModel();

        var viewModel = new ModelInspectionViewModel(service, request);
        Request = request;
        ViewModel = viewModel;
        Subscribe(viewModel);
        ApplyPresentation(ModelInspectionPresentationFactory.CreateInitial(
            request,
            viewModel.CancelCommand));
    }

    /// <summary>
    /// Relinquishes all ownership before the page leaves the navigation stack.
    /// </summary>
    protected override void OnNavigatedFrom(NavigationEventArgs eventArguments)
    {
        RetireCurrentViewModel();
        base.OnNavigatedFrom(eventArguments);
    }

    /// <summary>
    /// Starts at most one automatic inspection for the current navigation.
    /// A ViewModel retry remains a separate, explicit user attempt.
    /// </summary>
    internal Task? StartInspectionIfReadyAsync()
    {
        ModelInspectionViewModel? viewModel = ViewModel;
        if (viewModel is null)
        {
            return null;
        }

        if (ReferenceEquals(startedViewModel, viewModel))
        {
            return CurrentInspectionTask;
        }

        // Set the identity first so a re-entrant Loaded event cannot start a
        // duplicate service call.
        startedViewModel = viewModel;
        CurrentInspectionTask = viewModel.StartAsync();
        return CurrentInspectionTask;
    }

    private void ModelInspectionPage_Loaded(
        object sender,
        RoutedEventArgs eventArguments)
    {
        _ = StartInspectionIfReadyAsync();
    }

    private void Subscribe(ModelInspectionViewModel viewModel)
    {
        viewModel.PropertyChanged += ViewModel_PropertyChanged;
        viewModel.ChooseAnotherRequested += ViewModel_ChooseAnotherRequested;
        viewModel.CancelCommand.CanExecuteChanged += Command_CanExecuteChanged;
        viewModel.RetryCommand.CanExecuteChanged += Command_CanExecuteChanged;
        viewModel.ChooseAnotherCommand.CanExecuteChanged +=
            Command_CanExecuteChanged;
    }

    private void Unsubscribe(ModelInspectionViewModel viewModel)
    {
        viewModel.PropertyChanged -= ViewModel_PropertyChanged;
        viewModel.ChooseAnotherRequested -= ViewModel_ChooseAnotherRequested;
        viewModel.CancelCommand.CanExecuteChanged -= Command_CanExecuteChanged;
        viewModel.RetryCommand.CanExecuteChanged -= Command_CanExecuteChanged;
        viewModel.ChooseAnotherCommand.CanExecuteChanged -=
            Command_CanExecuteChanged;
    }

    private void ViewModel_PropertyChanged(
        object? sender,
        PropertyChangedEventArgs eventArguments)
    {
        if (sender is ModelInspectionViewModel viewModel &&
            ReferenceEquals(viewModel, ViewModel))
        {
            RefreshPresentation(viewModel);
        }
    }

    private void Command_CanExecuteChanged(object? sender, EventArgs eventArguments)
    {
        ModelInspectionViewModel? viewModel = ViewModel;
        if (viewModel is null ||
            (!ReferenceEquals(sender, viewModel.CancelCommand) &&
             !ReferenceEquals(sender, viewModel.RetryCommand) &&
             !ReferenceEquals(sender, viewModel.ChooseAnotherCommand)))
        {
            return;
        }

        RefreshPresentation(viewModel);
    }

    private void ViewModel_ChooseAnotherRequested(
        object? sender,
        EventArgs eventArguments)
    {
        if (sender is ModelInspectionViewModel viewModel &&
            ReferenceEquals(viewModel, ViewModel))
        {
            ChooseAnotherModelRequested?.Invoke(this, EventArgs.Empty);
        }
    }

    private void RefreshPresentation(ModelInspectionViewModel viewModel)
    {
        if (!ReferenceEquals(viewModel, ViewModel) ||
            !ReferenceEquals(viewModel.Request, Request))
        {
            return;
        }

        ModelInspectionPagePresentation presentation;
        if (viewModel.Result is ModelInspectionExecutionResult result)
        {
            presentation = ModelInspectionPresentationFactory.CreateTerminal(
                viewModel.Request,
                result,
                viewModel.RetryCommand,
                viewModel.ChooseAnotherCommand);
        }
        else if (viewModel.Progress is ModelInspectionProgress progress)
        {
            presentation = ModelInspectionPresentationFactory.CreateProgress(
                viewModel.Request,
                progress,
                viewModel.CancelCommand);
        }
        else
        {
            presentation = ModelInspectionPresentationFactory.CreateInitial(
                viewModel.Request,
                viewModel.CancelCommand);
        }

        ApplyPresentation(presentation);
    }

    private void ApplyPresentation(ModelInspectionPagePresentation presentation)
    {
        CurrentPresentation = presentation ??
            throw new ArgumentNullException(nameof(presentation));
        InspectionOutcomeCardControl.Presentation = presentation.OutcomeCard;
        InspectionModelCardControl.Presentation = presentation.ModelCard;
        InspectionContentCardControl.Presentation = presentation.ContentCard;
        InspectionActionCardControl.Presentation = presentation.ActionCard;
    }

    private void RetireCurrentViewModel()
    {
        ModelInspectionViewModel? retired = ViewModel;

        // Invalidate page ownership before cancellation can synchronously
        // invoke callbacks from the retired service attempt.
        Request = null;
        ViewModel = null;
        startedViewModel = null;
        CurrentInspectionTask = null;

        if (retired is null)
        {
            return;
        }

        Unsubscribe(retired);
        retired.Deactivate();
        retired.Dispose();
    }
}
