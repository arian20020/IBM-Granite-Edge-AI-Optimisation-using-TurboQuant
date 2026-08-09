using GraniteEdgeAI.Features.ModelInspection.Models;
using GraniteEdgeAI.Features.ModelInspection.ViewModels;
using System;

namespace GraniteEdgeAI.Features.ModelInspection.Presentation;

/// <summary>
/// Contains one complete, replaceable presentation snapshot for the four
/// Model Inspection cards.
/// </summary>
internal sealed class ModelInspectionPagePresentation
{
    internal ModelInspectionPagePresentation(
        ModelInspectionRenderKey renderKey,
        ModelInspectionFigmaState state,
        InspectionModelCardPresentation modelCard,
        InspectionContentCardPresentation contentCard,
        InspectionOutcomePresentation outcomeCard,
        InspectionActionCardPresentation actionCard,
        InspectionFooterStatus footerStatus,
        ModelInspectionRegionKeys regionKeys,
        string progressAnnouncement,
        string outcomeAnnouncement)
    {
        if (!Enum.IsDefined(state))
        {
            throw new ArgumentOutOfRangeException(nameof(state), state, null);
        }

        if (!Enum.IsDefined(footerStatus))
        {
            throw new ArgumentOutOfRangeException(
                nameof(footerStatus),
                footerStatus,
                null);
        }

        RenderKey = renderKey;
        State = state;
        ModelCard = modelCard ?? throw new ArgumentNullException(nameof(modelCard));
        ContentCard = contentCard ??
            throw new ArgumentNullException(nameof(contentCard));
        OutcomeCard = outcomeCard ??
            throw new ArgumentNullException(nameof(outcomeCard));
        ActionCard = actionCard ??
            throw new ArgumentNullException(nameof(actionCard));
        FooterStatus = footerStatus;
        RegionKeys = regionKeys ?? throw new ArgumentNullException(nameof(regionKeys));
        ProgressAnnouncement = ProjectAnnouncement(
            progressAnnouncement,
            nameof(progressAnnouncement));
        OutcomeAnnouncement = ProjectAnnouncement(
            outcomeAnnouncement,
            nameof(outcomeAnnouncement));

        ValidateExpansion(state, modelCard, contentCard);
    }

    internal ModelInspectionRenderKey RenderKey { get; }

    internal ModelInspectionFigmaState State { get; }

    internal InspectionModelCardPresentation ModelCard { get; }

    internal InspectionContentCardPresentation ContentCard { get; }

    internal InspectionOutcomePresentation OutcomeCard { get; }

    internal InspectionActionCardPresentation ActionCard { get; }

    internal InspectionFooterStatus FooterStatus { get; }

    internal ModelInspectionRegionKeys RegionKeys { get; }

    internal string ProgressAnnouncement { get; }

    internal string OutcomeAnnouncement { get; }

    private static string ProjectAnnouncement(string value, string parameterName)
    {
        ArgumentNullException.ThrowIfNull(value, parameterName);
        if (value.Length > 512)
        {
            throw new ArgumentOutOfRangeException(parameterName);
        }

        return value.Length == 0
            ? string.Empty
            : ModelInspectionDisplayTextPolicy.ProjectRequiredDetail(
                value,
                "Inspection status changed.");
    }

    private static void ValidateExpansion(
        ModelInspectionFigmaState state,
        InspectionModelCardPresentation modelCard,
        InspectionContentCardPresentation contentCard)
    {
        bool expectedModelExpansion = state == ModelInspectionFigmaState.ReadyExpanded;
        bool expectedContentExpansion = state is
            ModelInspectionFigmaState.ReadyWithWarningsExpanded or
            ModelInspectionFigmaState.ConversionRequiredExpanded or
            ModelInspectionFigmaState.InvalidExpanded;

        if (modelCard.IsInspectionDetailsExpanded != expectedModelExpansion ||
            contentCard.IsExpanded != expectedContentExpansion)
        {
            throw new ArgumentException(
                "Disclosure expansion does not match the approved Figma state.",
                nameof(state));
        }
    }
}
