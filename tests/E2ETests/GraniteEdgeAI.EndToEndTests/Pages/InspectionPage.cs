using GraniteEdgeAI.EndToEndTests.Automation;

namespace GraniteEdgeAI.EndToEndTests.Pages;

internal sealed class InspectionPage(AutomationSession session)
{
    internal void ContinueToHardware(CancellationToken cancellationToken)
    {
        string action = session.ExistsByName("Check model hardware fit")
            ? "Check model hardware fit"
            : "Continue to hardware check";
        AutomationSession.Invoke(session.ByName(action, cancellationToken));
    }
}
