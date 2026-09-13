using GraniteEdgeAI.Features.GgufRuntime.History;
using GraniteEdgeAI.Features.Prompting;

namespace GraniteEdgeAI.Features.OpenVinoRoute;

/// <summary>Transcript projection only. The inspection page retains session ownership.</summary>
internal sealed class OpenVinoChatController
{
    private readonly List<ChatMessage> _messages = [];
    private long _revision = -1;
    private int _assistantIndex = -1;
    private bool _retired;
    private bool _stopRequested;
    internal Guid ConversationId { get; } = Guid.NewGuid();
    internal IReadOnlyList<ChatMessage> Messages => _messages;

    internal void BeginTurn(string prompt)
    {
        if (_retired) return;
        _stopRequested = false;
        _messages.Add(ChatMessage.User(prompt, DateTimeOffset.UtcNow));
        _assistantIndex = _messages.Count;
        _messages.Add(ChatMessage.Assistant(string.Empty, ChatCompletionStatus.Pending, DateTimeOffset.UtcNow));
    }

    internal void Apply(PromptSurfaceState state)
    {
        if (_retired || state.EventRevision < _revision) return;
        _revision = state.EventRevision;
        if (_assistantIndex < 0) return;
        if (state.LastEventKind == PromptEventKind.StoppingTurn) _stopRequested = true;
        ChatCompletionStatus existing = _messages[_assistantIndex].Status;
        bool terminal = existing is ChatCompletionStatus.Completed or ChatCompletionStatus.Stopped or ChatCompletionStatus.Failed;
        // session shutdown and queued snapshots cannot rewrite a finished turn
        // complete carries the authoritative per-turn result independently
        if (terminal) return;
        ChatCompletionStatus status = state.LastEventKind switch
        {
            PromptEventKind.Failed => ChatCompletionStatus.Failed,
            PromptEventKind.Cancelled or PromptEventKind.SessionCompleted => terminal ? existing : ChatCompletionStatus.Stopped,
            PromptEventKind.TurnCompleted => _stopRequested ? ChatCompletionStatus.Stopped : ChatCompletionStatus.Completed,
            PromptEventKind.SessionReady => existing,
            PromptEventKind.CancellingSession => existing,
            PromptEventKind.StoppingTurn => existing,
            PromptEventKind.GeneratingTurn => ChatCompletionStatus.Pending,
            _ => ChatCompletionStatus.Streaming
        };
        _messages[_assistantIndex] = _messages[_assistantIndex].WithContent(state.ResponseText, status);
    }

    internal void Retire() => _retired = true;

    internal void Complete(PromptTurnResult result)
    {
        if (_retired || _assistantIndex < 0) return;
        ChatCompletionStatus status = result.Status switch
        {
            PromptTurnStatus.Completed => ChatCompletionStatus.Completed,
            PromptTurnStatus.Stopped => ChatCompletionStatus.Stopped,
            _ => ChatCompletionStatus.Failed
        };
        string text = result.Status == PromptTurnStatus.Failed && string.IsNullOrEmpty(result.Text)
            ? _messages[_assistantIndex].Content : result.Text;
        _messages[_assistantIndex] = _messages[_assistantIndex].WithContent(text, status);
    }
}
