namespace GraniteEdgeAI.ModelInspection.LlamaSharp.ModelProbe;

/// <summary>
/// Contains one controlled feasibility failure without retaining an exception
/// object or a native runtime type.
/// </summary>
/// <param name="Code">Stable project diagnostic code.</param>
/// <param name="Type">Observed exception type when available.</param>
/// <param name="Message">Technical message before privacy redaction.</param>
public sealed record ProbeFailure(
    string Code,
    string? Type,
    string Message);
