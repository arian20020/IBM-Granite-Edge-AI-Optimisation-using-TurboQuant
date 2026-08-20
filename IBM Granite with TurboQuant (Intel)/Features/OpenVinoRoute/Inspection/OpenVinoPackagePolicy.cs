using GraniteEdgeAI.OpenVino.Contracts;

namespace GraniteEdgeAI.Features.OpenVinoRoute.Inspection;

public static class OpenVinoPackagePolicy
{
    public const int PolicyVersion = 1;
    public const int MaximumEntries = 4_096;
    public const int MaximumDepth = 16;
    public const int MaximumJsonBytes = 16 * 1024 * 1024;
    public const int MaximumTextBytes = 16 * 1024 * 1024;
    public const long MaximumXmlBytes = 256L * 1024 * 1024;
    public const int MaximumJsonDepth = 32;
    public const long MaximumContextLength = 1_048_576;

    internal static readonly string[] RequiredResources =
    [
        "config.json",
        "generation_config.json",
        "openvino_detokenizer.bin",
        "openvino_detokenizer.xml",
        "openvino_model.bin",
        "openvino_model.xml",
        "openvino_tokenizer.bin",
        "openvino_tokenizer.xml",
        "tokenizer_config.json"
    ];

    private static readonly HashSet<string> AllowedResources = new(
        RequiredResources.Concat(
        [
            "added_tokens.json",
            "chat_template.jinja",
            "chat_template.json",
            "merges.txt",
            "special_tokens_map.json",
            "tokenizer.json",
            "tokenizer.model",
            "vocab.json"
        ]),
        StringComparer.Ordinal);

    internal static bool IsAllowedResource(string relativeName) =>
        !relativeName.Contains('/') && AllowedResources.Contains(relativeName);

    internal static bool IsRequiredResource(string relativeName) =>
        Array.BinarySearch(RequiredResources, relativeName, StringComparer.Ordinal) >= 0;

    internal static bool IsJsonResource(string relativeName) =>
        relativeName.EndsWith(".json", StringComparison.Ordinal);

    internal static bool IsXmlResource(string relativeName) =>
        relativeName.EndsWith(".xml", StringComparison.Ordinal);

    internal static bool IsTextResource(string relativeName) =>
        relativeName.Equals("chat_template.jinja", StringComparison.Ordinal) ||
        relativeName.Equals("merges.txt", StringComparison.Ordinal);

    internal static bool IsExecutableOrScriptName(string relativeName)
    {
        string extension = Path.GetExtension(relativeName);
        return extension.Equals(".bat", StringComparison.OrdinalIgnoreCase) ||
            extension.Equals(".cmd", StringComparison.OrdinalIgnoreCase) ||
            extension.Equals(".com", StringComparison.OrdinalIgnoreCase) ||
            extension.Equals(".dll", StringComparison.OrdinalIgnoreCase) ||
            extension.Equals(".exe", StringComparison.OrdinalIgnoreCase) ||
            extension.Equals(".jar", StringComparison.OrdinalIgnoreCase) ||
            extension.Equals(".js", StringComparison.OrdinalIgnoreCase) ||
            extension.Equals(".msi", StringComparison.OrdinalIgnoreCase) ||
            extension.Equals(".ps1", StringComparison.OrdinalIgnoreCase) ||
            extension.Equals(".py", StringComparison.OrdinalIgnoreCase) ||
            extension.Equals(".scr", StringComparison.OrdinalIgnoreCase) ||
            extension.Equals(".sh", StringComparison.OrdinalIgnoreCase) ||
            extension.Equals(".vbs", StringComparison.OrdinalIgnoreCase);
    }
}

public enum OpenVinoStaticInspectionStatus
{
    NativeValidationRequired,
    Rejected
}

public sealed record OpenVinoStaticPackageEvidence(
    int PolicyVersion,
    string PackageManifestDigest,
    string ModelSha256,
    long ModelLengthBytes,
    string ModelType,
    string Architecture,
    string Task,
    long ContextLength,
    string Precision,
    string TokenizerClass,
    int ResourceCount,
    bool HasChatTemplate);

public sealed record OpenVinoStaticPackageInspectionResult(
    OpenVinoStaticInspectionStatus Status,
    OpenVinoSupportCode? SupportCode,
    OpenVinoStaticPackageEvidence? Evidence)
{
    public static OpenVinoStaticPackageInspectionResult NativeValidationRequired(OpenVinoStaticPackageEvidence evidence) =>
        new(OpenVinoStaticInspectionStatus.NativeValidationRequired, null, evidence ?? throw new ArgumentNullException(nameof(evidence)));

    public static OpenVinoStaticPackageInspectionResult Rejected(OpenVinoSupportCode supportCode)
    {
        supportCode.Validate();
        return new(OpenVinoStaticInspectionStatus.Rejected, supportCode, null);
    }
}

public sealed record OpenVinoNativeValidationEvidence(
    ModelInspectionOutcome Outcome,
    string PackageManifestDigest,
    string ModelSha256,
    long ModelLengthBytes,
    bool MainModelParsed,
    bool TokenizerParsed,
    bool DetokenizerParsed);
