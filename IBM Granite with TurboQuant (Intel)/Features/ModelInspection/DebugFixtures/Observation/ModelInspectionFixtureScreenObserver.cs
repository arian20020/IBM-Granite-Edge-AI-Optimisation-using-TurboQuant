#if MODEL_INSPECTION_FIXTURE_GALLERY
using GraniteEdgeAI.Features.ModelInspection.Controls;
using GraniteEdgeAI.Features.ModelInspection.Models;
using GraniteEdgeAI.Features.ModelInspection.Presentation;
using GraniteEdgeAI.Features.ModelInspection.ViewModels;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Automation.Peers;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace GraniteEdgeAI.Features.ModelInspection.DebugFixtures.Observation;

internal sealed class ModelInspectionFixtureScreenObserver :
    IModelInspectionFixtureScreenObserver
{
    public IModelInspectionFixtureObservationSession Begin(
        ModelInspectionPage page)
    {
        ArgumentNullException.ThrowIfNull(page);
        return new ObservationSession(page);
    }

    private sealed class ObservationSession :
        IModelInspectionFixtureObservationSession
    {
        private readonly ModelInspectionPage page;
        private readonly ModelInspectionViewModel? viewModel;
        private long attemptGeneration;
        private int progressAnnouncementBaseline;
        private int outcomeAnnouncementBaseline;
        private object? disclosureItemsSource;
        private object? disclosureScrollOwner;
        private IReadOnlyList<object> disclosureContainers = Array.Empty<object>();
        private bool sampledBeforeDisclosure;
        private int disposed;

        internal ObservationSession(ModelInspectionPage page)
        {
            this.page = page;
            viewModel = page.ViewModel;
            attemptGeneration = viewModel?.Snapshot.RenderKey.AttemptGeneration ?? 0;
            InspectionContentCard content = ContentCard;
            InspectionOutcomeCard outcome = OutcomeCard;
            progressAnnouncementBaseline =
                content.LiveRegionChangeNotificationCount;
            outcomeAnnouncementBaseline =
                outcome.LiveRegionChangeNotificationCount;
            if (viewModel is not null)
            {
                viewModel.PropertyChanged += ViewModel_PropertyChanged;
            }

            QueuePreDisclosureSample();
        }

        public async Task<ModelInspectionObservedScreen> CaptureAsync(
            CancellationToken cancellationToken)
        {
            ThrowIfDisposed();
            cancellationToken.ThrowIfCancellationRequested();
            await WaitForLoadedAsync(page, cancellationToken);
            await DrainDispatcherAsync(page.DispatcherQueue, cancellationToken);
            page.UpdateLayout();
            await WaitForRenderingAsync(cancellationToken);
            await DrainDispatcherAsync(page.DispatcherQueue, cancellationToken);
            page.UpdateLayout();
            cancellationToken.ThrowIfCancellationRequested();

            ModelInspectionPagePresentation presentation =
                page.CurrentPresentation ?? throw new InvalidOperationException(
                    "The loaded fixture page has no current presentation.");
            ModelInspectionObservedModel model = ObserveModel();
            ModelInspectionObservedContent content = ObserveContent();
            ModelInspectionObservedActions actions = ObserveActions();
            ModelInspectionObservedOutcome outcome = ObserveOutcome();
            ModelInspectionObservedFigma figma = ObserveFigma(
                presentation,
                model,
                content,
                outcome);
            ModelInspectionObservedRowsAndScroll rowsAndScroll =
                ObserveRowsAndScroll(model, content);
            ModelInspectionObservedRetention retention = ObserveRetention(
                model,
                content,
                actions,
                rowsAndScroll);

            return new ModelInspectionObservedScreen
            {
                Figma = figma,
                Outcome = outcome,
                Model = model,
                Content = content,
                Actions = actions,
                Footer = ObserveFooter(),
                Focus = ObserveFocus(actions),
                Automation = ObserveAutomation(model, content, actions),
                Announcements = ObserveAnnouncements(),
                RowsAndScroll = rowsAndScroll,
                Retention = retention,
                RenderBarrier = new ModelInspectionObservedRenderBarrier
                {
                    DispatcherDrained = true,
                    LayoutUpdated = page.ActualWidth >= 0d &&
                        page.ActualHeight >= 0d,
                    CompositionCommitted = true
                }
            };
        }

        public void Dispose()
        {
            if (Interlocked.Exchange(ref disposed, 1) != 0)
            {
                return;
            }

            if (viewModel is not null)
            {
                viewModel.PropertyChanged -= ViewModel_PropertyChanged;
            }

            disclosureItemsSource = null;
            disclosureScrollOwner = null;
            disclosureContainers = Array.Empty<object>();
        }

        private InspectionOutcomeCard OutcomeCard =>
            Named<InspectionOutcomeCard>(page, "InspectionOutcomeCardControl");

        private InspectionModelCard ModelCard =>
            Named<InspectionModelCard>(page, "InspectionModelCardControl");

        private InspectionContentCard ContentCard =>
            Named<InspectionContentCard>(page, "InspectionContentCardControl");

        private InspectionActionCard ActionCard =>
            Named<InspectionActionCard>(page, "InspectionActionCardControl");

        private void ViewModel_PropertyChanged(
            object? sender,
            PropertyChangedEventArgs eventArguments)
        {
            if (Volatile.Read(ref disposed) != 0 ||
                sender is not ModelInspectionViewModel owner ||
                !ReferenceEquals(owner, viewModel) ||
                !string.Equals(
                    eventArguments.PropertyName,
                    nameof(ModelInspectionViewModel.Snapshot),
                    StringComparison.Ordinal))
            {
                return;
            }

            long currentAttempt = owner.Snapshot.RenderKey.AttemptGeneration;
            if (currentAttempt != attemptGeneration)
            {
                attemptGeneration = currentAttempt;
                progressAnnouncementBaseline =
                    ContentCard.LiveRegionChangeNotificationCount;
                outcomeAnnouncementBaseline =
                    OutcomeCard.LiveRegionChangeNotificationCount;
                disclosureItemsSource = null;
                disclosureScrollOwner = null;
                disclosureContainers = Array.Empty<object>();
                sampledBeforeDisclosure = false;
            }

            if (owner.Snapshot.TerminalResult is not null)
            {
                progressAnnouncementBaseline =
                    ContentCard.LiveRegionChangeNotificationCount;
            }

            QueuePreDisclosureSample();
        }

        private void QueuePreDisclosureSample()
        {
            DispatcherQueue queue = page.DispatcherQueue;
            if (!queue.TryEnqueue(TrySampleBeforeDisclosure))
            {
                throw new InvalidOperationException(
                    "The fixture observation sample could not be queued.");
            }
        }

        private void TrySampleBeforeDisclosure()
        {
            if (Volatile.Read(ref disposed) != 0 || sampledBeforeDisclosure)
            {
                return;
            }

            InspectionDisclosure? modelDisclosure = ModelCard.ActiveDisclosure;
            InspectionDisclosure? contentDisclosure = ContentCard.ActiveDisclosure;
            if ((modelDisclosure is null) == (contentDisclosure is null))
            {
                return;
            }

            InspectionDisclosure disclosure = modelDisclosure ?? contentDisclosure!;
            if (!disclosure.IsExpanded)
            {
                return;
            }

            ItemsControl items;
            ScrollViewer scroll;
            if (modelDisclosure is not null)
            {
                items = Named<ItemsControl>(
                    ModelCard,
                    "InspectionChecksItemsControl");
                scroll = Named<ScrollViewer>(
                    ModelCard,
                    "InspectionChecksScrollViewer");
            }
            else
            {
                items = Named<ItemsControl>(
                    ContentCard,
                    "ExpandedReportItemsControl");
                scroll = Named<ScrollViewer>(
                    ContentCard,
                    "ExpandedReportScrollViewer");
            }

            IReadOnlyList<object> containers = Containers(items);
            if (items.ItemsSource is null ||
                items.Items.Count == 0 ||
                containers.Count != items.Items.Count)
            {
                return;
            }

            disclosureItemsSource = items.ItemsSource;
            disclosureScrollOwner = scroll;
            disclosureContainers = containers;
            sampledBeforeDisclosure = true;
        }

        private ModelInspectionObservedFigma ObserveFigma(
            ModelInspectionPagePresentation presentation,
            ModelInspectionObservedModel model,
            ModelInspectionObservedContent content,
            ModelInspectionObservedOutcome outcome)
        {
            string inferred = InferFigmaState(model, content, outcome);
            FrameworkElement host = Named<FrameworkElement>(
                page,
                "InspectionContentHost");
            FrameworkElement scrollContent = Named<FrameworkElement>(
                page,
                "InspectionScrollContent");
            FrameworkElement reflowHost = Named<FrameworkElement>(
                page,
                "InspectionReflowHost");
            double canonicalWidth = Convert.ToDouble(
                Application.Current.Resources[
                    "InspectionContentColumnWidth"]);
            double availableWidth = Math.Max(
                0d,
                scrollContent.ActualWidth - host.Margin.Left -
                    host.Margin.Right);
            double expectedWidth = Math.Min(canonicalWidth, availableWidth);
            double expectedHeight = reflowHost.ActualHeight +
                reflowHost.Margin.Top + reflowHost.Margin.Bottom;
            bool canonicalGeometry = host.ActualWidth > 0d &&
                host.ActualHeight > 0d &&
                expectedWidth > 0d &&
                expectedHeight > 0d &&
                NearlyEqual(host.ActualWidth, expectedWidth) &&
                NearlyEqual(host.ActualHeight, expectedHeight);
            return new ModelInspectionObservedFigma
            {
                State = inferred,
                GeometryProfile = canonicalGeometry
                        ? "Canonical"
                        : string.Empty,
                PresentationStateMatches = string.Equals(
                    inferred,
                    presentation.State.ToString(),
                    StringComparison.Ordinal),
                ContentWidth = host.ActualWidth,
                ContentHeight = host.ActualHeight
            };
        }

        private static bool NearlyEqual(double left, double right) =>
            Math.Abs(left - right) <= 1d;

        private ModelInspectionObservedOutcome ObserveOutcome()
        {
            InspectionOutcomeCard card = OutcomeCard;
            bool visible = IsVisible(Named<Grid>(card, "LayoutRoot"));
            return new ModelInspectionObservedOutcome
            {
                Visible = visible,
                Kind = ObserveOutcomeKind(card, visible),
                Tone = ObserveOutcomeTone(card, visible),
                Badge = ObserveOutcomeBadge(card, visible),
                Title = visible
                    ? new ModelInspectionObservedText(
                        Named<TextBlock>(card, "OutcomeTitle").Text)
                    : null,
                SupportingText = visible
                    ? new ModelInspectionObservedText(
                        Named<TextBlock>(card, "OutcomeMessage").Text)
                    : null
            };
        }

        private static string ObserveOutcomeKind(
            InspectionOutcomeCard card,
            bool visible)
        {
            if (!visible)
            {
                return "Hidden";
            }

            string title = Named<TextBlock>(card, "OutcomeTitle").Text;
            return title switch
            {
                "Model inspection complete" => "Ready",
                "Model inspected with warnings" => "ReadyWithWarnings",
                "Conversion required" => "ConversionRequired",
                "Model package is incomplete" => "IncompletePackage",
                "Model is not supported" => "Unsupported",
                "Model is invalid" => "Invalid",
                "Inspection cancelled" => "Cancelled",
                "Inspection could not be completed" => "OperationalFailure",
                _ => $"Unmapped:{title}"
            };
        }

        private string ObserveOutcomeTone(
            InspectionOutcomeCard card,
            bool visible)
        {
            if (!visible)
            {
                return "Neutral";
            }

            Border border = Named<Border>(card, "OutcomeCardBorder");
            string kind = ObserveOutcomeKind(card, visible);
            (string Tone, string Surface, string Outline) mapping = kind switch
            {
                "Ready" => ("Success", "InspectionSuccessSurfaceBrush",
                    "InspectionSuccessBorderBrush"),
                "ReadyWithWarnings" or "IncompletePackage" =>
                    ("Warning", "InspectionWarningSurfaceBrush",
                        "InspectionWarningBorderBrush"),
                "ConversionRequired" =>
                    ("Information", "InspectionBlueSurfaceBrush",
                        "InspectionBlueBorderStrongBrush"),
                "Unsupported" or "Invalid" or "OperationalFailure" =>
                    ("Error", "InspectionErrorSurfaceBrush",
                        "InspectionErrorBorderBrush"),
                "Cancelled" => ("Neutral", "InspectionSurfaceMutedBrush",
                    "InspectionBorderMutedBrush"),
                _ => (string.Empty, string.Empty, string.Empty)
            };
            return !string.IsNullOrEmpty(mapping.Tone) &&
                MatchesCanonicalBrush(
                    border.Background,
                    card.ActualTheme,
                    mapping.Surface) &&
                MatchesCanonicalBrush(
                    border.BorderBrush,
                    card.ActualTheme,
                    mapping.Outline)
                    ? mapping.Tone
                    : "Unmapped";
        }

        private static ModelInspectionObservedText? ObserveOutcomeBadge(
            InspectionOutcomeCard card,
            bool visible)
        {
            if (!visible)
            {
                return null;
            }

            TextBlock[] badges = Descendants<TextBlock>(
                    Named<Border>(card, "OutcomeCardBorder"))
                .Where(text =>
                    IsVisible(text) &&
                    (string.Equals(
                         text.Name,
                         "OutcomeBadgeText",
                         StringComparison.Ordinal) ||
                     string.Equals(
                         text.Tag as string,
                         "InspectionOutcomeBadge",
                         StringComparison.Ordinal)))
                .ToArray();
            return badges.Length switch
            {
                0 => null,
                1 => new ModelInspectionObservedText(badges[0].Text),
                _ => new ModelInspectionObservedText("Unmapped")
            };
        }

        private bool MatchesCanonicalBrush(
            Brush? observed,
            ElementTheme actualTheme,
            string key)
        {
            if (page.Resources.ContainsKey(key))
            {
                object localCanonical = page.Resources[key];
                return ReferenceEquals(observed, localCanonical) ||
                    observed is SolidColorBrush localObservedColor &&
                    localCanonical is SolidColorBrush localCanonicalColor &&
                    localObservedColor.Color == localCanonicalColor.Color;
            }

            ResourceDictionary modelInspectionTheme =
                Application.Current.Resources.MergedDictionaries.Single(
                    dictionary => dictionary.Source?.OriginalString.EndsWith(
                        "/Features/ModelInspection/Presentation/" +
                        "ModelInspectionTheme.xaml",
                        StringComparison.OrdinalIgnoreCase) == true);
            ResourceDictionary theme =
                (ResourceDictionary)modelInspectionTheme.ThemeDictionaries[
                    actualTheme.ToString()];
            object canonical = theme[key];
            return ReferenceEquals(observed, canonical) ||
                observed is SolidColorBrush observedColor &&
                canonical is SolidColorBrush canonicalColor &&
                observedColor.Color.Equals(canonicalColor.Color);
        }

        private ModelInspectionObservedModel ObserveModel()
        {
            InspectionModelCard card = ModelCard;
            string mode = ObserveModelMode(card);
            bool detailed = string.Equals(
                mode,
                "Detailed",
                StringComparison.Ordinal);
            bool disclosureExpanded = ObserveModelDisclosureExpanded(card);
            IReadOnlyList<ModelInspectionObservedMetadataField> metadata = detailed
                ? ObserveMetadata(card)
                : Array.Empty<ModelInspectionObservedMetadataField>();
            IReadOnlyList<ModelInspectionObservedCheckRow> checks =
                detailed && disclosureExpanded
                ? ObserveModelChecks(card)
                : Array.Empty<ModelInspectionObservedCheckRow>();
            string displayName = detailed
                ? ReadField(card, "ModelNameField").Value
                : Descendants<TextBlock>(Named<StackPanel>(
                    card,
                    "CompactModelSummaryPanel")).First().Text;

            return new ModelInspectionObservedModel
            {
                Visible = IsVisible(card),
                Mode = mode,
                Badge = detailed ? null : ObserveModelBadge(card),
                DisplayName = displayName,
                LogicalDisplayFileName = page.Request?.FileName ??
                    string.Empty,
                Metadata = metadata,
                Checks = checks,
                DisclosureExpanded = disclosureExpanded
            };
        }

        private static bool ObserveModelDisclosureExpanded(
            InspectionModelCard card)
        {
            InspectionDisclosure disclosure = Named<InspectionDisclosure>(
                card,
                "InspectionDetailsDisclosure");
            return IsVisible(disclosure) && disclosure.IsExpanded;
        }

        private static string ObserveModelMode(InspectionModelCard card)
        {
            Visibility compact = Named<Border>(card, "CompactView").Visibility;
            Visibility detailed = Named<Border>(card, "DetailedView").Visibility;
            return (compact, detailed) switch
            {
                (Visibility.Visible, Visibility.Collapsed) => "Compact",
                (Visibility.Collapsed, Visibility.Visible) => "Detailed",
                _ => "Unmapped"
            };
        }

        private static string ObserveModelBadge(InspectionModelCard card)
        {
            string text = Named<TextBlock>(card, "CompactStatusText").Text;
            string automationName = AutomationProperties.GetName(
                Named<Border>(card, "CompactStatusChip"));
            if (!string.Equals(
                    automationName,
                    $"Model status: {text}",
                    StringComparison.Ordinal))
            {
                return "Unmapped";
            }

            return text switch
            {
                "MODEL SELECTED" => "ModelSelected",
                "INSPECTED" => "Inspected",
                "SOURCE MODEL" => "SourceModel",
                "INCOMPLETE" => "Incomplete",
                "UNSUPPORTED" => "Unsupported",
                "INVALID" => "Invalid",
                "NOT INSPECTED" => "NotInspected",
                "RESULT UNKNOWN" => "ResultUnknown",
                _ => "Unmapped"
            };
        }

        private IReadOnlyList<ModelInspectionObservedCheckRow>
            ObserveModelChecks(InspectionModelCard card)
        {
            ItemsControl items = Named<ItemsControl>(
                card,
                "InspectionChecksItemsControl");
            List<ModelInspectionObservedCheckRow> observed = [];
            for (int index = 0; index < items.Items.Count; index++)
            {
                FrameworkElement container =
                    items.ContainerFromIndex(index) as FrameworkElement ??
                    throw new InvalidOperationException(
                        "A loaded inspection-check row was not realized.");
                Border row = Descendants<Border>(container).First(border =>
                    string.Equals(
                        border.Tag as string,
                        "InspectionCheckRow",
                        StringComparison.Ordinal));
                TextBlock title = Descendants<TextBlock>(row).Single(text =>
                    string.Equals(
                        text.Tag as string,
                        "InspectionCheckAutomationElement",
                        StringComparison.Ordinal));
                StackPanel copy = Descendants<StackPanel>(row).Single(panel =>
                    Grid.GetColumn(panel) == 1);
                TextBlock[] copyText = Descendants<TextBlock>(copy).ToArray();
                Grid statusOwner = Descendants<Grid>(row).Single(grid =>
                    Grid.GetColumn(grid) == 2);
                TextBlock[] statuses = Descendants<TextBlock>(statusOwner)
                    .Where(IsVisible)
                    .ToArray();
                Border[] statusIcons = Descendants<Border>(row)
                    .Where(border =>
                        string.Equals(
                            border.Tag as string,
                            "InspectionCheckStatusIcon",
                            StringComparison.Ordinal) &&
                        IsVisible(border))
                    .ToArray();
                observed.Add(new(
                    CheckId(title.Text),
                    copyText.Length > 1 ? copyText[1].Text : string.Empty,
                    statuses.Length == 1 && statusIcons.Length == 1
                        ? CheckStatus(card, statuses[0], statusIcons[0])
                        : "Unmapped"));
            }

            return observed;
        }

        private static IReadOnlyList<ModelInspectionObservedMetadataField>
            ObserveMetadata(InspectionModelCard card)
        {
            List<ModelInspectionObservedMetadataField> observed = [];
            Grid metadata = Named<Grid>(card, "MetadataGrid");
            foreach (Border field in metadata.Children.OfType<Border>()
                         .Where(IsVisible))
            {
                string? id = MetadataFieldId(field.Name);
                if (id is null)
                {
                    continue;
                }

                (string label, string value) = ReadField(field, field.Name);
                observed.Add(new(id, label, value));
            }

            return observed;
        }

        private static string? MetadataFieldId(string name) => name switch
        {
            "PublisherField" => "metadata-publisher",
            "FormatField" => "metadata-format",
            "QuantisationField" => "metadata-quantisation",
            "ParametersField" => "metadata-parameters",
            "ModelTypeField" => "metadata-model-type",
            "DeclaredContextField" => "metadata-context",
            "FileSizeField" => "metadata-file-size",
            _ => null
        };

        private static (string Label, string Value) ReadField(
            Border field,
            string name)
        {
            TextBlock[] text = Descendants<TextBlock>(field).ToArray();
            if (text.Length < 2)
            {
                throw new InvalidOperationException(
                    $"The named model field '{name}' has no label/value pair.");
            }

            return (text[0].Text, text[1].Text);
        }

        private ModelInspectionObservedContent ObserveContent()
        {
            InspectionContentCard card = ContentCard;
            bool visible = IsVisible(Named<Grid>(card, "LayoutRoot"));
            string mode = ObserveContentMode(card, visible);
            int rowCount = visible ? ContentRowCount(card, mode) : 0;
            return new ModelInspectionObservedContent
            {
                Visible = visible,
                Mode = mode,
                Heading = visible
                    ? ReadContentHeading(card, mode)
                    : null,
                Rows = Enumerable.Range(0, rowCount)
                    .Select(index => ObserveContentRow(card, mode, index))
                    .ToArray(),
                DisclosureExpanded = ObserveContentDisclosureExpanded(card)
            };
        }

        private static bool ObserveContentDisclosureExpanded(
            InspectionContentCard card)
        {
            InspectionDisclosure disclosure = Named<InspectionDisclosure>(
                card,
                "FindingsDisclosure");
            return IsVisible(disclosure) && disclosure.IsExpanded;
        }

        private static string ObserveContentMode(
            InspectionContentCard card,
            bool visible)
        {
            if (!visible)
            {
                return "Hidden";
            }

            Visibility progress = Named<Grid>(card, "ProgressView").Visibility;
            Visibility findings = Named<Grid>(card, "FindingsView").Visibility;
            if (progress == Visibility.Visible &&
                findings == Visibility.Collapsed)
            {
                return "Progress";
            }

            if (progress != Visibility.Collapsed ||
                findings != Visibility.Visible)
            {
                return "Unmapped";
            }

            return Named<TextBlock>(card, "FindingsSectionTitle").Text switch
            {
                "Inspection warnings" => "Warnings",
                "Conversion required" => "ConversionRequired",
                "Incomplete model package" => "IncompletePackage",
                "Unsupported model" => "Unsupported",
                "Invalid model" => "Invalid",
                "Inspection cancelled" => "Cancelled",
                "Inspection did not complete" => "OperationalFailure",
                _ => "Unmapped"
            };
        }

        private static int ContentRowCount(
            InspectionContentCard card,
            string mode) => string.Equals(
                mode,
                "Progress",
                StringComparison.Ordinal)
                    ? ((IEnumerable?)Named<ItemsRepeater>(
                        card,
                        "ProgressItemsRepeater").ItemsSource)?.Cast<object>()
                        .Count() ?? 0
                    : Named<ItemsControl>(card, "FindingsItemsRepeater")
                        .Items.Count;

        private ModelInspectionObservedContentRow ObserveContentRow(
            InspectionContentCard card,
            string mode,
            int index)
        {
            FrameworkElement element = RowElement(card, mode, index) ??
                throw new InvalidOperationException(
                    "A loaded content row was not realized.");
            StackPanel copy = Descendants<StackPanel>(element)
                .First(panel => Grid.GetColumn(panel) == 1);
            TextBlock[] copyText = Descendants<TextBlock>(copy).ToArray();
            if (copyText.Length != 2)
            {
                throw new InvalidOperationException(
                    "A loaded content row has an invalid copy shape.");
            }

            string primary = copyText[0].Text;
            string? secondary = NullIfEmpty(copyText[1].Text) ??
                NullIfEmpty(AutomationProperties.GetHelpText(element));
            string status = ObserveContentStatus(element, mode);
            double? fraction = ObserveStageFraction(element, status);
            return new(
                ContentRowId(mode, primary, index),
                primary,
                secondary,
                status,
                fraction);
        }

        private string ObserveContentStatus(
            FrameworkElement element,
            string mode)
        {
            FrameworkElement rightOwner = Descendants<FrameworkElement>(element)
                .First(owner => Grid.GetColumn(owner) == 2);
            TextBlock[] right = SelfAndDescendants<TextBlock>(rightOwner)
                .Where(IsVisible)
                .ToArray();
            string text = right.Length == 1 ? right[0].Text : string.Empty;
            if (string.Equals(mode, "Progress", StringComparison.Ordinal))
            {
                ProgressRing[] activeRings = Descendants<ProgressRing>(element)
                    .Where(IsVisible)
                    .ToArray();
                if (activeRings.Length == 1 &&
                    string.Equals(text, "Checking", StringComparison.Ordinal))
                {
                    return "Active";
                }

                Border? motionTarget = Descendants<Border>(element)
                    .SingleOrDefault(border => string.Equals(
                        border.Name,
                        "ProgressStatusMotionTarget",
                        StringComparison.Ordinal));
                TextBlock[] stageNumbers = motionTarget is null
                    ? Array.Empty<TextBlock>()
                    : Descendants<TextBlock>(motionTarget)
                        .Where(IsVisible)
                        .ToArray();
                if (stageNumbers.Length == 1 &&
                    string.Equals(text, "Waiting", StringComparison.Ordinal))
                {
                    return "Waiting";
                }
            }

            Border[] markers = Descendants<Border>(element)
                .Where(marker =>
                    IsVisible(marker) &&
                    marker.Child is SymbolIcon)
                .ToArray();
            if (markers.Length != 1 || markers[0].Child is not SymbolIcon icon)
            {
                return $"Unmapped:{mode}:{text}";
            }

            Border marker = markers[0];
            ElementTheme theme = element.ActualTheme;
            return (text, icon.Symbol) switch
            {
                ("Checking", Symbol.Clock) when MatchesCanonicalBrush(
                    marker.Background,
                    theme,
                    "InspectionBlueSurfaceBrush") => "Active",
                ("Waiting", Symbol.Clock) when MatchesCanonicalBrush(
                    marker.Background,
                    theme,
                    "InspectionSurfaceMutedBrush") => "Waiting",
                ("Passed", Symbol.Accept) when MatchesCanonicalBrush(
                    marker.Background,
                    theme,
                    "InspectionSuccessSurfaceBrush") => "Passed",
                ("Warning" or "Incomplete", Symbol.Important)
                    when MatchesCanonicalBrush(
                        marker.Background,
                        theme,
                        "InspectionWarningSurfaceBrush") => "Warning",
                ("Failed" or "Unsupported" or "Invalid" or "Not completed",
                    Symbol.Cancel) when MatchesCanonicalBrush(
                        marker.Background,
                        theme,
                        "InspectionErrorSurfaceBrush") => "Error",
                ("Cancelled" or "Information", Symbol.Help)
                    when MatchesCanonicalBrush(
                        marker.Background,
                        theme,
                        "InspectionBlueSurfaceBrush") => "Information",
                _ => $"Unmapped:{mode}:{text}"
            };
        }

        private static double? ObserveStageFraction(
            FrameworkElement element,
            string status)
        {
            if (!string.Equals(status, "Active", StringComparison.Ordinal))
            {
                return null;
            }

            ProgressRing? ring = Descendants<ProgressRing>(element)
                .SingleOrDefault(value => value.Visibility == Visibility.Visible);
            return ring is null || ring.IsIndeterminate
                ? null
                : ring.Value / 100d;
        }

        private ModelInspectionObservedActions ObserveActions()
        {
            InspectionActionCard card = ActionCard;
            List<ModelInspectionObservedAction> items = [];
            string mode = ObserveActionMode(card);
            IEnumerable<Button> buttons = string.Equals(
                    mode,
                    "Inspecting",
                    StringComparison.Ordinal)
                ? [Named<Button>(card, "CancelActionButton")]
                : string.Equals(mode, "Result", StringComparison.Ordinal)
                    ? new[]
                    {
                        (Host: Named<StackPanel>(card,
                            "SecondaryActionOneHost"),
                            Button: Named<Button>(card,
                                "SecondaryActionOneButton")),
                        (Host: Named<StackPanel>(card,
                            "SecondaryActionTwoHost"),
                            Button: Named<Button>(card,
                                "SecondaryActionTwoButton")),
                        (Host: Named<StackPanel>(card, "PrimaryActionHost"),
                            Button: Named<Button>(card, "PrimaryActionButton"))
                    }
                    .OrderBy(item => Grid.GetRow(item.Host))
                    .ThenBy(item => Grid.GetColumn(item.Host))
                    .Select(item => item.Button)
                : Array.Empty<Button>();
            foreach (Button button in buttons)
            {
                if (!IsVisible(button))
                {
                    continue;
                }

                string accessibleName = AutomationProperties.GetName(button);
                items.Add(new ModelInspectionObservedAction(
                    ActionId(accessibleName),
                    ReadButtonText(button),
                    IsVisible(button),
                    button.IsEnabled,
                    NullIfEmpty(AutomationProperties.GetHelpText(button))));
            }

            return new ModelInspectionObservedActions
            {
                Visible = IsVisible(Named<Grid>(card, "LayoutRoot")),
                Mode = mode,
                Items = items
            };
        }

        private static string ObserveActionMode(InspectionActionCard card)
        {
            if (!IsVisible(Named<Grid>(card, "LayoutRoot")))
            {
                return "Hidden";
            }

            Visibility inspecting = Named<StackPanel>(
                card,
                "InspectingView").Visibility;
            Visibility result = Named<Border>(card, "ResultView").Visibility;
            return (inspecting, result) switch
            {
                (Visibility.Visible, Visibility.Collapsed) => "Inspecting",
                (Visibility.Collapsed, Visibility.Visible) => "Result",
                _ => "Unmapped"
            };
        }

        private ModelInspectionObservedFooter ObserveFooter() => new()
        {
            // This is the page's real shell-bound emission. The visible
            // onboarding footer is a shell-owned sibling, so a page-only
            // observer must not fabricate its own stage rows.
            Status = page.CurrentFooterStatus.ToString()
        };

        private ModelInspectionObservedFocus ObserveFocus(
            ModelInspectionObservedActions actions)
        {
            DependencyObject? focused = page.XamlRoot is null
                ? null
                : FocusManager.GetFocusedElement(page.XamlRoot) as
                    DependencyObject;
            string target = string.Empty;
            if (focused is not null)
            {
                if (IsDescendantOrSelf(focused, ModelCard))
                {
                    target = "model-card";
                }
                else
                {
                    foreach (ModelInspectionObservedAction action in actions.Items)
                    {
                        Button? button = ButtonForId(action.Id);
                        if (button is not null && IsDescendantOrSelf(focused, button))
                        {
                            target = action.Id;
                            break;
                        }
                    }
                }
            }

            return new ModelInspectionObservedFocus { Target = target };
        }

        private ModelInspectionObservedAutomation ObserveAutomation(
            ModelInspectionObservedModel model,
            ModelInspectionObservedContent content,
            ModelInspectionObservedActions actions)
        {
            List<ModelInspectionObservedAutomationControl> controls = [];
            AddObservedAutomationControl(controls, "model-card", ModelCard);
            if (string.Equals(content.Mode, "Progress", StringComparison.Ordinal))
            {
                AddProgressAutomation(controls, content);
                AddActionAutomation(controls, actions);
            }
            else
            {
                AddActionAutomation(controls, actions);
                AddDisclosureAutomation(controls);
                if (content.Visible)
                {
                    AddContentAutomation(controls, content);
                }
            }

            return new ModelInspectionObservedAutomation
            {
                Controls = controls
            };
        }

        private void AddProgressAutomation(
            ICollection<ModelInspectionObservedAutomationControl> controls,
            ModelInspectionObservedContent content)
        {
            ItemsRepeater repeater = Named<ItemsRepeater>(
                ContentCard,
                "ProgressItemsRepeater");
            AddObservedAutomationControl(controls, "progress-list", repeater);
            for (int index = 0; index < content.Rows.Count; index++)
            {
                FrameworkElement? element = repeater.TryGetElement(index) as
                    FrameworkElement;
                AddObservedAutomationControl(
                    controls,
                    content.Rows[index].Id,
                    element ?? throw new InvalidOperationException(
                        "A loaded automation row was not realized."));
            }
        }

        private void AddActionAutomation(
            ICollection<ModelInspectionObservedAutomationControl> controls,
            ModelInspectionObservedActions actions)
        {
            foreach (ModelInspectionObservedAction action in actions.Items)
            {
                Button button = ButtonForId(action.Id) ??
                    throw new InvalidOperationException(
                        "The observed action has no named button owner.");
                AddObservedAutomationControl(controls, action.Id, button);
            }
        }

        private void AddDisclosureAutomation(
            ICollection<ModelInspectionObservedAutomationControl> controls)
        {
            (InspectionDisclosure? disclosure, string? id) =
                VisibleDisclosure();
            if (disclosure is null)
            {
                return;
            }

            AddObservedAutomationControl(controls, id!, disclosure);
        }

        private void AddContentAutomation(
            ICollection<ModelInspectionObservedAutomationControl> controls,
            ModelInspectionObservedContent content)
        {
            ItemsControl list = Named<ItemsControl>(
                ContentCard,
                "FindingsItemsRepeater");
            AddObservedAutomationControl(controls, "content-list", list);
            for (int index = 0; index < content.Rows.Count; index++)
            {
                FrameworkElement? container = list.ContainerFromIndex(index) as
                    FrameworkElement;
                AddObservedAutomationControl(
                    controls,
                    content.Rows[index].Id,
                    AccessibleItemElement(container) ??
                        throw new InvalidOperationException(
                            "A loaded automation row was not realized."));
            }
        }

        private static ModelInspectionObservedAutomationControl
            ObservedAutomationControl(string id, FrameworkElement element)
        {
            AutomationPeer peer =
                FrameworkElementAutomationPeer.FromElement(element) ??
                FrameworkElementAutomationPeer.CreatePeerForElement(element) ??
                throw new InvalidOperationException(
                    $"The loaded automation owner '{id}' exposed no peer.");
            return new(
                id,
                peer.GetName(),
                peer.GetAutomationControlType().ToString(),
                AutomationProperties.GetLiveSetting(element).ToString(),
                NullIfEmpty(peer.GetHelpText()));
        }

        private static void AddObservedAutomationControl(
            ICollection<ModelInspectionObservedAutomationControl> controls,
            string id,
            FrameworkElement element)
        {
            if (AutomationProperties.GetAccessibilityView(element) ==
                AccessibilityView.Raw)
            {
                return;
            }

            controls.Add(ObservedAutomationControl(id, element));
        }

        private ModelInspectionObservedAnnouncements ObserveAnnouncements()
        {
            int progress = Math.Max(
                0,
                ContentCard.LiveRegionChangeNotificationCount -
                    progressAnnouncementBaseline);
            int outcome = Math.Max(
                0,
                OutcomeCard.LiveRegionChangeNotificationCount -
                    outcomeAnnouncementBaseline);
            List<string> items = [];
            items.AddRange(Enumerable.Repeat(
                AutomationProperties.GetName(ContentCard),
                progress));
            items.AddRange(Enumerable.Repeat(
                AutomationProperties.GetName(OutcomeCard),
                outcome));
            return new ModelInspectionObservedAnnouncements
            {
                Count = progress + outcome,
                Items = items
            };
        }

        private ModelInspectionObservedRowsAndScroll ObserveRowsAndScroll(
            ModelInspectionObservedModel model,
            ModelInspectionObservedContent content)
        {
            ScrollViewer pageScroll = Named<ScrollViewer>(
                page,
                "InspectionPageScrollViewer");
            if (pageScroll.VerticalScrollMode == ScrollMode.Disabled)
            {
                return new ModelInspectionObservedRowsAndScroll();
            }

            if (string.Equals(content.Mode, "Progress", StringComparison.Ordinal))
            {
                return new ModelInspectionObservedRowsAndScroll
                {
                    OrderedRowIds = content.Rows.Select(row => row.Id).ToArray(),
                    ScrollOwner = "progress-list"
                };
            }

            if (content.Visible)
            {
                ScrollViewer owner = content.DisclosureExpanded
                    ? Named<ScrollViewer>(
                        ContentCard,
                        "ExpandedReportScrollViewer")
                    : pageScroll;
                if (!IsVisible(owner) ||
                    owner.VerticalScrollMode == ScrollMode.Disabled)
                {
                    return new ModelInspectionObservedRowsAndScroll();
                }

                return new ModelInspectionObservedRowsAndScroll
                {
                    OrderedRowIds = content.Rows.Select(row => row.Id).ToArray(),
                    ScrollOwner = "content-list"
                };
            }

            if (model.DisclosureExpanded)
            {
                ScrollViewer owner = Named<ScrollViewer>(
                    ModelCard,
                    "InspectionChecksScrollViewer");
                if (!IsVisible(owner) ||
                    owner.VerticalScrollMode == ScrollMode.Disabled)
                {
                    return new ModelInspectionObservedRowsAndScroll();
                }

                return new ModelInspectionObservedRowsAndScroll
                {
                    OrderedRowIds = model.Checks.Select(row => row.Id).ToArray(),
                    ScrollOwner = "model-card"
                };
            }

            return new ModelInspectionObservedRowsAndScroll();
        }

        private ModelInspectionObservedRetention ObserveRetention(
            ModelInspectionObservedModel model,
            ModelInspectionObservedContent content,
            ModelInspectionObservedActions actions,
            ModelInspectionObservedRowsAndScroll rowsAndScroll)
        {
            List<string> ids = ["model-card"];
            if (model.Metadata.Count != 0)
            {
                ids.AddRange(model.Metadata.Select(field => field.Id));
                ids.AddRange(model.Checks.Select(row => row.Id));
            }

            if (string.Equals(content.Mode, "Progress", StringComparison.Ordinal))
            {
                ids.Add("progress-list");
                ids.AddRange(content.Rows.Select(row => row.Id));
            }
            else if (content.Visible)
            {
                ids.Add("content-list");
                ids.AddRange(content.Rows.Select(row => row.Id));
            }

            ids.AddRange(actions.Items.Select(action => action.Id));
            (InspectionDisclosure? disclosure, string? disclosureId) =
                VisibleDisclosure();
            if (disclosure is not null)
            {
                ids.Add(disclosureId!);
            }

            bool sampledAfter = model.DisclosureExpanded ||
                content.DisclosureExpanded;
            bool itemsRetained = false;
            bool containersRetained = false;
            bool scrollRetained = false;
            if (sampledBeforeDisclosure && sampledAfter)
            {
                (object? items, object? scroll, IReadOnlyList<object> containers) =
                    CurrentDisclosureOwners(model);
                itemsRetained = ReferenceEquals(disclosureItemsSource, items);
                scrollRetained = ReferenceEquals(disclosureScrollOwner, scroll);
                containersRetained = SameReferences(
                    disclosureContainers,
                    containers);
            }

            return new ModelInspectionObservedRetention
            {
                Ids = ids,
                ItemsSourceRetained = itemsRetained,
                RowContainersRetained = containersRetained,
                ScrollOwnerRetained = scrollRetained,
                SampledBeforeDisclosure = sampledBeforeDisclosure,
                SampledAfterDisclosure = sampledAfter
            };
        }

        private (InspectionDisclosure? Disclosure, string? Id)
            VisibleDisclosure()
        {
            InspectionDisclosure model = Named<InspectionDisclosure>(
                ModelCard,
                "InspectionDetailsDisclosure");
            InspectionDisclosure content = Named<InspectionDisclosure>(
                ContentCard,
                "FindingsDisclosure");
            bool modelVisible = IsVisible(model);
            bool contentVisible = IsVisible(content);
            if (modelVisible && contentVisible)
            {
                throw new InvalidOperationException(
                    "More than one disclosure is visible in the loaded tree.");
            }

            return modelVisible
                ? (model, "inspection-details-disclosure")
                : contentVisible
                    ? (content, "findings-disclosure")
                    : (null, null);
        }

        private (object? Items, object? Scroll, IReadOnlyList<object> Containers)
            CurrentDisclosureOwners(ModelInspectionObservedModel model)
        {
            if (model.DisclosureExpanded)
            {
                ItemsControl items = Named<ItemsControl>(
                    ModelCard,
                    "InspectionChecksItemsControl");
                return (
                    items.ItemsSource,
                    Named<ScrollViewer>(ModelCard, "InspectionChecksScrollViewer"),
                    Containers(items));
            }

            ItemsControl contentItems = Named<ItemsControl>(
                ContentCard,
                "ExpandedReportItemsControl");
            return (
                contentItems.ItemsSource,
                Named<ScrollViewer>(ContentCard, "ExpandedReportScrollViewer"),
                Containers(contentItems));
        }

        private static string InferFigmaState(
            ModelInspectionObservedModel model,
            ModelInspectionObservedContent content,
            ModelInspectionObservedOutcome outcome)
        {
            if (string.Equals(content.Mode, "Progress", StringComparison.Ordinal))
            {
                return "InspectionProgress";
            }

            return outcome.Kind switch
            {
                "Ready" => model.DisclosureExpanded
                    ? "ReadyExpanded"
                    : "ReadyCollapsed",
                "ReadyWithWarnings" => content.DisclosureExpanded
                    ? "ReadyWithWarningsExpanded"
                    : "ReadyWithWarningsCollapsed",
                "ConversionRequired" => content.DisclosureExpanded
                    ? "ConversionRequiredExpanded"
                    : "ConversionRequiredCollapsed",
                "Invalid" => content.DisclosureExpanded
                    ? "InvalidExpanded"
                    : "InvalidCollapsed",
                "IncompletePackage" => "IncompletePackage",
                "Unsupported" => "Unsupported",
                "Cancelled" => "Cancelled",
                "OperationalFailure" => "OperationalFailure",
                _ => string.Empty
            };
        }

        private static FrameworkElement? RowElement(
            InspectionContentCard card,
            string mode,
            int index)
        {
            if (string.Equals(mode, "Progress", StringComparison.Ordinal))
            {
                ItemsRepeater repeater = Named<ItemsRepeater>(
                    card,
                    "ProgressItemsRepeater");
                return (repeater.TryGetElement(index) ??
                    repeater.GetOrCreateElement(index)) as FrameworkElement;
            }

            FrameworkElement? container = Named<ItemsControl>(
                card,
                "FindingsItemsRepeater").ContainerFromIndex(index) as
                    FrameworkElement;
            return AccessibleItemElement(container);
        }

        private static FrameworkElement? AccessibleItemElement(
            FrameworkElement? container)
        {
            if (container is null ||
                !string.IsNullOrWhiteSpace(
                    AutomationProperties.GetName(container)))
            {
                return container;
            }

            return Descendants<FrameworkElement>(container).FirstOrDefault(
                element => !string.IsNullOrWhiteSpace(
                    AutomationProperties.GetName(element)));
        }

        private static string ReadContentHeading(
            InspectionContentCard card,
            string mode)
        {
            FrameworkElement owner = string.Equals(
                mode,
                "Progress",
                StringComparison.Ordinal)
                ? Named<Grid>(card, "ProgressView")
                : Named<TextBlock>(card, "FindingsSectionTitle");
            return owner is TextBlock text
                ? text.Text
                : Descendants<TextBlock>(owner).First().Text;
        }

        private static string ContentRowId(
            string mode,
            string title,
            int index) => mode switch
            {
                "Progress" => title switch
                {
                    "Check model package" => "progress-1",
                    "Read model configuration" => "progress-2",
                    "Validate tokenizer and chat setup" => "progress-3",
                    "Validate model structure" => "progress-4",
                    "Confirm core runtime compatibility" => "progress-5",
                    _ => $"unmapped:{title}"
                },
                "Warnings" when string.Equals(
                    title,
                    "Chat template not reported",
                    StringComparison.Ordinal) => "MI-WARN-CHAT-TEMPLATE-MISSING",
                "ConversionRequired" =>
                    "conversion-required-row",
                "IncompletePackage" =>
                    "incomplete-package-row",
                "Unsupported" => "unsupported-model-row",
                "Invalid" => "invalid-report-row",
                "Cancelled" => "cancelled-row",
                "OperationalFailure" =>
                    "operational-failure-row",
                _ => $"content-row-{index + 1}"
            };

        private static string CheckId(string title) => title switch
        {
            "Model package" => "check-package",
            "Model configuration" => "check-configuration",
            "Tokenizer and chat setup" => "check-tokenizer",
            "Model structure" => "check-structure",
            "Core runtime support" => "check-runtime",
            _ => $"unmapped:{title}"
        };

        private string CheckStatus(
            InspectionModelCard card,
            TextBlock status,
            Border icon)
        {
            Symbol symbol = Descendants<SymbolIcon>(icon).Single().Symbol;
            return status.Text switch
            {
                "Passed" when symbol == Symbol.Accept &&
                    MatchesCanonicalBrush(
                        icon.Background,
                        card.ActualTheme,
                        "InspectionSuccessSurfaceBrush") => "Passed",
                "Warning" when symbol == Symbol.Important &&
                    MatchesCanonicalBrush(
                        icon.Background,
                        card.ActualTheme,
                        "InspectionWarningSurfaceBrush") => "Warning",
                "Failed" when symbol == Symbol.Cancel &&
                    MatchesCanonicalBrush(
                        icon.Background,
                        card.ActualTheme,
                        "InspectionErrorSurfaceBrush") => "Error",
                "Information" when symbol == Symbol.Help &&
                    MatchesCanonicalBrush(
                        icon.Background,
                        card.ActualTheme,
                        "InspectionBlueSurfaceBrush") => "Information",
                _ => $"Unmapped:{status.Text}"
            };
        }

        private static string ActionId(string automationName) =>
            automationName switch
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
                _ => $"unmapped:{automationName}"
            };

        private Button? ButtonForId(string id)
        {
            foreach (string name in new[]
                     {
                         "CancelActionButton",
                         "SecondaryActionOneButton",
                         "SecondaryActionTwoButton",
                         "PrimaryActionButton"
                     })
            {
                Button button = Named<Button>(ActionCard, name);
                if (string.Equals(
                    ActionId(AutomationProperties.GetName(button)),
                    id,
                    StringComparison.Ordinal))
                {
                    return button;
                }
            }

            return null;
        }

        private static (string Label, string Value) ReadField(
            FrameworkElement root,
            string name)
        {
            FrameworkElement field = Named<FrameworkElement>(root, name);
            TextBlock[] text = Descendants<TextBlock>(field).ToArray();
            if (text.Length < 2)
            {
                throw new InvalidOperationException(
                    $"The named model field '{name}' has no label/value pair.");
            }

            AutomationPeer peer =
                FrameworkElementAutomationPeer.FromElement(text[0]) ??
                FrameworkElementAutomationPeer.CreatePeerForElement(text[0]) ??
                throw new InvalidOperationException(
                    $"The named model field '{name}' label has no automation peer.");
            return (peer.GetName(), text[1].Text);
        }

        private static string ReadButtonText(Button button) =>
            button.Content is TextBlock text
                ? text.Text
                : Descendants<TextBlock>(button).FirstOrDefault()?.Text ??
                  string.Empty;

        private static IReadOnlyList<object> Containers(ItemsControl items)
        {
            List<object> result = [];
            for (int index = 0; index < items.Items.Count; index++)
            {
                object? container = items.ContainerFromIndex(index);
                if (container is not null)
                {
                    result.Add(container);
                }
            }

            return result;
        }

        private static bool SameReferences(
            IReadOnlyList<object> left,
            IReadOnlyList<object> right) =>
            left.Count != 0 &&
            left.Count == right.Count &&
            left.Select((item, index) => ReferenceEquals(item, right[index]))
                .All(value => value);

        private static bool IsVisible(FrameworkElement element)
        {
            DependencyObject? cursor = element;
            while (cursor is not null)
            {
                if (cursor is FrameworkElement current &&
                    (current.Visibility != Visibility.Visible ||
                     current.Opacity <= 0d))
                {
                    return false;
                }

                cursor = VisualTreeHelper.GetParent(cursor);
            }

            return true;
        }

        private static string? NullIfEmpty(string? value) =>
            string.IsNullOrEmpty(value) ? null : value;

        private static bool IsDescendantOrSelf(
            DependencyObject element,
            DependencyObject owner)
        {
            DependencyObject? cursor = element;
            while (cursor is not null)
            {
                if (ReferenceEquals(cursor, owner))
                {
                    return true;
                }

                cursor = VisualTreeHelper.GetParent(cursor);
            }

            return false;
        }

        private static T Named<T>(FrameworkElement root, string name)
            where T : DependencyObject => root.FindName(name) is T value
                ? value
                : throw new InvalidOperationException(
                    $"The fixture screen is missing '{name}'.");

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

        private static IEnumerable<T> SelfAndDescendants<T>(
            DependencyObject root)
            where T : DependencyObject
        {
            if (root is T match)
            {
                yield return match;
            }

            foreach (T descendant in Descendants<T>(root))
            {
                yield return descendant;
            }
        }

        private static async Task WaitForLoadedAsync(
            FrameworkElement element,
            CancellationToken cancellationToken)
        {
            if (element.IsLoaded)
            {
                return;
            }

            var loaded = new TaskCompletionSource<bool>(
                TaskCreationOptions.RunContinuationsAsynchronously);
            RoutedEventHandler? handler = null;
            handler = (_, _) => loaded.TrySetResult(true);
            element.Loaded += handler;
            try
            {
                await loaded.Task.WaitAsync(cancellationToken);
            }
            finally
            {
                element.Loaded -= handler;
            }
        }

        private static async Task DrainDispatcherAsync(
            DispatcherQueue dispatcher,
            CancellationToken cancellationToken)
        {
            var drained = new TaskCompletionSource<bool>(
                TaskCreationOptions.RunContinuationsAsynchronously);
            if (!dispatcher.TryEnqueue(() => drained.TrySetResult(true)))
            {
                throw new InvalidOperationException(
                    "The fixture observation dispatcher barrier could not be queued.");
            }

            await drained.Task.WaitAsync(cancellationToken);
        }

        private static async Task WaitForRenderingAsync(
            CancellationToken cancellationToken)
        {
            var rendered = new TaskCompletionSource<bool>(
                TaskCreationOptions.RunContinuationsAsynchronously);
            EventHandler<object>? handler = null;
            handler = (_, _) => rendered.TrySetResult(true);
            CompositionTarget.Rendering += handler;
            try
            {
                await rendered.Task.WaitAsync(cancellationToken);
            }
            finally
            {
                CompositionTarget.Rendering -= handler;
            }
        }

        private void ThrowIfDisposed()
        {
            if (Volatile.Read(ref disposed) != 0)
            {
                throw new ObjectDisposedException(
                    nameof(IModelInspectionFixtureObservationSession));
            }
        }
    }
}
#endif
