#if MODEL_INSPECTION_FIXTURE_GALLERY
using GraniteEdgeAI.Features.ModelInspection.Controls;
using GraniteEdgeAI.Features.ModelInspection.DebugFixtures.Observation;
using GraniteEdgeAI.ModelInspection.Fixtures;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Windows.Foundation;

namespace GraniteEdgeAI.Features.ModelInspection.DebugFixtures.Presets;

internal sealed record ModelInspectionObservedFixtureGeometry
{
    internal double HostWidth { get; init; }
    internal double HostHeight { get; init; }
    internal double ContentX { get; init; }
    internal double ContentY { get; init; }
    internal double ContentWidth { get; init; }
    internal double ContentHeight { get; init; }
    internal double HorizontalExtent { get; init; }
    internal double VerticalExtent { get; init; }
}

internal sealed record ModelInspectionFixturePresetObservation
{
    internal ModelInspectionFixtureResponsiveLayout ResponsiveLayout
        { get; init; }
    internal double ContentColumnWidth { get; init; }
    internal bool NoClipping { get; init; }
    internal string NoClippingDiagnostic { get; init; } = string.Empty;
    internal bool NoOverlap { get; init; }
    internal bool AllRequiredContentReachable { get; init; }
    internal string PageState { get; init; } = string.Empty;
    internal IReadOnlyList<string> ModelStates { get; init; } =
        Array.Empty<string>();
    internal IReadOnlyList<string> ContentStates { get; init; } =
        Array.Empty<string>();
    internal string ActionState { get; init; } = string.Empty;
    internal IReadOnlyDictionary<string, ModelInspectionFixtureTextBehavior>
        TextRoles { get; init; } =
            new Dictionary<string, ModelInspectionFixtureTextBehavior>(
                StringComparer.Ordinal);
    internal string? ScrollOwner { get; init; }
    internal IReadOnlyList<string> OrderedRowIds { get; init; } =
        Array.Empty<string>();
    internal double MinimumPointerTargetWidth { get; init; }
    internal double MinimumPointerTargetHeight { get; init; }
    internal IReadOnlyList<string> LogicalReadingOrder { get; init; } =
        Array.Empty<string>();
    internal IReadOnlyList<string> TabOrder { get; init; } =
        Array.Empty<string>();
    internal string FocusTarget { get; init; } = string.Empty;
    internal bool SemanticBrushesResolvedWithoutColorOnlyMeaning { get; init; }
    internal bool NaturalTextReflow { get; init; }
    internal ModelInspectionObservedScreen Screen { get; init; } =
        ModelInspectionObservedScreen.Empty;
    internal ModelInspectionObservedFixtureGeometry Geometry { get; init; } =
        new();
    internal bool DispatcherDrained { get; init; }
    internal bool LayoutUpdated { get; init; }
    internal bool CompositionCommitted { get; init; }
}

internal sealed class ModelInspectionFixturePresetApplier : IDisposable
{
    private sealed record RequiredRowGroup(
        ScrollViewer ScrollOwner,
        FrameworkElement CoordinateRoot,
        IReadOnlyList<FrameworkElement> Rows);

    private readonly ModelInspectionPage page;
    private readonly ModelInspectionFixturePreset preset;
    private readonly CancellationTokenSource lifetime = new();
    private readonly IModelInspectionFixtureObservationSession observation;
    private int applying;
    private int repairSuppression;
    private int disposed;
    private int activeHandlerCount;

    internal ModelInspectionFixturePresetApplier(
        ModelInspectionPage page,
        ModelInspectionFixturePreset preset)
    {
        this.page = page ?? throw new ArgumentNullException(nameof(page));
        this.preset = preset ?? throw new ArgumentNullException(nameof(preset));
        HostWidth = Width(preset.Width);
        page.Width = HostWidth;
        page.RequestedTheme =
            ModelInspectionFixturePreviewResources.RequestedTheme(
                preset.Resources);
        page.ApplyFixtureResponsiveState(preset.Width);
        observation = new ModelInspectionFixtureScreenObserver().Begin(page);

        page.SizeChanged += Page_SizeChanged;
        Interlocked.Increment(ref activeHandlerCount);
        page.LayoutUpdated += Page_LayoutUpdated;
        Interlocked.Increment(ref activeHandlerCount);
        CompositionTarget.Rendering += CompositionTarget_Rendering;
        Interlocked.Increment(ref activeHandlerCount);
    }

    internal double HostWidth { get; }

    internal int ActiveHandlerCount => Volatile.Read(ref activeHandlerCount);

    internal async Task<ModelInspectionFixturePresetObservation> ApplyAsync(
        CancellationToken cancellationToken)
    {
        ThrowIfDisposed();
        await WaitForLoadedAsync(cancellationToken);
        ApplyCore();
        await WaitForReapplicationAsync(cancellationToken);
        return await CaptureAsync(cancellationToken);
    }

    internal async Task<ModelInspectionFixturePresetObservation> ObserveAsync(
        CancellationToken cancellationToken)
    {
        ThrowIfDisposed();
        await WaitForLoadedAsync(cancellationToken);
        await WaitForReapplicationAsync(cancellationToken);
        return await CaptureAsync(cancellationToken);
    }

    internal async Task<ModelInspectionFixturePresetObservation>
        CaptureCurrentLoadedTreeForTestingAsync(
            CancellationToken cancellationToken)
    {
        ThrowIfDisposed();
        Interlocked.Increment(ref repairSuppression);
        try
        {
            await WaitForLoadedAsync(cancellationToken);
            page.UpdateLayout();
            return await CaptureAsync(cancellationToken);
        }
        finally
        {
            Interlocked.Decrement(ref repairSuppression);
        }
    }

    internal async Task WaitForReapplicationAsync(
        CancellationToken cancellationToken)
    {
        ThrowIfDisposed();
        await DrainDispatcherAsync(cancellationToken);
        await WaitForLayoutUpdatedAsync(cancellationToken);
        await WaitForRenderingAsync(cancellationToken);
        await DrainDispatcherAsync(cancellationToken);
        page.UpdateLayout();
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref disposed, 1) != 0)
        {
            return;
        }

        lifetime.Cancel();
        page.SizeChanged -= Page_SizeChanged;
        Interlocked.Decrement(ref activeHandlerCount);
        page.LayoutUpdated -= Page_LayoutUpdated;
        Interlocked.Decrement(ref activeHandlerCount);
        CompositionTarget.Rendering -= CompositionTarget_Rendering;
        Interlocked.Decrement(ref activeHandlerCount);
        observation.Dispose();
        lifetime.Dispose();
    }

    private async Task<ModelInspectionFixturePresetObservation> CaptureAsync(
        CancellationToken cancellationToken)
    {
        using CancellationTokenSource linked = Linked(cancellationToken);
        ModelInspectionObservedScreen screen = await observation.CaptureAsync(
            linked.Token);
        return ObserveLoadedTree(screen);
    }

    private ModelInspectionFixturePresetObservation ObserveLoadedTree(
        ModelInspectionObservedScreen screen)
    {
        FrameworkElement contentHost = Named<FrameworkElement>(
            page,
            "InspectionContentHost");
        ScrollViewer scroll = Named<ScrollViewer>(
            page,
            "InspectionPageScrollViewer");
        Point contentOrigin = contentHost.TransformToVisual(page)
            .TransformPoint(default);
        (double minimumWidth, double minimumHeight) = ObservePointerTargets();
        bool noClipping = ObserveNoClipping(out string noClippingDiagnostic);
        string pageState = page.FixturePageResponsiveStateName ?? string.Empty;
        return new ModelInspectionFixturePresetObservation
        {
            ResponsiveLayout = pageState switch
            {
                "DesktopPageState" =>
                    ModelInspectionFixtureResponsiveLayout.Desktop,
                "MediumPageState" =>
                    ModelInspectionFixtureResponsiveLayout.Medium,
                "NarrowPageState" =>
                    ModelInspectionFixtureResponsiveLayout.Narrow,
                _ => throw new InvalidOperationException(
                    $"The loaded page responsive state '{pageState}' is unknown.")
            },
            ContentColumnWidth = contentHost.ActualWidth,
            NoClipping = noClipping,
            NoClippingDiagnostic = noClippingDiagnostic,
            NoOverlap = ObserveNoOverlap(),
            AllRequiredContentReachable = ObserveReachability(scroll),
            PageState = pageState,
            ModelStates = [page.FixtureModelResponsiveStateName ?? string.Empty],
            ContentStates =
            [
                page.FixtureContentResponsiveStateName ?? string.Empty,
                page.FixtureOutgoingContentResponsiveStateName ?? string.Empty
            ],
            ActionState = page.FixtureActionResponsiveStateName ?? string.Empty,
            TextRoles = ObserveTextRoles(),
            ScrollOwner = ObserveScrollOwner(scroll),
            OrderedRowIds = screen.RowsAndScroll.OrderedRowIds,
            MinimumPointerTargetWidth = minimumWidth,
            MinimumPointerTargetHeight = minimumHeight,
            LogicalReadingOrder = ObserveLogicalReadingOrder(),
            TabOrder = ObserveTabOrder(),
            FocusTarget = screen.Focus.Target,
            SemanticBrushesResolvedWithoutColorOnlyMeaning =
                ObserveSemanticBrushes(),
            NaturalTextReflow = ObserveNaturalTextReflow(),
            Screen = screen,
            Geometry = new ModelInspectionObservedFixtureGeometry
            {
                HostWidth = page.ActualWidth,
                HostHeight = page.ActualHeight,
                ContentX = contentOrigin.X,
                ContentY = contentOrigin.Y,
                ContentWidth = contentHost.ActualWidth,
                ContentHeight = contentHost.ActualHeight,
                HorizontalExtent = scroll.ExtentWidth,
                VerticalExtent = scroll.ExtentHeight
            },
            DispatcherDrained = true,
            LayoutUpdated = page.IsLoaded && page.ActualWidth > 0d,
            CompositionCommitted = true
        };
    }

    private void ApplyCore()
    {
        if (Volatile.Read(ref disposed) != 0 ||
            Volatile.Read(ref repairSuppression) != 0 ||
            Interlocked.Exchange(ref applying, 1) != 0)
        {
            return;
        }

        try
        {
            if (!NearlyEqual(page.Width, HostWidth))
            {
                page.Width = HostWidth;
            }

            ElementTheme theme =
                ModelInspectionFixturePreviewResources.RequestedTheme(
                    preset.Resources);
            if (page.RequestedTheme != theme)
            {
                page.RequestedTheme = theme;
            }

            page.ApplyFixtureResponsiveState(preset.Width);
        }
        finally
        {
            Volatile.Write(ref applying, 0);
        }
    }

    private void Page_SizeChanged(object sender, SizeChangedEventArgs args) =>
        ApplyCore();

    private void Page_LayoutUpdated(object? sender, object args) => ApplyCore();

    private void CompositionTarget_Rendering(object? sender, object args) =>
        ApplyCore();

    private async Task WaitForLoadedAsync(CancellationToken cancellationToken)
    {
        if (page.IsLoaded)
        {
            return;
        }

        using CancellationTokenSource linked = Linked(cancellationToken);
        var loaded = new TaskCompletionSource<bool>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        RoutedEventHandler handler = (_, _) => loaded.TrySetResult(true);
        page.Loaded += handler;
        try
        {
            await loaded.Task.WaitAsync(linked.Token);
        }
        finally
        {
            page.Loaded -= handler;
        }
    }

    private async Task DrainDispatcherAsync(CancellationToken cancellationToken)
    {
        using CancellationTokenSource linked = Linked(cancellationToken);
        var drained = new TaskCompletionSource<bool>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        if (!page.DispatcherQueue.TryEnqueue(
                DispatcherQueuePriority.Normal,
                () => drained.TrySetResult(true)))
        {
            throw new InvalidOperationException(
                "The preset dispatcher boundary could not be queued.");
        }

        await drained.Task.WaitAsync(linked.Token);
    }

    private async Task WaitForLayoutUpdatedAsync(
        CancellationToken cancellationToken)
    {
        using CancellationTokenSource linked = Linked(cancellationToken);
        var updated = new TaskCompletionSource<bool>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        void Handler(object? sender, object args) => updated.TrySetResult(true);
        page.LayoutUpdated += Handler;
        try
        {
            page.InvalidateMeasure();
            page.UpdateLayout();
            if (!updated.Task.IsCompleted)
            {
                if (!page.DispatcherQueue.TryEnqueue(
                        () => page.InvalidateMeasure()))
                {
                    throw new InvalidOperationException(
                        "The preset layout boundary could not be queued.");
                }
            }

            await updated.Task.WaitAsync(linked.Token);
        }
        finally
        {
            page.LayoutUpdated -= Handler;
        }
    }

    private async Task WaitForRenderingAsync(
        CancellationToken cancellationToken)
    {
        using CancellationTokenSource linked = Linked(cancellationToken);
        var rendered = new TaskCompletionSource<bool>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        EventHandler<object> handler = (_, _) => rendered.TrySetResult(true);
        CompositionTarget.Rendering += handler;
        try
        {
            await rendered.Task.WaitAsync(linked.Token);
        }
        finally
        {
            CompositionTarget.Rendering -= handler;
        }
    }

    private bool ObserveNoClipping(out string diagnostic)
    {
        foreach (TextBlock text in Descendants<TextBlock>(page)
                     .Where(IsDisplayed))
        {
            if (string.IsNullOrEmpty(text.Text) ||
                HasVisualAncestor<SymbolIcon>(text))
            {
                continue;
            }

            bool hasValidTextGeometry =
                double.IsFinite(text.ActualWidth) && text.ActualWidth > 0d &&
                double.IsFinite(text.ActualHeight) &&
                text.ActualHeight + 1d >= text.FontSize;
            if (!hasValidTextGeometry)
            {
                diagnostic = Bounded(
                    $"TextGeometry element={ElementLabel(text)} " +
                    $"text={Quoted(text.Text)} " +
                    $"actual={Number(text.ActualWidth)}x" +
                    $"{Number(text.ActualHeight)} font={Number(text.FontSize)}");
                return false;
            }

            if (!FitsVisibleAncestors(text, out string ancestorDiagnostic))
            {
                diagnostic = Bounded(
                    $"TextAncestor element={ElementLabel(text)} " +
                    $"text={Quoted(text.Text)} {ancestorDiagnostic}");
                return false;
            }
        }

        foreach (FrameworkElement element in RequiredVisibleElements())
        {
            if (!FitsVisibleAncestors(element, out string ancestorDiagnostic))
            {
                diagnostic = Bounded(
                    $"RequiredAncestor element={ElementLabel(element)} " +
                    ancestorDiagnostic);
                return false;
            }
        }

        diagnostic = string.Empty;
        return true;
    }

    private bool ObserveNoOverlap()
    {
        FrameworkElement[] regions = RequiredTopLevelRegions()
            .Where(IsVisible)
            .ToArray();
        for (int left = 0; left < regions.Length; left++)
        for (int right = left + 1; right < regions.Length; right++)
        {
            Rect intersection = RectHelper.Intersect(
                BoundsInPage(regions[left]),
                BoundsInPage(regions[right]));
            if (intersection.Width > 1d && intersection.Height > 1d)
            {
                return false;
            }
        }

        Button[] actionButtons = Visible<Button>(Named<InspectionActionCard>(
                page,
                "InspectionActionCardControl"))
            .Where(button => button.IsHitTestVisible)
            .ToArray();
        for (int left = 0; left < actionButtons.Length; left++)
        for (int right = left + 1; right < actionButtons.Length; right++)
        {
            Rect intersection = RectHelper.Intersect(
                BoundsInPage(actionButtons[left]),
                BoundsInPage(actionButtons[right]));
            if (intersection.Width > 1d && intersection.Height > 1d)
            {
                return false;
            }
        }

        if (!TryGetRequiredRowGroups(out IReadOnlyList<RequiredRowGroup> groups))
        {
            return false;
        }

        foreach (RequiredRowGroup group in groups)
        {
            for (int left = 0; left < group.Rows.Count; left++)
            for (int right = left + 1; right < group.Rows.Count; right++)
            {
                Rect intersection = RectHelper.Intersect(
                    BoundsIn(group.Rows[left], group.CoordinateRoot),
                    BoundsIn(group.Rows[right], group.CoordinateRoot));
                if (intersection.Width > 1d && intersection.Height > 1d)
                {
                    return false;
                }
            }
        }

        return true;
    }

    private bool ObserveReachability(ScrollViewer scroll)
    {
        if (!IsVisible(scroll) || !scroll.IsHitTestVisible ||
            scroll.VerticalScrollMode == ScrollMode.Disabled)
        {
            return false;
        }

        if (Visible<ScrollViewer>(page).Any(owner =>
                owner.IsTabStop && owner.IsHitTestVisible &&
                owner.VerticalScrollMode == ScrollMode.Disabled))
        {
            return false;
        }

        if (!TryGetRequiredRowGroups(out IReadOnlyList<RequiredRowGroup> groups))
        {
            return false;
        }

        FrameworkElement content = Named<FrameworkElement>(
            page,
            "InspectionScrollContent");
        foreach (FrameworkElement element in RequiredVisibleElements())
        {
            if (!IsWithinRequiredRow(element, groups) &&
                !FitsExtent(element, scroll, content))
            {
                return false;
            }
        }

        foreach (RequiredRowGroup group in groups)
        foreach (FrameworkElement row in group.Rows)
        {
            if (!FitsRowExtent(row, group))
            {
                return false;
            }
        }

        return true;
    }

    private static bool IsWithinRequiredRow(
        DependencyObject element,
        IReadOnlyList<RequiredRowGroup> groups)
    {
        DependencyObject? current = element;
        while (current is not null)
        {
            if (groups.Any(group => group.Rows.Any(row =>
                    ReferenceEquals(row, current))))
            {
                return true;
            }

            current = VisualTreeHelper.GetParent(current);
        }

        return false;
    }

    private bool TryGetRequiredRowGroups(
        out IReadOnlyList<RequiredRowGroup> groups)
    {
        var observed = new List<RequiredRowGroup>();
        ScrollViewer pageScroll = Named<ScrollViewer>(
            page,
            "InspectionPageScrollViewer");
        FrameworkElement pageContent = Named<FrameworkElement>(
            page,
            "InspectionScrollContent");

        InspectionModelCard modelCard = Named<InspectionModelCard>(
            page,
            "InspectionModelCardControl");
        ItemsControl checkItems = Named<ItemsControl>(
            modelCard,
            "InspectionChecksItemsControl");
        if (IsDisplayed(checkItems))
        {
            if (!TryGetItemsControlRows(
                    checkItems,
                    "InspectionCheckRow",
                    out IReadOnlyList<FrameworkElement> rows))
            {
                groups = Array.Empty<RequiredRowGroup>();
                return false;
            }

            observed.Add(new RequiredRowGroup(
                Named<ScrollViewer>(modelCard, "InspectionChecksScrollViewer"),
                checkItems,
                rows));
        }

        InspectionContentCard contentCard = Named<InspectionContentCard>(
            page,
            "InspectionContentCardControl");
        ItemsRepeater progressItems = Named<ItemsRepeater>(
            contentCard,
            "ProgressItemsRepeater");
        if (IsDisplayed(progressItems))
        {
            if (!TryGetItemsRepeaterRows(
                    progressItems,
                    contentCard.Presentation.ProgressRows.Items.Count,
                    "InspectionProgressRow",
                    out IReadOnlyList<FrameworkElement> rows))
            {
                groups = Array.Empty<RequiredRowGroup>();
                return false;
            }

            observed.Add(new RequiredRowGroup(
                pageScroll,
                pageContent,
                rows));
        }

        ItemsControl expandedItems = Named<ItemsControl>(
            contentCard,
            "ExpandedReportItemsControl");
        if (IsDisplayed(expandedItems))
        {
            if (!TryGetItemsControlRows(
                    expandedItems,
                    "InspectionReportRow",
                    out IReadOnlyList<FrameworkElement> rows))
            {
                groups = Array.Empty<RequiredRowGroup>();
                return false;
            }

            observed.Add(new RequiredRowGroup(
                Named<ScrollViewer>(contentCard, "ExpandedReportScrollViewer"),
                expandedItems,
                rows));
        }
        else
        {
            ItemsControl findingItems = Named<ItemsControl>(
                contentCard,
                "FindingsItemsRepeater");
            if (IsDisplayed(findingItems))
            {
                if (!TryGetItemsControlRows(
                        findingItems,
                        "InspectionFindingRow",
                        out IReadOnlyList<FrameworkElement> rows))
                {
                    groups = Array.Empty<RequiredRowGroup>();
                    return false;
                }

                observed.Add(new RequiredRowGroup(
                    pageScroll,
                    pageContent,
                    rows));
            }
        }

        groups = observed;
        return true;
    }

    private static bool TryGetItemsRepeaterRows(
        ItemsRepeater items,
        int count,
        string tag,
        out IReadOnlyList<FrameworkElement> rows)
    {
        var realized = new List<FrameworkElement>(count);
        for (int index = 0; index < count; index++)
        {
            if ((items.TryGetElement(index) ??
                    items.GetOrCreateElement(index)) is not
                    FrameworkElement container ||
                !TryGetTaggedRow(container, tag, out FrameworkElement row))
            {
                rows = Array.Empty<FrameworkElement>();
                return false;
            }

            realized.Add(row);
        }

        rows = realized;
        return true;
    }

    private static bool TryGetItemsControlRows(
        ItemsControl items,
        string tag,
        out IReadOnlyList<FrameworkElement> rows)
    {
        var realized = new List<FrameworkElement>(items.Items.Count);
        for (int index = 0; index < items.Items.Count; index++)
        {
            if (items.ContainerFromIndex(index) is not
                    FrameworkElement container ||
                !TryGetTaggedRow(container, tag, out FrameworkElement row))
            {
                rows = Array.Empty<FrameworkElement>();
                return false;
            }

            realized.Add(row);
        }

        rows = realized;
        return true;
    }

    private static bool TryGetTaggedRow(
        FrameworkElement container,
        string tag,
        out FrameworkElement row)
    {
        FrameworkElement[] matches = SelfAndDescendants(container)
            .Where(element => string.Equals(
                element.Tag as string,
                tag,
                StringComparison.Ordinal))
            .ToArray();
        row = matches.Length == 1 ? matches[0] : null!;
        return matches.Length == 1;
    }

    private static IEnumerable<FrameworkElement> SelfAndDescendants(
        FrameworkElement root)
    {
        yield return root;
        foreach (FrameworkElement descendant in Descendants<FrameworkElement>(
                     root))
        {
            yield return descendant;
        }
    }

    private static bool FitsRowExtent(
        FrameworkElement row,
        RequiredRowGroup group) =>
        row.IsLoaded && IsDisplayed(row) &&
        double.IsFinite(row.ActualWidth) && row.ActualWidth > 0d &&
        double.IsFinite(row.ActualHeight) && row.ActualHeight > 0d &&
        FitsExtent(row, group.ScrollOwner, group.CoordinateRoot);

    private static bool FitsExtent(
        FrameworkElement element,
        ScrollViewer owner,
        FrameworkElement coordinateRoot)
    {
        Rect bounds = BoundsIn(element, coordinateRoot);
        if (!double.IsFinite(bounds.X) || !double.IsFinite(bounds.Y) ||
            !double.IsFinite(bounds.Width) || bounds.Width <= 0d ||
            !double.IsFinite(bounds.Height) || bounds.Height <= 0d)
        {
            return false;
        }

        double horizontalLimit = owner.HorizontalScrollMode ==
            ScrollMode.Disabled && owner.ViewportWidth > 0d
                ? owner.ViewportWidth
                : owner.ExtentWidth;
        return bounds.X >= -1d && bounds.Y >= -1d &&
            bounds.X + bounds.Width <= horizontalLimit + 1d &&
            bounds.Y + bounds.Height <= owner.ExtentHeight + 1d;
    }

    private IReadOnlyDictionary<string, ModelInspectionFixtureTextBehavior>
        ObserveTextRoles()
    {
        FrameworkElement[] elements = Visible<FrameworkElement>(page).ToArray();
        bool includeMetadataPlaceholders =
            HasCompleteMetadataPlaceholderSignature(elements);
        var roles = new Dictionary<string, ModelInspectionFixtureTextBehavior>(
            StringComparer.Ordinal);
        foreach (FrameworkElement element in elements)
        {
            string? id = TextRoleId(element);
            if (id is null ||
                IsMetadataPlaceholderRole(id) && !includeMetadataPlaceholders)
            {
                continue;
            }

            ModelInspectionFixtureTextBehavior? behavior = element switch
            {
                TextBlock text when text.TextTrimming != TextTrimming.None =>
                    ModelInspectionFixtureTextBehavior.Truncate,
                TextBlock text when text.TextWrapping != TextWrapping.NoWrap =>
                    ModelInspectionFixtureTextBehavior.Wrap,
                _ => null
            };
            if (behavior is { } observed)
            {
                roles[id] = observed;
            }
        }

        return roles;
    }

    private static bool HasCompleteMetadataPlaceholderSignature(
        IEnumerable<FrameworkElement> elements)
    {
        HashSet<string> ids = elements
            .Select(TextRoleId)
            .Where(IsMetadataPlaceholderRole)
            .Cast<string>()
            .ToHashSet(StringComparer.Ordinal);
        return ids.Count == 3 &&
            ids.Contains("metadata-publisher") &&
            ids.Contains("metadata-model-type") &&
            ids.Contains("metadata-context");
    }

    private static bool IsMetadataPlaceholderRole(string? id) =>
        id is "metadata-publisher" or "metadata-model-type" or
            "metadata-context";

    private static string? TextRoleId(FrameworkElement element)
    {
        if (element is not TextBlock text)
        {
            return null;
        }

        if (string.Equals(text.Text, "Not reported", StringComparison.Ordinal))
        {
            if (HasAncestorNamed(text, "PublisherField"))
            {
                return "metadata-publisher";
            }

            if (HasAncestorNamed(text, "ModelTypeField"))
            {
                return "metadata-model-type";
            }

            if (HasAncestorNamed(text, "DeclaredContextField"))
            {
                return "metadata-context";
            }
        }

        if (text.Text.Length > 80 &&
            HasAncestorNamed(text, "ModelNameField"))
        {
            return "model-card";
        }

        return text.Text switch
        {
            "Check model package" => "progress-1",
            "Read model configuration" => "progress-2",
            "Validate tokenizer and chat setup" => "progress-3",
            "Validate model structure" => "progress-4",
            "Confirm core runtime compatibility" => "progress-5",
            "Inspection stopped" => "cancelled-row",
            "Conversion required" => "conversion-required-row",
            "Required package content was not reported." =>
                "incomplete-package-row",
            "Runtime support was not reported for this model." =>
                "unsupported-model-row",
            "Structural validation did not produce a usable model result." =>
                "invalid-report-row",
            "Chat template not reported" =>
                "MI-WARN-CHAT-TEMPLATE-MISSING",
            "Model result unavailable" => "operational-failure-row",
            string value when value.StartsWith(
                "GGUF version 3; package boundaries",
                StringComparison.Ordinal) => "check-package",
            string value when value.StartsWith(
                "Architecture llama;",
                StringComparison.Ordinal) => "check-configuration",
            string value when value.StartsWith(
                "Tokenizer sentencepiece;",
                StringComparison.Ordinal) => "check-tokenizer",
            string value when value.StartsWith(
                "Model structure validation",
                StringComparison.Ordinal) => "check-structure",
            string value when value.StartsWith(
                "CPU X64 VocabOnly",
                StringComparison.Ordinal) => "check-runtime",
            _ => null
        };
    }

    private string? ObserveScrollOwner(ScrollViewer pageScroll)
    {
        if (!IsVisible(pageScroll) ||
            pageScroll.VerticalScrollMode == ScrollMode.Disabled)
        {
            return null;
        }

        InspectionContentCard contentCard = Named<InspectionContentCard>(
            page,
            "InspectionContentCardControl");
        FrameworkElement progress = Named<FrameworkElement>(
            contentCard,
            "ProgressView");
        FrameworkElement progressList = Named<FrameworkElement>(
            contentCard,
            "ProgressItemsRepeater");
        if (IsVisible(progress) && IsVisible(progressList))
        {
            return "progress-list";
        }

        FrameworkElement findings = Named<FrameworkElement>(
            contentCard,
            "FindingsView");
        if (IsVisible(findings))
        {
            FrameworkElement expandedItems = Named<FrameworkElement>(
                contentCard,
                "ExpandedReportItemsControl");
            ScrollViewer owner = IsVisible(expandedItems)
                ? Named<ScrollViewer>(
                    contentCard,
                    "ExpandedReportScrollViewer")
                : pageScroll;
            bool ownerIsValid = ReferenceEquals(owner, pageScroll)
                ? IsVisible(owner) &&
                  owner.VerticalScrollMode != ScrollMode.Disabled
                : IsApprovedInnerScrollOwner(owner);
            return ownerIsValid
                    ? "content-list"
                    : null;
        }

        InspectionModelCard modelCard = Named<InspectionModelCard>(
            page,
            "InspectionModelCardControl");
        ScrollViewer checks = Named<ScrollViewer>(
            modelCard,
            "InspectionChecksScrollViewer");
        return IsApprovedInnerScrollOwner(checks)
                ? "model-card"
                : null;
    }

    private static bool IsApprovedInnerScrollOwner(ScrollViewer owner) =>
        IsVisible(owner) &&
        owner.VerticalScrollMode == ScrollMode.Enabled &&
        NearlyEqual(owner.MaxHeight, 172d) &&
        double.IsFinite(owner.ActualHeight) &&
        owner.ActualHeight > 0d &&
        owner.ActualHeight <= owner.MaxHeight + 1d;

    private (double Width, double Height) ObservePointerTargets()
    {
        Control[] controls = Visible<Control>(page)
            .Where(control => control.IsEnabled &&
                control.IsHitTestVisible &&
                (SemanticId(control) is not null ||
                 control is ScrollViewer { IsTabStop: true }))
            .ToArray();
        return controls.Length == 0
            ? (44d, 44d)
            : (controls.Min(control => control.ActualWidth),
                controls.Min(control => control.ActualHeight));
    }

    private IReadOnlyList<string> ObserveLogicalReadingOrder()
    {
        return SemanticElements()
            .Select(SemanticId)
            .Where(id => id is not null)
            .Cast<string>()
            .Distinct(StringComparer.Ordinal)
            .ToArray();
    }

    private IReadOnlyList<string> ObserveTabOrder()
    {
        return Visible<Control>(page)
            .Where(control => control is not ScrollViewer &&
                control.IsTabStop && control.IsEnabled &&
                control.IsHitTestVisible)
            .OrderBy(control => control is InspectionDisclosure
                ? 0
                : control.TabIndex)
            .ThenBy(control => BoundsInPage(control).Y)
            .ThenBy(control => BoundsInPage(control).X)
            .Select(SemanticOrAccessibleId)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
    }

    private IEnumerable<FrameworkElement> SemanticElements()
    {
        FrameworkElement model = Named<FrameworkElement>(
            page,
            "InspectionModelCardControl");
        if (IsVisible(model))
        {
            yield return model;
        }

        FrameworkElement progress = Named<FrameworkElement>(
            Named<InspectionContentCard>(page, "InspectionContentCardControl"),
            "ProgressView");
        if (IsVisible(progress))
        {
            FrameworkElement progressList = Named<FrameworkElement>(
                Named<InspectionContentCard>(page,
                    "InspectionContentCardControl"),
                "ProgressItemsRepeater");
            if (IsVisible(progressList))
            {
                yield return progressList;
                foreach (FrameworkElement row in Visible<FrameworkElement>(
                             progressList)
                             .Where(element => TextRoleId(element) is not null)
                             .OrderBy(element => BoundsInPage(element).Y)
                             .ThenBy(element => BoundsInPage(element).X))
                {
                    yield return row;
                }
            }
        }

        foreach (Button action in Visible<Button>(
                     Named<InspectionActionCard>(page,
                         "InspectionActionCardControl"))
                     .Where(button => SemanticId(button) is not null)
                     .OrderBy(button => BoundsInPage(button).Y)
                     .ThenBy(button => BoundsInPage(button).X))
        {
            yield return action;
        }

        if (IsVisible(progress))
        {
            yield break;
        }

        foreach (InspectionDisclosure disclosure in
                 Visible<InspectionDisclosure>(page)
                     .OrderBy(disclosure => BoundsInPage(disclosure).Y)
                     .ThenBy(disclosure => BoundsInPage(disclosure).X))
        {
            if (SemanticId(disclosure) is not null)
            {
                yield return disclosure;
            }
        }

        InspectionContentCard contentCard = Named<InspectionContentCard>(
            page,
            "InspectionContentCardControl");
        FrameworkElement findings = Named<FrameworkElement>(
            contentCard,
            "FindingsView");
        if (IsVisible(findings))
        {
            FrameworkElement contentList = Named<FrameworkElement>(
                contentCard,
                IsVisible(Named<FrameworkElement>(
                    contentCard,
                    "ExpandedReportItemsControl"))
                    ? "ExpandedReportItemsControl"
                    : "FindingsItemsRepeater");
            if (IsVisible(contentList))
            {
                yield return contentList;
                foreach (FrameworkElement row in Visible<FrameworkElement>(
                             contentList)
                             .Where(element => TextRoleId(element) is not null)
                             .OrderBy(element => BoundsInPage(element).Y)
                             .ThenBy(element => BoundsInPage(element).X))
                {
                    yield return row;
                }
            }
        }
    }

    private static string? SemanticId(FrameworkElement element)
    {
        if (element.Name is "ProgressItemsRepeater")
        {
            return "progress-list";
        }

        if (element.Name is "FindingsItemsRepeater" or
            "ExpandedReportItemsControl")
        {
            return "content-list";
        }

        if (string.Equals(
                element.Name,
                "InspectionModelCardControl",
                StringComparison.Ordinal))
        {
            return "model-card";
        }

        if (element is InspectionDisclosure disclosure)
        {
            return disclosure.Name switch
            {
                "InspectionDetailsDisclosure" =>
                    "inspection-details-disclosure",
                "FindingsDisclosure" => "findings-disclosure",
                _ => null
            };
        }

        if (element is Button button)
        {
            return ActionId(
                Microsoft.UI.Xaml.Automation.AutomationProperties.GetName(
                    button));
        }

        return TextRoleId(element);
    }

    private static string? ActionId(string automationName) => automationName switch
    {
        "Cancel model inspection" => "cancel",
        "Choose another model" => "choose-another",
        "View technical inspection report" => "technical-report",
        "Check model hardware fit" => "hardware-fit",
        "Continue to model hardware check" => "continue-hardware",
        "Choose model conversion format" => "conversion-format",
        "Locate missing model file" => "locate-missing",
        "Restart model inspection" => "restart",
        "Retry model inspection" => "retry",
        _ => null
    };

    private bool ObserveSemanticBrushes()
    {
        TextBlock title = Named<TextBlock>(page, "PageTitle");
        SolidColorBrush[] brushes = SemanticBrushKeys()
            .Select(key => ResolveResource(key) as SolidColorBrush)
            .Where(brush => brush is not null)
            .Cast<SolidColorBrush>()
            .ToArray();
        if (brushes.Length != SemanticBrushKeys().Length ||
            brushes.Any(brush => brush.Color.A == 0) ||
            brushes.Select(brush => brush.Color).Distinct().Count() < 2 ||
            title.Foreground is not SolidColorBrush titleBrush ||
            !SameColor(titleBrush, RequiredBrush("InspectionTextPrimaryBrush")))
        {
            return false;
        }

        FrameworkElement outcome = Named<FrameworkElement>(
            page,
            "InspectionOutcomeCardControl");
        if (!IsVisible(outcome))
        {
            return ObserveVisibleStatusCues(brushes);
        }

        SymbolIcon icon = Named<SymbolIcon>(outcome, "OutcomeIcon");
        TextBlock outcomeTitle = Named<TextBlock>(outcome, "OutcomeTitle");
        string toneState = CurrentVisualStateName(
            Named<FrameworkElement>(outcome, "LayoutRoot"),
            "OutcomeToneStates") ?? string.Empty;
        string expectedTitleBrush = toneState switch
        {
            "SuccessTone" => "InspectionSuccessTextStrongBrush",
            "WarningTone" => "InspectionWarningTextStrongBrush",
            "ErrorTone" => "InspectionErrorTextStrongBrush",
            "InformationTone" or "NeutralTone" =>
                "InspectionTextPrimaryBrush",
            _ => string.Empty
        };
        return expectedTitleBrush.Length != 0 &&
            outcomeTitle.Foreground is SolidColorBrush outcomeTitleBrush &&
            SameColor(outcomeTitleBrush, RequiredBrush(expectedTitleBrush)) &&
            IsVisible(icon) && !string.IsNullOrWhiteSpace(outcomeTitle.Text) &&
            !string.IsNullOrWhiteSpace(
                Microsoft.UI.Xaml.Automation.AutomationProperties.GetName(
                    outcome)) &&
            ObserveVisibleStatusCues(brushes);
    }

    private bool ObserveVisibleStatusCues(
        IReadOnlyCollection<SolidColorBrush> semanticBrushes)
    {
        string[] statusBrushKeys =
        [
            "InspectionSuccessTextBrush",
            "InspectionWarningTextBrush",
            "InspectionErrorTextBrush",
            "InspectionPrimaryBlueBrush",
            "InspectionTextSecondaryMutedBrush",
            "InspectionTextSecondaryStrongBrush",
            "InspectionSurfaceBrush"
        ];
        HashSet<Windows.UI.Color> allowed = semanticBrushes
            .Select(brush => brush.Color)
            .Concat(statusBrushKeys.Select(key => RequiredBrush(key).Color))
            .ToHashSet();
        FrameworkElement[] statusText = Visible<TextBlock>(page)
            .Where(text => IsStatusText(text))
            .Cast<FrameworkElement>()
            .ToArray();
        FrameworkElement[] statusIcons = Visible<SymbolIcon>(page)
            .Where(IsSemanticStatusIcon)
            .Cast<FrameworkElement>()
            .ToArray();
        FrameworkElement[] cues = statusText.Concat(statusIcons).ToArray();
        if (cues.Length == 0)
        {
            return false;
        }

        return cues.All(cue => cue switch
        {
            TextBlock text => !string.IsNullOrWhiteSpace(text.Text) &&
                text.Foreground is SolidColorBrush brush &&
                brush.Color.A != 0 &&
                IsResolvedSemanticColor(brush.Color, allowed),
            SymbolIcon icon => icon.Foreground is SolidColorBrush brush &&
                brush.Color.A != 0 &&
                IsResolvedSemanticColor(brush.Color, allowed),
            _ => false
        });
    }

    private static bool IsResolvedSemanticColor(
        Windows.UI.Color color,
        IReadOnlySet<Windows.UI.Color> requiredColors) =>
        requiredColors.Contains(color);

    private static bool IsStatusText(TextBlock text) =>
        string.Equals(text.Name, "CompactStatusText", StringComparison.Ordinal) ||
        HasStatusRowAncestor(text) &&
        (text.Text is "Passed" or "Warning" or "Failed" or "Active" or
         "Waiting" or "Information" or "Completed" or "Cancelled");

    private static bool HasStatusRowAncestor(DependencyObject element)
    {
        return HasAncestorNamed(element, "ProgressItemsRepeater") ||
            HasAncestorNamed(element, "FindingsItemsRepeater") ||
            HasAncestorNamed(element, "ExpandedReportItemsControl") ||
            HasAncestorNamed(element, "InspectionChecksItemsControl");
    }

    private static bool IsSemanticStatusIcon(SymbolIcon icon) =>
        string.Equals(icon.Name, "OutcomeIcon", StringComparison.Ordinal) ||
        HasStatusRowAncestor(icon);

    private bool ObserveNaturalTextReflow()
    {
        TextBlock title = Named<TextBlock>(page, "PageTitle");
        TextBlock modelSection = Named<TextBlock>(
            Named<InspectionModelCard>(page, "InspectionModelCardControl"),
            "ModelOverviewTitle");
        TextBlock contentSection = Named<TextBlock>(
            Named<InspectionContentCard>(page, "InspectionContentCardControl"),
            "FindingsSectionTitle");
        double scale = preset.Text ==
            ModelInspectionFixtureTextProfile.Preview200 ? 2d : 1d;
        (string Key, double Standard)[] typography =
        [
            ("InspectionPageTitleFontSize", 32d),
            ("InspectionSectionTitleFontSize", 18d),
            ("InspectionBodyFontSize", 14d),
            ("InspectionHelperFontSize", 12d),
            ("InspectionLabelFontSize", 10d)
        ];
        return typography.All(item =>
                   page.Resources[item.Key] is double value &&
                   NearlyEqual(value, item.Standard * scale)) &&
            NearlyEqual(title.FontSize, 32d * scale) &&
            (!IsVisible(modelSection) ||
             NearlyEqual(modelSection.FontSize, 18d * scale)) &&
            (!IsVisible(contentSection) ||
             NearlyEqual(contentSection.FontSize, 18d * scale)) &&
            Visible<TextBlock>(page).All(text =>
            {
                double desiredContentHeight = Math.Max(
                    0d,
                    text.DesiredSize.Height - Math.Max(0d, text.Margin.Top) -
                    Math.Max(0d, text.Margin.Bottom));
                return string.IsNullOrEmpty(text.Text) ||
                    (text.ActualHeight + 1d >= text.FontSize &&
                     text.ActualHeight + 1d >= desiredContentHeight);
            });
    }

    private static string[] SemanticBrushKeys() =>
    [
        "InspectionTextPrimaryBrush",
        "InspectionPrimaryBlueBrush",
        "InspectionSuccessTextBrush",
        "InspectionWarningTextBrush",
        "InspectionErrorTextBrush",
        "InspectionSurfaceBrush",
        "InspectionBorderStrongBrush"
    ];

    private SolidColorBrush RequiredBrush(string key) =>
        ResolveResource(key) as SolidColorBrush ??
        throw new InvalidOperationException(
            $"The semantic brush resource '{key}' was not resolved.");

    private static bool SameColor(
        SolidColorBrush left,
        SolidColorBrush right) => left.Color == right.Color;

    private static string? CurrentVisualStateName(
        FrameworkElement root,
        string groupName)
    {
        foreach (VisualStateGroup group in
                 VisualStateManager.GetVisualStateGroups(root))
        {
            if (string.Equals(group.Name, groupName, StringComparison.Ordinal))
            {
                return group.CurrentState?.Name;
            }
        }

        return null;
    }

    private object? ResolveResource(string key)
    {
        if (page.Resources.ContainsKey(key))
        {
            return page.Resources[key];
        }

        foreach (ResourceDictionary dictionary in
                 Application.Current.Resources.MergedDictionaries)
        {
            string themeKey = page.RequestedTheme.ToString();
            if (dictionary.ThemeDictionaries.ContainsKey(themeKey) &&
                dictionary.ThemeDictionaries[themeKey] is
                    ResourceDictionary theme &&
                theme.ContainsKey(key))
            {
                return theme[key];
            }
        }

        return null;
    }

    private IEnumerable<FrameworkElement> RequiredTopLevelRegions()
    {
        yield return Named<FrameworkElement>(page, "Header");
        yield return Named<FrameworkElement>(
            page,
            "InspectionOutcomeCardControl");
        yield return Named<FrameworkElement>(
            page,
            "InspectionModelCardControl");
        yield return Named<FrameworkElement>(
            page,
            "InspectionContentCardControl");
        yield return Named<FrameworkElement>(
            page,
            "OutgoingProgressContentCard");
        yield return Named<FrameworkElement>(
            page,
            "InspectionActionCardControl");
    }

    private IEnumerable<FrameworkElement> RequiredVisibleElements()
    {
        foreach (FrameworkElement element in RequiredTopLevelRegions())
        {
            if (IsVisible(element))
            {
                yield return element;
            }
        }

        foreach (Control control in Visible<Control>(page).Where(control =>
                     control.IsEnabled && control.IsHitTestVisible &&
                     (control.IsTabStop || SemanticId(control) is not null)))
        {
            yield return control;
        }

        foreach (SymbolIcon icon in Visible<SymbolIcon>(page)
                     .Where(IsSemanticStatusIcon))
        {
            yield return icon;
        }
    }

    private Rect BoundsInPage(FrameworkElement element)
        => BoundsIn(element, page);

    private static Rect BoundsIn(
        FrameworkElement element,
        FrameworkElement coordinateRoot) =>
        element.TransformToVisual(coordinateRoot).TransformBounds(
            new Rect(0d, 0d, element.ActualWidth, element.ActualHeight));

    private static bool FitsVisibleAncestors(FrameworkElement element) =>
        FitsVisibleAncestors(element, out _);

    private static bool FitsVisibleAncestors(
        FrameworkElement element,
        out string diagnostic)
    {
        bool constrainX = true;
        bool constrainY = true;
        DependencyObject? current = VisualTreeHelper.GetParent(element);
        while (current is not null)
        {
            if (current is FrameworkElement ancestor)
            {
                ScrollViewer? scrollViewer = ancestor switch
                {
                    ScrollViewer owner => owner,
                    ScrollContentPresenter presenter =>
                        FindNearestOwningScrollViewer(presenter),
                    _ => null
                };
                if (scrollViewer is not null)
                {
                    constrainX &= scrollViewer.HorizontalScrollMode ==
                        ScrollMode.Disabled;
                    constrainY &= scrollViewer.VerticalScrollMode ==
                        ScrollMode.Disabled;
                }

                bool checkX = constrainX &&
                    ConstrainsDescendantsHorizontally(ancestor);
                bool checkY = constrainY &&
                    ConstrainsDescendantsVertically(ancestor);
                if (checkX || checkY)
                {
                    Rect bounds = BoundsIn(element, ancestor);
                    double x = bounds.X;
                    double y = bounds.Y;
                    const double layoutTolerance = 2d;
                    if ((checkX &&
                         (x < -layoutTolerance || x + bounds.Width >
                             ancestor.ActualWidth + layoutTolerance)) ||
                        (checkY &&
                         (y < -layoutTolerance || y + bounds.Height >
                             ancestor.ActualHeight + layoutTolerance)))
                    {
                        diagnostic = Bounded(
                            $"ancestor={ElementLabel(ancestor)} " +
                            $"checkX={checkX} checkY={checkY} " +
                            $"bounds={Number(bounds.X)},{Number(bounds.Y)}," +
                            $"{Number(bounds.Width)}," +
                            $"{Number(bounds.Height)} " +
                            $"elementActual={Number(element.ActualWidth)}x" +
                            $"{Number(element.ActualHeight)} " +
                            $"ancestorActual={Number(ancestor.ActualWidth)}x" +
                            $"{Number(ancestor.ActualHeight)} " +
                            $"width={Number(ancestor.Width)} " +
                            $"height={Number(ancestor.Height)} " +
                            $"maxWidth={Number(ancestor.MaxWidth)} " +
                            $"maxHeight={Number(ancestor.MaxHeight)} " +
                            $"clip={ClipLabel(ancestor.Clip)} " +
                            $"constrainX={constrainX} " +
                            $"constrainY={constrainY}");
                        return false;
                    }
                }

                if (!constrainX && !constrainY)
                {
                    diagnostic = string.Empty;
                    return true;
                }
            }

            current = VisualTreeHelper.GetParent(current);
        }

        diagnostic = string.Empty;
        return true;
    }

    private static ScrollViewer? FindNearestOwningScrollViewer(
        ScrollContentPresenter presenter)
    {
        const int maximumAncestorDepth = 64;
        if (!string.Equals(
                presenter.Name,
                "ScrollContentPresenter",
                StringComparison.Ordinal))
        {
            return null;
        }

        var interveningAncestors = new List<DependencyObject>();
        DependencyObject? current = VisualTreeHelper.GetParent(presenter);
        for (int depth = 0;
             current is not null && depth < maximumAncestorDepth;
             depth++)
        {
            if (current is ScrollViewer owner)
            {
                DependencyObject? content = owner.Content as DependencyObject;
                if (ReferenceEquals(content, presenter) ||
                    interveningAncestors.Any(ancestor =>
                        ReferenceEquals(content, ancestor)))
                {
                    return null;
                }

                return owner;
            }

            if (current is ScrollContentPresenter)
            {
                return null;
            }

            interveningAncestors.Add(current);
            current = VisualTreeHelper.GetParent(current);
        }

        return null;
    }

    private static bool ConstrainsDescendantsHorizontally(
        FrameworkElement element) =>
        element.Clip is not null ||
        !double.IsNaN(element.Width) ||
        !double.IsPositiveInfinity(element.MaxWidth);

    private static bool ConstrainsDescendantsVertically(
        FrameworkElement element) =>
        element.Clip is not null ||
        !double.IsNaN(element.Height) ||
        !double.IsPositiveInfinity(element.MaxHeight);

    private static string ElementLabel(FrameworkElement element)
    {
        string name = string.IsNullOrWhiteSpace(element.Name)
            ? "-"
            : BoundedToken(element.Name);
        string id = SemanticId(element) is { } semantic
            ? BoundedToken(semantic)
            : "-";
        return $"type={element.GetType().Name},name={name},id={id}";
    }

    private static string Number(double value) =>
        value.ToString("0.###", CultureInfo.InvariantCulture);

    private static string ClipLabel(Geometry? clip) => clip switch
    {
        null => "none",
        RectangleGeometry rectangle =>
            $"Rectangle({Number(rectangle.Rect.X)}," +
            $"{Number(rectangle.Rect.Y)}," +
            $"{Number(rectangle.Rect.Width)}," +
            $"{Number(rectangle.Rect.Height)})",
        _ => clip.GetType().Name
    };

    private static string Quoted(string value) =>
        $"\"{BoundedToken(value)}\"";

    private static string BoundedToken(string value)
    {
        string singleLine = value
            .Replace('\r', ' ')
            .Replace('\n', ' ')
            .Replace('"', '\'');
        return singleLine.Length <= 96
            ? singleLine
            : singleLine[..96];
    }

    private static string Bounded(string value) =>
        value.Length <= 768 ? value : value[..768];

    private static string SemanticOrAccessibleId(FrameworkElement element)
    {
        string? semantic = SemanticId(element);
        if (semantic is not null)
        {
            return semantic;
        }

        string accessible =
            Microsoft.UI.Xaml.Automation.AutomationProperties.GetName(element);
        return string.IsNullOrWhiteSpace(accessible)
            ? $"{element.GetType().Name}:{element.Name}"
            : $"accessible:{accessible}";
    }

    private CancellationTokenSource Linked(CancellationToken cancellationToken) =>
        CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken,
            lifetime.Token);

    private static T Named<T>(FrameworkElement owner, string name)
        where T : class => owner.FindName(name) as T ??
            throw new InvalidOperationException(
                $"The fixture element '{name}' was not found.");

    private static IEnumerable<T> Visible<T>(DependencyObject root)
        where T : FrameworkElement => Descendants<T>(root).Where(IsVisible);

    private static IEnumerable<T> Descendants<T>(DependencyObject root)
        where T : DependencyObject
    {
        int count = VisualTreeHelper.GetChildrenCount(root);
        for (int index = 0; index < count; index++)
        {
            DependencyObject child = VisualTreeHelper.GetChild(root, index);
            if (child is T match)
            {
                yield return match;
            }

            foreach (T nested in Descendants<T>(child))
            {
                yield return nested;
            }
        }
    }

    private static bool IsVisible(FrameworkElement element) =>
        element.Visibility == Visibility.Visible &&
        element.Opacity > 0d &&
        element.ActualWidth > 0d &&
        element.ActualHeight > 0d &&
        AncestorsAreVisible(element);

    private static bool IsDisplayed(FrameworkElement element)
    {
        if (!element.IsLoaded)
        {
            return false;
        }

        DependencyObject? current = element;
        while (current is not null)
        {
            if (current is FrameworkElement ancestor &&
                (ancestor.Visibility != Visibility.Visible ||
                 ancestor.Opacity <= 0d))
            {
                return false;
            }

            current = VisualTreeHelper.GetParent(current);
        }

        return true;
    }

    private static bool AncestorsAreVisible(DependencyObject element)
    {
        DependencyObject? current = VisualTreeHelper.GetParent(element);
        while (current is not null)
        {
            if (current is FrameworkElement ancestor &&
                (ancestor.Visibility != Visibility.Visible ||
                 ancestor.Opacity <= 0d ||
                 ancestor.ActualWidth <= 0d ||
                 ancestor.ActualHeight <= 0d))
            {
                return false;
            }

            current = VisualTreeHelper.GetParent(current);
        }

        return true;
    }

    private static bool HasAncestorNamed(
        DependencyObject element,
        string name)
    {
        DependencyObject? current = element;
        while (current is not null)
        {
            if (current is FrameworkElement owner &&
                string.Equals(owner.Name, name, StringComparison.Ordinal))
            {
                return true;
            }

            current = VisualTreeHelper.GetParent(current);
        }

        return false;
    }

    private static bool HasVisualAncestor<T>(DependencyObject element)
        where T : DependencyObject
    {
        DependencyObject? current = VisualTreeHelper.GetParent(element);
        while (current is not null)
        {
            if (current is T)
            {
                return true;
            }

            current = VisualTreeHelper.GetParent(current);
        }

        return false;
    }

    private static double Width(ModelInspectionFixtureWidthProfile width) =>
        width switch
        {
            ModelInspectionFixtureWidthProfile.Desktop1440 => 1440d,
            ModelInspectionFixtureWidthProfile.Medium600 => 600d,
            ModelInspectionFixtureWidthProfile.Narrow360 => 360d,
            _ => throw new ArgumentOutOfRangeException(
                nameof(width), width, "Unknown fixture width profile.")
        };

    private static bool NearlyEqual(double left, double right) =>
        Math.Abs(left - right) <= 0.01d;

    private void ThrowIfDisposed() =>
        ObjectDisposedException.ThrowIf(
            Volatile.Read(ref disposed) != 0,
            this);
}
#endif
