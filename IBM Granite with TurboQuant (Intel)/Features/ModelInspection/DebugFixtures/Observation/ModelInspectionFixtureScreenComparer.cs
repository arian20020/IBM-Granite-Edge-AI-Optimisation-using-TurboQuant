#if MODEL_INSPECTION_FIXTURE_GALLERY
using GraniteEdgeAI.Features.ModelInspection.Models;
using GraniteEdgeAI.ModelInspection.Fixtures;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace GraniteEdgeAI.Features.ModelInspection.DebugFixtures.Observation;

internal interface IModelInspectionFixtureScreenComparer
{
    IReadOnlyList<ModelInspectionFixtureScreenDifference> Compare(
        string fileName,
        ModelInspectionExpectedScreen expected,
        ModelInspectionObservedScreen observed,
        IReadOnlyDictionary<string, string> copyRegistry);
}

internal static class ModelInspectionFixtureScreenComparerExtensions
{
    internal static IReadOnlyList<ModelInspectionFixtureScreenDifference> Compare(
        this IModelInspectionFixtureScreenComparer comparer,
        ValidatedModelInspectionFixture fixture,
        ModelInspectionObservedScreen observed,
        IReadOnlyDictionary<string, string> copyRegistry)
    {
        ArgumentNullException.ThrowIfNull(comparer);
        ArgumentNullException.ThrowIfNull(fixture);
        return comparer.Compare(
            fixture.FileName,
            fixture.Expected,
            observed,
            copyRegistry);
    }
}

internal sealed record ModelInspectionFixtureScreenDifference(
    string RuleCode,
    string FileName,
    string ExpectedPath,
    string ExpectedToken,
    string ObservedToken)
{
    internal string Diagnostic =>
        $"{FileName}|{RuleCode}|{ExpectedPath}|{ExpectedToken}|{ObservedToken}";
}

internal sealed class ModelInspectionFixtureScreenComparer :
    IModelInspectionFixtureScreenComparer
{
    public IReadOnlyList<ModelInspectionFixtureScreenDifference> Compare(
        string fileName,
        ModelInspectionExpectedScreen expected,
        ModelInspectionObservedScreen observed,
        IReadOnlyDictionary<string, string> copyRegistry)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);
        ArgumentNullException.ThrowIfNull(expected);
        ArgumentNullException.ThrowIfNull(observed);
        ArgumentNullException.ThrowIfNull(copyRegistry);
        var differences = new DifferenceCollector(fileName, copyRegistry);

        differences.Equal(
            "$.Figma.State",
            expected.Figma.State.ToString(),
            observed.Figma.State);
        differences.Equal(
            "$.Figma.GeometryProfile",
            expected.Figma.GeometryProfile.ToString(),
            observed.Figma.GeometryProfile);
        differences.Equal(
            "$.Figma.PresentationStateMatches",
            true,
            observed.Figma.PresentationStateMatches);

        CompareOutcome(differences, expected.Outcome, observed.Outcome);
        CompareModel(differences, expected.Model, observed.Model);
        CompareContent(differences, expected.Content, observed.Content);
        CompareActions(differences, expected.Actions, observed.Actions);
        CompareFooter(differences, expected.Footer, observed.Footer);
        differences.Equal(
            "$.Focus.Target",
            expected.Focus.Target,
            observed.Focus.Target);
        CompareAutomation(
            differences,
            expected.Automation,
            observed.Automation);
        CompareAnnouncements(
            differences,
            expected.Announcements,
            observed.Announcements);
        differences.StringList(
            "$.RowsAndScroll.OrderedRowIds",
            expected.RowsAndScroll.OrderedRowIds,
            observed.RowsAndScroll.OrderedRowIds);
        differences.Equal(
            "$.RowsAndScroll.ScrollOwner",
            expected.RowsAndScroll.ScrollOwner,
            observed.RowsAndScroll.ScrollOwner);
        differences.StringList(
            "$.RetainedIdentities.Ids",
            expected.RetainedIdentities.Ids,
            observed.Retention.Ids);

        foreach ((string path, string value) in
                 observed.VisibleTextMutations.OrderBy(
                     item => item.Key,
                     StringComparer.Ordinal))
        {
            differences.Equal(
                $"$.Observed.{path}",
                "canonical-visible-text",
                value);
        }

        return differences.Build();
    }

    private static void CompareOutcome(
        DifferenceCollector differences,
        ModelInspectionExpectedOutcomeRegion expected,
        ModelInspectionObservedOutcome observed)
    {
        differences.Equal("$.Outcome.Visible", expected.Visible, observed.Visible);
        differences.Equal(
            "$.Outcome.Kind",
            expected.Kind.ToString(),
            observed.Kind);
        differences.Equal(
            "$.Outcome.Tone",
            expected.Tone.ToString(),
            observed.Tone);
        differences.Copy("$.Outcome.Badge", expected.Badge, observed.Badge?.Text);
        differences.Copy("$.Outcome.Title", expected.Title, observed.Title?.Text);
        differences.Copy(
            "$.Outcome.SupportingText",
            expected.SupportingText,
            observed.SupportingText?.Text);
    }

    private static void CompareModel(
        DifferenceCollector differences,
        ModelInspectionExpectedModelRegion expected,
        ModelInspectionObservedModel observed)
    {
        differences.Equal("$.Model.Visible", expected.Visible, observed.Visible);
        differences.Equal(
            "$.Model.Mode",
            expected.Mode.ToString(),
            observed.Mode);
        differences.Equal(
            "$.Model.Badge",
            expected.Badge?.ToString(),
            observed.Badge);
        differences.Copy(
            "$.Model.DisplayName",
            expected.DisplayName,
            observed.DisplayName);
        differences.Copy(
            "$.Model.DisplayFileName",
            expected.DisplayFileName,
            observed.LogicalDisplayFileName);

        IReadOnlyList<ModelInspectionExpectedMetadataField>? metadata =
            expected.Metadata;
        differences.Count(
            "$.Model.Metadata",
            metadata,
            observed.Metadata);
        if (metadata is not null)
        {
            for (int index = 0;
                 index < Math.Min(metadata.Count, observed.Metadata.Count);
                 index++)
            {
                string path = $"$.Model.Metadata[{index}]";
                differences.Equal(
                    path + ".Id",
                    metadata[index].Id,
                    observed.Metadata[index].Id);
                differences.Copy(
                    path + ".Label",
                    metadata[index].Label,
                    observed.Metadata[index].Label);
                differences.Copy(
                    path + ".Value",
                    metadata[index].Value,
                    observed.Metadata[index].Value);
            }
        }

        IReadOnlyList<ModelInspectionExpectedCheckRow>? checks = expected.Checks;
        differences.Count("$.Model.Checks", checks, observed.Checks);
        if (checks is not null)
        {
            for (int index = 0;
                 index < Math.Min(checks.Count, observed.Checks.Count);
                 index++)
            {
                string path = $"$.Model.Checks[{index}]";
                differences.Equal(
                    path + ".Id",
                    checks[index].Id,
                    observed.Checks[index].Id);
                differences.Copy(
                    path + ".Text",
                    checks[index].Text,
                    observed.Checks[index].Text);
                differences.Equal(
                    path + ".Status",
                    checks[index].Status.ToString(),
                    observed.Checks[index].Status);
                differences.Equal(
                    path + ".GlyphKind",
                    ExpectedGlyphKind(checks[index].Status),
                    observed.Checks[index].GlyphKind);
            }
        }

        differences.Equal(
            "$.Model.DisclosureExpanded",
            expected.DisclosureExpanded,
            observed.DisclosureExpanded);
    }

    private static void CompareContent(
        DifferenceCollector differences,
        ModelInspectionExpectedContentRegion expected,
        ModelInspectionObservedContent observed)
    {
        differences.Equal("$.Content.Visible", expected.Visible, observed.Visible);
        differences.Equal(
            "$.Content.Mode",
            expected.Mode.ToString(),
            observed.Mode);
        differences.Copy("$.Content.Heading", expected.Heading, observed.Heading);
        differences.Copy(
            "$.Content.StartupStatus",
            expected.StartupStatus,
            observed.StartupStatus);
        differences.Equal(
            "$.Content.StartupVisible",
            expected.StartupVisible ?? false,
            observed.StartupVisible);
        differences.Equal(
            "$.Content.StartupActive",
            expected.StartupActive ?? false,
            observed.StartupActive);
        IReadOnlyList<ModelInspectionExpectedContentRow>? rows = expected.Rows;
        differences.Count("$.Content.Rows", rows, observed.Rows);
        if (rows is not null)
        {
            for (int index = 0;
                 index < Math.Min(rows.Count, observed.Rows.Count);
                 index++)
            {
                string path = $"$.Content.Rows[{index}]";
                differences.Equal(
                    path + ".Id",
                    rows[index].Id,
                    observed.Rows[index].Id);
                differences.Copy(
                    path + ".PrimaryText",
                    rows[index].PrimaryText,
                    observed.Rows[index].PrimaryText);
                differences.Copy(
                    path + ".SecondaryText",
                    rows[index].SecondaryText,
                    observed.Rows[index].SecondaryText);
                differences.Equal(
                    path + ".Status",
                    rows[index].Status.ToString(),
                    observed.Rows[index].Status);
                differences.Equal(
                    path + ".GlyphKind",
                    ExpectedGlyphKind(rows[index].Status),
                    observed.Rows[index].GlyphKind);
            }
        }

        differences.Equal(
            "$.Content.DisclosureExpanded",
            expected.DisclosureExpanded,
            observed.DisclosureExpanded);
    }

    private static void CompareActions(
        DifferenceCollector differences,
        ModelInspectionExpectedActionRegion expected,
        ModelInspectionObservedActions observed)
    {
        differences.Equal("$.Actions.Visible", expected.Visible, observed.Visible);
        differences.Equal(
            "$.Actions.Mode",
            expected.Mode.ToString(),
            observed.Mode);
        IReadOnlyList<ModelInspectionExpectedAction>? items = expected.Items;
        differences.Count("$.Actions.Items", items, observed.Items);
        if (items is null)
        {
            return;
        }

        for (int index = 0;
             index < Math.Min(items.Count, observed.Items.Count);
             index++)
        {
            string path = $"$.Actions.Items[{index}]";
            differences.Equal(
                path + ".Id",
                items[index].Id,
                observed.Items[index].Id);
            differences.Copy(
                path + ".Label",
                items[index].Label,
                observed.Items[index].Label);
            differences.Equal(
                path + ".Visible",
                items[index].Visible,
                observed.Items[index].Visible);
            differences.Equal(
                path + ".Enabled",
                items[index].Enabled,
                observed.Items[index].Enabled);
            differences.Copy(
                path + ".HelpText",
                items[index].HelpText,
                observed.Items[index].HelpText);
        }
    }

    private static InspectionStatusGlyphKind? ExpectedGlyphKind(
        ModelInspectionExpectedRowStatus status) => status switch
        {
            ModelInspectionExpectedRowStatus.Neutral => null,
            ModelInspectionExpectedRowStatus.Waiting =>
                InspectionStatusGlyphKind.Waiting,
            ModelInspectionExpectedRowStatus.Active =>
                InspectionStatusGlyphKind.Active,
            ModelInspectionExpectedRowStatus.Passed =>
                InspectionStatusGlyphKind.Success,
            ModelInspectionExpectedRowStatus.Warning =>
                InspectionStatusGlyphKind.Warning,
            ModelInspectionExpectedRowStatus.Error =>
                InspectionStatusGlyphKind.Error,
            ModelInspectionExpectedRowStatus.Information =>
                InspectionStatusGlyphKind.Information,
            _ => throw new ArgumentOutOfRangeException(nameof(status), status, null)
        };

    private static void CompareFooter(
        DifferenceCollector differences,
        ModelInspectionExpectedFooter expected,
        ModelInspectionObservedFooter observed)
    {
        differences.Equal(
            "$.Footer.Status",
            expected.Status.ToString(),
            observed.Status);
    }

    private static void CompareAutomation(
        DifferenceCollector differences,
        ModelInspectionExpectedAutomation expected,
        ModelInspectionObservedAutomation observed)
    {
        IReadOnlyList<ModelInspectionExpectedAutomationControl>? controls =
            expected.Controls;
        differences.Count(
            "$.Automation.Controls",
            controls,
            observed.Controls);
        if (controls is null)
        {
            return;
        }

        for (int index = 0;
             index < Math.Min(controls.Count, observed.Controls.Count);
             index++)
        {
            string path = $"$.Automation.Controls[{index}]";
            differences.Equal(
                path + ".Id",
                controls[index].Id,
                observed.Controls[index].Id);
            differences.Copy(
                path + ".AccessibleName",
                controls[index].AccessibleName,
                observed.Controls[index].AccessibleName);
            differences.Equal(
                path + ".ControlType",
                controls[index].ControlType.ToString(),
                observed.Controls[index].ControlType);
            differences.Equal(
                path + ".LiveSetting",
                controls[index].LiveSetting.ToString(),
                observed.Controls[index].LiveSetting);
            differences.Copy(
                path + ".HelpText",
                controls[index].HelpText,
                observed.Controls[index].HelpText);
        }
    }

    private static void CompareAnnouncements(
        DifferenceCollector differences,
        ModelInspectionExpectedAnnouncements expected,
        ModelInspectionObservedAnnouncements observed)
    {
        differences.Equal(
            "$.Announcements.Count",
            expected.Count,
            observed.Count);
        IReadOnlyList<ModelInspectionExpectedCopy>? items = expected.Items;
        differences.Count("$.Announcements.Items", items, observed.Items);
        if (items is null)
        {
            return;
        }

        for (int index = 0;
             index < Math.Min(items.Count, observed.Items.Count);
             index++)
        {
            differences.Copy(
                $"$.Announcements.Items[{index}]",
                items[index],
                observed.Items[index]);
        }
    }

    private sealed class DifferenceCollector
    {
        private const string RuleCode = "MI-SCREEN-MISMATCH";
        private readonly string fileName;
        private readonly IReadOnlyDictionary<string, string> copyRegistry;
        private readonly List<ModelInspectionFixtureScreenDifference> items = [];

        internal DifferenceCollector(
            string fileName,
            IReadOnlyDictionary<string, string> copyRegistry)
        {
            this.fileName = SafeFileName(fileName);
            this.copyRegistry = copyRegistry;
        }

        internal void Copy(
            string path,
            ModelInspectionExpectedCopy? expected,
            string? observed)
        {
            if (expected is null)
            {
                Equal(path, null, observed);
                return;
            }

            if (!copyRegistry.TryGetValue(expected.CopyKey, out string? text) ||
                !string.Equals(text, expected.DefaultText, StringComparison.Ordinal))
            {
                Add(
                    path + ".CopyKey",
                    $"registered:{expected.CopyKey}",
                    text is null ? "missing" : $"registered:{text}");
            }

            Equal(path + ".DefaultText", expected.DefaultText, observed);
        }

        internal void Equal(string path, object? expected, object? observed)
        {
            if (Equals(expected, observed))
            {
                return;
            }

            Add(path, expected, observed);
        }

        internal void Count<TExpected, TObserved>(
            string path,
            IReadOnlyList<TExpected>? expected,
            IReadOnlyList<TObserved> observed) => Equal(
                path + ".Count",
                expected?.Count ?? -1,
                observed.Count);

        internal void StringList(
            string path,
            IReadOnlyList<string>? expected,
            IReadOnlyList<string> observed)
        {
            Count(path, expected, observed);
            if (expected is null)
            {
                return;
            }

            for (int index = 0;
                 index < Math.Min(expected.Count, observed.Count);
                 index++)
            {
                Equal($"{path}[{index}]", expected[index], observed[index]);
            }
        }

        internal IReadOnlyList<ModelInspectionFixtureScreenDifference> Build() =>
            items.OrderBy(item => item.ExpectedPath, StringComparer.Ordinal)
                .ThenBy(item => item.ExpectedToken, StringComparer.Ordinal)
                .ThenBy(item => item.ObservedToken, StringComparer.Ordinal)
                .ToArray();

        private void Add(string path, object? expected, object? observed) =>
            items.Add(new ModelInspectionFixtureScreenDifference(
                RuleCode,
                fileName,
                path,
                SafeToken(expected),
                SafeToken(observed)));

        private static string SafeFileName(string value)
        {
            if (value.Length is < 1 or > 128 || value.Any(character =>
                    !(char.IsAsciiLetterOrDigit(character) ||
                      character is '.' or '-' or '_')))
            {
                return "<invalid-fixture>";
            }

            return value;
        }

        private static string SafeToken(object? value)
        {
            if (value is null)
            {
                return "<null>";
            }

            string text = value switch
            {
                bool boolean => boolean ? "true" : "false",
                double number => number.ToString("R", CultureInfo.InvariantCulture),
                float number => number.ToString("R", CultureInfo.InvariantCulture),
                IFormattable formattable => formattable.ToString(
                    null,
                    CultureInfo.InvariantCulture) ?? string.Empty,
                _ => value.ToString() ?? string.Empty
            };
            bool unsafeText = text.Length > 96 || text.Any(character =>
                char.IsControl(character) ||
                character is '\\' or '/' or '{' or '}' or '|' ||
                character == ':' ||
                character is '\u202A' or '\u202B' or '\u202C' or '\u202D' or
                    '\u202E' or '\u2066' or '\u2067' or '\u2068' or '\u2069');
            if (!unsafeText)
            {
                return text;
            }

            byte[] bytes = Encoding.UTF8.GetBytes(text);
            return $"len={text.Length};sha256={Convert.ToHexString(
                SHA256.HashData(bytes)).ToLowerInvariant()}";
        }
    }
}
#endif
