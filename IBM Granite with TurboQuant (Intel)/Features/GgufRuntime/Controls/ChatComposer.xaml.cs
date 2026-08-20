using System;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace GraniteEdgeAI.Features.GgufRuntime.Controls;

public sealed partial class ChatComposer : UserControl
{
    public static readonly DependencyProperty IsGeneratingProperty =
        DependencyProperty.Register(
            nameof(IsGenerating),
            typeof(bool),
            typeof(ChatComposer),
            new PropertyMetadata(false, OnIsGeneratingChanged));

    public ChatComposer()
    {
        InitializeComponent();
        ApplyGeneratingState();
    }

    public event EventHandler<string>? SendRequested;
    public event EventHandler? StopRequested;

    public bool IsGenerating
    {
        get => (bool)GetValue(IsGeneratingProperty);
        set => SetValue(IsGeneratingProperty, value);
    }

    public string PromptText
    {
        get => PromptTextBox.Text;
        set => PromptTextBox.Text = value ?? string.Empty;
    }

    private static void OnIsGeneratingChanged(
        DependencyObject sender,
        DependencyPropertyChangedEventArgs eventArguments) =>
        ((ChatComposer)sender).ApplyGeneratingState();

    private void ApplyGeneratingState()
    {
        if (StopButton is null)
        {
            return;
        }

        StopButton.Visibility = IsGenerating ? Visibility.Visible : Visibility.Collapsed;
        SendButton.Visibility = IsGenerating ? Visibility.Collapsed : Visibility.Visible;
        PromptTextBox.IsEnabled = !IsGenerating;
    }

    private void SendButton_Click(object sender, RoutedEventArgs eventArguments)
    {
        string prompt = PromptTextBox.Text.Trim();
        if (prompt.Length == 0)
        {
            return;
        }

        PromptTextBox.Text = string.Empty;
        SendRequested?.Invoke(this, prompt);
    }

    private void StopButton_Click(object sender, RoutedEventArgs eventArguments) =>
        StopRequested?.Invoke(this, EventArgs.Empty);
}
