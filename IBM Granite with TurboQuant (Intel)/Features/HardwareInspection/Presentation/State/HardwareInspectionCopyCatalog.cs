using System;

namespace GraniteEdgeAI.Features.HardwareInspection.Presentation.State;

public static class HardwareInspectionCopyCatalog
{
    private static readonly HardwareInspectionStageCopy[] Stages =
    [
        new(
            "Starting hardware inspection",
            "Preparing the approved local inspection tools and a safe run context.",
            "The approved local inspection tools were prepared.",
            "Waiting for the inspection to begin."),
        new(
            "Reading processor information",
            "Reading the processor name, architecture, core count, thread count, and supported instruction information.",
            "Processor information was collected.",
            "Starts after the local inspection tools are ready."),
        new(
            "Reading system memory",
            "Reading installed, Windows-usable, and currently available memory as separate values.",
            "Installed, usable, and currently available memory were collected separately.",
            "Starts after processor information is read."),
        new(
            "Detecting graphics hardware",
            "Checking graphics devices and keeping dedicated and shared memory separate.",
            "Graphics hardware and separate dedicated and shared memory values were collected.",
            "Starts after system memory is read."),
        new(
            "Checking local inference runtimes",
            "Checking which processor and graphics routes the installed local inference runtime can see.",
            "The installed local tools reported the processor and graphics routes they can see.",
            "Starts after graphics hardware is detected."),
        new(
            "Normalising hardware information",
            "Comparing trusted evidence and selecting safe hardware values under the approved rules.",
            "The collected information was compared and resolved using the approved rules.",
            "Starts after the available hardware information is collected."),
        new(
            "Creating the hardware report",
            "Recording the reliable hardware facts, provenance, and review notes in the hardware report.",
            "The reliable hardware facts and review notes were recorded.",
            "Starts after the hardware information is normalised."),
    ];

    public const string ActiveSubtitle =
        "Checking this computer. Hardware information stays on this device.";
    public const string ContinueUnavailableHelp =
        "Continue to compatibility is unavailable until this run has a usable hardware handoff and the compatibility step is available.";

    public static HardwareInspectionStageCopy Stage(HardwareInspectionStage stage)
    {
        int index = (int)stage;
        if (index < 0 || index >= Stages.Length)
        {
            throw new ArgumentOutOfRangeException(nameof(stage));
        }

        return Stages[index];
    }

    public static string ActionLabel(HardwareInspectionActionKind kind) => kind switch
    {
        HardwareInspectionActionKind.BackToModelInspection => "Back to model inspection",
        HardwareInspectionActionKind.CancelInspection => "Cancel inspection",
        HardwareInspectionActionKind.Stopping => "Stopping...",
        HardwareInspectionActionKind.ContinueToCompatibility => "Continue to compatibility",
        HardwareInspectionActionKind.RunInspectionAgain => "Run inspection again",
        HardwareInspectionActionKind.Back => "Back",
        HardwareInspectionActionKind.TryAgain => "Try again",
        _ => throw new ArgumentOutOfRangeException(nameof(kind)),
    };
}
