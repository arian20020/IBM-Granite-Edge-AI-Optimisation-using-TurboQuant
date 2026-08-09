using Microsoft.UI.Xaml;
using System;
using System.Collections.Generic;

namespace GraniteEdgeAI.Features.ModelInspection.Presentation;

internal interface IModelInspectionAnimationDriver : IDisposable
{
    void StartStageStatus(
        UIElement target,
        ModelInspectionVisualOperationKey key,
        Action<ModelInspectionVisualOperationKey> completed);

    void StartActiveDetail(
        UIElement target,
        ModelInspectionVisualOperationKey key,
        Action<ModelInspectionVisualOperationKey> completed);

    void StartDisclosure(
        UIElement chevron,
        FrameworkElement viewport,
        IReadOnlyList<UIElement> followingElements,
        bool isExpanded,
        IReadOnlyList<double> previousTopOffsets,
        ModelInspectionVisualOperationKey key,
        Action<ModelInspectionVisualOperationKey> completed);

    void StartTerminal(
        UIElement outgoing,
        UIElement incoming,
        ModelInspectionVisualOperationKey key,
        Action<ModelInspectionVisualOperationKey> completed);

    void CancelAll();
}
