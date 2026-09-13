namespace GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Optimization.Evidence;

public enum OptimizationQualityLevel
{
    BelowMinimum = 0,
    Acceptable,
    Fair,
    Good,
    VeryGood,
    Excellent
}

/// <summary>A methodology-bound quality result on the closed 0..10 scale.</summary>
public readonly record struct OptimizationQualityScore
{
    public OptimizationQualityScore(decimal value)
    {
        if (value is < 0m or > 10m)
        {
            throw new ArgumentOutOfRangeException(
                nameof(value), value, "Quality must be between 0 and 10 inclusive.");
        }

        Value = value;
    }

    public decimal Value { get; }

    public OptimizationQualityLevel Level => Value switch
    {
        < 4m => OptimizationQualityLevel.BelowMinimum,
        < 5m => OptimizationQualityLevel.Acceptable,
        < 6m => OptimizationQualityLevel.Fair,
        < 7m => OptimizationQualityLevel.Good,
        < 8m => OptimizationQualityLevel.VeryGood,
        _ => OptimizationQualityLevel.Excellent
    };
}
