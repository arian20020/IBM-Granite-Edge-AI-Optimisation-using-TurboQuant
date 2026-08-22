namespace GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Presentation;

/// <summary>
/// Which of the approved screens a run's outcome calls for.
///
/// The transient states — analysing and verifying — are driven by the ViewModel
/// as work progresses, not by a finished result, so nothing here produces them.
/// Everything else is a projection of what the engine concluded.
/// </summary>
internal enum CompatibilityScreenState
{
    Unspecified = 0,

    /// <summary>Screen 02: a safe configuration was established.</summary>
    EstimatedCompatible,

    /// <summary>
    /// Screen 04: something fits, but only narrowly — optimisation is
    /// recommended before the user commits to it.
    /// </summary>
    OptimisationRequired,

    /// <summary>
    /// Screen 05: every candidate was sized and compared against a real budget,
    /// and none fit. A conclusion, reached on evidence.
    /// </summary>
    NoEstimatedSafeConfiguration,

    /// <summary>
    /// Screen 06: no conclusion could be reached. Distinct from screen 05 in the
    /// way that matters most — "we could not tell you" rather than "we checked
    /// and the answer is no". The production behaviour until the owner adapters
    /// exist.
    /// </summary>
    NotEstablished,

    /// <summary>Screen 10: the user stopped the run.</summary>
    Cancelled
}
