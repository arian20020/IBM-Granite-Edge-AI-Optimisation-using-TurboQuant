namespace GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Contracts;

/// <summary>
/// How a run ended. Success requires a value; cancelled and failed must not
/// retain one.
/// </summary>
internal enum CompatibilityRunOutcome
{
    Unspecified = 0,
    Completed,
    NotEstablished,
    Failed,
    Cancelled
}
