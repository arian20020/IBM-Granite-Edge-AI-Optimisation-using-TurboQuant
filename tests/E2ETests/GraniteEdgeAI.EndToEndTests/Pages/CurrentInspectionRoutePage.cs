using System.Windows.Automation;
using GraniteEdgeAI.EndToEndTests.Automation;

namespace GraniteEdgeAI.EndToEndTests.Pages;

// Uses only public UI Automation. No production fixtures or runtime overrides.
internal sealed class CurrentInspectionRoutePage(AutomationSession session)
{
    private static readonly TimeSpan StageTimeout = TimeSpan.FromMinutes(3);

    internal AutomationElement ById(string id) => Await(() => Find(id), id);

    private AutomationElement? Find(string id) => session.Window(CancellationToken.None)
        .FindFirst(TreeScope.Descendants, new PropertyCondition(AutomationElement.AutomationIdProperty, id));

    internal void InvokeEnabled(string id) => AutomationSession.Invoke(Await(() =>
    {
        AutomationElement? element = Find(id);
        return element?.Current.IsEnabled == true ? element : null;
    }, $"enabled {id}"));

    internal void Import(string route, string path)
    {
        InvokeEnabled("BtnChooseLocalModel");
        InvokeEnabled(route == "gguf" ? "GgufFormatButton" : "OpenVinoFormatButton");
        int pid = session.Window(CancellationToken.None).Current.ProcessId;
        AutomationElement field = Await(() => AutomationElement.RootElement.FindFirst(TreeScope.Descendants,
            new AndCondition(new PropertyCondition(AutomationElement.ProcessIdProperty, pid),
                new PropertyCondition(AutomationElement.AutomationIdProperty, route == "gguf" ? "1148" : "1152"))), "owned file picker");
        AutomationSession.SetValue(field, Path.GetFullPath(path));
        AutomationElement dialog = TreeWalker.ControlViewWalker.GetParent(field);
        AutomationElement? accept = null;
        while (dialog is not null && dialog != AutomationElement.RootElement)
        {
            accept = dialog.FindFirst(TreeScope.Descendants,
                new AndCondition(new PropertyCondition(AutomationElement.AutomationIdProperty, "1"),
                    new OrCondition(new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Button),
                        new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Pane))));
            if (accept is not null) break;
            dialog = TreeWalker.ControlViewWalker.GetParent(dialog);
        }
        Assert.IsNotNull(accept, "The owned picker must expose its accept action.");
        AutomationSession.Invoke(accept);
    }

    internal void ClearSelection() => InvokeEnabled("RemoveFailedModelButton");

    internal void ContinueAfterModelInspection()
    {
        AutomationElement action = Await(() => new[] { "BtnCheckHardwareFit", "BtnContinueHardwareWithWarning" }
            .Select(Find).FirstOrDefault(element => element?.Current.IsEnabled == true),
            "successful inspection hardware action");
        AutomationSession.Invoke(action);
    }

    internal void AwaitCompatibilityDecision()
    {
        // Hardware completion navigates automatically in the current application.
        ById("CompatibilityBay");
        Await(() => Find("CompatibilityAction.Back"), "compatibility terminal Back action");
        string heading = ById("CompatibilityOutcomeHeading").Current.Name;
        Assert.IsTrue(new[] { "Yes — this model should run", "Not enough free RAM",
            "This model needs to be quantised to run on your computer", "Optimisation is optional" }.Contains(heading),
            "Expected a fit, optimisation or memory decision; unknown and operational failures are not passes.");
    }

    internal void AssertConfigurationAvailable()
    {
        AutomationElement? start = Find("CompatibilityConfigureAction.StartOptimization");
        if (start is null)
        {
            AutomationSession.Invoke(Await(() => new[] { "CompatibilityAction.ConfigureModel", "CompatibilityAction.OptimiseFurther" }
                .Select(Find).FirstOrDefault(element => element?.Current.IsEnabled == true), "configuration action"));
        }
        Assert.IsTrue(ById("CompatibilityPreferenceSlider").Current.IsEnabled,
            "The supplied fit fixture must have a selectable configuration; low RAM is not a pass.");
        Assert.IsTrue(ById("CompatibilityConfigureAction.StartOptimization").Current.IsEnabled);
    }

    private static AutomationElement Await(Func<AutomationElement?> probe, string description)
    {
        AutomationElement? found = null;
        bool complete = ConditionWait.Until(() => (found = probe()) is not null,
            StageTimeout, TimeSpan.FromMilliseconds(150), CancellationToken.None);
        Assert.IsTrue(complete, $"Timed out waiting for {description}.");
        return found!;
    }
}
