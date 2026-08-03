using GraniteEdgeAI.Features.ModelInspection.Models;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;

namespace GraniteEdgeAI.Features.ModelInspection.Controls
{
    /// <summary>
    /// Selects the progress layout or the shared findings layout for the
    /// current inspection-content presentation.
    /// </summary>
    public sealed class InspectionContentTemplateSelector
        : DataTemplateSelector
    {
        /// <summary>
        /// Gets or sets the template used while inspection is running.
        /// </summary>
        public DataTemplate? ProgressTemplate { get; set; }

        /// <summary>
        /// Gets or sets the template used by completed non-ready states.
        /// </summary>
        public DataTemplate? FindingsTemplate { get; set; }

        /// <summary>
        /// Handles selector calls that provide only an item.
        /// </summary>
        protected override DataTemplate SelectTemplateCore(object item)
        {
            return ChooseTemplate(item, container: null);
        }

        /// <summary>
        /// Handles selector calls that provide an item and its container.
        /// </summary>
        protected override DataTemplate SelectTemplateCore(
            object item,
            DependencyObject container)
        {
            return ChooseTemplate(item, container);
        }

        /// <summary>
        /// Resolves the presentation from either selector argument and maps its
        /// mode to the required XAML template.
        /// </summary>
        private DataTemplate ChooseTemplate(
            object? item,
            DependencyObject? container)
        {
            InspectionContentCardPresentation? presentation =
                ResolvePresentation(item, container);

            // WinUI can ask for a template before the Content binding has supplied
            // its value. The surrounding card is still collapsed at that point, so
            // the progress template is a safe, deterministic bootstrap template.
            if (presentation is null)
            {
                return GetRequiredTemplate(
                    ProgressTemplate,
                    nameof(ProgressTemplate));
            }

            return presentation.Mode switch
            {
                InspectionContentCardMode.Hidden or
                InspectionContentCardMode.Progress =>
                    GetRequiredTemplate(
                        ProgressTemplate,
                        nameof(ProgressTemplate)),

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

                _ => throw new ArgumentOutOfRangeException(
                    nameof(item),
                    presentation.Mode,
                    "The inspection-content mode has no template mapping.")
            };
        }

        /// <summary>
        /// Finds the presentation whether WinUI supplies it directly or through
        /// a ContentControl or ContentPresenter container.
        /// </summary>
        private static InspectionContentCardPresentation? ResolvePresentation(
            object? item,
            DependencyObject? container)
        {
            if (item is InspectionContentCardPresentation directPresentation)
            {
                return directPresentation;
            }

            if (item is ContentControl itemControl &&
                itemControl.Content is InspectionContentCardPresentation itemContent)
            {
                return itemContent;
            }

            if (item is ContentPresenter itemPresenter &&
                itemPresenter.Content is InspectionContentCardPresentation presenterContent)
            {
                return presenterContent;
            }

            if (container is ContentControl containerControl &&
                containerControl.Content is InspectionContentCardPresentation containerContent)
            {
                return containerContent;
            }

            if (container is ContentPresenter containerPresenter &&
                containerPresenter.Content is InspectionContentCardPresentation containerPresenterContent)
            {
                return containerPresenterContent;
            }

            return null;
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
