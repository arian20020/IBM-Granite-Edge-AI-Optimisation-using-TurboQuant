namespace GraniteEdgeAI.Features.ModelInspection.Models;

/// <summary>
/// Identifies the semantic vector mark displayed by an inspection status glyph.
/// </summary>
public enum InspectionStatusGlyphKind
{
    Success,
    Warning,
    Error,
    Information,
    Waiting,
    NotComplete,
    Active
}
