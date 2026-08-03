using GraniteEdgeAI.Features.ModelInspection.Models;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;

namespace GraniteEdgeAI.Features.ModelInspection.Controls
{
    /// <summary>
    /// Selects the correct layout for the current inspection-content state.
    /// </summary>
    public sealed class InspectionContentTemplateSelector
        : DataTemplateSelector
    {
        /// <summary>
        /// Gets or sets the template used while inspection is running.
        /// </summary>
        public DataTemplate? ProgressTemplate { get; set; }

        /// <summary>
        /// Gets or sets the shared template used by completed non-ready states.
        /// </summary>
        public DataTemplate? FindingsTemplate { get; set; }

        /// <summary>
        /// Handles controls that ask for a template using only the content item.
        /// </summary>
        protected override DataTemplate SelectTemplateCore(object item)
        {
            // Keep all selection rules in one method so both WinUI overloads
            // always produce the same result.
            return ChooseTemplate(item);
        }

        /// <summary>
        /// Handles controls that ask for a template using the content item
        /// and its containing element.
        /// </summary>
        protected override DataTemplate SelectTemplateCore(
            object item,
            DependencyObject container)
        {
            // ContentControl can use this overload. The container itself is
            // not needed because selection depends only on Presentation.Mode.
            return ChooseTemplate(item);
        }

        /// <summary>
        /// Maps one inspection-content presentation to its required template.
        /// </summary>
        private DataTemplate ChooseTemplate(object item)
        {
            // The ContentControl contract requires exactly one presentation
            // object. An unexpected type indicates broken XAML wiring.
            if (item is not InspectionContentCardPresentation presentation)
            {
                throw new ArgumentException(
                    "InspectionContentTemplateSelector requires an " +
                    "InspectionContentCardPresentation.",
                    nameof(item));
            }

            return presentation.Mode switch
            {
                // Display the five-stage inspection tracker.
                InspectionContentCardMode.Progress =>
                    GetRequiredTemplate(
                        ProgressTemplate,
                        nameof(ProgressTemplate)),

                // Hidden presentations are collapsed by the surrounding card.
                // A non-null template is still returned so the ContentControl
                // never falls back to displaying the object's type name.
                InspectionContentCardMode.Hidden =>
                    GetRequiredTemplate(
                        ProgressTemplate,
                        nameof(ProgressTemplate)),

                // Every completed non-ready state shares FindingsTemplate.
                InspectionContentCardMode.Warnings or
                InspectionContentCardMode.ConversionRequired or
                InspectionContentCardMode.IncompletePackage or
                InspectionContentCardMode.Unsupported or
                InspectionContentCardMode.Invalid or
                InspectionContentCardMode.Cancelled or
                InspectionContentCardMode.OperationalFailure =>
                    GetRequiredTemplate(
                        FindingsTemplate,
                        nameof(FindingsTemplate)),

                // Make future enum additions fail clearly until a deliberate
                // template mapping is added.
                _ => throw new ArgumentOutOfRangeException(
                    nameof(item),
                    presentation.Mode,
                    "The inspection-content mode has no template mapping.")
            };
        }

        /// <summary>
        /// Returns a configured template or reports a clear XAML configuration
        /// error.
        /// </summary>
        private static DataTemplate GetRequiredTemplate(
            DataTemplate? template,
            string propertyName)
        {
            return template ??
                throw new InvalidOperationException(
                    $"{propertyName} was not assigned in " +
                    "InspectionContentCard.xaml.");
        }
    }
}