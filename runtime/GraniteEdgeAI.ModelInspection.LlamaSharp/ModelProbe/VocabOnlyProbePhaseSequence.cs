namespace GraniteEdgeAI.ModelInspection.LlamaSharp.ModelProbe;

internal sealed class VocabOnlyProbePhaseSequence(
    IProgress<VocabOnlyProbeProgress>? progress)
{
    private readonly object _sync = new();
    private readonly IProgress<VocabOnlyProbeProgress>? _progress = progress;
    private VocabOnlyProbePhase? _activePhase;

    internal T Run<T>(
        VocabOnlyProbePhase phase,
        Func<T> operation)
    {
        ArgumentNullException.ThrowIfNull(operation);
        Begin(phase);

        T result;

        try
        {
            result = operation();
        }
        catch
        {
            Clear(phase);
            throw;
        }

        Complete(phase);
        return result;
    }

    internal async Task<T> RunAsync<T>(
        VocabOnlyProbePhase phase,
        Func<Task<T>> operation)
    {
        ArgumentNullException.ThrowIfNull(operation);
        Begin(phase);

        T result;

        try
        {
            result = await operation().ConfigureAwait(false);
        }
        catch
        {
            Clear(phase);
            throw;
        }

        Complete(phase);
        return result;
    }

    internal void ReportNativeFraction(float fraction)
        => ReportMeasuredFraction(VocabOnlyProbePhase.ReadModelConfiguration, fraction);

    internal void ReportMeasuredFraction(VocabOnlyProbePhase phase, float fraction)
    {
        lock (_sync)
        {
            EnsureActive(phase);

            _progress?.Report(
                new VocabOnlyProbeProgress(
                    phase,
                    VocabOnlyProbePhaseStatus.Fraction,
                    fraction));
        }
    }

    private void Begin(VocabOnlyProbePhase phase)
    {
        lock (_sync)
        {
            if (_activePhase is not null)
            {
                throw new InvalidOperationException(
                    "A probe phase is already active.");
            }

            _activePhase = phase;

            try
            {
                _progress?.Report(
                    new VocabOnlyProbeProgress(
                        phase,
                        VocabOnlyProbePhaseStatus.Active));
            }
            catch
            {
                _activePhase = null;
                throw;
            }
        }
    }

    private void Complete(VocabOnlyProbePhase phase)
    {
        lock (_sync)
        {
            EnsureActive(phase);

            try
            {
                _progress?.Report(
                    new VocabOnlyProbeProgress(
                        phase,
                        VocabOnlyProbePhaseStatus.Completed));
            }
            finally
            {
                _activePhase = null;
            }
        }
    }

    private void Clear(VocabOnlyProbePhase phase)
    {
        lock (_sync)
        {
            EnsureActive(phase);
            _activePhase = null;
        }
    }

    private void EnsureActive(VocabOnlyProbePhase phase)
    {
        if (_activePhase != phase)
        {
            throw new InvalidOperationException(
                "The probe phase transition is inconsistent.");
        }
    }
}
