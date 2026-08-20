using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace GraniteEdgeAI.Features.GgufRuntime.Controls;

public sealed partial class ChatMessageBubble : UserControl
{
    public static readonly DependencyProperty MessageContentProperty = DependencyProperty.Register(
        nameof(MessageContent), typeof(string), typeof(ChatMessageBubble),
        new PropertyMetadata(string.Empty));
    public static readonly DependencyProperty StatusTextProperty = DependencyProperty.Register(
        nameof(StatusText), typeof(string), typeof(ChatMessageBubble),
        new PropertyMetadata(string.Empty));
    public static readonly DependencyProperty IsUserProperty = DependencyProperty.Register(
        nameof(IsUser), typeof(bool), typeof(ChatMessageBubble),
        new PropertyMetadata(false, OnIsUserChanged));

    public ChatMessageBubble()
    {
        InitializeComponent();
        ApplyRole();
    }

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

    private void ApplyRole()
    {
        if (BubbleBorder is null)
        {
            return;
        }

        BubbleBorder.HorizontalAlignment = IsUser
            ? HorizontalAlignment.Right
            : HorizontalAlignment.Left;
        BubbleBorder.Background = IsUser
            ? (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["GgufChatUserBubbleBrush"]
            : (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["GgufChatSurfaceBrush"];
    }
}
