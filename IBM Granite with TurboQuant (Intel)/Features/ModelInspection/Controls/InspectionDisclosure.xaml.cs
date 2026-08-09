using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Automation.Peers;
using Microsoft.UI.Xaml.Automation.Provider;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using System;
using System.Collections.Generic;
using Windows.System;

namespace GraniteEdgeAI.Features.ModelInspection.Controls;

internal sealed partial class InspectionDisclosure : UserControl
{
    private bool _isExpanded;
    private bool? _preparedTarget;
    private readonly Dictionary<DependencyObject, AccessibilityView>
        _viewportAccessibilityViews = [];
    private readonly Dictionary<Control, (bool IsEnabled, bool IsTabStop)>
        _viewportControlStates = [];

    internal static readonly DependencyProperty HeaderContentProperty =
        DependencyProperty.Register(
            nameof(HeaderContent),
            typeof(object),
            typeof(InspectionDisclosure),
            new PropertyMetadata(null));

    internal static readonly DependencyProperty ViewportContentProperty =
        DependencyProperty.Register(
            nameof(ViewportContent),
            typeof(object),
            typeof(InspectionDisclosure),
            new PropertyMetadata(null));

    internal static readonly DependencyProperty HeaderMinHeightProperty =
        DependencyProperty.Register(
            nameof(HeaderMinHeight),
            typeof(double),
            typeof(InspectionDisclosure),
            new PropertyMetadata(58d));

    internal InspectionDisclosure()
    {
        InitializeComponent();
        ApplyCompletedState(isExpanded: false, raiseAutomationEvent: false);
    }

    internal event EventHandler<InspectionDisclosureToggleRequestedEventArgs>?
        ToggleRequested;

    public object? HeaderContent
    {
        get => GetValue(HeaderContentProperty);
        set => SetValue(HeaderContentProperty, value);
    }

    public object? ViewportContent
    {
        get => GetValue(ViewportContentProperty);
        set => SetValue(ViewportContentProperty, value);
    }

    public double HeaderMinHeight
    {
        get => (double)GetValue(HeaderMinHeightProperty);
        set => SetValue(HeaderMinHeightProperty, value);
    }

    internal UIElement ChevronTarget => DisclosureChevron;

    internal FrameworkElement ViewportTarget => DisclosureViewport;

    internal bool IsExpanded => _isExpanded;

    internal void PrepareTargetState(bool isExpanded)
    {
        _preparedTarget = isExpanded;
        DisclosureViewport.Visibility = Visibility.Visible;
        DisclosureViewport.IsHitTestVisible = false;
        AutomationProperties.SetAccessibilityView(
            DisclosureViewport,
            AccessibilityView.Raw);

        SuppressViewportSubtree();

        if (isExpanded && !_isExpanded)
        {
            DisclosureViewport.Opacity = 0d;
        }

        DisclosureChevronRotation.Angle = isExpanded ? 180d : 0d;
    }

    internal void CompleteTargetState(bool isExpanded)
    {
        if (_preparedTarget != isExpanded)
        {
            return;
        }

        ApplyCompletedState(isExpanded, raiseAutomationEvent: true);
    }

    internal void RequestTargetState(bool isExpanded)
    {
        if (EffectiveTarget == isExpanded)
        {
            return;
        }

        ToggleRequested?.Invoke(
            this,
            new InspectionDisclosureToggleRequestedEventArgs(isExpanded));
    }

    internal bool HandleKeyboardActivation(
        VirtualKey key,
        uint repeatCount,
        bool wasKeyDown)
    {
        if (key is not VirtualKey.Enter and not VirtualKey.Space ||
            repeatCount > 1 ||
            wasKeyDown ||
            XamlRoot is null ||
            !ReferenceEquals(
                FocusManager.GetFocusedElement(XamlRoot),
                this))
        {
            return false;
        }

        RequestTargetState(!EffectiveTarget);
        return true;
    }

    protected override AutomationPeer OnCreateAutomationPeer() =>
        new InspectionDisclosureAutomationPeer(this);

    private void DisclosureToggleButton_Click(
        object sender,
        RoutedEventArgs eventArguments)
    {
        Focus(FocusState.Pointer);
        RequestTargetState(!EffectiveTarget);
    }

    private bool EffectiveTarget => _preparedTarget ?? _isExpanded;

    private void Root_KeyDown(object sender, KeyRoutedEventArgs eventArguments)
    {
        if (!HandleKeyboardActivation(
                eventArguments.Key,
                eventArguments.KeyStatus.RepeatCount,
                eventArguments.KeyStatus.WasKeyDown))
        {
            return;
        }

        eventArguments.Handled = true;
    }

    private void ApplyCompletedState(
        bool isExpanded,
        bool raiseAutomationEvent)
    {
        bool previous = _isExpanded;
        _isExpanded = isExpanded;
        _preparedTarget = isExpanded;
        DisclosureChevronRotation.Angle = isExpanded ? 180d : 0d;
        DisclosureViewport.Opacity = isExpanded ? 1d : 0d;
        DisclosureViewport.Visibility = isExpanded
            ? Visibility.Visible
            : Visibility.Collapsed;
        DisclosureViewport.IsHitTestVisible = isExpanded;

        if (isExpanded)
        {
            RestoreViewportSubtree();
        }
        else
        {
            SuppressViewportSubtree();
        }

        AutomationProperties.SetAccessibilityView(
            DisclosureViewport,
            isExpanded ? AccessibilityView.Content : AccessibilityView.Raw);

        if (!raiseAutomationEvent || previous == isExpanded)
        {
            return;
        }

        if (FrameworkElementAutomationPeer.FromElement(this) is
            InspectionDisclosureAutomationPeer peer)
        {
            peer.RaiseExpandCollapseStateChanged(previous, isExpanded);
        }
    }

    private void SuppressViewportSubtree()
    {
        SuppressElementAndDescendants(DisclosureViewport);
        if (ViewportContent is DependencyObject viewportContent)
        {
            SuppressElementAndDescendants(viewportContent);
        }
    }

    private void SuppressElementAndDescendants(DependencyObject root)
    {
        foreach (DependencyObject descendant in EnumerateSelfAndDescendants(root))
        {
            if (!_viewportAccessibilityViews.ContainsKey(descendant))
            {
                _viewportAccessibilityViews.Add(
                    descendant,
                    AutomationProperties.GetAccessibilityView(descendant));
            }

            AutomationProperties.SetAccessibilityView(
                descendant,
                AccessibilityView.Raw);

            if (descendant is Control control)
            {
                if (!_viewportControlStates.ContainsKey(control))
                {
                    _viewportControlStates.Add(
                        control,
                        (control.IsEnabled, control.IsTabStop));
                }

                control.IsEnabled = false;
                control.IsTabStop = false;
            }
        }
    }

    private void RestoreViewportSubtree()
    {
        foreach ((DependencyObject element, AccessibilityView view) in
                 _viewportAccessibilityViews)
        {
            AutomationProperties.SetAccessibilityView(element, view);
        }

        foreach ((Control control, (bool isEnabled, bool isTabStop)) in
                 _viewportControlStates)
        {
            control.IsEnabled = isEnabled;
            control.IsTabStop = isTabStop;
        }

        _viewportAccessibilityViews.Clear();
        _viewportControlStates.Clear();
    }

    private static IEnumerable<DependencyObject> EnumerateSelfAndDescendants(
        DependencyObject parent)
    {
        yield return parent;
        int childCount = VisualTreeHelper.GetChildrenCount(parent);
        for (int index = 0; index < childCount; index++)
        {
            DependencyObject child = VisualTreeHelper.GetChild(parent, index);
            foreach (DependencyObject descendant in
                     EnumerateSelfAndDescendants(child))
            {
                yield return descendant;
            }
        }
    }

    private sealed class InspectionDisclosureAutomationPeer :
        FrameworkElementAutomationPeer,
        IExpandCollapseProvider
    {
        internal InspectionDisclosureAutomationPeer(InspectionDisclosure owner)
            : base(owner)
        {
        }

        private InspectionDisclosure Disclosure =>
            (InspectionDisclosure)Owner;

        public ExpandCollapseState ExpandCollapseState =>
            Disclosure._isExpanded
                ? ExpandCollapseState.Expanded
                : ExpandCollapseState.Collapsed;

        public void Collapse() => Disclosure.RequestTargetState(isExpanded: false);

        public void Expand() => Disclosure.RequestTargetState(isExpanded: true);

        internal void RaiseExpandCollapseStateChanged(
            bool previous,
            bool current)
        {
            RaisePropertyChangedEvent(
                ExpandCollapsePatternIdentifiers.ExpandCollapseStateProperty,
                previous
                    ? ExpandCollapseState.Expanded
                    : ExpandCollapseState.Collapsed,
                current
                    ? ExpandCollapseState.Expanded
                    : ExpandCollapseState.Collapsed);
        }

        protected override object? GetPatternCore(PatternInterface patternInterface) =>
            patternInterface == PatternInterface.ExpandCollapse
                ? this
                : base.GetPatternCore(patternInterface);

        protected override string GetClassNameCore() => "InspectionDisclosure";

        protected override AutomationControlType GetAutomationControlTypeCore() =>
            AutomationControlType.Group;

        protected override string GetNameCore()
        {
            string name = AutomationProperties.GetName(Disclosure);
            return string.IsNullOrWhiteSpace(name)
                ? "Inspection details"
                : name;
        }

        protected override bool IsContentElementCore() => true;

        protected override bool IsControlElementCore() => true;

        protected override bool IsKeyboardFocusableCore() => true;

        protected override void SetFocusCore()
        {
            Disclosure.Focus(FocusState.Programmatic);
        }
    }
}
