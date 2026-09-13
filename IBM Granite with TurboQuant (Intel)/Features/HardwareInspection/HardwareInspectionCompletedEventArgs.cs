using GraniteEdgeAI.Features.HardwareInspection.Application;
using System;

namespace GraniteEdgeAI.Features.HardwareInspection;

internal sealed class HardwareInspectionCompletedEventArgs : EventArgs
{
    internal HardwareInspectionCompletedEventArgs(HardwareInspectionHandoff handoff)
    {
        Handoff = handoff ?? throw new ArgumentNullException(nameof(handoff));
    }

    internal HardwareInspectionHandoff Handoff { get; }
}
