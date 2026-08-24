namespace GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Candidates;

/// <summary>
/// What must happen before a candidate can run. This is the distinction
/// between changing a runtime setting and creating a new model file on disk,
/// which the user must be told about before confirming.
/// </summary>
internal enum CandidatePreparation
{
    Unspecified = 0,

    /// <summary>The imported artifact runs directly.</summary>
    None,

    /// <summary>Runtime settings change; no new model file is created.</summary>
    RuntimeProfileOnly,

    /// <summary>A new artifact is produced from a trusted higher-precision source.</summary>
    WeightConversionRequired
}
