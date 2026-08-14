namespace GraniteEdgeAI.ModelInspection.LlamaSharp.ModelProbe;

public enum VocabOnlyProbePhase
{
    CheckModelPackage,
    ReadModelConfiguration,
    ValidateTokenizerAndChatSetup,
    ValidateModelStructure
}

public enum VocabOnlyProbePhaseStatus
{
    Active,
    Fraction,
    Completed
}

/// <summary>
/// Reports one typed runtime phase fact without crossing the worker protocol.
/// </summary>
public sealed record VocabOnlyProbeProgress
{
    public VocabOnlyProbeProgress(
        VocabOnlyProbePhase Phase,
        VocabOnlyProbePhaseStatus Status,
        float? NativeFraction = null)
    {
        if (!Enum.IsDefined(Phase))
        {
            throw new ArgumentOutOfRangeException(nameof(Phase));
        }

        if (!Enum.IsDefined(Status))
        {
            throw new ArgumentOutOfRangeException(nameof(Status));
        }

        if (Status == VocabOnlyProbePhaseStatus.Fraction &&
            (Phase != VocabOnlyProbePhase.ReadModelConfiguration ||
             !NativeFraction.HasValue))
        {
            throw new ArgumentException(
                "A configuration fraction fact must carry a value.",
                nameof(NativeFraction));
        }

        if (Status != VocabOnlyProbePhaseStatus.Fraction &&
            NativeFraction.HasValue)
        {
            throw new ArgumentException(
                "Active and completed facts must not carry a fraction.",
                nameof(NativeFraction));
        }

        if (NativeFraction is float fraction &&
            (!float.IsFinite(fraction) || fraction < 0f || fraction > 1f))
        {
            throw new ArgumentOutOfRangeException(nameof(NativeFraction));
        }

        this.Phase = Phase;
        this.Status = Status;
        this.NativeFraction = NativeFraction;
    }

    public VocabOnlyProbePhase Phase { get; }

    public VocabOnlyProbePhaseStatus Status { get; }

    public float? NativeFraction { get; }

    public void Deconstruct(
        out VocabOnlyProbePhase phase,
        out VocabOnlyProbePhaseStatus status,
        out float? nativeFraction)
    {
        phase = Phase;
        status = Status;
        nativeFraction = NativeFraction;
    }
}
