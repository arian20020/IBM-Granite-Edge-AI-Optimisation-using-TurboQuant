using System;

namespace GraniteEdgeAI.Features.ModelInspection.Handoff;

/// <summary>
/// Carries only the approved immutable handoff across the Model page boundary.
/// </summary>
internal sealed class HardwareInspectionRequestedEventArgs : EventArgs
{
    internal HardwareInspectionRequestedEventArgs(
        ModelInspectionHandoff handoff)
    {
        Handoff = handoff ?? throw new ArgumentNullException(nameof(handoff));
    }

    internal ModelInspectionHandoff Handoff { get; }
}
