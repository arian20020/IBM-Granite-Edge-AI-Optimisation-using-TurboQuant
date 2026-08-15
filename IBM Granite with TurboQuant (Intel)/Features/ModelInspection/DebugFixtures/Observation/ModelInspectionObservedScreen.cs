#if MODEL_INSPECTION_FIXTURE_GALLERY
using GraniteEdgeAI.Features.ModelInspection.Models;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace GraniteEdgeAI.Features.ModelInspection.DebugFixtures.Observation;

internal interface IModelInspectionFixtureScreenObserver
{
    IModelInspectionFixtureObservationSession Begin(ModelInspectionPage page);
}

internal interface IModelInspectionFixtureObservationSession : IDisposable
{
    Task<ModelInspectionObservedScreen> CaptureAsync(
        CancellationToken cancellationToken);
}

internal sealed record ModelInspectionObservedScreen
{
    internal static ModelInspectionObservedScreen Empty { get; } = new();

    internal ModelInspectionObservedFigma Figma { get; init; } = new();

    internal ModelInspectionObservedOutcome Outcome { get; init; } = new();

    internal ModelInspectionObservedModel Model { get; init; } = new();

    internal ModelInspectionObservedContent Content { get; init; } = new();

    internal ModelInspectionObservedActions Actions { get; init; } = new();

    internal ModelInspectionObservedFooter Footer { get; init; } = new();

    internal ModelInspectionObservedFocus Focus { get; init; } = new();

    internal ModelInspectionObservedAutomation Automation { get; init; } = new();

    internal ModelInspectionObservedAnnouncements Announcements { get; init; } =
        new();

    internal ModelInspectionObservedRowsAndScroll RowsAndScroll { get; init; } =
        new();

    internal ModelInspectionObservedRetention Retention { get; init; } = new();

    internal ModelInspectionObservedRenderBarrier RenderBarrier { get; init; } =
        new();

    internal IReadOnlyDictionary<string, string> VisibleTextMutations
        { get; init; } = new Dictionary<string, string>(StringComparer.Ordinal);

    internal ModelInspectionObservedScreen WithVisibleTextMutation(
        string path,
        string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentNullException.ThrowIfNull(value);
        return this with
        {
            VisibleTextMutations = new Dictionary<string, string>(
                StringComparer.Ordinal)
            {
                [path] = value
            }
        };
    }
}

internal sealed record ModelInspectionObservedFigma
{
    internal string State { get; init; } = string.Empty;
    internal string GeometryProfile { get; init; } = string.Empty;
    internal bool PresentationStateMatches { get; init; }
    internal double ContentWidth { get; init; }
    internal double ContentHeight { get; init; }
}

internal sealed record ModelInspectionObservedText(string Text = "");

internal sealed record ModelInspectionObservedOutcome
{
    internal bool Visible { get; init; }
    internal string Kind { get; init; } = string.Empty;
    internal string Tone { get; init; } = string.Empty;
    internal ModelInspectionObservedText? Badge { get; init; }
    internal ModelInspectionObservedText? Title { get; init; }
    internal ModelInspectionObservedText? SupportingText { get; init; }
}

internal sealed record ModelInspectionObservedModel
{
    internal bool Visible { get; init; }
    internal string Mode { get; init; } = string.Empty;
    internal string? Badge { get; init; }
    internal string DisplayName { get; init; } = string.Empty;
    internal string LogicalDisplayFileName { get; init; } = string.Empty;
    internal IReadOnlyList<ModelInspectionObservedMetadataField> Metadata
        { get; init; } = Array.Empty<ModelInspectionObservedMetadataField>();
    internal IReadOnlyList<ModelInspectionObservedCheckRow> Checks
        { get; init; } = Array.Empty<ModelInspectionObservedCheckRow>();
    internal bool DisclosureExpanded { get; init; }
}

internal sealed record ModelInspectionObservedMetadataField(
    string Id,
    string Label,
    string Value);

internal sealed record ModelInspectionObservedCheckRow(
    string Id,
    string Text,
    string Status,
    InspectionStatusGlyphKind? GlyphKind = null);

internal sealed record ModelInspectionObservedContent
{
    internal bool Visible { get; init; }
    internal string Mode { get; init; } = string.Empty;
    internal string? Heading { get; init; }
    internal string? StartupStatus { get; init; }
    internal bool StartupVisible { get; init; }
    internal bool StartupActive { get; init; }
    internal IReadOnlyList<ModelInspectionObservedContentRow> Rows
        { get; init; } = Array.Empty<ModelInspectionObservedContentRow>();
    internal bool DisclosureExpanded { get; init; }
}

internal sealed record ModelInspectionObservedContentRow(
    string Id,
    string PrimaryText,
    string? SecondaryText,
    string Status,
    double? StageFraction = null,
    InspectionStatusGlyphKind? GlyphKind = null);

internal sealed record ModelInspectionObservedActions
{
    internal bool Visible { get; init; }
    internal string Mode { get; init; } = string.Empty;
    internal IReadOnlyList<ModelInspectionObservedAction> Items
        { get; init; } = Array.Empty<ModelInspectionObservedAction>();
}

internal sealed record ModelInspectionObservedAction(
    string Id,
    string Label,
    bool Visible,
    bool Enabled,
    string? HelpText);

internal sealed record ModelInspectionObservedFooter
{
    internal string Status { get; init; } = string.Empty;
}

internal sealed record ModelInspectionObservedFocus
{
    internal string Target { get; init; } = string.Empty;
}

internal sealed record ModelInspectionObservedAutomation
{
    internal IReadOnlyList<ModelInspectionObservedAutomationControl> Controls
        { get; init; } = Array.Empty<ModelInspectionObservedAutomationControl>();
}

internal sealed record ModelInspectionObservedAutomationControl(
    string Id,
    string AccessibleName,
    string ControlType,
    string LiveSetting,
    string? HelpText);

internal sealed record ModelInspectionObservedAnnouncements
{
    internal int Count { get; init; }
    internal IReadOnlyList<string> Items { get; init; } = Array.Empty<string>();
}

internal sealed record ModelInspectionObservedRowsAndScroll
{
    internal IReadOnlyList<string> OrderedRowIds { get; init; } =
        Array.Empty<string>();
    internal string? ScrollOwner { get; init; }
}

internal sealed record ModelInspectionObservedRetention
{
    internal IReadOnlyList<string> Ids { get; init; } = Array.Empty<string>();
    internal bool ItemsSourceRetained { get; init; }
    internal bool RowContainersRetained { get; init; }
    internal bool ScrollOwnerRetained { get; init; }
    internal bool SampledBeforeDisclosure { get; init; }
    internal bool SampledAfterDisclosure { get; init; }
}

internal sealed record ModelInspectionObservedRenderBarrier
{
    internal bool DispatcherDrained { get; init; }
    internal bool LayoutUpdated { get; init; }
    internal bool CompositionCommitted { get; init; }
}
#endif
