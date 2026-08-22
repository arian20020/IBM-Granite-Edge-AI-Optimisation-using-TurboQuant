using GraniteEdgeAI.Features.ModelInspection.Contracts;
using System;

namespace GraniteEdgeAI.Features.ModelInspection.Presentation;

internal sealed class ModelInspectionChatRequestedEventArgs : EventArgs
{
    internal ModelInspectionChatRequestedEventArgs(
        ModelInspectionRequest request,
        ModelInspectionExecutionResult execution,
        Action? reportLaunchFailed = null)
    {
        Request = request ?? throw new ArgumentNullException(nameof(request));
        Execution = execution ??
            throw new ArgumentNullException(nameof(execution));
        _reportLaunchFailed = reportLaunchFailed;
    }

    internal ModelInspectionRequest Request { get; }

    internal ModelInspectionExecutionResult Execution { get; }

    private readonly Action? _reportLaunchFailed;

    internal void ReportLaunchFailed() => _reportLaunchFailed?.Invoke();
}
