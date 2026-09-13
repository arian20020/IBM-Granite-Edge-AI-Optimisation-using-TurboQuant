using GraniteEdgeAI.EndToEndTests.Automation;

namespace GraniteEdgeAI.EndToEndTests.Pages;

internal sealed class ChatPage(AutomationSession session)
{
    internal void AssertReady(CancellationToken cancellationToken)
    {
        Assert.IsNotNull(session.ByName("Conversation messages", cancellationToken));
        Assert.IsNotNull(session.ByName("Message", cancellationToken));
        Assert.IsNotNull(session.ByName("Send message", cancellationToken));
    }

    internal void Send(string prompt, CancellationToken cancellationToken)
    {
        AutomationSession.SetValue(session.ByName("Message", cancellationToken), prompt);
        AutomationSession.Invoke(session.ByName("Send message", cancellationToken));
    }

    internal void Stop(CancellationToken cancellationToken) =>
        AutomationSession.Invoke(session.ByName("Stop generation", cancellationToken));

    internal void AssertNoOnboardingChrome()
    {
        Assert.IsFalse(session.ExistsByName("Onboarding indicator"));
        Assert.IsFalse(session.ExistsByName("Onboarding footer"));
    }
}
