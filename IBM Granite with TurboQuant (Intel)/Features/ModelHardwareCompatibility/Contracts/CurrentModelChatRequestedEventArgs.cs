using System;

namespace GraniteEdgeAI.Features.ModelHardwareCompatibility.Contracts;

internal sealed class CurrentModelChatRequestedEventArgs(
    CurrentModelLaunchHandoff handoff) : EventArgs
{
    internal CurrentModelLaunchHandoff Handoff { get; } = handoff
        ?? throw new ArgumentNullException(nameof(handoff));
}
