using System;

namespace GraniteEdgeAI.Features.ModelInspection.Presentation;

internal interface IModelInspectionRenderDispatcher
{
    bool TryEnqueue(Action callback);
}
