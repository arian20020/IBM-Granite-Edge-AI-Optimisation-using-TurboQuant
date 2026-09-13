using System;

namespace GraniteEdgeAI.Features.ModelInspection.Presentation;

internal interface IModelInspectionMotionSettings : IDisposable
{
    bool AnimationsEnabled { get; }

    event EventHandler? AnimationsEnabledChanged;
}
