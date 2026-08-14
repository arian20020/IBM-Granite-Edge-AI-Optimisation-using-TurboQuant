using Microsoft.UI.Xaml;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace GraniteEdgeAI.Features.ModelInspection.Models
{
    /// <summary>
    /// Contains the display data for one inspection stage, warning or report row.
    /// Progress-only values notify compiled one-way bindings when the stable row
    /// owner changes them. Finding and report rows remain immutable after setup.
    /// </summary>
    public sealed class InspectionContentItemPresentation : INotifyPropertyChanged
    {
        private string _detail = string.Empty;
        private Visibility _detailVisibility = Visibility.Collapsed;
        private InspectionContentStatus _status = InspectionContentStatus.Neutral;
        private string _statusText = string.Empty;
        private bool _isActive;
        private double? _stageFraction;
        private string _automationName = string.Empty;

        /// <summary>
        /// Gets the stage number shown while the item is used in the progress tracker.
        /// </summary>
        public string StageNumber { get; init; } = string.Empty;

        /// <summary>
        /// Gets the primary row label.
        /// </summary>
        public string Title { get; init; } = string.Empty;

        /// <summary>
        /// Gets the stable accessible explanation used when the visual detail is
        /// intentionally collapsed for a waiting or completed progress row.
        /// </summary>
        public string DefaultDetail { get; init; } = string.Empty;

        /// <summary>
        /// Gets the optional explanation shown beneath the row label.
        /// </summary>
        public string Detail
        {
            get => _detail;
            internal set
            {
                if (string.Equals(_detail, value, StringComparison.Ordinal))
                {
                    return;
                }

                _detail = value;
                PropertyChanged?.Invoke(
                    this,
                    new PropertyChangedEventArgs(nameof(Detail)));
                PropertyChanged?.Invoke(
                    this,
                    new PropertyChangedEventArgs(nameof(AutomationHelpText)));
            }
        }

        /// <summary>
        /// Gets the row's current accessible explanation without making a
        /// collapsed visual detail visible.
        /// </summary>
        public string AutomationHelpText => string.IsNullOrWhiteSpace(Detail)
            ? DefaultDetail
            : Detail;

        /// <summary>
        /// Gets whether the optional explanation is displayed.
        /// </summary>
        public Visibility DetailVisibility
        {
            get => _detailVisibility;
            internal set => SetProperty(ref _detailVisibility, value);
        }

        /// <summary>
        /// Gets the semantic status used for the icon, text and badge.
        /// </summary>
        public InspectionContentStatus Status
        {
            get => _status;
            internal set => SetProperty(ref _status, value);
        }

        /// <summary>
        /// Gets the explicit status text shown to the user.
        /// </summary>
        public string StatusText
        {
            get => _statusText;
            internal set => SetProperty(ref _statusText, value);
        }

        /// <summary>
        /// Gets whether the progress ring should animate for this stage.
        /// </summary>
        public bool IsActive
        {
            get => _isActive;
            internal set => SetProperty(ref _isActive, value);
        }

        /// <summary>
        /// Gets genuine measurable progress within this stage, or null when
        /// the runtime cannot truthfully quantify the remaining work.
        /// </summary>
        public double? StageFraction
        {
            get => _stageFraction;
            internal set => SetProperty(ref _stageFraction, value);
        }

        /// <summary>
        /// Gets whether the connector below this stage is displayed.
        /// </summary>
        public bool ShowConnector { get; init; }

        /// <summary>
        /// Gets the complete accessible description of this row.
        /// </summary>
        public string AutomationName
        {
            get => _automationName;
            internal set => SetProperty(ref _automationName, value);
        }

        /// <summary>
        /// Notifies one-way bindings only when a mutable progress value changes.
        /// </summary>
        public event PropertyChangedEventHandler? PropertyChanged;

        private void SetProperty<T>(
            ref T field,
            T value,
            [CallerMemberName] string? propertyName = null)
        {
            if (EqualityComparer<T>.Default.Equals(field, value))
            {
                return;
            }

            field = value;
            PropertyChanged?.Invoke(
                this,
                new PropertyChangedEventArgs(propertyName));
        }
    }
}
