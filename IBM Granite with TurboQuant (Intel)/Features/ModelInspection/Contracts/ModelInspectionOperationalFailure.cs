namespace GraniteEdgeAI.Features.ModelInspection.Contracts;

/// <summary>
/// Describes an application-safe infrastructure failure that prevented a
/// reliable model outcome from being produced.
/// </summary>
internal sealed record ModelInspectionOperationalFailure
{
    /// <summary>
    /// creates one immutable failure with separate user and technical detail
    /// </summary>
    internal ModelInspectionOperationalFailure(
        string code,
        string userMessage,
        string technicalDetail)
    {
        Code = ModelInspectionContractValidation.RequireText(
            code,
            nameof(code));
        UserMessage = ModelInspectionContractValidation.RequireText(
            userMessage,
            nameof(userMessage));
        TechnicalDetail = ModelInspectionContractValidation.RequireText(
            technicalDetail,
            nameof(technicalDetail));
    }

    internal string Code { get; }

    internal string UserMessage { get; }

    internal string TechnicalDetail { get; }
}
