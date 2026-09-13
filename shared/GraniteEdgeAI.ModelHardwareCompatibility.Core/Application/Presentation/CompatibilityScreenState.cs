namespace GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Presentation;

/// <summary>
/// Which of the approved screens a run's outcome calls for.
///
/// The transient states - analysing and verifying - are driven by the ViewModel
/// as work progresses, not by a finished result, so nothing here produces them.
/// Everything else is a projection of what the engine concluded.
/// </summary>
public enum CompatibilityScreenState
{
    Unspecified = 0,

    /// <summary>Screen 02: the imported/current setup fits safely.</summary>
    EstimatedCompatible,

    /// <summary>
    /// Screen 04: the imported/current setup does not fit, but at least one
    /// separately admitted hardware-relative alternative does.
    /// </summary>
    OptimisationRequired,

    /// <summary>
    /// Screen 05: every candidate was sized and compared against a real budget,
    /// and none fit. A conclusion, reached on evidence.
    /// </summary>
    NoEstimatedSafeConfiguration,

    /// <summary>
    /// Screen 06: no conclusion could be reached. Distinct from screen 05 in the
    /// way that matters most - "we could not tell you" rather than "we checked
    /// and the answer is no". The production behaviour until the owner adapters
    /// exist.
    /// </summary>
    NotEstablished,

    /// <summary>Screen 10: the user stopped the run.</summary>
    Cancelled
}
