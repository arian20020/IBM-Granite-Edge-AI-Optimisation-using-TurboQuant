using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using GraniteEdgeAI.Features.GgufRuntime.Services;

namespace GraniteEdgeAI.Features.GgufRuntime.ViewModels;

internal sealed class ChatPageViewModel : INotifyPropertyChanged, IAsyncDisposable
{
    private readonly GgufChatCoordinator coordinator;

    internal ChatPageViewModel(GgufChatCoordinator coordinator)
    {
        this.coordinator = coordinator ?? throw new ArgumentNullException(nameof(coordinator));
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    internal ObservableCollection<ChatHistoryGroupViewModel> HistoryGroups { get; } = [];
    internal ChatConversationViewModel? SelectedConversation { get; private set; }
    internal bool CanSend => SelectedConversation is not null && !coordinator.IsGenerating;
    internal bool CanStop => coordinator.IsGenerating;

    internal async Task InitializeAsync(CancellationToken cancellationToken)
    {
        await coordinator.InitializeAsync(cancellationToken);
        Refresh();
    }

    internal async Task NewChatAsync(
        string modelId,
        string profileId,
        CancellationToken cancellationToken)
    {
        await coordinator.NewChatAsync(modelId, profileId, cancellationToken);
        Refresh();
    }

    internal async Task SendAsync(string prompt, CancellationToken cancellationToken)
    {
        OnStateChanged();
        try
        {
            await coordinator.SendAsync(prompt, cancellationToken);
        }
        finally
        {
            Refresh();
        }
    }

    internal ValueTask StopAsync(CancellationToken cancellationToken) =>
        coordinator.StopAsync(cancellationToken);

    public ValueTask DisposeAsync() => coordinator.DisposeAsync();

    private void Refresh()
    {
        ChatCoordinatorSnapshot snapshot = coordinator.CaptureSnapshot();
        HistoryGroups.Clear();
        foreach (var group in snapshot.Groups)
        {
            HistoryGroups.Add(new ChatHistoryGroupViewModel(group));
        }

        SelectedConversation = snapshot.SelectedConversation is null
            ? null
            : new ChatConversationViewModel(snapshot.SelectedConversation);
        OnStateChanged();
    }

    private void OnStateChanged()
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(HistoryGroups)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(SelectedConversation)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CanSend)));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CanStop)));
    }
}
