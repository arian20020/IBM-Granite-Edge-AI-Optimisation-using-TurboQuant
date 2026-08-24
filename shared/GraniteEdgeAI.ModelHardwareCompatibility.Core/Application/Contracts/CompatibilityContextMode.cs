namespace GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Contracts;

/// <summary>
/// How the planning context length was chosen.
/// </summary>
internal enum CompatibilityContextMode
{
    Unspecified = 0,
    ApplicationDefault,
    UserRequested
}
