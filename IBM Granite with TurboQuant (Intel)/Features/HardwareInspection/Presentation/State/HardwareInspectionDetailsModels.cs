using System;
using System.Collections.Generic;
using System.Linq;

namespace GraniteEdgeAI.Features.HardwareInspection.Presentation.State;

public sealed record HardwareFactPresentation
{
    public HardwareFactPresentation(string group, string label, string value, string? helper = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(group);
        ArgumentException.ThrowIfNullOrWhiteSpace(label);
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        Group = group;
        Label = label;
        Value = value;
        Helper = helper;
    }

    public string Group { get; }
    public string Label { get; }
    public string Value { get; }
    public string? Helper { get; }
}

public sealed class HardwareSummaryPresentation
{
    public HardwareSummaryPresentation(IEnumerable<HardwareFactPresentation> facts)
    {
        ArgumentNullException.ThrowIfNull(facts);
        HardwareFactPresentation[] copy = facts.ToArray();
        if (copy.Any(fact => fact is null))
        {
            throw new ArgumentException("Facts cannot contain null.", nameof(facts));
        }

        Facts = Array.AsReadOnly(copy);
    }

    public IReadOnlyList<HardwareFactPresentation> Facts { get; }
}

public sealed record HardwareInspectionDetailRow
{
    public HardwareInspectionDetailRow(string title, string sentence, string status)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        ArgumentException.ThrowIfNullOrWhiteSpace(sentence);
        ArgumentException.ThrowIfNullOrWhiteSpace(status);
        Title = title;
        Sentence = sentence;
        Status = status;
        AccessibleName = $"{title}. {sentence} Status: {status}.";
    }

    public string Title { get; }
    public string Sentence { get; }
    public string Status { get; }
    public string AccessibleName { get; }
}

public sealed record HardwareInspectionTechnicalItem
{
    public HardwareInspectionTechnicalItem(string label, string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(label);
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        Label = label;
        Value = value;
    }

    public string Label { get; }
    public string Value { get; }
}

public sealed class HardwareInspectionTechnicalGroup
{
    public HardwareInspectionTechnicalGroup(
        string title,
        string helper,
        IEnumerable<HardwareInspectionTechnicalItem> items)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        ArgumentException.ThrowIfNullOrWhiteSpace(helper);
        ArgumentNullException.ThrowIfNull(items);
        HardwareInspectionTechnicalItem[] copy = items.ToArray();
        if (copy.Length == 0 || copy.Any(item => item is null))
        {
            throw new ArgumentException("Technical group requires non-null items.", nameof(items));
        }

        Title = title;
        Helper = helper;
        Items = Array.AsReadOnly(copy);
    }

    public string Title { get; }
    public string Helper { get; }
    public IReadOnlyList<HardwareInspectionTechnicalItem> Items { get; }
}

public sealed class HardwareInspectionDetailsState
{
    public HardwareInspectionDetailsState(
        string helper,
        string summary,
        string reportDescription,
        string reportBadge,
        IEnumerable<HardwareInspectionDetailRow> rows,
        IEnumerable<HardwareInspectionTechnicalGroup> technicalGroups)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(helper);
        ArgumentException.ThrowIfNullOrWhiteSpace(summary);
        ArgumentException.ThrowIfNullOrWhiteSpace(reportDescription);
        ArgumentException.ThrowIfNullOrWhiteSpace(reportBadge);
        ArgumentNullException.ThrowIfNull(rows);
        ArgumentNullException.ThrowIfNull(technicalGroups);
        HardwareInspectionDetailRow[] rowCopy = rows.ToArray();
        HardwareInspectionTechnicalGroup[] groupCopy = technicalGroups.ToArray();
        if (rowCopy.Length != 7 || rowCopy.Any(row => row is null))
        {
            throw new ArgumentException("Details require exactly seven non-null stage rows.", nameof(rows));
        }
        if (groupCopy.Any(group => group is null))
        {
            throw new ArgumentException("Technical groups cannot contain null.", nameof(technicalGroups));
        }

        Helper = helper;
        Summary = summary;
        ReportDescription = reportDescription;
        ReportBadge = reportBadge;
        Rows = Array.AsReadOnly(rowCopy);
        TechnicalGroups = Array.AsReadOnly(groupCopy);
    }

    public string Helper { get; }
    public string Summary { get; }
    public string ReportDescription { get; }
    public string ReportBadge { get; }
    public IReadOnlyList<HardwareInspectionDetailRow> Rows { get; }
    public IReadOnlyList<HardwareInspectionTechnicalGroup> TechnicalGroups { get; }
}
