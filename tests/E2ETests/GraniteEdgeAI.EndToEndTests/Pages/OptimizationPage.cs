using GraniteEdgeAI.EndToEndTests.Automation;

namespace GraniteEdgeAI.EndToEndTests.Pages;

internal sealed class OptimizationPage(AutomationSession session)
{
    internal void Start(CancellationToken cancellationToken) => Invoke("Start optimisation", cancellationToken);
    internal void Cancel(CancellationToken cancellationToken) => Invoke("Cancel optimisation", cancellationToken);
    internal void Retry(CancellationToken cancellationToken) => Invoke("Try again", cancellationToken);
    internal void ChatWithResult(CancellationToken cancellationToken) => Invoke("Chat with this model", cancellationToken);
    internal void ChatWithOriginal(CancellationToken cancellationToken) => Invoke("Chat with original model", cancellationToken);
    internal void AssertDestination(CancellationToken cancellationToken) => Assert.IsNotNull(session.ByName("Optimisation destination", cancellationToken));

    internal void AwaitResult(CancellationToken cancellationToken) =>
        Assert.IsNotNull(session.ByName("Chat with this model", TimeSpan.FromMinutes(10), cancellationToken));

    private void Invoke(string name, CancellationToken cancellationToken) =>
        AutomationSession.Invoke(session.ByName(name, cancellationToken));
}
