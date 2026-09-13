using System.Globalization;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Optimization;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Optimization.Execution;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Routes.Gguf;

namespace GraniteEdgeAI.Features.ModelOptimization.Export;

internal static class OptimizationExportDestinationNamePolicy
{
    private const int MaximumCandidateCount = 1000;

    internal static string? CreateAbsentDestination(
        string parentFolder,
        OptimizationRoute requestedRoute,
        OptimizationExecutionPlan plan,
        OptimizationExecutionResult result,
        Func<string, bool>? targetExists = null) =>
        CreateAbsentDestinationCore(
            parentFolder, requestedRoute, plan, result, null, targetExists);

    internal static string? CreateAbsentDestination(
        string parentFolder,
        OptimizationRoute requestedRoute,
        OptimizationExecutionPlan plan,
        OptimizationExecutionResult result,
        string importedModelName,
        Func<string, bool>? targetExists = null) =>
        CreateAbsentDestinationCore(
            parentFolder, requestedRoute, plan, result,
            importedModelName, targetExists);

    private static string? CreateAbsentDestinationCore(
        string parentFolder,
        OptimizationRoute requestedRoute,
        OptimizationExecutionPlan plan,
        OptimizationExecutionResult result,
        string? importedModelName,
        Func<string, bool>? targetExists)
    {
        if (string.IsNullOrWhiteSpace(parentFolder)
            || plan is null
            || result is null
            || !MatchesIssuedResult(requestedRoute, plan, result))
        {
            return null;
        }

        string? extension = null;
        string? baseName;
        if (requestedRoute == OptimizationRoute.OpenVino
            && plan.ExecutionPayload is
                { Route: OptimizationRoute.OpenVino, OpenVino: { } openVino }
            && TryWeightToken(openVino.TargetWeightPrecision, out string? weight)
            && TryCacheToken(openVino.KvCachePrecision, out string? cache))
        {
            baseName = $"OpenVINO-{weight}-{cache}";
        }
        else if (requestedRoute == OptimizationRoute.Gguf
            && plan.ExecutionPayload is
                { Route: OptimizationRoute.Gguf, Gguf: { } gguf }
            && TryGgufWeightToken(
                gguf.PersistentTargetWeightFormat, out string? target)
            && TrySanitizeGgufModelStem(importedModelName, out string? modelStem))
        {
            baseName = $"{modelStem}-{target}";
            extension = ".gguf";
        }
        else
        {
            return null;
        }

        targetExists ??= static candidate =>
            File.Exists(candidate) || Directory.Exists(candidate);
        for (int candidateNumber = 1;
             candidateNumber <= MaximumCandidateCount;
             candidateNumber++)
        {
            string suffix = candidateNumber == 1
                ? string.Empty
                : string.Create(CultureInfo.InvariantCulture,
                    $"-{candidateNumber}");
            string leaf = $"{baseName}{suffix}{extension}";
            string candidate = Path.Combine(parentFolder, leaf);
            if (!targetExists(candidate))
            {
                return candidate;
            }
        }

        return null;
    }

    internal static bool MatchesIssuedResult(
        OptimizationRoute requestedRoute,
        OptimizationExecutionPlan plan,
        OptimizationExecutionResult result) =>
        plan is not null
        && result is not null
        && result.Status == OptimizationExecutionStatus.SucceededPersistent
        && result.SourceUnchanged
        && requestedRoute == plan.Route
        && requestedRoute == result.Route
        && result.OptimizationPlanId == plan.OptimizationPlanId
        && string.Equals(
            result.ConfigurationSha256,
            plan.ConfigurationSha256,
            StringComparison.Ordinal)
        && string.Equals(
            result.SourceSha256,
            plan.Binding.ModelSha256,
            StringComparison.Ordinal)
        && result.SourceLengthBytes == plan.Binding.ModelLengthBytes
        && string.Equals(
            result.ModelInspectionRunId,
            plan.Binding.ModelInspectionRunId,
            StringComparison.Ordinal)
        && string.Equals(
            result.ModelInspectionHandoffId,
            plan.Binding.ModelInspectionHandoffId,
            StringComparison.Ordinal)
        && string.Equals(
            result.ProductHardwareRunId,
            plan.Binding.ProductHardwareRunId,
            StringComparison.Ordinal)
        && string.Equals(
            result.HardwareSnapshotSha256,
            plan.Binding.HardwareSnapshotSha256,
            StringComparison.Ordinal);

    private static bool TryWeightToken(
        OpenVinoWeightPrecision value,
        out string? token)
    {
        token = value switch
        {
            OpenVinoWeightPrecision.Fp16 => "FP16",
            OpenVinoWeightPrecision.EightBit => "INT8",
            OpenVinoWeightPrecision.FourBit => "INT4",
            OpenVinoWeightPrecision.MxFp4 => "MXFP4",
            _ => null
        };
        return token is not null;
    }

    private static bool TryCacheToken(
        OpenVinoKvCachePrecision value,
        out string? token)
    {
        token = value switch
        {
            OpenVinoKvCachePrecision.ReleasedDefault => "DEFAULT",
            OpenVinoKvCachePrecision.U8 => "U8",
            OpenVinoKvCachePrecision.F16 => "F16",
            OpenVinoKvCachePrecision.Bf16 => "BF16",
            OpenVinoKvCachePrecision.U4 => "U4",
            OpenVinoKvCachePrecision.Tbq4 => "TBQ4",
            OpenVinoKvCachePrecision.Tbq3 => "TBQ3",
            _ => null
        };
        return token is not null;
    }

    private static bool TryGgufWeightToken(
        GgufWeightFormat value,
        out string? token)
    {
        token = value switch
        {
            GgufWeightFormat.F16 => "F16",
            GgufWeightFormat.BF16 => "BF16",
            GgufWeightFormat.Q8_0 => "Q8_0",
            GgufWeightFormat.Q6K => "Q6_K",
            GgufWeightFormat.Q5KM => "Q5_K_M",
            GgufWeightFormat.Q4KM => "Q4_K_M",
            GgufWeightFormat.Q3KM => "Q3_K_M",
            GgufWeightFormat.Q2K => "Q2_K",
            GgufWeightFormat.TQ4_1S => "TQ4_1S",
            GgufWeightFormat.TQ3_1S => "TQ3_1S",
            _ => null
        };
        return token is not null;
    }

    private static bool TrySanitizeGgufModelStem(
        string? importedModelName,
        out string? stem)
    {
        stem = null;
        if (string.IsNullOrWhiteSpace(importedModelName)) return false;
        string fileName = Path.GetFileName(importedModelName.Trim());
        if (fileName.EndsWith(".gguf", StringComparison.OrdinalIgnoreCase))
        {
            fileName = fileName[..^5];
        }

        char[] invalid = Path.GetInvalidFileNameChars();
        var sanitized = new System.Text.StringBuilder(fileName.Length);
        bool separatorPending = false;
        foreach (char character in fileName)
        {
            bool separator = char.IsWhiteSpace(character)
                || char.IsControl(character)
                || invalid.Contains(character);
            if (separator)
            {
                separatorPending = sanitized.Length > 0;
                continue;
            }
            if (separatorPending && sanitized[^1] != '-') sanitized.Append('-');
            separatorPending = false;
            sanitized.Append(character);
            if (sanitized.Length >= 96) break;
        }

        string value = sanitized.ToString().Trim(' ', '.', '-');
        if (string.IsNullOrWhiteSpace(value)) return false;
        stem = value;
        return true;
    }
}
