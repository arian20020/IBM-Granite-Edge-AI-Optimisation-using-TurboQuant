using System.Windows.Automation;
using GraniteEdgeAI.EndToEndTests.Automation;

namespace GraniteEdgeAI.EndToEndTests.Pages;

internal sealed class ImportPage(AutomationSession session)
{
    internal void SelectRoute(string route, CancellationToken cancellationToken)
    {
        string automationId = route switch
        {
            "gguf" => "GgufFormatButton",
            "openvino" => "OpenVinoFormatButton",
            _ => throw new ArgumentOutOfRangeException(nameof(route)),
        };
        AutomationSession.Invoke(session.ByAutomationId(automationId, cancellationToken));
    }

    internal void ChooseModel(string path, CancellationToken cancellationToken)
    {
        if (!File.Exists(path) && !Directory.Exists(path))
        {
            throw new FileNotFoundException("The guarded model asset does not exist.", path);
        }

        AutomationSession.Invoke(session.ByName("Choose model file", cancellationToken));
        AutomationElement fileName = AutomationSession.GlobalByAutomationId("1148", cancellationToken);
        AutomationSession.SetValue(fileName, Path.GetFullPath(path));
        AutomationSession.Invoke(AutomationSession.GlobalByAutomationId("1", cancellationToken));
    }

    internal void Continue(CancellationToken cancellationToken) =>
        AutomationSession.Invoke(session.ByName("Continue to model inspection", cancellationToken));

    internal void AssertDropSurface(CancellationToken cancellationToken) =>
        Assert.IsNotNull(session.ByName("Model drop area", cancellationToken));
}
