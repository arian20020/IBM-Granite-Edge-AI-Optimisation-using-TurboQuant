using GraniteEdgeAI.Features.OpenVinoRoute.Inspection;

namespace GraniteEdgeAI.Features.OpenVinoRoute.Conversion;

public static class SourceModelPolicy
{
    public const int PolicyVersion = 1;
    public const int MaximumEntries = 4_096;
    public const int MaximumDepth = 16;
    public const int MaximumJsonBytes = 16 * 1024 * 1024;
    public const int MaximumTextBytes = 16 * 1024 * 1024;
    public const long MaximumSafeTensorsBytes = 512L * 1024 * 1024 * 1024;
    public const int MaximumSafeTensorsHeaderBytes = 16 * 1024 * 1024;
    public const long MaximumContextLength = 1_048_576;

    internal static readonly string[] RequiredJsonResources =
    [
        "config.json",
        "generation_config.json",
        "special_tokens_map.json",
        "tokenizer.json",
        "tokenizer_config.json"
    ];

    private static readonly HashSet<string> AllowedFixedResources = new(
        RequiredJsonResources.Concat(
        [
            "added_tokens.json",
            "chat_template.jinja",
            "model.safetensors",
            "model.safetensors.index.json"
        ]),
        StringComparer.Ordinal);

    internal static OpenVinoSnapshotPolicy SnapshotPolicy { get; } = new(
        MaximumEntries,
        MaximumDepth,
        MaximumJsonBytes,
        MaximumSafeTensorsBytes,
        MaximumTextBytes,
        MaximumSafeTensorsBytes,
        IsAllowedResource,
        OpenVinoPackagePolicy.IsExecutableOrScriptName,
        IsJsonResource,
        static _ => false,
        static name => name == "chat_template.jinja");

    internal static bool IsAllowedResource(string relativeName) =>
        !relativeName.Contains('/') &&
        (AllowedFixedResources.Contains(relativeName) || IsCanonicalShard(relativeName));

    internal static bool IsJsonResource(string relativeName) =>
        relativeName.EndsWith(".json", StringComparison.Ordinal);

    internal static bool IsCanonicalShard(string relativeName)
        => TryParseCanonicalShard(relativeName, out _, out _);

    internal static bool TryParseCanonicalShard(
        string relativeName,
        out int shardIndex,
        out int shardCount)
    {
        shardIndex = 0;
        shardCount = 0;
        const string prefix = "model-";
        const string middle = "-of-";
        const string suffix = ".safetensors";
        if (!relativeName.StartsWith(prefix, StringComparison.Ordinal) ||
            !relativeName.EndsWith(suffix, StringComparison.Ordinal))
        {
            return false;
        }

        string body = relativeName[prefix.Length..^suffix.Length];
        int separator = body.IndexOf(middle, StringComparison.Ordinal);
        if (separator != 5 || body.Length != 5 + middle.Length + 5)
        {
            return false;
        }

        string index = body[..separator];
        string count = body[(separator + middle.Length)..];
        return index.All(char.IsAsciiDigit) && count.All(char.IsAsciiDigit) &&
            int.TryParse(index, out shardIndex) && int.TryParse(count, out shardCount) &&
            shardIndex > 0 && shardCount > 0 && shardIndex <= shardCount;
    }
}
