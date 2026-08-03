using GraniteEdgeAI.Features.ModelInspection.Models;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;

namespace GraniteEdgeAI.Features.ModelInspection.Controls
{
    /// <summary>
    /// Selects the correct content layout for the current inspection state.
    /// </summary>
    public sealed class InspectionContentTemplateSelector
        : DataTemplateSelector
    {
        /// <summary>
        /// Gets or sets the template displayed while inspection is running.
        /// </summary>
        public DataTemplate? ProgressTemplate { get; set; }

        /// <summary>
        /// Gets or sets the template displayed for completed non-ready states.
        /// </summary>
        public DataTemplate? FindingsTemplate { get; set; }

        /// <summary>
        /// Chooses one template based on the supplied presentation mode.
        /// </summary>
        protected override DataTemplate SelectTemplateCore(object item)
        {
            // WinUI may call the selector before valid content has been assigned.
            //
            // In that situation, let the base selector return no template
            // instead of crashing the complete page.
            if (item is not InspectionContentCardPresentation presentation)
            {
                return base.SelectTemplateCore(item);
            }

            // Select only between the genuinely different layouts.
            return presentation.Mode switch
            {
                // Inspection is currently running.
                InspectionContentCardMode.Progress =>
                    GetRequiredTemplate(
                        ProgressTemplate,
                        nameof(ProgressTemplate)),

                // The complete card is collapsed in Hidden mode,
                // so no content template is required.
                InspectionContentCardMode.Hidden =>
                    base.SelectTemplateCore(item),

                // Warnings, conversion and failure states all use
                // the shared findings structure.
                _ =>
                    GetRequiredTemplate(
                        FindingsTemplate,
                        nameof(FindingsTemplate))
            };
        }

        /// <summary>
        /// Returns a configured template or reports a clear configuration error.
        /// </summary>
        private static DataTemplate GetRequiredTemplate(
            DataTemplate? template,
            string propertyName)
        {
            // A visible mode cannot be rendered unless its XAML template
            // was supplied to this selector.
            return template ??
                throw new InvalidOperationException(
                    $"{propertyName} was not assigned in XAML.");
        }
    }
}