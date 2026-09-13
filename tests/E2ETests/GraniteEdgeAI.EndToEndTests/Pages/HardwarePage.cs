using GraniteEdgeAI.EndToEndTests.Automation;

namespace GraniteEdgeAI.EndToEndTests.Pages;

internal sealed class HardwarePage(AutomationSession session)
{
    internal void ContinueToCompatibility(CancellationToken cancellationToken)
    {
        Assert.IsNotNull(session.ByName("Hardware inspection", cancellationToken));
        AutomationSession.Invoke(session.ByName("Continue to compatibility", TimeSpan.FromMinutes(2), cancellationToken));
    }
}
