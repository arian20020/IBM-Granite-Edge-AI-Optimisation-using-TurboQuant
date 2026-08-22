using System;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;

namespace GraniteEdgeAI.Features.GgufRuntime.Controls;

public sealed partial class ChatMessageBubble : UserControl
{
    private const string CopyMessageLabel = "Copy message";
    private readonly Microsoft.UI.Dispatching.DispatcherQueueTimer copyFeedbackTimer;
    private bool isPointerOver;

    public static readonly DependencyProperty MessageContentProperty = DependencyProperty.Register(
        nameof(MessageContent), typeof(string), typeof(ChatMessageBubble),
        new PropertyMetadata(string.Empty, OnMessageContentChanged));
    public static readonly DependencyProperty StatusTextProperty = DependencyProperty.Register(
        nameof(StatusText), typeof(string), typeof(ChatMessageBubble),
        new PropertyMetadata(string.Empty));
    public static readonly DependencyProperty IsUserProperty = DependencyProperty.Register(
        nameof(IsUser), typeof(bool), typeof(ChatMessageBubble),
        new PropertyMetadata(false, OnIsUserChanged));

    public ChatMessageBubble()
    {
        InitializeComponent();
        copyFeedbackTimer = DispatcherQueue.CreateTimer();
        copyFeedbackTimer.Interval = TimeSpan.FromSeconds(1.5);
        copyFeedbackTimer.Tick += CopyFeedbackTimer_Tick;
        Unloaded += ChatMessageBubble_Unloaded;
        ApplyRole();
        UpdateCopyAvailability();
    }

    public event EventHandler<string>? CopyRequested;

    public string MessageContent
    {
        get => (string)GetValue(MessageContentProperty);
        set => SetValue(MessageContentProperty, value ?? string.Empty);
    }

    public string StatusText
    {
        get => (string)GetValue(StatusTextProperty);
        set => SetValue(StatusTextProperty, value ?? string.Empty);
    }

    public bool IsUser
    {
        get => (bool)GetValue(IsUserProperty);
        set => SetValue(IsUserProperty, value);
    }

    private static void OnIsUserChanged(
        DependencyObject sender,
        DependencyPropertyChangedEventArgs eventArguments) =>
        ((ChatMessageBubble)sender).ApplyRole();

    private static void OnMessageContentChanged(
        DependencyObject sender,
        DependencyPropertyChangedEventArgs eventArguments) =>
        ((ChatMessageBubble)sender).UpdateCopyAvailability();

    private void ApplyRole()
    {
        if (BubbleBorder is null)
        {
            return;
        }

        MessageContainer.HorizontalAlignment = IsUser
            ? HorizontalAlignment.Right
            : HorizontalAlignment.Left;
        CopyMessageButton.HorizontalAlignment = IsUser
            ? HorizontalAlignment.Right
            : HorizontalAlignment.Left;
        string surfaceKey = IsUser
            ? "GgufChatUserBubbleBrush"
            : "GgufChatAssistantBubbleBrush";
        string textKey = IsUser
            ? "GgufChatUserBubbleTextBrush"
            : "GgufChatAssistantBubbleTextBrush";
        BubbleBorder.Background = (Brush)Application.Current.Resources[surfaceKey];
        MessageText.Foreground = (Brush)Application.Current.Resources[textKey];
        AssistantIdentityText.Visibility = IsUser
            ? Visibility.Collapsed
            : Visibility.Visible;
    }

    internal void ShowCopyResult(bool succeeded)
    {
        copyFeedbackTimer.Stop();
        CopyMessageGlyph.Glyph = succeeded ? "\uE73E" : "\uE783";
        string label = succeeded ? "Copied" : "Couldn't copy";
        AutomationProperties.SetName(CopyMessageButton, label);
        ToolTipService.SetToolTip(CopyMessageButton, label);
        CopyMessageButton.Opacity = 1;
        copyFeedbackTimer.Start();
    }

    private void UpdateCopyAvailability()
    {
        if (CopyMessageButton is not null)
        {
            CopyMessageButton.IsEnabled = !string.IsNullOrEmpty(MessageContent);
        }
    }

    private void CopyMessageButton_Click(
        object sender,
        RoutedEventArgs eventArguments)
    {
        if (!string.IsNullOrEmpty(MessageContent))
        {
            CopyRequested?.Invoke(this, MessageContent);
        }
    }

    private void MessageContainer_PointerEntered(
        object sender,
        PointerRoutedEventArgs eventArguments)
    {
        isPointerOver = true;
        CopyMessageButton.Opacity = 1;
    }

    private void MessageContainer_PointerExited(
        object sender,
        PointerRoutedEventArgs eventArguments)
    {
        isPointerOver = false;
        UpdateCopyActionOpacity();
    }

    private void CopyMessageButton_GotFocus(
        object sender,
        RoutedEventArgs eventArguments) =>
        CopyMessageButton.Opacity = 1;

    private void CopyMessageButton_LostFocus(
        object sender,
        RoutedEventArgs eventArguments) =>
        UpdateCopyActionOpacity();

    private void CopyFeedbackTimer_Tick(
        Microsoft.UI.Dispatching.DispatcherQueueTimer sender,
        object eventArguments)
    {
        sender.Stop();
        CopyMessageGlyph.Glyph = "\uE8C8";
        AutomationProperties.SetName(CopyMessageButton, CopyMessageLabel);
        ToolTipService.SetToolTip(CopyMessageButton, CopyMessageLabel);
        UpdateCopyActionOpacity();
    }

    private void UpdateCopyActionOpacity()
    {
        CopyMessageButton.Opacity = isPointerOver ||
            CopyMessageButton.FocusState != FocusState.Unfocused
                ? 1
                : 0;
    }

    private void ChatMessageBubble_Unloaded(
        object sender,
        RoutedEventArgs eventArguments)
    {
        copyFeedbackTimer.Stop();
    }
}
