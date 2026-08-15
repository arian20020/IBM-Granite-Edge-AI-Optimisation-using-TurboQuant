using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace HardwareInspection.LlmFitSpike.Inspection;

public static class LlmFitSystemJsonAssessor
{
    private const string JsonInvalid = "HI-LLMFIT-JSON-INVALID";
    private const string CpuRamMissing = "HI-LLMFIT-CPU-RAM-MISSING";
    private const string GpuInconsistent = "HI-LLMFIT-GPU-INCONSISTENT";
    private const string IntelMemorySemanticsGap = "HI-LLMFIT-WINDOWS-INTEL-MEMORY-SEMANTICS-GAP";
    private const string IntelNpuGap = "HI-LLMFIT-WINDOWS-INTEL-NPU-GAP";
    private const string SchemaDocumentationDrift = "HI-LLMFIT-SCHEMA-DOCUMENTATION-DRIFT";
    private const string DetectionUnavailable = "DetectionUnavailable";

    public static LlmFitSystemAssessment Assess(string rawJson)
    {
        ArgumentNullException.ThrowIfNull(rawJson);

        string rawJsonSha256 = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(rawJson))).ToLowerInvariant();
        var diagnostics = new List<string>();

        try
        {
            using JsonDocument document = JsonDocument.Parse(
                rawJson,
                new JsonDocumentOptions { AllowTrailingCommas = false, CommentHandling = JsonCommentHandling.Disallow });

            if (document.RootElement.ValueKind != JsonValueKind.Object ||
                !document.RootElement.TryGetProperty("system", out JsonElement system) ||
                system.ValueKind != JsonValueKind.Object)
            {
                return CreateAssessment(false, false, false, null, null, null, null, false, 0, false, rawJsonSha256, diagnostics, true, false);
            }

            CpuRamData cpuRam = ReadCpuRam(system);
            GpuData gpu = ReadGpu(system);
            bool jsonValid = cpuRam.TypesValid && gpu.IsConsistent;

            if (!cpuRam.RequiredPresent)
            {
                AddDiagnostic(diagnostics, CpuRamMissing);
            }

            if (!gpu.IsConsistent)
            {
                AddDiagnostic(diagnostics, GpuInconsistent);
            }

            if (!jsonValid)
            {
                AddDiagnostic(diagnostics, JsonInvalid);
            }

            bool intelGpuReported = gpu.GpuReported && gpu.IntelGpuReported;
            if (jsonValid && intelGpuReported)
            {
                AddDiagnostic(diagnostics, IntelMemorySemanticsGap);
            }

            return CreateAssessment(
                jsonValid,
                cpuRam.RequiredPresent,
                cpuRam.CpuNamePresent,
                cpuRam.CpuName,
                cpuRam.CpuLogicalProcessorCount,
                cpuRam.TotalRamGiB,
                cpuRam.AvailableRamGiB,
                gpu.GpuReported,
                gpu.ReportedGpuCount,
                intelGpuReported,
                rawJsonSha256,
                diagnostics,
                false,
                false);
        }
        catch (JsonException)
        {
            return CreateAssessment(false, false, false, null, null, null, null, false, 0, false, rawJsonSha256, diagnostics, true, false);
        }
    }

    private static LlmFitSystemAssessment CreateAssessment(
        bool jsonValid,
        bool requiredCpuRamPresent,
        bool cpuNamePresent,
        string? cpuName,
        int? cpuLogicalProcessorCount,
        double? totalRamGiB,
        double? availableRamGiB,
        bool gpuReported,
        int reportedGpuCount,
        bool intelGpuReported,
        string rawJsonSha256,
        List<string> diagnostics,
        bool jsonInvalid,
        bool gpuInconsistent)
    {
        if (jsonInvalid)
        {
            AddDiagnostic(diagnostics, JsonInvalid);
        }

        if (gpuInconsistent)
        {
            AddDiagnostic(diagnostics, GpuInconsistent);
        }

        AddDiagnostic(diagnostics, IntelNpuGap);
        AddDiagnostic(diagnostics, SchemaDocumentationDrift);

        return new LlmFitSystemAssessment(
            jsonValid,
            requiredCpuRamPresent,
            cpuNamePresent,
            cpuName,
            cpuLogicalProcessorCount,
            totalRamGiB,
            availableRamGiB,
            gpuReported,
            reportedGpuCount,
            intelGpuReported,
            false,
            DetectionUnavailable,
            rawJsonSha256,
            diagnostics);
    }

    private static CpuRamData ReadCpuRam(JsonElement system)
    {
        bool totalRamTypeValid = TryReadFiniteNumber(system, "total_ram_gb", out double totalRam) && totalRam > 0;
        bool availableRamTypeValid = TryReadFiniteNumber(system, "available_ram_gb", out double availableRam);
        bool cpuCoresTypeValid = TryReadInteger(system, "cpu_cores", out int cpuCores) && cpuCores > 0;
        bool cpuNameTypeValid = TryReadString(system, "cpu_name", out string? cpuName);

        bool cpuNamePresent = cpuNameTypeValid && !string.IsNullOrWhiteSpace(cpuName);
        bool availableRamPresent = availableRamTypeValid && totalRamTypeValid && availableRam >= 0 && availableRam <= totalRam;
        bool requiredPresent = totalRamTypeValid && availableRamPresent && cpuCoresTypeValid && cpuNamePresent;
        bool typesValid = HasExpectedNumberType(system, "total_ram_gb") &&
            HasExpectedNumberType(system, "available_ram_gb") &&
            HasExpectedNumberType(system, "cpu_cores") &&
            HasExpectedStringType(system, "cpu_name");

        return new CpuRamData(
            requiredPresent,
            typesValid,
            cpuNamePresent ? cpuName : null,
            cpuCoresTypeValid ? cpuCores : null,
            totalRamTypeValid ? totalRam : null,
            availableRamPresent ? availableRam : null,
            cpuNamePresent);
    }

    private static GpuData ReadGpu(JsonElement system)
    {
        bool hasGpuTypeValid = TryReadBoolean(system, "has_gpu", out bool hasGpu);
        bool gpuCountTypeValid = TryReadInteger(system, "gpu_count", out int gpuCount) && gpuCount >= 0;
        bool gpusTypeValid = system.TryGetProperty("gpus", out JsonElement gpus) && gpus.ValueKind == JsonValueKind.Array;
        bool gpuEntriesValid = gpusTypeValid;
        long gpuEntryCount = 0;
        bool intelNameFound = false;

        if (gpusTypeValid)
        {
            foreach (JsonElement gpu in gpus.EnumerateArray())
            {
                if (gpu.ValueKind != JsonValueKind.Object || !TryReadInteger(gpu, "count", out int gpuInstanceCount) || gpuInstanceCount <= 0)
                {
                    gpuEntriesValid = false;
                    continue;
                }

                if (gpuEntryCount > int.MaxValue - (long)gpuInstanceCount)
                {
                    gpuEntriesValid = false;
                    continue;
                }

                gpuEntryCount += gpuInstanceCount;
                intelNameFound |= TryReadString(gpu, "name", out string? gpuName) && ContainsIntelToken(gpuName);
            }
        }

        bool gpusEmpty = !gpusTypeValid || gpus.GetArrayLength() == 0;
        bool flagsAndCountsAgree = hasGpuTypeValid && gpuCountTypeValid &&
            hasGpu == (gpuCount > 0) &&
            (hasGpu ? !gpusEmpty : gpusEmpty) &&
            gpuEntriesValid &&
            gpuEntryCount == gpuCount;
        bool isConsistent = flagsAndCountsAgree && gpusTypeValid;

        bool topLevelIntelNameFound = TryReadString(system, "gpu_name", out string? topLevelGpuName) && ContainsIntelToken(topLevelGpuName);
        return new GpuData(
            isConsistent,
            hasGpuTypeValid && hasGpu,
            gpuCountTypeValid ? gpuCount : 0,
            topLevelIntelNameFound || intelNameFound);
    }

    private static bool TryReadFiniteNumber(JsonElement parent, string propertyName, out double value)
    {
        value = default;
        return parent.TryGetProperty(propertyName, out JsonElement property) &&
            property.ValueKind == JsonValueKind.Number &&
            property.TryGetDouble(out value) &&
            double.IsFinite(value);
    }

    private static bool TryReadInteger(JsonElement parent, string propertyName, out int value)
    {
        value = default;
        return parent.TryGetProperty(propertyName, out JsonElement property) &&
            property.ValueKind == JsonValueKind.Number &&
            property.TryGetInt32(out value);
    }

    private static bool TryReadBoolean(JsonElement parent, string propertyName, out bool value)
    {
        value = default;
        if (!parent.TryGetProperty(propertyName, out JsonElement property) ||
            (property.ValueKind != JsonValueKind.True && property.ValueKind != JsonValueKind.False))
        {
            return false;
        }

        value = property.GetBoolean();
        return true;
    }

    private static bool TryReadString(JsonElement parent, string propertyName, out string? value)
    {
        value = null;
        return parent.TryGetProperty(propertyName, out JsonElement property) &&
            property.ValueKind == JsonValueKind.String &&
            (value = property.GetString()) is not null;
    }

    private static bool HasExpectedNumberType(JsonElement parent, string propertyName)
    {
        return parent.TryGetProperty(propertyName, out JsonElement property) && property.ValueKind == JsonValueKind.Number;
    }

    private static bool HasExpectedStringType(JsonElement parent, string propertyName)
    {
        return parent.TryGetProperty(propertyName, out JsonElement property) && property.ValueKind == JsonValueKind.String;
    }

    private static bool ContainsIntelToken(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return false;
        }

        const string intel = "Intel";
        for (int index = value.IndexOf(intel, StringComparison.OrdinalIgnoreCase);
            index >= 0;
            index = value.IndexOf(intel, index + intel.Length, StringComparison.OrdinalIgnoreCase))
        {
            int after = index + intel.Length;
            if ((index == 0 || !IsTokenCharacter(value[index - 1])) &&
                (after == value.Length || !IsTokenCharacter(value[after])))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsTokenCharacter(char value)
    {
        return char.IsLetterOrDigit(value) || value == '_';
    }

    private static void AddDiagnostic(List<string> diagnostics, string diagnosticCode)
    {
        if (!diagnostics.Contains(diagnosticCode, StringComparer.Ordinal))
        {
            diagnostics.Add(diagnosticCode);
        }
    }

    private sealed record CpuRamData(
        bool RequiredPresent,
        bool TypesValid,
        string? CpuName,
        int? CpuLogicalProcessorCount,
        double? TotalRamGiB,
        double? AvailableRamGiB,
        bool CpuNamePresent);

    private sealed record GpuData(
        bool IsConsistent,
        bool GpuReported,
        int ReportedGpuCount,
        bool IntelGpuReported);
}
