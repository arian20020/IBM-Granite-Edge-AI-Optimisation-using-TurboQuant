using System;
using System.Collections.Generic;

namespace GraniteEdgeAI.Features.ModelInspection.Models
{
    /// <summary>
    /// Contains all data displayed by <c>InspectionModelCard</c>.
    /// </summary>
    public sealed class InspectionModelCardPresentation
    {
        /// <summary>
        /// Provides safe placeholder values before a real model is supplied.
        /// </summary>
        public static InspectionModelCardPresentation Empty { get; } = new();

        /// <summary>
        /// Gets the structural layout used by the model card.
        /// </summary>
        public InspectionModelCardMode DisplayMode { get; init; } =
            InspectionModelCardMode.Compact;

        /// <summary>
        /// Gets the compact status-badge state.
        /// </summary>
        public InspectionModelBadgeState BadgeState { get; init; } =
            InspectionModelBadgeState.ModelSelected;

        /// <summary>
        /// Gets the model name.
        /// </summary>
        public string ModelName { get; init; } = string.Empty;

        /// <summary>
        /// Gets the concise format, quantisation and size summary.
        /// </summary>
        public string CompactSummary { get; init; } = string.Empty;

        /// <summary>
        /// Gets the short label shown inside the format icon.
        /// </summary>
        public string FormatShortName { get; init; } = string.Empty;

        /// <summary>
        /// Gets the format badge shown in the detailed overview.
        /// </summary>
        public string OverviewFormatBadgeText { get; init; } = string.Empty;

        /// <summary>
        /// Gets the model publisher.
        /// </summary>
        public string Publisher { get; init; } = string.Empty;

        /// <summary>
        /// Gets the complete model-format name.
        /// </summary>
        public string FormatName { get; init; } = string.Empty;

        /// <summary>
        /// Gets the model quantisation description.
        /// </summary>
        public string Quantisation { get; init; } = string.Empty;

        /// <summary>
        /// Gets the formatted model parameter count.
        /// </summary>
        public string ParameterCount { get; init; } = string.Empty;

        /// <summary>
        /// Gets the model type, for example “Instruction tuned”.
        /// </summary>
        public string ModelType { get; init; } = string.Empty;

        /// <summary>
        /// Gets the declared maximum context from model metadata.
        /// </summary>
        public string DeclaredContext { get; init; } = string.Empty;

        /// <summary>
        /// Gets the formatted model-package size.
        /// </summary>
        public string FileSize { get; init; } = string.Empty;

        /// <summary>
        /// Gets the inspection-check summary shown in the expander header.
        /// </summary>
        public string InspectionChecksSummary { get; init; } = string.Empty;

        /// <summary>
        /// Gets the inspection checks displayed in the expanded report.
        /// </summary>
        public IReadOnlyList<InspectionCheckPresentation> InspectionChecks { get; init; } =
            Array.Empty<InspectionCheckPresentation>();
    }
}
