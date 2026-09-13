using GraniteEdgeAI.Features.ModelInspection.Models;
using GraniteEdgeAI.Features.ModelInspection.Presentation;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;

namespace GraniteEdgeAI.Features.ModelInspection.Views;

internal interface IModelInspectionPreviewView
{
    UserControl Element { get; }
    FrameworkElement ModelSurface { get; }
    FrameworkElement ContentSurface { get; }
    FrameworkElement OutcomeSurface { get; }
    FrameworkElement OutcomeFocusTarget { get; }
    Button CancelActionButton { get; }
    Expander? ActiveDisclosure { get; }
    bool IsDisclosureExpanded { get; }
    event EventHandler<GraniteEdgeAI.Features.ModelInspection.Controls.InspectionDisclosureToggleRequestedEventArgs>? DisclosureToggleRequested;
    object? FindElement(string name);
    void ApplyPresentation(ModelInspectionPagePresentation presentation);
    void ApplyModel(InspectionModelCardPresentation presentation);
    void ApplyContent(InspectionContentCardPresentation presentation);
    void ApplyOutcome(InspectionOutcomePresentation presentation);
    void ApplyActions(InspectionActionCardPresentation presentation);
    void SetDisclosureState(bool expanded);
    void SetMotionEnabled(bool enabled);
    void CancelProgressMotion();
    void RefreshProgressPresentation();
    void AnnounceProgress(string announcement);
    void AnnounceOutcome(string announcement);
    bool FocusOutcome();
    void ApplyResponsiveState(string stateName);
    string ResponsiveStateName { get; }
}
