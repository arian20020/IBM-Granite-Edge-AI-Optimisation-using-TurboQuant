using System;
using System.Numerics;

namespace GraniteEdgeAI.Features.ModelInspection.Presentation;

internal sealed record ModelInspectionMotionSpec
{
    internal static readonly TimeSpan PrecisionOrbitDuration =
        TimeSpan.FromMilliseconds(1050);

    internal static ModelInspectionMotionSpec Approved { get; } = new(
        TimeSpan.FromMilliseconds(160),
        TimeSpan.FromMilliseconds(180),
        TimeSpan.FromMilliseconds(240),
        new Vector2(0f, 0f),
        new Vector2(0.2f, 1f),
        statusOpacityFrom: 0d,
        statusOpacityTo: 1d,
        activeDetailOpacityFrom: 0d,
        activeDetailOpacityTo: 1d,
        activeDetailOffsetYFrom: 8d,
        activeDetailOffsetYTo: 0d,
        terminalOutgoingOpacityFrom: 1d,
        terminalOutgoingOpacityTo: 0d,
        terminalIncomingOpacityFrom: 0d,
        terminalIncomingOpacityTo: 1d,
        collapsedChevronDegrees: 0d,
        expandedChevronDegrees: 180d,
        collapsedRevealProgress: 0d,
        expandedRevealProgress: 1d);

    internal ModelInspectionMotionSpec(
        TimeSpan fastDuration,
        TimeSpan standardDuration,
        TimeSpan disclosureDuration,
        Vector2 easeOutControlPoint1,
        Vector2 easeOutControlPoint2,
        double statusOpacityFrom,
        double statusOpacityTo,
        double activeDetailOpacityFrom,
        double activeDetailOpacityTo,
        double activeDetailOffsetYFrom,
        double activeDetailOffsetYTo,
        double terminalOutgoingOpacityFrom,
        double terminalOutgoingOpacityTo,
        double terminalIncomingOpacityFrom,
        double terminalIncomingOpacityTo,
        double collapsedChevronDegrees,
        double expandedChevronDegrees,
        double collapsedRevealProgress,
        double expandedRevealProgress)
    {
        FastDuration = ValidateDuration(fastDuration, nameof(fastDuration));
        StandardDuration = ValidateDuration(
            standardDuration,
            nameof(standardDuration));
        DisclosureDuration = ValidateDuration(
            disclosureDuration,
            nameof(disclosureDuration));
        EaseOutControlPoint1 = ValidateControlPoint(
            easeOutControlPoint1,
            nameof(easeOutControlPoint1));
        EaseOutControlPoint2 = ValidateControlPoint(
            easeOutControlPoint2,
            nameof(easeOutControlPoint2));
        StatusOpacityFrom = ValidateUnitEndpoint(
            statusOpacityFrom,
            nameof(statusOpacityFrom));
        StatusOpacityTo = ValidateUnitEndpoint(
            statusOpacityTo,
            nameof(statusOpacityTo));
        ActiveDetailOpacityFrom = ValidateUnitEndpoint(
            activeDetailOpacityFrom,
            nameof(activeDetailOpacityFrom));
        ActiveDetailOpacityTo = ValidateUnitEndpoint(
            activeDetailOpacityTo,
            nameof(activeDetailOpacityTo));
        ActiveDetailOffsetYFrom = ValidateFinite(
            activeDetailOffsetYFrom,
            nameof(activeDetailOffsetYFrom));
        ActiveDetailOffsetYTo = ValidateFinite(
            activeDetailOffsetYTo,
            nameof(activeDetailOffsetYTo));
        TerminalOutgoingOpacityFrom = ValidateUnitEndpoint(
            terminalOutgoingOpacityFrom,
            nameof(terminalOutgoingOpacityFrom));
        TerminalOutgoingOpacityTo = ValidateUnitEndpoint(
            terminalOutgoingOpacityTo,
            nameof(terminalOutgoingOpacityTo));
        TerminalIncomingOpacityFrom = ValidateUnitEndpoint(
            terminalIncomingOpacityFrom,
            nameof(terminalIncomingOpacityFrom));
        TerminalIncomingOpacityTo = ValidateUnitEndpoint(
            terminalIncomingOpacityTo,
            nameof(terminalIncomingOpacityTo));
        CollapsedChevronDegrees = ValidateFinite(
            collapsedChevronDegrees,
            nameof(collapsedChevronDegrees));
        ExpandedChevronDegrees = ValidateFinite(
            expandedChevronDegrees,
            nameof(expandedChevronDegrees));
        CollapsedRevealProgress = ValidateUnitEndpoint(
            collapsedRevealProgress,
            nameof(collapsedRevealProgress));
        ExpandedRevealProgress = ValidateUnitEndpoint(
            expandedRevealProgress,
            nameof(expandedRevealProgress));
    }

    internal TimeSpan FastDuration { get; }

    internal TimeSpan StandardDuration { get; }

    internal TimeSpan DisclosureDuration { get; }

    internal Vector2 EaseOutControlPoint1 { get; }

    internal Vector2 EaseOutControlPoint2 { get; }

    internal double StatusOpacityFrom { get; }

    internal double StatusOpacityTo { get; }

    internal double ActiveDetailOpacityFrom { get; }

    internal double ActiveDetailOpacityTo { get; }

    internal double ActiveDetailOffsetYFrom { get; }

    internal double ActiveDetailOffsetYTo { get; }

    internal double TerminalOutgoingOpacityFrom { get; }

    internal double TerminalOutgoingOpacityTo { get; }

    internal double TerminalIncomingOpacityFrom { get; }

    internal double TerminalIncomingOpacityTo { get; }

    internal double CollapsedChevronDegrees { get; }

    internal double ExpandedChevronDegrees { get; }

    internal double CollapsedRevealProgress { get; }

    internal double ExpandedRevealProgress { get; }

    private static TimeSpan ValidateDuration(TimeSpan value, string parameterName)
    {
        if (value <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(
                parameterName,
                value,
                "Motion duration must be positive.");
        }

        return value;
    }

    private static Vector2 ValidateControlPoint(
        Vector2 value,
        string parameterName)
    {
        if (!float.IsFinite(value.X) ||
            !float.IsFinite(value.Y) ||
            value.X < 0f ||
            value.X > 1f)
        {
            throw new ArgumentOutOfRangeException(
                parameterName,
                value,
                "A cubic-bezier control point must be finite and its X coordinate must be between zero and one.");
        }

        return value;
    }

    private static double ValidateUnitEndpoint(double value, string parameterName)
    {
        ValidateFinite(value, parameterName);
        if (value < 0d || value > 1d)
        {
            throw new ArgumentOutOfRangeException(
                parameterName,
                value,
                "Opacity and reveal endpoints must be between zero and one.");
        }

        return value;
    }

    private static double ValidateFinite(double value, string parameterName)
    {
        if (!double.IsFinite(value))
        {
            throw new ArgumentOutOfRangeException(
                parameterName,
                value,
                "Motion endpoints must be finite.");
        }

        return value;
    }
}
