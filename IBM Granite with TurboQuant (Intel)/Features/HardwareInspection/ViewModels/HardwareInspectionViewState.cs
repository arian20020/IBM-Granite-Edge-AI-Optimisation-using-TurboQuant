using GraniteEdgeAI.Features.HardwareInspection.Application;
using GraniteEdgeAI.Features.HardwareInspection.Presentation.State;
using System;

namespace GraniteEdgeAI.Features.HardwareInspection.ViewModels;

public sealed class HardwareInspectionViewState
{
    public HardwareInspectionViewState(
        long revision,
        long attemptGeneration,
        Guid? inspectionId,
        bool isRunActive,
        HardwareInspectionPresentationState presentation,
        HardwareSummaryPresentation? summary,
        HardwareInspectionDetailsState? details,
        HardwareInspectionHandoff? handoff,
        string? safeDiagnosticCode)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(revision);
        ArgumentOutOfRangeException.ThrowIfNegative(attemptGeneration);
        ArgumentNullException.ThrowIfNull(presentation);
        if (isRunActive && (!inspectionId.HasValue || inspectionId == Guid.Empty))
        {
            throw new ArgumentException(
                "An active run requires a non-empty inspection identity.",
                nameof(inspectionId));
        }

        Revision = revision;
        AttemptGeneration = attemptGeneration;
        InspectionId = inspectionId;
        IsRunActive = isRunActive;
        Presentation = presentation;
        Summary = summary;
        Details = details;
        Handoff = handoff;
        SafeDiagnosticCode = safeDiagnosticCode;
    }

    public long Revision { get; }
    public long AttemptGeneration { get; }
    public Guid? InspectionId { get; }
    public bool IsRunActive { get; }
    public HardwareInspectionPresentationState Presentation { get; }
    public HardwareSummaryPresentation? Summary { get; }
    public HardwareInspectionDetailsState? Details { get; }
    public HardwareInspectionHandoff? Handoff { get; }
    public string? SafeDiagnosticCode { get; }
}
