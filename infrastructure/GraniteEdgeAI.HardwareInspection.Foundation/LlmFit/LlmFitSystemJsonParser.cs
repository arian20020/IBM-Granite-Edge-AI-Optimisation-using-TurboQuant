using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace GraniteEdgeAI.HardwareInspection.Foundation.LlmFit;

internal static class LlmFitSystemJsonParser
{
    private const int MaximumGpuCount = 64;

    internal static LlmFitSystemParseResult Parse(string json)
    {
        ArgumentNullException.ThrowIfNull(json);
        string sha256 = ComputeSha256(json);

        try
        {
            using JsonDocument document = JsonDocument.Parse(
                json,
                new JsonDocumentOptions
                {
                    AllowTrailingCommas = false,
                    CommentHandling = JsonCommentHandling.Disallow,
                    MaxDepth = 16,
                });

            JsonElement root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object ||
                !HasUniquePropertyNames(root) ||
                !root.TryGetProperty("system", out JsonElement system) ||
                system.ValueKind != JsonValueKind.Object ||
                !HasUniquePropertyNames(system) ||
                !HasUniqueGpuObjectProperties(system))
            {
                return StructuralFailure(sha256);
            }

            return ParseSystem(system, sha256);
        }
        catch (JsonException)
        {
            return StructuralFailure(sha256);
        }
        catch (InvalidOperationException)
        {
            return StructuralFailure(sha256);
        }
    }

    private static LlmFitSystemParseResult ParseSystem(JsonElement system, string sha256)
    {
        FieldState totalState = ReadFiniteDouble(system, "total_ram_gb", out double totalRamGiB);
        FieldState availableState = ReadFiniteDouble(system, "available_ram_gb", out double availableRamGiB);
        FieldState coreState = ReadBoundedInteger(system, "cpu_cores", 1, 4096, out int logicalProcessors);
        FieldState cpuNameState = ReadSafeName(system, "cpu_name", allowNull: false, out string? cpuName);

        bool totalValid = totalState == FieldState.Valid && totalRamGiB is > 0 and <= 16384;
        bool availableIndividuallyValid =
            availableState == FieldState.Valid && availableRamGiB is >= 0 and <= 16384;
        bool ramRelationshipInvalid =
            totalValid && availableIndividuallyValid && availableRamGiB > totalRamGiB;
        bool availableRetainable = availableIndividuallyValid && !ramRelationshipInvalid;
        bool cpuMissing = totalState == FieldState.Missing ||
            availableState == FieldState.Missing ||
            coreState == FieldState.Missing ||
            cpuNameState == FieldState.Missing;
        bool cpuInvalid =
            totalState == FieldState.Invalid ||
            availableState == FieldState.Invalid ||
            coreState == FieldState.Invalid ||
            cpuNameState == FieldState.Invalid ||
            (totalState == FieldState.Valid && !totalValid) ||
            (availableState == FieldState.Valid && !availableIndividuallyValid) ||
            ramRelationshipInvalid;

        GpuParseData gpu = ParseGpu(system);
        List<LlmFitDiagnosticCode> diagnostics = [];
        if (cpuMissing)
        {
            diagnostics.Add(LlmFitDiagnosticCode.RequiredCpuRamMissing);
        }

        if (cpuInvalid)
        {
            diagnostics.Add(LlmFitDiagnosticCode.RequiredCpuRamInvalid);
        }

        if (gpu.ShapeMissing)
        {
            diagnostics.Add(LlmFitDiagnosticCode.GpuShapeMissing);
        }

        if (gpu.HasInconsistency)
        {
            diagnostics.Add(LlmFitDiagnosticCode.GpuInconsistent);
        }

        bool available = diagnostics.Count == 0;
        return new(
            available ? LlmFitEvidenceState.Available : LlmFitEvidenceState.Invalid,
            cpuNameState == FieldState.Valid ? cpuName : null,
            coreState == FieldState.Valid ? logicalProcessors : null,
            totalValid ? totalRamGiB : null,
            availableRetainable ? availableRamGiB : null,
            gpu.IsConsistent ? gpu.State : LlmFitGpuDetectionState.Invalid,
            gpu.IsConsistent ? gpu.Gpus : [],
            sha256,
            diagnostics);
    }

    private static GpuParseData ParseGpu(JsonElement system)
    {
        FieldState hasGpuState = ReadBoolean(system, "has_gpu", out bool hasGpu);
        FieldState countState = ReadBoundedInteger(system, "gpu_count", 0, MaximumGpuCount, out int expectedCount);
        FieldState arrayState = GetArray(system, "gpus", out JsonElement gpuArray);
        bool shapeMissing = hasGpuState == FieldState.Missing ||
            countState == FieldState.Missing || arrayState == FieldState.Missing;
        bool representationInvalid = hasGpuState == FieldState.Invalid ||
            countState == FieldState.Invalid || arrayState == FieldState.Invalid;
        if (hasGpuState != FieldState.Valid || countState != FieldState.Valid || arrayState != FieldState.Valid)
        {
            return new(shapeMissing, representationInvalid, false, LlmFitGpuDetectionState.Invalid, []);
        }

        if (gpuArray.GetArrayLength() > MaximumGpuCount)
        {
            return new(false, true, false, LlmFitGpuDetectionState.Invalid, []);
        }

        List<LlmFitReportedGpu> gpus = [];
        HashSet<string> names = new(StringComparer.OrdinalIgnoreCase);
        int reportedCount = 0;
        foreach (JsonElement gpu in gpuArray.EnumerateArray())
        {
            if (gpu.ValueKind != JsonValueKind.Object || !HasUniquePropertyNames(gpu) ||
                ReadSafeName(gpu, "name", allowNull: false, out string? name) != FieldState.Valid ||
                ReadBoundedInteger(gpu, "count", 1, MaximumGpuCount, out int count) != FieldState.Valid ||
                name is null || !names.Add(name) || reportedCount > MaximumGpuCount - count)
            {
                return new(false, true, false, LlmFitGpuDetectionState.Invalid, []);
            }

            reportedCount += count;
            gpus.Add(new(name, count));
        }

        FieldState topLevelNameState = ReadSafeName(system, "gpu_name", allowNull: true, out _);
        bool falseGpuHasReportedName = !hasGpu &&
            system.TryGetProperty("gpu_name", out JsonElement topLevelName) &&
            topLevelName.ValueKind != JsonValueKind.Null;
        bool topLevelNameValid =
            (topLevelNameState is FieldState.Valid or FieldState.Missing) &&
            !falseGpuHasReportedName;
        bool consistent = topLevelNameValid &&
            hasGpu == (expectedCount > 0) &&
            expectedCount == reportedCount &&
            (hasGpu ? gpus.Count > 0 : gpus.Count == 0);

        return new(
            false,
            !consistent,
            consistent,
            consistent && hasGpu ? LlmFitGpuDetectionState.Reported :
                consistent ? LlmFitGpuDetectionState.NotReported : LlmFitGpuDetectionState.Invalid,
            consistent ? gpus : []);
    }

    private static bool HasUniquePropertyNames(JsonElement element)
    {
        HashSet<string> names = new(StringComparer.Ordinal);
        foreach (JsonProperty property in element.EnumerateObject())
        {
            if (!names.Add(property.Name))
            {
                return false;
            }
        }

        return true;
    }

    private static bool HasUniqueGpuObjectProperties(JsonElement system)
    {
        if (!system.TryGetProperty("gpus", out JsonElement gpus) || gpus.ValueKind != JsonValueKind.Array)
        {
            return true;
        }

        foreach (JsonElement gpu in gpus.EnumerateArray())
        {
            if (gpu.ValueKind == JsonValueKind.Object && !HasUniquePropertyNames(gpu))
            {
                return false;
            }
        }

        return true;
    }

    private static FieldState ReadFiniteDouble(
        JsonElement parent,
        string propertyName,
        out double value)
    {
        value = default;
        if (!parent.TryGetProperty(propertyName, out JsonElement property))
        {
            return FieldState.Missing;
        }

        return property.ValueKind == JsonValueKind.Number &&
            property.TryGetDouble(out value) &&
            double.IsFinite(value)
                ? FieldState.Valid
                : FieldState.Invalid;
    }

    private static FieldState ReadBoundedInteger(
        JsonElement parent,
        string propertyName,
        int minimum,
        int maximum,
        out int value)
    {
        value = default;
        if (!parent.TryGetProperty(propertyName, out JsonElement property))
        {
            return FieldState.Missing;
        }

        return property.ValueKind == JsonValueKind.Number &&
            property.TryGetInt32(out value) &&
            value >= minimum &&
            value <= maximum
                ? FieldState.Valid
                : FieldState.Invalid;
    }

    private static FieldState ReadSafeName(
        JsonElement parent,
        string propertyName,
        bool allowNull,
        out string? value)
    {
        value = null;
        if (!parent.TryGetProperty(propertyName, out JsonElement property))
        {
            return FieldState.Missing;
        }

        if (allowNull && property.ValueKind == JsonValueKind.Null)
        {
            return FieldState.Valid;
        }

        if (property.ValueKind != JsonValueKind.String)
        {
            return FieldState.Invalid;
        }

        value = property.GetString();
        return LlmFitContractValidation.IsSafeName(value) ? FieldState.Valid : FieldState.Invalid;
    }

    private static FieldState ReadBoolean(JsonElement parent, string propertyName, out bool value)
    {
        value = default;
        if (!parent.TryGetProperty(propertyName, out JsonElement property))
        {
            return FieldState.Missing;
        }

        if (property.ValueKind is not (JsonValueKind.True or JsonValueKind.False))
        {
            return FieldState.Invalid;
        }

        value = property.GetBoolean();
        return FieldState.Valid;
    }

    private static FieldState GetArray(JsonElement parent, string propertyName, out JsonElement value)
    {
        if (!parent.TryGetProperty(propertyName, out value))
        {
            return FieldState.Missing;
        }

        return value.ValueKind == JsonValueKind.Array ? FieldState.Valid : FieldState.Invalid;
    }

    private static string ComputeSha256(string json) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(json))).ToLowerInvariant();

    private static LlmFitSystemParseResult StructuralFailure(string sha256) =>
        new(
            LlmFitEvidenceState.Invalid,
            null,
            null,
            null,
            null,
            LlmFitGpuDetectionState.Invalid,
            [],
            sha256,
            [LlmFitDiagnosticCode.JsonInvalid]);

    private enum FieldState
    {
        Missing,
        Invalid,
        Valid,
    }

    private sealed record GpuParseData(
        bool ShapeMissing,
        bool HasInconsistency,
        bool IsConsistent,
        LlmFitGpuDetectionState State,
        IReadOnlyList<LlmFitReportedGpu> Gpus);
}
