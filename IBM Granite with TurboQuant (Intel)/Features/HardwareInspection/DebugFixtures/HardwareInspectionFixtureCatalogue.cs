#if HARDWARE_INSPECTION_FIXTURE_GALLERY
using GraniteEdgeAI.Features.HardwareInspection.Application;
using GraniteEdgeAI.Features.HardwareInspection.Presentation.Factories;
using GraniteEdgeAI.Features.HardwareInspection.Presentation.State;
using System;
using System.Collections.Generic;
using System.Linq;

namespace GraniteEdgeAI.Features.HardwareInspection.DebugFixtures;

public sealed record HardwareInspectionFixtureScenario(
    string Id,
    string Title,
    HardwareInspectionPresentationState Presentation,
    HardwareInspectionStage? ActiveStage = null,
    HardwareSummaryPresentation? Summary = null,
    HardwareInspectionDetailsState? Details = null);

public static class HardwareInspectionFixtureCatalogue
{
    public static IReadOnlyList<HardwareInspectionFixtureScenario> Create()
    {
        HardwareInspectionPresentationFactory factory = new();
        List<HardwareInspectionFixtureScenario> scenarios =
        [
            new(
                "HI-INVALID-HANDOFF",
                "Invalid handoff",
                factory.CreateInvalidHandoff()),
        ];

        foreach (HardwareInspectionStage stage in
                 Enum.GetValues<HardwareInspectionStage>())
        {
            scenarios.Add(new(
                $"HI-ACTIVE-{(int)stage + 1:00}",
                $"Active · {HardwareInspectionCopyCatalog.Stage(stage).Title}",
                factory.CreateActive(stage),
                ActiveStage: stage));
        }

        scenarios.Add(new(
            "HI-STOPPING",
            "Stopping safely",
            factory.CreateStopping()));
        scenarios.Add(Terminal(
            "HI-COMPLETED",
            "Completed",
            factory.CreateTerminal(HardwareInspectionOutcome.Completed),
            includeSummary: true));
        scenarios.Add(Terminal(
            "HI-COMPLETED-WARNINGS",
            "Completed with warnings",
            factory.CreateTerminal(HardwareInspectionOutcome.CompletedWithWarnings),
            includeSummary: true));
        scenarios.Add(Terminal(
            "HI-FAILED-EVIDENCE",
            "Failed · critical evidence",
            factory.CreateTerminal(
                HardwareInspectionOutcome.Failed,
                HardwareInspectionFailureClass.CriticalEvidence)));
        scenarios.Add(Terminal(
            "HI-FAILED-TRANSIENT",
            "Failed · transient operation",
            factory.CreateTerminal(
                HardwareInspectionOutcome.Failed,
                HardwareInspectionFailureClass.TransientOperation)));
        scenarios.Add(Terminal(
            "HI-FAILED-REPAIR",
            "Failed · application repair required",
            factory.CreateTerminal(
                HardwareInspectionOutcome.Failed,
                HardwareInspectionFailureClass.ApplicationRepairRequired)));
        scenarios.Add(Terminal(
            "HI-CANCELLED",
            "Cancelled",
            factory.CreateTerminal(HardwareInspectionOutcome.Cancelled)));

        return scenarios.AsReadOnly();
    }

    private static HardwareInspectionFixtureScenario Terminal(
        string id,
        string title,
        HardwareInspectionPresentationState presentation,
        bool includeSummary = false) =>
        new(
            id,
            title,
            presentation,
            Summary: includeSummary ? CreateSummary() : null,
            Details: CreateDetails(presentation));

    private static HardwareSummaryPresentation CreateSummary() =>
        new(
        [
            new("Processor", "Processor", "Intel fixture processor"),
            new("Memory", "Installed memory", "32 GiB"),
            new("Graphics", "Graphics adapter", "Intel fixture graphics"),
            new("Storage", "Available storage", "256 GiB"),
            new("Local AI tools", "Runtime", "Synthetic fixture only"),
            new("Information sources", "Evidence", "Deterministic fixture data"),
        ]);

    private static HardwareInspectionDetailsState CreateDetails(
        HardwareInspectionPresentationState presentation)
    {
        string status = presentation.Kind switch
        {
            HardwareInspectionPresentationKind.Completed => "Completed",
            HardwareInspectionPresentationKind.CompletedWithWarnings => "Review",
            HardwareInspectionPresentationKind.Cancelled => "Cancelled",
            _ => "Unavailable",
        };
        HardwareInspectionDetailRow[] rows =
            Enum.GetValues<HardwareInspectionStage>()
                .Select(stage => new HardwareInspectionDetailRow(
                    HardwareInspectionCopyCatalog.Stage(stage).Title,
                    status == "Completed"
                        ? HardwareInspectionCopyCatalog.Stage(stage).CompletedSentence
                        : "Synthetic fixture state; no hardware inspection was run.",
                    status))
                .ToArray();
        string reportBadge = presentation.ReportCreated
            ? "Report created"
            : "No report created";
        return new HardwareInspectionDetailsState(
            "Deterministic debug-only fixture details",
            "This screen uses synthetic fixture data and does not inspect the device.",
            "No external tool or process is used by this fixture.",
            reportBadge,
            rows,
            [
                new HardwareInspectionTechnicalGroup(
                    "Fixture information",
                    "Safe debug metadata",
                    [new HardwareInspectionTechnicalItem("Scenario", presentation.Kind.ToString())]),
            ]);
    }
}
#endif
