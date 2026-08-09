using Microsoft.UI.Xaml;
using GraniteEdgeAI.Features.ModelInspection.Presentation;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;

namespace GraniteEdgeAI.Features.ModelInspection.Models
{
    /// <summary>
    /// Contains all display data required by <c>InspectionContentCard</c>.
    /// </summary>
    public sealed class InspectionContentCardPresentation : INotifyPropertyChanged
    {
        // Stores the only presentation value that changes inside the XAML control.
        private bool _isExpanded;
        private string _progressSummary = string.Empty;
        private IReadOnlyList<InspectionContentItemPresentation> _items =
            new List<InspectionContentItemPresentation>().AsReadOnly();
        private InspectionProgressRows? _progressRows;

        /// <summary>
        /// Provides a safe presentation while the card is hidden.
        /// </summary>
        public static InspectionContentCardPresentation Hidden => new();

        /// <summary>
        /// Gets the structural layout displayed by the card.
        /// </summary>
        public InspectionContentCardMode Mode { get; init; } =
            InspectionContentCardMode.Hidden;

        /// <summary>
        /// Gets the heading displayed at the top of the active layout.
        /// </summary>
        public string SectionTitle { get; init; } = string.Empty;

        /// <summary>
        /// Gets the progress summary, for example “3 of 5 checks complete”.
        /// </summary>
        public string ProgressSummary
        {
            get => Mode == InspectionContentCardMode.Progress
                ? ProgressRows.ProgressSummary
                : _progressSummary;
            init => _progressSummary = value;
        }

        /// <summary>
        /// Gets the primary stage or finding rows.
        /// </summary>
        public IReadOnlyList<InspectionContentItemPresentation> Items
        {
            get => Mode == InspectionContentCardMode.Progress
                ? ProgressRows.Items
                : _items;
            init => _items = value;
        }

        /// <summary>
        /// Gets the stable observable owner used only by the progress layout.
        /// </summary>
        public InspectionProgressRows ProgressRows
        {
            get => _progressRows ??= new InspectionProgressRows();
            init
            {
                ArgumentNullException.ThrowIfNull(value);
                _progressRows = value;
            }
        }

        /// <summary>
        /// Gets the optional supporting instruction beneath the findings.
        /// </summary>
        public string SupportingText { get; init; } = string.Empty;

        /// <summary>
        /// Gets whether the supporting instruction is displayed.
        /// </summary>
        public Visibility SupportingTextVisibility { get; init; } =
            Visibility.Collapsed;

        /// <summary>
        /// Gets the optional blocking or safety statement.
        /// </summary>
        public string TertiaryText { get; init; } = string.Empty;

        /// <summary>
        /// Gets the status used to emphasise the tertiary statement.
        /// </summary>
        public InspectionContentStatus TertiaryStatus { get; init; } =
            InspectionContentStatus.Neutral;

        /// <summary>
        /// Gets whether the tertiary statement is displayed.
        /// </summary>
        public Visibility TertiaryTextVisibility { get; init; } =
            Visibility.Collapsed;

        /// <summary>
        /// Gets the optional stable diagnostic code.
        /// </summary>
        public string DiagnosticCode { get; init; } = string.Empty;

        /// <summary>
        /// Gets the semantic status used to colour the diagnostic code.
        /// </summary>
        public InspectionContentStatus DiagnosticStatus { get; init; } =
            InspectionContentStatus.Error;

        /// <summary>
        /// Gets whether the diagnostic code is displayed.
        /// </summary>
        public Visibility DiagnosticCodeVisibility { get; init; } =
            Visibility.Collapsed;

        /// <summary>
        /// Gets the semantic status shown in the disclosure header.
        /// </summary>
        public InspectionContentStatus DisclosureStatus { get; init; } =
            InspectionContentStatus.Information;

        /// <summary>
        /// Gets the summary shown beside the disclosure icon.
        /// </summary>
        public string DisclosureSummary { get; init; } = string.Empty;

        /// <summary>
        /// Gets the disclosure label used while the report is collapsed.
        /// </summary>
        public string CollapsedDisclosureText { get; init; } =
            "View details";

        /// <summary>
        /// Gets the disclosure label used while the report is expanded.
        /// </summary>
        public string ExpandedDisclosureText { get; init; } =
            "Hide details";

        /// <summary>
        /// Gets the accessible name of the disclosure control.
        /// </summary>
        public string DisclosureAutomationName { get; init; } =
            "Inspection details";

        /// <summary>
        /// Gets or sets whether the inline report is expanded.
        /// </summary>
        public bool IsExpanded
        {
            get => _isExpanded;
            set
            {
                if (_isExpanded == value)
                {
                    return;
                }

                _isExpanded = value;
                OnPropertyChanged();
            }
        }

        /// <summary>
        /// Gets whether the inline report expander is displayed.
        /// </summary>
        public Visibility DisclosureVisibility { get; init; } =
            Visibility.Collapsed;

        /// <summary>
        /// Gets the rows displayed inside the expanded report.
        /// </summary>
        public IReadOnlyList<InspectionContentItemPresentation> ExpandedItems { get; init; } =
            Array.Empty<InspectionContentItemPresentation>();

        /// <summary>
        /// Gets the command that opens a separate technical-details view.
        /// </summary>
        public ICommand? OpenTechnicalDetailsCommand { get; init; }

        /// <summary>
        /// Gets the text shown by the technical-details action.
        /// </summary>
        public string TechnicalDetailsActionText { get; init; } =
            "View technical details";

        /// <summary>
        /// Gets the accessible name of the technical-details action.
        /// </summary>
        public string TechnicalDetailsAutomationName { get; init; } =
            "View technical details";

        /// <summary>
        /// Gets whether the technical-details action can currently execute.
        /// </summary>
        public bool IsTechnicalDetailsEnabled { get; init; }

        /// <summary>
        /// Gets the accessible explanation for an unavailable details action.
        /// </summary>
        public string TechnicalDetailsAutomationHelpText { get; init; } =
            string.Empty;

        /// <summary>
        /// Gets whether the technical-details action is displayed.
        /// </summary>
        public Visibility TechnicalDetailsVisibility { get; init; } =
            Visibility.Collapsed;

        /// <summary>
        /// Notifies compiled bindings when a mutable presentation value changes.
        /// </summary>
        public event PropertyChangedEventHandler? PropertyChanged;

        /// <summary>
        /// Raises a standard property-change notification.
        /// </summary>
        private void OnPropertyChanged(
            [CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(
                this,
                new PropertyChangedEventArgs(propertyName));
        }
    }
}
