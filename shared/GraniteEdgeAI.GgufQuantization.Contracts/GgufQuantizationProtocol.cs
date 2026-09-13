namespace GraniteEdgeAI.GgufQuantization.Contracts;

public static class GgufQuantizationProtocol
{
    public const int CurrentVersion = 1;

    public const string RequantizationPolicyVersion = "gguf-requantisation-v1";
}

public enum GgufQuantizationFormat
{
    F32 = 0,
    BF16,
    F16,
    Q8_0,
    Q6K,
    Q5KM,
    Q4KM,
    Q3KM,
    Q2K,
}

internal static class GgufQuantizationContractValidation
{
    internal static void RequireDigest(string value, string parameter)
    {
        if (value is not { Length: 64 }
            || value.Any(character => character is not (>= '0' and <= '9')
                and not (>= 'a' and <= 'f')))
        {
            throw new ArgumentException(
                "A SHA-256 identity must be exactly 64 lowercase hexadecimal characters.",
                parameter);
        }
    }

    internal static void RequireOpaqueToken(string value, string parameter)
    {
        bool valid = value is { Length: >= 1 and <= 128 }
            && value.All(character =>
                character is >= 'a' and <= 'z'
                    or >= 'A' and <= 'Z'
                    or >= '0' and <= '9'
                    or '-' or '_');
        if (!valid)
        {
            throw new ArgumentException(
                "An opaque token must contain only ASCII letters, digits, hyphen, or underscore.",
                parameter);
        }
    }

    internal static bool IsQuantized(GgufQuantizationFormat format) =>
        format is GgufQuantizationFormat.Q8_0
            or GgufQuantizationFormat.Q6K
            or GgufQuantizationFormat.Q5KM
            or GgufQuantizationFormat.Q4KM
            or GgufQuantizationFormat.Q3KM
            or GgufQuantizationFormat.Q2K;

    internal static int QualityRank(GgufQuantizationFormat format) => format switch
    {
        GgufQuantizationFormat.F32 => 9,
        GgufQuantizationFormat.BF16 => 8,
        GgufQuantizationFormat.F16 => 7,
        GgufQuantizationFormat.Q8_0 => 6,
        GgufQuantizationFormat.Q6K => 5,
        GgufQuantizationFormat.Q5KM => 4,
        GgufQuantizationFormat.Q4KM => 3,
        GgufQuantizationFormat.Q3KM => 2,
        GgufQuantizationFormat.Q2K => 1,
        _ => throw new ArgumentOutOfRangeException(nameof(format), format, "Unknown format."),
    };

    internal static bool RequiresRequantization(
        GgufQuantizationFormat source,
        GgufQuantizationFormat target) =>
        IsQuantized(source) && QualityRank(target) < QualityRank(source);
}
