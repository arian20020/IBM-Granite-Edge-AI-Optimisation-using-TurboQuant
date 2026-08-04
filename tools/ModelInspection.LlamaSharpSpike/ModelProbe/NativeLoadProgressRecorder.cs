using System.Diagnostics;

namespace GraniteEdgeAI.Tools.ModelInspection.LlamaSharpSpike.ModelProbe;

/// <summary>
/// Records one genuine progress fraction reported by LLamaSharp.
/// </summary>
/// <param name="ElapsedMilliseconds">Elapsed time since recording began.</param>
/// <param name="Fraction">Clamped native fraction from zero to one.</param>
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

    /// <summary>
    /// Records a clamped fraction unless it is identical to the previous one.
    /// </summary>
    public void Report(float value)
    {
        float fraction = Math.Clamp(value, 0f, 1f);

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
