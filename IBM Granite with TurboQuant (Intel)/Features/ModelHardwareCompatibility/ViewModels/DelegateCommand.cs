using System;
using System.Windows.Input;

namespace GraniteEdgeAI.Features.ModelHardwareCompatibility.ViewModels;

/// <summary>
/// A command backed by a delegate, with an optional guard.
///
/// Declared here rather than shared with another feature: this one must not
/// depend on Model Inspection's internals, and a command this small is cheaper
/// to own than to couple to.
/// </summary>
internal sealed class DelegateCommand : ICommand
{
    private readonly Action _execute;
    private readonly Func<bool>? _canExecute;

    internal DelegateCommand(Action execute, Func<bool>? canExecute = null)
    {
        ArgumentNullException.ThrowIfNull(execute);

        _execute = execute;
        _canExecute = canExecute;
    }

    public event EventHandler? CanExecuteChanged;

    public bool CanExecute(object? parameter) => _canExecute?.Invoke() ?? true;

    public void Execute(object? parameter)
    {
        if (CanExecute(parameter))
        {
            _execute();
        }
    }

    /// <summary>
    /// Tells any bound control to ask again. Called when the state a guard reads
    /// has changed, since a guard cannot announce that for itself.
    /// </summary>
    internal void RaiseCanExecuteChanged() =>
        CanExecuteChanged?.Invoke(this, EventArgs.Empty);
}
