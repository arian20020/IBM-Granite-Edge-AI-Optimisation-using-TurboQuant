using System.Diagnostics;

namespace GraniteEdgeAI.ModelInspection.LlamaSharp.ModelProbe;

/// <summary>
/// Records one genuine progress fraction reported by LLamaSharp.
/// </summary>
/// <param name="ElapsedMilliseconds">Elapsed time since recording began.</param>
/// <param name="Fraction">Normalized native fraction from zero to one.</param>
public sealed record NativeLoadProgressSample(
    long ElapsedMilliseconds,
    float Fraction);

/// <summary>
/// Captures thread-safe snapshots of native load progress without generating
/// synthetic intermediate percentages.
/// </summary>
public sealed class NativeLoadProgressRecorder : IProgress<float>
{
    private readonly object _sync = new();
    private readonly Stopwatch _stopwatch = Stopwatch.StartNew();
    private readonly List<NativeLoadProgressSample> _samples = new();
    private readonly IProgress<VocabOnlyProbeProgress>? _probeProgress;

    public NativeLoadProgressRecorder()
    {
    }

    internal NativeLoadProgressRecorder(
        IProgress<VocabOnlyProbeProgress>? probeProgress)
    {
        _probeProgress = probeProgress;
    }

    /// <summary>
    /// Ignores NaN, normalizes infinities, clamps finite values and omits only
    /// consecutive duplicate fractions.
    /// </summary>
    public void Report(float value)
    {
        if (float.IsNaN(value))
        {
            return;
        }

        float fraction = value switch
        {
            float.NegativeInfinity => 0f,
            float.PositiveInfinity => 1f,
            _ => Math.Clamp(value, 0f, 1f)
        };

        lock (_sync)
        {
            if (_samples.Count > 0 &&
                _samples[^1].Fraction.Equals(fraction))
            {
                return;
            }

            _samples.Add(
                new NativeLoadProgressSample(
                    _stopwatch.ElapsedMilliseconds,
                    fraction));
        }

        _probeProgress?.Report(
            new VocabOnlyProbeProgress(
                PackageValidated: false,
                NativeFraction: fraction));

    }

    /// <summary>
    /// Returns an independent snapshot safe for JSON evidence.
    /// </summary>
    public IReadOnlyList<NativeLoadProgressSample> GetSnapshot()
    {
        lock (_sync)
        {
            return _samples.ToArray();
        }
    }
}
