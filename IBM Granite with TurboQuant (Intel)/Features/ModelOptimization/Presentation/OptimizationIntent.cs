using System;
namespace GraniteEdgeAI.Features.ModelOptimization.Presentation;

internal enum OptimizationCommand
{
    Confirm,
    Cancel,
    Retry,
    BackToCompatibility,
    Chat,
    ChatWithOriginal,
    Save,
    Done,
    ImportAnotherModel
}

internal sealed class OptimizationIntentEventArgs : EventArgs
{
    internal OptimizationIntentEventArgs(OptimizationCommand command)
    {
        Command = command;
    }

    internal OptimizationCommand Command { get; }
}
