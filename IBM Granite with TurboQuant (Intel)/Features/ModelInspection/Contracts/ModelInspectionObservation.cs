namespace GraniteEdgeAI.Features.ModelInspection.Contracts;

/// <summary>
/// Preserves one stable technical observation for deterministic application
/// classification without importing a worker transport type.
/// </summary>
internal sealed record ModelInspectionObservation
{
    /// <summary>
    /// creates one immutable technical observation
    /// </summary>
    internal ModelInspectionObservation(
        string code,
        string domain,
        string impact,
        string technicalDetail)
    {
        Code = ModelInspectionContractValidation.RequireText(
            code,
            nameof(code));
        Domain = ModelInspectionContractValidation.RequireText(
            domain,
            nameof(domain));
        Impact = ModelInspectionContractValidation.RequireText(
            impact,
            nameof(impact));
        TechnicalDetail = ModelInspectionContractValidation.RequireText(
            technicalDetail,
            nameof(technicalDetail));
    }

    internal string Code { get; }

    internal string Domain { get; }

    internal string Impact { get; }

    internal string TechnicalDetail { get; }
}
