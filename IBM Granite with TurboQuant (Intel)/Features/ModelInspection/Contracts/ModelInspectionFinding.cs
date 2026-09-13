namespace GraniteEdgeAI.Features.ModelInspection.Contracts;

/// <summary>
/// Describes one application-classified model finding using stable diagnostics
/// and beginner-readable guidance.
/// </summary>
internal sealed record ModelInspectionFinding
{
    /// <summary>
    /// creates one immutable finding after validating every explanation field
    /// </summary>
    internal ModelInspectionFinding(
        string code,
        ModelInspectionFindingSeverity severity,
        string title,
        string explanation,
        string recommendedAction,
        string technicalDetail)
    {
        Code = ModelInspectionContractValidation.RequireText(
            code,
            nameof(code));
        Severity = ModelInspectionContractValidation.RequireDefinedEnum(
            severity,
            nameof(severity));
        Title = ModelInspectionContractValidation.RequireText(
            title,
            nameof(title));
        Explanation = ModelInspectionContractValidation.RequireText(
            explanation,
            nameof(explanation));
        RecommendedAction = ModelInspectionContractValidation.RequireText(
            recommendedAction,
            nameof(recommendedAction));
        TechnicalDetail = ModelInspectionContractValidation.RequireText(
            technicalDetail,
            nameof(technicalDetail));
    }

    internal string Code { get; }

    internal ModelInspectionFindingSeverity Severity { get; }

    internal string Title { get; }

    internal string Explanation { get; }

    internal string RecommendedAction { get; }

    internal string TechnicalDetail { get; }
}
