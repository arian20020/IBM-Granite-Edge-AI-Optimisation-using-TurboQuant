using System;

namespace GraniteEdgeAI.Features.ModelInspection.Contracts;

/// <summary>
/// Represents the mutually exclusive terminal states of one complete Model
/// Inspection execution.
/// </summary>
internal sealed record ModelInspectionExecutionResult
{
    private ModelInspectionExecutionResult(
        ModelInspectionExecutionStatus status,
        ModelInspectionResult? result,
        ModelInspectionOperationalFailure? failure,
        bool? cancellationWasCooperative)
    {
        Status = status;
        Result = result;
        Failure = failure;
        CancellationWasCooperative = cancellationWasCooperative;
    }

    internal ModelInspectionExecutionStatus Status { get; }

    internal ModelInspectionResult? Result { get; }

    internal ModelInspectionOperationalFailure? Failure { get; }

    internal bool? CancellationWasCooperative { get; }

    /// <summary>
    /// Creates the terminal state used only when reliable evidence was
    /// successfully classified into a model result.
    /// </summary>
    internal static ModelInspectionExecutionResult Completed(
        ModelInspectionResult result)
    {
        ArgumentNullException.ThrowIfNull(result);

        return new ModelInspectionExecutionResult(
            ModelInspectionExecutionStatus.Completed,
            result,
            failure: null,
            cancellationWasCooperative: null);
    }

    /// <summary>
    /// Creates the terminal state used only when the worker acknowledged and
    /// completed a cooperative user cancellation.
    /// </summary>
    internal static ModelInspectionExecutionResult Cancelled(bool cooperative)
    {
        // Forced termination is an operational failure and must not look like a
        // successful user cancellation in the application state.
        if (!cooperative)
        {
            throw new ArgumentException(
                "Cancelled execution requires cooperative worker termination.",
                nameof(cooperative));
        }

        return new ModelInspectionExecutionResult(
            ModelInspectionExecutionStatus.Cancelled,
            result: null,
            failure: null,
            cancellationWasCooperative: true);
    }

    /// <summary>
    /// Creates the terminal state used when reliable model classification could
    /// not be produced because infrastructure or containment failed.
    /// </summary>
    internal static ModelInspectionExecutionResult OperationalFailure(
        ModelInspectionOperationalFailure failure)
    {
        ArgumentNullException.ThrowIfNull(failure);

        return new ModelInspectionExecutionResult(
            ModelInspectionExecutionStatus.OperationalFailure,
            result: null,
            failure: failure,
            cancellationWasCooperative: null);
    }
}
