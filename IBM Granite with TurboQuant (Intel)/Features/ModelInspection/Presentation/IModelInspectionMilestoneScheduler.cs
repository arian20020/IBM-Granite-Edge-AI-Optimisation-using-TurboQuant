using System;

namespace GraniteEdgeAI.Features.ModelInspection.Presentation;

internal interface IModelInspectionMilestoneScheduler : IDisposable
{
    TimeSpan Elapsed { get; }

    IDisposable Schedule(TimeSpan delay, Action callback);
}
