using System;

namespace GraniteEdgeAI.Features.ModelInspection.Controls;

internal sealed class InspectionDisclosureToggleRequestedEventArgs : EventArgs
{
    internal InspectionDisclosureToggleRequestedEventArgs(bool isExpanded)
    {
        IsExpanded = isExpanded;
    }

    internal bool IsExpanded { get; }
}
