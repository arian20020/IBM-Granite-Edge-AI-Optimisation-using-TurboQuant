using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Optimization;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.Windows.Storage.Pickers;

namespace GraniteEdgeAI.Features.OpenVinoRoute.Optimization;

internal sealed class OpenVinoOptimizationPage : Page
{
    private readonly OptimizationExecutionPlan _plan;
    private readonly Func<string?, IProgress<OpenVinoOptimizationProgress>,
        CancellationToken, Task<OptimizationExecutionResult>> _execute;
    private readonly TextBlock _status;
    private readonly Button _primary;
    private readonly Button _back;
    private CancellationTokenSource? _cancellation;

    internal OpenVinoOptimizationPage(
        OptimizationExecutionPlan plan,
        Func<string?, IProgress<OpenVinoOptimizationProgress>, CancellationToken,
            Task<OptimizationExecutionResult>> execute)
    {
        _plan = plan ?? throw new ArgumentNullException(nameof(plan));
        _execute = execute ?? throw new ArgumentNullException(nameof(execute));

        Background = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 246, 248, 252));
        var title = new TextBlock
        {
            Text = "Configure OpenVINO",
            FontSize = 38,
            FontWeight = Microsoft.UI.Text.FontWeights.Bold,
            HorizontalAlignment = HorizontalAlignment.Center
        };
        var lede = new TextBlock
        {
            Text = "Review and apply the exact OpenVINO configuration selected by compatibility.",
            FontSize = 17,
            Foreground = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 91, 103, 128)),
            TextWrapping = TextWrapping.Wrap,
            HorizontalAlignment = HorizontalAlignment.Center
        };
        var card = new Border
        {
            Background = new SolidColorBrush(
                Windows.UI.Color.FromArgb(255, 255, 255, 255)),
            BorderBrush = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 214, 222, 236)),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(16),
            Padding = new Thickness(30),
            MaxWidth = 1050,
            HorizontalAlignment = HorizontalAlignment.Stretch
        };
        string disposition = plan.ProducesPersistentArtifact
            ? "This creates a new converted OpenVINO package. The original remains unchanged."
            : "This stores a runtime profile only. It does not create or rewrite a model package.";
        var details = new StackPanel { Spacing = 14 };
        details.Children.Add(new TextBlock
        {
            Text = plan.ProducesPersistentArtifact
                ? "Persistent OpenVINO optimisation"
                : "OpenVINO runtime configuration",
            FontSize = 24,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold
        });
        details.Children.Add(new TextBlock
        {
            Text = disposition,
            TextWrapping = TextWrapping.Wrap,
            FontSize = 16
        });
        details.Children.Add(Row("Device", plan.ExecutionPayload.OpenVino!.Device));
        details.Children.Add(Row("Configuration", plan.ExecutionPayload.OpenVino.ConfigurationId));
        details.Children.Add(Row("Evidence", plan.ExecutionPayload.OpenVino.EvidenceId));
        details.Children.Add(Row("TurboQuant", "Not used or claimed"));
        _status = new TextBlock
        {
            Text = "Ready to apply. Nothing has changed yet.",
            TextWrapping = TextWrapping.Wrap,
            Foreground = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 63, 76, 102))
        };
        details.Children.Add(_status);
        var actions = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Spacing = 12,
            HorizontalAlignment = HorizontalAlignment.Right
        };
        _back = new Button { Content = "Back", MinWidth = 140 };
        _back.Click += (_, _) =>
        {
            if (_cancellation is { } active)
            {
                active.Cancel();
                _status.Text = "Cancelling safelyâ€¦";
                _back.IsEnabled = false;
                return;
            }

            BackRequested?.Invoke(this, EventArgs.Empty);
        };
        _primary = new Button
        {
            Content = plan.ProducesPersistentArtifact
                ? "Choose destination and optimise"
                : "Apply configuration",
            MinWidth = 230
        };
        _primary.Click += async (_, _) => await ExecuteAsync();
        actions.Children.Add(_back);
        actions.Children.Add(_primary);
        details.Children.Add(actions);
        card.Child = details;

        var content = new StackPanel
        {
            Spacing = 24,
            Margin = new Thickness(40, 28, 40, 40),
            HorizontalAlignment = HorizontalAlignment.Stretch
        };
        content.Children.Add(title);
        content.Children.Add(lede);
        content.Children.Add(card);
        Content = new ScrollViewer { Content = content };
    }

    internal event EventHandler? BackRequested;
    internal event EventHandler<OpenVinoOptimizationCompletedEventArgs>? Completed;

    private async Task ExecuteAsync()
    {
        string? destination = null;
        if (_plan.ProducesPersistentArtifact)
        {
            PickFolderResult? folder = await new FolderPicker(App.MainWindow.AppWindow.Id)
                .PickSingleFolderAsync();
            if (folder is null) return;
            destination = folder.Path;
        }

        _cancellation?.Cancel();
        _cancellation?.Dispose();
        _cancellation = new CancellationTokenSource();
        _primary.IsEnabled = false;
        _back.Content = "Cancel";
        _back.IsEnabled = true;
        _status.Text = "Checking the plan and current evidence…";
        bool succeeded = false;
        try
        {
            var progress = new Progress<OpenVinoOptimizationProgress>(value =>
                _status.Text = value.Stage switch
                {
                    OpenVinoOptimizationStage.Preflight => "Checking model, hardware, capability and build identities…",
                    OpenVinoOptimizationStage.Optimizing => "Optimising the OpenVINO package…",
                    OpenVinoOptimizationStage.ValidatingOutput => "Validating the output…",
                    OpenVinoOptimizationStage.SmokeTesting => "Testing the selected setup…",
                    OpenVinoOptimizationStage.Publishing => "Publishing atomically…",
                    OpenVinoOptimizationStage.Reinspecting => "Re-inspecting the published package…",
                    OpenVinoOptimizationStage.Completed => "Configuration complete.",
                    _ => "Working…"
                });
            OptimizationExecutionResult result = await _execute(
                destination,
                progress,
                _cancellation.Token);
            _status.Text = result.Status switch
            {
                OptimizationExecutionStatus.SucceededPersistent =>
                    "Optimisation completed. A verified package was created and the original is unchanged.",
                OptimizationExecutionStatus.SucceededRuntimeProfile =>
                    "Configuration completed. A runtime profile was stored and no model artifact was created.",
                OptimizationExecutionStatus.ReplanRequired =>
                    "The evidence changed. Run model and hardware inspection again before continuing.",
                OptimizationExecutionStatus.Cancelled => "Configuration cancelled. Nothing was published.",
                _ => $"Configuration failed safely. Support code: {result.SupportCode}."
            };
            if (result.IsSuccessful)
            {
                succeeded = true;
                _primary.Visibility = Visibility.Collapsed;
                Completed?.Invoke(
                    this,
                    new OpenVinoOptimizationCompletedEventArgs(result));
            }
        }
        catch (OperationCanceledException)
        {
            _status.Text = "Configuration cancelled. Nothing was published.";
        }
        catch (Exception)
        {
            _status.Text = "Configuration failed safely. Run the inspections again or check the local OpenVINO installation.";
        }
        finally
        {
            _cancellation?.Dispose();
            _cancellation = null;
            _back.IsEnabled = true;
            _back.Content = succeeded ? "Done" : "Back";
            if (_primary.Visibility == Visibility.Visible) _primary.IsEnabled = true;
        }
    }

    internal void CancelAndDispose()
    {
        _cancellation?.Cancel();
        _cancellation?.Dispose();
        _cancellation = null;
    }

    private static Grid Row(string label, string value)
    {
        var grid = new Grid { ColumnSpacing = 20 };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(170) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        var left = new TextBlock { Text = label, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold };
        var right = new TextBlock { Text = value, TextWrapping = TextWrapping.Wrap };
        Grid.SetColumn(right, 1);
        grid.Children.Add(left);
        grid.Children.Add(right);
        return grid;
    }
}

internal sealed class OpenVinoOptimizationCompletedEventArgs : EventArgs
{
    internal OpenVinoOptimizationCompletedEventArgs(
        OptimizationExecutionResult result)
    {
        Result = result ?? throw new ArgumentNullException(nameof(result));
    }

    internal OptimizationExecutionResult Result { get; }
}
