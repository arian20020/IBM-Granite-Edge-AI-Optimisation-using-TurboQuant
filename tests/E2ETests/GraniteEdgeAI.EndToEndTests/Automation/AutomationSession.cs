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

    internal AutomationElement ByName(string accessibleName, TimeSpan timeout, CancellationToken cancellationToken) =>
        FindFrom(Window(cancellationToken), AutomationElement.NameProperty, accessibleName, timeout, cancellationToken);

    internal static AutomationElement GlobalWindowContainingAutomationId(string automationId, CancellationToken cancellationToken)
    {
        AutomationElement? dialog = null;
        bool observed = ConditionWait.Until(() =>
        {
            AutomationElementCollection windows = AutomationElement.RootElement.FindAll(TreeScope.Children, Condition.TrueCondition);
            List<AutomationElement> matches = [];
            foreach (AutomationElement window in windows)
            {
                if (window.FindFirst(TreeScope.Descendants, new PropertyCondition(AutomationElement.AutomationIdProperty, automationId)) is not null)
                {
                    matches.Add(window);
                }
            }
            if (matches.Count > 1)
            {
                throw new InvalidOperationException($"Ambiguous desktop state: multiple dialogs contain automation element '{automationId}'.");
            }
            dialog = matches.SingleOrDefault();
            return dialog is not null;
        }, DefaultTimeout, PollInterval, cancellationToken);
        return observed ? dialog! : throw new TimeoutException($"Timed out waiting for the unique dialog containing '{automationId}'.");
    }

    internal static AutomationElement DescendantByAutomationId(AutomationElement root, string automationId, CancellationToken cancellationToken) =>
        FindFrom(root, AutomationElement.AutomationIdProperty, automationId, cancellationToken);

    internal bool ExistsByName(string accessibleName) =>
        Window(CancellationToken.None).FindFirst(TreeScope.Descendants, new PropertyCondition(AutomationElement.NameProperty, accessibleName)) is not null;

    internal int CountByName(string accessibleName) =>
        Window(CancellationToken.None).FindAll(TreeScope.Descendants, new PropertyCondition(AutomationElement.NameProperty, accessibleName)).Count;

    internal void AssertUnavailableByName(string accessibleName)
    {
        AutomationElement? element = Window(CancellationToken.None).FindFirst(
            TreeScope.Descendants,
            new PropertyCondition(AutomationElement.NameProperty, accessibleName));
        if (element is null)
        {
            return;
        }
        Assert.IsFalse(element.Current.IsEnabled, $"'{accessibleName}' remains enabled to UI Automation.");
        Assert.IsFalse(element.Current.IsKeyboardFocusable, $"'{accessibleName}' remains keyboard-focusable while disabled.");
    }

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
        return FindFrom(Window(cancellationToken), property, value, cancellationToken);
    }

    private static AutomationElement FindFrom(AutomationElement root, AutomationProperty property, string value, CancellationToken cancellationToken)
        => FindFrom(root, property, value, DefaultTimeout, cancellationToken);

    private static AutomationElement FindFrom(AutomationElement root, AutomationProperty property, string value, TimeSpan timeout, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        return WaitFor(() => root.FindFirst(TreeScope.Descendants, new PropertyCondition(property, value)), $"automation element '{value}'", timeout, cancellationToken);
    }

    private static AutomationElement WaitFor(Func<AutomationElement?> probe, string description, CancellationToken cancellationToken)
        => WaitFor(probe, description, DefaultTimeout, cancellationToken);

    private static AutomationElement WaitFor(Func<AutomationElement?> probe, string description, TimeSpan timeout, CancellationToken cancellationToken)
    {
        AutomationElement? found = null;
        bool observed = ConditionWait.Until(() => (found = probe()) is not null, timeout, PollInterval, cancellationToken);
        return observed ? found! : throw new TimeoutException($"Timed out waiting for {description}.");
    }
}
