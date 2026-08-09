using Microsoft.UI.Xaml;
using System.Windows.Input;

namespace GraniteEdgeAI.Features.ModelInspection.Models
{
    /// <summary>
    /// Contains everything needed to display and execute one action-card button.
    /// </summary>
    public sealed class InspectionActionPresentation
    {
        /// <summary>
        /// Provides a safe non-null action for button slots that are not used.
        /// </summary>
        public static InspectionActionPresentation Hidden { get; } = new();

        /// <summary>
        /// Gets the text shown inside the button.
        /// </summary>
        public string Text { get; init; } = string.Empty;

        /// <summary>
        /// Gets the command executed when the button is selected.
        /// </summary>
        public ICommand? Command { get; init; }

        /// <summary>
        /// Gets optional data passed to the command.
        /// </summary>
        public object? CommandParameter { get; init; }

        /// <summary>
        /// Gets whether the user can currently select the action.
        /// </summary>
        public bool IsEnabled { get; init; } = true;

        /// <summary>
        /// Gets whether this button slot is displayed.
        /// </summary>
        public Visibility Visibility { get; init; } = Visibility.Collapsed;

        /// <summary>
        /// Gets the descriptive name exposed to accessibility tools.
        /// </summary>
        public string AutomationName { get; init; } = string.Empty;

        /// <summary>
        /// Gets the accessible explanation for an unavailable action.
        /// </summary>
        public string AutomationHelpText { get; init; } = string.Empty;

        /// <summary>
        /// Gets the minimum width of the button in effective pixels.
        /// </summary>
        public double MinimumWidth { get; init; } = 174d;
    }
}
