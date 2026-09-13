using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Optimization;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Routes.Gguf;

namespace GraniteEdgeAI.Features.ModelOptimization.Execution.Gguf;

internal enum GgufOptimizationOutputValidationPhase
{
    Validate = 0,
    SmokeTest,
    Reinspect,
}

internal sealed record GgufOptimizationOutputValidationResult
{
    private GgufOptimizationOutputValidationResult(
        OptimizationSupportCode supportCode,
        GgufWeightFormat? validatedTarget,
        string? outputSha256,
        ulong outputLengthBytes)
    {
        SupportCode = supportCode;
        ValidatedTarget = validatedTarget;
        OutputSha256 = outputSha256;
        OutputLengthBytes = outputLengthBytes;
    }

    internal OptimizationSupportCode SupportCode { get; }
    internal GgufWeightFormat? ValidatedTarget { get; }
    internal string? OutputSha256 { get; }
    internal ulong OutputLengthBytes { get; }
    internal bool Succeeded => SupportCode == OptimizationSupportCode.None
        && ValidatedTarget is not null
        && OutputSha256 is not null
        && OutputLengthBytes > 0;

    internal static GgufOptimizationOutputValidationResult Success(
        GgufWeightFormat validatedTarget,
        string outputSha256,
        ulong outputLengthBytes)
    {
        if (!Enum.IsDefined(validatedTarget)
            || validatedTarget is GgufWeightFormat.Unspecified
                or GgufWeightFormat.Imported)
        {
            throw new ArgumentOutOfRangeException(nameof(validatedTarget));
        }
        if (!IsCanonicalSha256(outputSha256))
        {
            throw new ArgumentException(
                "A canonical lowercase output digest is required.",
                nameof(outputSha256));
        }
        if (outputLengthBytes == 0)
        {
            throw new ArgumentOutOfRangeException(nameof(outputLengthBytes));
        }
        return new(
            OptimizationSupportCode.None,
            validatedTarget,
            outputSha256,
            outputLengthBytes);
    }

    internal static GgufOptimizationOutputValidationResult Failure(
        OptimizationSupportCode supportCode)
    {
        if (supportCode == OptimizationSupportCode.None)
        {
            throw new ArgumentOutOfRangeException(nameof(supportCode));
        }
        return new(supportCode, null, null, 0);
    }

    private static bool IsCanonicalSha256(string? value) =>
        value is { Length: 64 }
        && value.All(character => character is >= '0' and <= '9'
            or >= 'a' and <= 'f');
}

internal interface IGgufOptimizationOutputValidator
{
    Task<OptimizationSupportCode> ValidateRuntimeProfileAsync(
        OptimizationExecutionPlan plan,
        string sourcePath,
        IProgress<GgufOptimizationOutputValidationPhase> progress,
        CancellationToken cancellationToken);

    Task<GgufOptimizationOutputValidationResult> ValidateAsync(
        OptimizationExecutionPlan plan,
        string outputPath,
        IProgress<GgufOptimizationOutputValidationPhase> progress,
        CancellationToken cancellationToken);
}
