using System.Buffers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace HardwareInspection.LlmFitSpike.Inspection;

public static class LlmFitSystemJsonAssessor
{
    private const long ExponentComparisonSlack = 1024;
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
                !HasUniquePropertyNames(document.RootElement) ||
                !document.RootElement.TryGetProperty("system", out JsonElement system) ||
                system.ValueKind != JsonValueKind.Object ||
                !HasUniquePropertyNames(system) ||
                !HasUniqueGpuPropertyNames(system))
            {
                return CreateJsonInvalidAssessment(rawJsonSha256, diagnostics);
            }

            CpuRamData cpuRam = ReadCpuRam(system);
            GpuData gpu = ReadGpu(system);
            bool jsonValid = cpuRam.RepresentationsValid && gpu.IsConsistent;

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
                diagnostics);
        }
        catch (JsonException)
        {
            return CreateJsonInvalidAssessment(rawJsonSha256, diagnostics);
        }
        catch (InvalidOperationException)
        {
            return CreateJsonInvalidAssessment(rawJsonSha256, diagnostics);
        }
    }

    private static LlmFitSystemAssessment CreateJsonInvalidAssessment(string rawJsonSha256, List<string> diagnostics)
    {
        AddDiagnostic(diagnostics, JsonInvalid);
        return CreateAssessment(false, false, false, null, null, null, null, false, 0, false, rawJsonSha256, diagnostics);
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
        List<string> diagnostics)
    {
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
        MemberState totalRamState = ReadFiniteExactNumber(system, "total_ram_gb", out ExactJsonDecimal totalRam);
        MemberState availableRamState = ReadFiniteExactNumber(system, "available_ram_gb", out ExactJsonDecimal availableRam);
        MemberState cpuCoresState = ReadInteger(system, "cpu_cores", out int cpuCores);
        MemberState cpuNameState = ReadString(system, "cpu_name", out string? cpuName);

        bool totalRamPresent = totalRamState == MemberState.Valid && totalRam.IsPositive;
        bool cpuCoresPresent = cpuCoresState == MemberState.Valid && cpuCores > 0;
        bool cpuNamePresent = cpuNameState == MemberState.Valid && !string.IsNullOrWhiteSpace(cpuName);
        bool availableRamPresent = availableRamState == MemberState.Valid &&
            totalRamPresent &&
            availableRam.IsNonNegative &&
            availableRam.CompareMagnitude(totalRam) <= 0;
        bool requiredPresent = totalRamPresent && availableRamPresent && cpuCoresPresent && cpuNamePresent;
        bool representationsValid = totalRamState != MemberState.InvalidRepresentation &&
            availableRamState != MemberState.InvalidRepresentation &&
            cpuCoresState != MemberState.InvalidRepresentation &&
            cpuNameState != MemberState.InvalidRepresentation;

        return new CpuRamData(
            requiredPresent,
            representationsValid,
            cpuNamePresent ? cpuName : null,
            cpuCoresPresent ? cpuCores : null,
            totalRamPresent ? totalRam.Value : null,
            availableRamPresent ? availableRam.Value : null,
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
                intelNameFound |= ReadString(gpu, "name", out string? gpuName) == MemberState.Valid && ContainsIntelToken(gpuName);
            }
        }

        bool gpusEmpty = !gpusTypeValid || gpus.GetArrayLength() == 0;
        bool flagsAndCountsAgree = hasGpuTypeValid && gpuCountTypeValid &&
            hasGpu == (gpuCount > 0) &&
            (hasGpu ? !gpusEmpty : gpusEmpty) &&
            gpuEntriesValid &&
            gpuEntryCount == gpuCount;
        bool isConsistent = flagsAndCountsAgree && gpusTypeValid;

        bool topLevelIntelNameFound = ReadString(system, "gpu_name", out string? topLevelGpuName) == MemberState.Valid && ContainsIntelToken(topLevelGpuName);
        return new GpuData(
            isConsistent,
            hasGpuTypeValid && hasGpu,
            gpuCountTypeValid ? gpuCount : 0,
            topLevelIntelNameFound || intelNameFound);
    }

    private static MemberState ReadFiniteExactNumber(JsonElement parent, string propertyName, out ExactJsonDecimal value)
    {
        value = default;
        if (!parent.TryGetProperty(propertyName, out JsonElement property))
        {
            return MemberState.Missing;
        }

        if (property.ValueKind != JsonValueKind.Number ||
            !property.TryGetDouble(out double doubleValue) ||
            !double.IsFinite(doubleValue) ||
            !TryParseExactJsonDecimal(property.GetRawText(), doubleValue, out value))
        {
            return MemberState.InvalidRepresentation;
        }

        return MemberState.Valid;
    }

    private static MemberState ReadInteger(JsonElement parent, string propertyName, out int value)
    {
        value = default;
        if (!parent.TryGetProperty(propertyName, out JsonElement property))
        {
            return MemberState.Missing;
        }

        return property.ValueKind == JsonValueKind.Number && property.TryGetInt32(out value)
            ? MemberState.Valid
            : MemberState.InvalidRepresentation;
    }

    private static bool TryReadInteger(JsonElement parent, string propertyName, out int value)
    {
        return ReadInteger(parent, propertyName, out value) == MemberState.Valid;
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

    private static MemberState ReadString(JsonElement parent, string propertyName, out string? value)
    {
        value = null;
        if (!parent.TryGetProperty(propertyName, out JsonElement property))
        {
            return MemberState.Missing;
        }

        if (property.ValueKind != JsonValueKind.String)
        {
            return MemberState.InvalidRepresentation;
        }

        try
        {
            value = property.GetString();
            return value is null ? MemberState.InvalidRepresentation : MemberState.Valid;
        }
        catch (InvalidOperationException)
        {
            return MemberState.InvalidRepresentation;
        }
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
            if (IsUnicodeTokenBoundaryBefore(value, index) && IsUnicodeTokenBoundaryAfter(value, after))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsUnicodeTokenBoundaryBefore(string value, int index)
    {
        if (index == 0)
        {
            return true;
        }

        return Rune.DecodeLastFromUtf16(value.AsSpan(0, index), out Rune rune, out _) == OperationStatus.Done &&
            !IsTokenRune(rune);
    }

    private static bool IsUnicodeTokenBoundaryAfter(string value, int index)
    {
        if (index == value.Length)
        {
            return true;
        }

        return Rune.DecodeFromUtf16(value.AsSpan(index), out Rune rune, out _) == OperationStatus.Done &&
            !IsTokenRune(rune);
    }

    private static bool IsTokenRune(Rune value)
    {
        return Rune.IsLetterOrDigit(value) || value.Value == '_';
    }

    private static bool HasUniquePropertyNames(JsonElement objectElement)
    {
        var names = new HashSet<string>(StringComparer.Ordinal);
        foreach (JsonProperty property in objectElement.EnumerateObject())
        {
            if (!names.Add(property.Name))
            {
                return false;
            }
        }

        return true;
    }

    private static bool HasUniqueGpuPropertyNames(JsonElement system)
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

    private static bool TryParseExactJsonDecimal(string rawNumber, double doubleValue, out ExactJsonDecimal value)
    {
        value = default;
        ReadOnlySpan<char> text = rawNumber.AsSpan();
        if (text.IsEmpty)
        {
            return false;
        }

        int index = 0;
        bool isNegative = text[index] == '-';
        if (isNegative)
        {
            index++;
        }

        if (index >= text.Length)
        {
            return false;
        }

        var digits = new StringBuilder(text.Length);
        if (text[index] == '0')
        {
            digits.Append('0');
            index++;
            if (index < text.Length && IsAsciiDigit(text[index]))
            {
                return false;
            }
        }
        else if (IsAsciiNonzeroDigit(text[index]))
        {
            do
            {
                digits.Append(text[index]);
                index++;
            }
            while (index < text.Length && IsAsciiDigit(text[index]));
        }
        else
        {
            return false;
        }

        int fractionalDigitCount = 0;
        if (index < text.Length && text[index] == '.')
        {
            index++;
            int fractionalStart = index;
            while (index < text.Length && IsAsciiDigit(text[index]))
            {
                digits.Append(text[index]);
                fractionalDigitCount++;
                index++;
            }

            if (index == fractionalStart)
            {
                return false;
            }
        }

        bool exponentIsNegative = false;
        long exponentMagnitude = 0;
        bool exponentExceedsBound = false;
        if (index < text.Length && (text[index] == 'e' || text[index] == 'E'))
        {
            index++;
            if (index < text.Length && (text[index] == '+' || text[index] == '-'))
            {
                exponentIsNegative = text[index] == '-';
                index++;
            }

            int exponentStart = index;
            long exponentLimit = (long)text.Length + ExponentComparisonSlack;
            while (index < text.Length && IsAsciiDigit(text[index]))
            {
                int digit = text[index] - '0';
                if (!exponentExceedsBound)
                {
                    if (exponentMagnitude > (exponentLimit - digit) / 10)
                    {
                        exponentExceedsBound = true;
                    }
                    else
                    {
                        exponentMagnitude = (exponentMagnitude * 10) + digit;
                    }
                }

                index++;
            }

            if (index == exponentStart)
            {
                return false;
            }
        }

        if (index != text.Length)
        {
            return false;
        }

        string allDigits = digits.ToString();
        int firstNonzero = 0;
        while (firstNonzero < allDigits.Length && allDigits[firstNonzero] == '0')
        {
            firstNonzero++;
        }

        if (firstNonzero == allDigits.Length)
        {
            value = new ExactJsonDecimal(isNegative, true, string.Empty, 0, doubleValue);
            return true;
        }

        if (exponentExceedsBound || doubleValue == 0)
        {
            return false;
        }

        int lastNonzero = allDigits.Length - 1;
        while (allDigits[lastNonzero] == '0')
        {
            lastNonzero--;
        }

        long exponent = exponentIsNegative ? -exponentMagnitude : exponentMagnitude;
        long powerOfTen = exponent - fractionalDigitCount + (allDigits.Length - 1 - lastNonzero);
        value = new ExactJsonDecimal(
            isNegative,
            false,
            allDigits.Substring(firstNonzero, lastNonzero - firstNonzero + 1),
            powerOfTen,
            doubleValue);
        return true;
    }

    private static bool IsAsciiDigit(char value)
    {
        return value is >= '0' and <= '9';
    }

    private static bool IsAsciiNonzeroDigit(char value)
    {
        return value is >= '1' and <= '9';
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
        bool RepresentationsValid,
        string? CpuName,
        int? CpuLogicalProcessorCount,
        double? TotalRamGiB,
        double? AvailableRamGiB,
        bool CpuNamePresent);

    private enum MemberState
    {
        Missing,
        Valid,
        InvalidRepresentation,
    }

    private readonly record struct ExactJsonDecimal(
        bool IsNegative,
        bool IsZero,
        string SignificantDigits,
        long PowerOfTen,
        double Value)
    {
        public bool IsPositive => !IsNegative && !IsZero;

        public bool IsNonNegative => IsZero || !IsNegative;

        public int CompareMagnitude(ExactJsonDecimal other)
        {
            if (IsZero)
            {
                return other.IsZero ? 0 : -1;
            }

            if (other.IsZero)
            {
                return 1;
            }

            long decimalOrder = SignificantDigits.Length + PowerOfTen;
            long otherDecimalOrder = other.SignificantDigits.Length + other.PowerOfTen;
            if (decimalOrder != otherDecimalOrder)
            {
                return decimalOrder < otherDecimalOrder ? -1 : 1;
            }

            int comparisonLength = Math.Max(SignificantDigits.Length, other.SignificantDigits.Length);
            for (int index = 0; index < comparisonLength; index++)
            {
                char digit = index < SignificantDigits.Length ? SignificantDigits[index] : '0';
                char otherDigit = index < other.SignificantDigits.Length ? other.SignificantDigits[index] : '0';
                if (digit != otherDigit)
                {
                    return digit < otherDigit ? -1 : 1;
                }
            }

            return 0;
        }
    }

    private sealed record GpuData(
        bool IsConsistent,
        bool GpuReported,
        int ReportedGpuCount,
        bool IntelGpuReported);
}
