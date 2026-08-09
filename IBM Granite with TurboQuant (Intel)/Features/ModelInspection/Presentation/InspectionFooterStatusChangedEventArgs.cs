using GraniteEdgeAI.Features.ModelInspection.Models;
using System;

namespace GraniteEdgeAI.Features.ModelInspection.Presentation;

internal sealed class InspectionFooterStatusChangedEventArgs : EventArgs
{
    internal InspectionFooterStatusChangedEventArgs(InspectionFooterStatus status)
    {
        if (!Enum.IsDefined(status))
        {
            throw new ArgumentOutOfRangeException(
                nameof(status),
                status,
                "Inspection footer status must be defined.");
        }

        Status = status;
    }

    internal InspectionFooterStatus Status { get; }
}
