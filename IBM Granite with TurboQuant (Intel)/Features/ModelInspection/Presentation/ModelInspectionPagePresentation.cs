using GraniteEdgeAI.Features.ModelInspection.Models;
using System;

namespace GraniteEdgeAI.Features.ModelInspection.Presentation;

/// <summary>
/// Contains one complete, replaceable presentation snapshot for the four
/// Model Inspection cards.
/// </summary>
internal sealed class ModelInspectionPagePresentation
{
    internal ModelInspectionPagePresentation(
        InspectionModelCardPresentation modelCard,
        InspectionContentCardPresentation contentCard,
        InspectionOutcomePresentation outcomeCard,
        InspectionActionCardPresentation actionCard)
    {
        ModelCard = modelCard ?? throw new ArgumentNullException(nameof(modelCard));
        ContentCard = contentCard ??
            throw new ArgumentNullException(nameof(contentCard));
        OutcomeCard = outcomeCard ??
            throw new ArgumentNullException(nameof(outcomeCard));
        ActionCard = actionCard ??
            throw new ArgumentNullException(nameof(actionCard));
    }

    internal InspectionModelCardPresentation ModelCard { get; }

    internal InspectionContentCardPresentation ContentCard { get; }

    internal InspectionOutcomePresentation OutcomeCard { get; }

    internal InspectionActionCardPresentation ActionCard { get; }
}
