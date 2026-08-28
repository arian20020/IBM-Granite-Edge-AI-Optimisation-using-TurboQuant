using System.Windows.Automation;

namespace GraniteEdgeAI.EndToEndTests.Automation;

internal sealed class AutomationSession
{
    private static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(15);
    private static readonly TimeSpan PollInterval = TimeSpan.FromMilliseconds(100);
    private readonly int processId;

    internal AutomationSession(int processId) => this.processId = processId > 0 ? processId : throw new ArgumentOutOfRangeException(nameof(processId));

    internal AutomationElement Window(CancellationToken cancellationToken) =>
        WaitFor(() => AutomationElement.RootElement.FindFirst(TreeScope.Children, new PropertyCondition(AutomationElement.ProcessIdProperty, processId)), "candidate window", cancellationToken);

    internal AutomationElement ByAutomationId(string automationId, CancellationToken cancellationToken) =>
        FindDescendant(AutomationElement.AutomationIdProperty, automationId, cancellationToken);

    internal AutomationElement ByName(string accessibleName, CancellationToken cancellationToken) =>
        FindDescendant(AutomationElement.NameProperty, accessibleName, cancellationToken);

    internal bool ExistsByName(string accessibleName) =>
        Window(CancellationToken.None).FindFirst(TreeScope.Descendants, new PropertyCondition(AutomationElement.NameProperty, accessibleName)) is not null;

    internal static void Invoke(AutomationElement element)
    {
        if (!element.TryGetCurrentPattern(InvokePattern.Pattern, out object? pattern))
        {
            throw new InvalidOperationException($"Element '{PrivacyRedactor.Redact(element.Current.Name)}' does not support InvokePattern.");
        }

        ((InvokePattern)pattern).Invoke();
    }

    internal static void SetValue(AutomationElement element, string value)
    {
        if (!element.TryGetCurrentPattern(ValuePattern.Pattern, out object? pattern))
        {
            throw new InvalidOperationException($"Element '{PrivacyRedactor.Redact(element.Current.Name)}' does not support ValuePattern.");
        }

        ((ValuePattern)pattern).SetValue(value);
    }

    private AutomationElement FindDescendant(AutomationProperty property, string value, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        return WaitFor(() => Window(cancellationToken).FindFirst(TreeScope.Descendants, new PropertyCondition(property, value)), $"automation element '{value}'", cancellationToken);
    }

    private static AutomationElement WaitFor(Func<AutomationElement?> probe, string description, CancellationToken cancellationToken)
    {
        AutomationElement? found = null;
        bool observed = ConditionWait.Until(() => (found = probe()) is not null, DefaultTimeout, PollInterval, cancellationToken);
        return observed ? found! : throw new TimeoutException($"Timed out waiting for {description}.");
    }
}
