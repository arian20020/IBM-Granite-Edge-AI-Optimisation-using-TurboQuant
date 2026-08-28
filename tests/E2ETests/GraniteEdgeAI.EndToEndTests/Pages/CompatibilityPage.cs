using GraniteEdgeAI.EndToEndTests.Automation;

namespace GraniteEdgeAI.EndToEndTests.Pages;

internal sealed class CompatibilityPage(AutomationSession session)
{
    internal void Chat(CancellationToken cancellationToken) => Invoke("Chat with current model", cancellationToken);
    internal void Optimize(CancellationToken cancellationToken) => Invoke("Optimise this model before chatting", cancellationToken);
    internal void RefreshMemory(CancellationToken cancellationToken) => Invoke("Refresh memory and check again", cancellationToken);

    internal void AssertExecutionDisabled(CancellationToken cancellationToken)
    {
        Assert.IsFalse(session.IsEnabledByName("Chat with current model", cancellationToken));
        Assert.IsFalse(session.IsEnabledByName("Optimise this model before chatting", cancellationToken));
    }

    private void Invoke(string name, CancellationToken cancellationToken) =>
        AutomationSession.Invoke(session.ByName(name, cancellationToken));
}
