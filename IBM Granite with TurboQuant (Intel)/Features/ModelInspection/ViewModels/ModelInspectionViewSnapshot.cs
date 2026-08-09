using GraniteEdgeAI.Features.ModelInspection.Contracts;
using System;

namespace GraniteEdgeAI.Features.ModelInspection.ViewModels;

/// <summary>
/// Identifies one immutable render state within a replaceable attempt.
/// </summary>
internal readonly record struct ModelInspectionRenderKey
{
    internal ModelInspectionRenderKey(
        long attemptGeneration,
        long presentationRevision)
    {
        if (attemptGeneration < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(attemptGeneration),
                attemptGeneration,
                "Attempt generation cannot be negative.");
        }

        if (presentationRevision < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(presentationRevision),
                presentationRevision,
                "Presentation revision cannot be negative.");
        }

        AttemptGeneration = attemptGeneration;
        PresentationRevision = presentationRevision;
    }

    internal long AttemptGeneration { get; }

    internal long PresentationRevision { get; }
}

/// <summary>
/// Provides one UI-independent, internally consistent inspection view state.
/// </summary>
internal sealed record ModelInspectionViewSnapshot
{
    internal static ModelInspectionViewSnapshot Initial { get; } = new(
        new ModelInspectionRenderKey(0, 0),
        isRunActive: false,
        isCancellationRequested: false,
        progress: null,
        terminalResult: null);

    internal ModelInspectionViewSnapshot(
        ModelInspectionRenderKey renderKey,
        bool isRunActive,
        bool isCancellationRequested,
        ModelInspectionProgress? progress,
        ModelInspectionExecutionResult? terminalResult)
    {
        if (isRunActive && terminalResult is not null)
        {
            throw new ArgumentException(
                "An active run cannot also have a terminal result.",
                nameof(terminalResult));
        }

        if (progress is not null && terminalResult is not null)
        {
            throw new ArgumentException(
                "Progress and a terminal result cannot both be current.",
                nameof(terminalResult));
        }

        if (progress is not null && !isRunActive)
        {
            throw new ArgumentException(
                "Progress can be current only for an active run.",
                nameof(progress));
        }

        if (isCancellationRequested && !isRunActive)
        {
            throw new ArgumentException(
                "Cancellation can be requested only for an active run.",
                nameof(isCancellationRequested));
        }

        RenderKey = renderKey;
        IsRunActive = isRunActive;
        IsCancellationRequested = isCancellationRequested;
        Progress = progress;
        TerminalResult = terminalResult;
    }

    internal ModelInspectionRenderKey RenderKey { get; }

    internal bool IsRunActive { get; }

    internal bool IsCancellationRequested { get; }

    internal ModelInspectionProgress? Progress { get; }

    internal ModelInspectionExecutionResult? TerminalResult { get; }
}
