using System.Text.Json;

namespace GraniteEdgeAI.Tools.ModelInspection.LlamaSharpSpike.TestSupport;

/// <summary>
/// Provides framework-neutral assertions shared by hosted and trusted
/// integration tests. Methods throw ordinary exceptions rather than depending
/// on MSTest.
/// </summary>
public static class EvidenceAssertions
{
    public static JsonDocument LoadJson(string path)
    {
        AssertFileExists(path);

        try
        {
            return JsonDocument.Parse(File.ReadAllText(path));
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException(
                $"Evidence JSON is invalid: {path}",
                exception);
        }
    }

    public static void AssertFileExists(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        if (!File.Exists(path))
        {
            throw new FileNotFoundException(
                "Expected evidence file was not created.",
                path);
        }
    }

    public static void AssertSuccessfulGraniteVocabOnly(
        JsonElement root,
        ControlledModelManifest manifest)
    {
        ArgumentNullException.ThrowIfNull(manifest);

        RequireEqual("1.1", RequireString(root, "schemaVersion"), "schemaVersion");
        RequireEqual("VocabOnly", RequireString(root, "probeMode"), "probeMode");
        RequireEqual("Succeeded", RequireString(root, "completionStatus"), "completionStatus");
        RequireTrue(RequireBoolean(root, "succeeded"), "succeeded");
        RequireTrue(RequireBoolean(root, "vocabOnlyRequested"), "vocabOnlyRequested");
        RequireEqual(0, RequireInt32(root, "gpuLayerCount"), "gpuLayerCount");
        RequireEqual("0.27.0", RequireString(root, "managedPackageVersion"), "managedPackageVersion");
        RequireEqual("0.27.0", RequireString(root, "backendPackageVersion"), "backendPackageVersion");
        RequireEqual(
            "3f7c29d318e317b63f54c558bc69803963d7d88c",
            RequireString(root, "expectedLlamaCppCommit"),
            "expectedLlamaCppCommit");
        RequireEqual("X64", RequireString(root, "processArchitecture"), "processArchitecture", ignoreCase: true);

        JsonElement backend = RequireObject(root, "selectedBackend");
        RequireFalse(RequireBoolean(backend, "usesCuda"), "selectedBackend.usesCuda");
        RequireFalse(RequireBoolean(backend, "usesVulkan"), "selectedBackend.usesVulkan");
        if (string.IsNullOrWhiteSpace(RequireString(backend, "nativeLibraryName")))
        {
            throw new InvalidDataException("selectedBackend.nativeLibraryName is blank.");
        }

        JsonElement model = RequireObject(root, "modelEvidence");
        RequireEqual(manifest.Architecture, RequireString(model, "architecture"), "modelEvidence.architecture");
        RequireEqual(manifest.ModelName, RequireString(model, "modelName"), "modelEvidence.modelName");
        RequireEqual(manifest.FileType, RequireString(model, "fileType"), "modelEvidence.fileType");
        RequireEqual(
            manifest.QuantizationVersion,
            RequireString(model, "quantizationVersion"),
            "modelEvidence.quantizationVersion");
        RequireEqual(
            manifest.TokenizerModel,
            RequireString(model, "tokenizerModel"),
            "modelEvidence.tokenizerModel");
        RequireEqual(manifest.ContextSize, RequireInt32(model, "contextSize"), "modelEvidence.contextSize");
        RequireEqual(manifest.EmbeddingSize, RequireInt32(model, "embeddingSize"), "modelEvidence.embeddingSize");
        RequireEqual(manifest.LayerCount, RequireInt32(model, "layerCount"), "modelEvidence.layerCount");
        RequireEqual(manifest.HeadCount, RequireInt32(model, "headCount"), "modelEvidence.headCount");
        RequireEqual(manifest.KvHeadCount, RequireInt32(model, "kvHeadCount"), "modelEvidence.kvHeadCount");
        RequireEqual(manifest.MetadataCount, RequireInt32(model, "metadataCount"), "modelEvidence.metadataCount");
        RequireNull(model, "parameterCount");

        JsonElement vocabulary = RequireObject(model, "vocabulary");
        RequireEqual(manifest.VocabularyCount, RequireInt32(vocabulary, "count"), "modelEvidence.vocabulary.count");

        JsonElement tokenizerSmoke = RequireObject(model, "tokenizerSmoke");
        RequireTrue(RequireBoolean(tokenizerSmoke, "succeeded"), "modelEvidence.tokenizerSmoke.succeeded");
        RequireEqual(
            manifest.TokenizerSmokeTokenCount,
            RequireInt32(tokenizerSmoke, "tokenCount"),
            "modelEvidence.tokenizerSmoke.tokenCount");

        JsonElement chatTemplate = RequireObject(model, "chatTemplate");
        RequireEqual(
            manifest.ChatTemplatePresent,
            RequireBoolean(chatTemplate, "present"),
            "modelEvidence.chatTemplate.present");

        JsonElement progress = RequireArray(root, "progressSamples");
        if (progress.GetArrayLength() < 1)
        {
            throw new InvalidDataException("Successful probe did not record native progress.");
        }
        AssertProgressFractions(progress);

        RequireNonNegativeInt64(root, "durationMilliseconds");
        RequireNonNegativeInt64(root, "loadDurationMilliseconds");
        RequireTrue(
            RequireBoolean(root, "nativeHandleClosedAfterDispose"),
            "nativeHandleClosedAfterDispose");

        JsonElement integrity = RequireObject(root, "integrity");
        RequireTrue(RequireBoolean(integrity, "isPreserved"), "integrity.isPreserved");

        JsonElement before = RequireObject(root, "beforeSnapshot");
        JsonElement after = RequireObject(root, "afterSnapshot");
        RequireEqual(manifest.Sha256, RequireString(before, "sha256"), "beforeSnapshot.sha256");
        RequireEqual(manifest.Sha256, RequireString(after, "sha256"), "afterSnapshot.sha256");
        RequireEqual(manifest.LengthBytes, RequireInt64(before, "lengthBytes"), "beforeSnapshot.lengthBytes");
        RequireEqual(manifest.LengthBytes, RequireInt64(after, "lengthBytes"), "afterSnapshot.lengthBytes");

        RequireNull(root, "failureCode");
        RequireNull(root, "failureType");
        RequireNull(root, "failureMessage");
    }

    public static void AssertCancelled(
        JsonElement root,
        ControlledModelManifest manifest,
        bool requireSelectedBackend)
    {
        ArgumentNullException.ThrowIfNull(manifest);

        RequireEqual("Cancelled", RequireString(root, "completionStatus"), "completionStatus");
        RequireFalse(RequireBoolean(root, "succeeded"), "succeeded");
        RequireEqual("MI-PROBE-CANCELLED", RequireString(root, "failureCode"), "failureCode");

        if (requireSelectedBackend)
        {
            JsonElement backend = RequireObject(root, "selectedBackend");
            RequireFalse(RequireBoolean(backend, "usesCuda"), "selectedBackend.usesCuda");
            RequireFalse(RequireBoolean(backend, "usesVulkan"), "selectedBackend.usesVulkan");
        }

        JsonElement integrity = RequireObject(root, "integrity");
        RequireTrue(RequireBoolean(integrity, "isPreserved"), "integrity.isPreserved");
        RequireEqual(
            manifest.Sha256,
            RequireString(RequireObject(root, "beforeSnapshot"), "sha256"),
            "beforeSnapshot.sha256");
        RequireEqual(
            manifest.Sha256,
            RequireString(RequireObject(root, "afterSnapshot"), "sha256"),
            "afterSnapshot.sha256");

        if (root.TryGetProperty(
                "nativeHandleClosedAfterDispose",
                out JsonElement closed) &&
            closed.ValueKind is not JsonValueKind.Null)
        {
            RequireTrue(closed.GetBoolean(), "nativeHandleClosedAfterDispose");
        }

        AssertProgressFractions(RequireArray(root, "progressSamples"));
    }

    public static void AssertDoesNotContainCanonicalPath(
        string value,
        string canonicalPath)
    {
        ArgumentNullException.ThrowIfNull(value);
        ArgumentException.ThrowIfNullOrWhiteSpace(canonicalPath);

        StringComparison comparison = OperatingSystem.IsWindows()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;

        if (value.Contains(canonicalPath, comparison))
        {
            throw new InvalidDataException(
                "Output exposed the canonical model path.");
        }
    }

    public static void AssertNoGgufFiles(string directory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);

        if (!Directory.Exists(directory))
        {
            throw new DirectoryNotFoundException(
                $"Evidence directory does not exist: {directory}");
        }

        string[] ggufFiles = Directory.GetFiles(
            directory,
            "*.gguf",
            SearchOption.AllDirectories);

        if (ggufFiles.Length > 0)
        {
            throw new InvalidDataException(
                "Evidence directory contains GGUF files: " +
                string.Join(", ", ggufFiles.Select(Path.GetFileName)));
        }
    }

    public static string RequireString(
        JsonElement element,
        string propertyName)
    {
        JsonElement property = RequireProperty(element, propertyName);
        if (property.ValueKind != JsonValueKind.String)
        {
            throw new InvalidDataException(
                $"Evidence property '{propertyName}' is not a string.");
        }

        return property.GetString() ?? string.Empty;
    }

    public static bool RequireBoolean(
        JsonElement element,
        string propertyName)
    {
        JsonElement property = RequireProperty(element, propertyName);
        if (property.ValueKind is not
            (JsonValueKind.True or JsonValueKind.False))
        {
            throw new InvalidDataException(
                $"Evidence property '{propertyName}' is not Boolean.");
        }

        return property.GetBoolean();
    }

    private static JsonElement RequireProperty(
        JsonElement element,
        string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out JsonElement property))
        {
            throw new InvalidDataException(
                $"Evidence property '{propertyName}' is missing.");
        }

        return property;
    }

    private static JsonElement RequireObject(
        JsonElement element,
        string propertyName)
    {
        JsonElement property = RequireProperty(element, propertyName);
        if (property.ValueKind != JsonValueKind.Object)
        {
            throw new InvalidDataException(
                $"Evidence property '{propertyName}' is not an object.");
        }

        return property;
    }

    private static JsonElement RequireArray(
        JsonElement element,
        string propertyName)
    {
        JsonElement property = RequireProperty(element, propertyName);
        if (property.ValueKind != JsonValueKind.Array)
        {
            throw new InvalidDataException(
                $"Evidence property '{propertyName}' is not an array.");
        }

        return property;
    }

    private static int RequireInt32(
        JsonElement element,
        string propertyName)
    {
        JsonElement property = RequireProperty(element, propertyName);
        if (!property.TryGetInt32(out int value))
        {
            throw new InvalidDataException(
                $"Evidence property '{propertyName}' is not an Int32.");
        }

        return value;
    }

    private static long RequireInt64(
        JsonElement element,
        string propertyName)
    {
        JsonElement property = RequireProperty(element, propertyName);
        if (!property.TryGetInt64(out long value))
        {
            throw new InvalidDataException(
                $"Evidence property '{propertyName}' is not an Int64.");
        }

        return value;
    }

    private static void RequireNonNegativeInt64(
        JsonElement element,
        string propertyName)
    {
        if (RequireInt64(element, propertyName) < 0)
        {
            throw new InvalidDataException(
                $"Evidence property '{propertyName}' is negative.");
        }
    }

    private static void RequireNull(
        JsonElement element,
        string propertyName)
    {
        if (RequireProperty(element, propertyName).ValueKind != JsonValueKind.Null)
        {
            throw new InvalidDataException(
                $"Evidence property '{propertyName}' must be null.");
        }
    }

    private static void AssertProgressFractions(JsonElement progress)
    {
        foreach (JsonElement sample in progress.EnumerateArray())
        {
            JsonElement fractionElement = RequireProperty(sample, "fraction");
            if (!fractionElement.TryGetSingle(out float fraction) ||
                !float.IsFinite(fraction) ||
                fraction < 0f ||
                fraction > 1f)
            {
                throw new InvalidDataException(
                    "Progress evidence contains an invalid fraction.");
            }

            if (RequireInt64(sample, "elapsedMilliseconds") < 0)
            {
                throw new InvalidDataException(
                    "Progress evidence contains negative elapsed time.");
            }
        }
    }

    private static void RequireTrue(bool value, string name)
    {
        if (!value)
        {
            throw new InvalidDataException($"Expected '{name}' to be true.");
        }
    }

    private static void RequireFalse(bool value, string name)
    {
        if (value)
        {
            throw new InvalidDataException($"Expected '{name}' to be false.");
        }
    }

    private static void RequireEqual<T>(
        T expected,
        T actual,
        string name,
        bool ignoreCase = false)
    {
        bool equal = expected is string expectedString &&
                     actual is string actualString
            ? string.Equals(
                expectedString,
                actualString,
                ignoreCase
                    ? StringComparison.OrdinalIgnoreCase
                    : StringComparison.Ordinal)
            : EqualityComparer<T>.Default.Equals(expected, actual);

        if (!equal)
        {
            throw new InvalidDataException(
                $"Unexpected '{name}'. Expected '{expected}', actual '{actual}'.");
        }
    }
}
