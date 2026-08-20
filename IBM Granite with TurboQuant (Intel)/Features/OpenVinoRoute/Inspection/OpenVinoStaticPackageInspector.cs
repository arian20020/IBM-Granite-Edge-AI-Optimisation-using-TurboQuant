using System.Globalization;
using System.Diagnostics.CodeAnalysis;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Xml;
using GraniteEdgeAI.OpenVino.Contracts;

namespace GraniteEdgeAI.Features.OpenVinoRoute.Inspection;

public sealed class OpenVinoStaticPackageInspector
{
    private static readonly UTF8Encoding StrictUtf8 = new(false, true);
    private static readonly JsonDocumentOptions JsonOptions = new()
    {
        AllowTrailingCommas = false,
        CommentHandling = JsonCommentHandling.Disallow,
        MaxDepth = OpenVinoPackagePolicy.MaximumJsonDepth
    };

    private static readonly HashSet<string> ConfigProperties = new(StringComparer.Ordinal)
    {
        "architectures", "attention_bias", "attention_dropout", "auto_map", "bos_token_id", "eos_token_id",
        "hidden_size", "initializer_range", "intermediate_size", "max_position_embeddings", "mlp_bias",
        "model_type", "num_attention_heads", "num_hidden_layers", "num_key_value_heads", "pad_token_id",
        "residual_multiplier", "rope_theta", "task", "tie_word_embeddings", "torch_dtype", "trust_remote_code",
        "transformers_version", "use_cache", "vocab_size"
    };

    private static readonly HashSet<string> GenerationProperties = new(StringComparer.Ordinal)
    {
        "bos_token_id", "do_sample", "eos_token_id", "max_new_tokens", "pad_token_id"
    };

    private static readonly HashSet<string> TokenizerProperties = new(StringComparer.Ordinal)
    {
        "add_bos_token", "add_eos_token", "bos_token", "eos_token", "model_max_length", "pad_token", "tokenizer_class"
    };

    private static readonly HashSet<string> ConfigStringProperties = new(StringComparer.Ordinal)
    {
        "model_type", "task", "torch_dtype", "transformers_version"
    };

    private static readonly HashSet<string> ConfigBooleanProperties = new(StringComparer.Ordinal)
    {
        "attention_bias", "mlp_bias", "tie_word_embeddings", "use_cache"
    };

    private static readonly HashSet<string> ConfigFloatingPointProperties = new(StringComparer.Ordinal)
    {
        "attention_dropout", "initializer_range", "residual_multiplier", "rope_theta"
    };

    [SuppressMessage("Performance", "CA1822:Mark members as static", Justification = "The instance boundary permits operation-scoped inspector composition.")]
    public OpenVinoStaticPackageInspectionResult Inspect(string packageRoot)
    {
        OpenVinoPackageSnapshotCapture capture = new OpenVinoPackageSnapshotter().Capture(packageRoot);
        if (capture.Snapshot is null)
        {
            return RejectSnapshotFailure(capture.Failure);
        }

        using OpenVinoPackageSnapshot snapshot = capture.Snapshot;
        foreach (string required in OpenVinoPackagePolicy.RequiredResources)
        {
            if (!snapshot.TryGetEntry(required, out OpenVinoPackageSnapshotEntry entry))
            {
                return OpenVinoStaticPackageInspectionResult.Rejected(OpenVinoSupportCode.PackageMissingResource);
            }

            if (entry.Length <= 0)
            {
                return OpenVinoStaticPackageInspectionResult.Rejected(OpenVinoSupportCode.PackageInconsistentResource);
            }
        }

        try
        {
            using JsonDocument config = ReadJson(snapshot, "config.json", ConfigProperties);
            using JsonDocument generation = ReadJson(snapshot, "generation_config.json", GenerationProperties);
            using JsonDocument tokenizer = ReadJson(snapshot, "tokenizer_config.json", TokenizerProperties);
            foreach (OpenVinoPackageSnapshotEntry optionalJson in snapshot.Entries.Where(static entry =>
                OpenVinoPackagePolicy.IsJsonResource(entry.RelativeName) &&
                !OpenVinoPackagePolicy.IsRequiredResource(entry.RelativeName)))
            {
                using JsonDocument _ = ReadJson(optionalJson, allowedProperties: null);
            }

            JsonElement configRoot = config.RootElement;
            JsonElement generationRoot = generation.RootElement;
            JsonElement tokenizerRoot = tokenizer.RootElement;
            if (configRoot.TryGetProperty("auto_map", out _) ||
                configRoot.TryGetProperty("trust_remote_code", out _))
            {
                return OpenVinoStaticPackageInspectionResult.Rejected(OpenVinoSupportCode.ModelArchitectureUnsupported);
            }

            ValidateConfigurationShape(configRoot, generationRoot, tokenizerRoot);

            string modelType = RequiredString(configRoot, "model_type");
            string architecture = RequiredSingleString(configRoot, "architectures");
            if (!string.Equals(modelType, "granite", StringComparison.Ordinal) ||
                !string.Equals(architecture, "GraniteForCausalLM", StringComparison.Ordinal))
            {
                return OpenVinoStaticPackageInspectionResult.Rejected(OpenVinoSupportCode.ModelArchitectureUnsupported);
            }

            string task = configRoot.TryGetProperty("task", out JsonElement taskElement)
                ? RequiredString(taskElement)
                : "text-generation-with-past";
            if (!string.Equals(task, "text-generation-with-past", StringComparison.Ordinal))
            {
                return OpenVinoStaticPackageInspectionResult.Rejected(OpenVinoSupportCode.ModelTaskUnsupported);
            }

            long contextLength = RequiredPositiveInt64(configRoot, "max_position_embeddings");
            string precision = RequiredString(configRoot, "torch_dtype");
            if (contextLength > OpenVinoPackagePolicy.MaximumContextLength ||
                precision is not ("float32" or "float16" or "bfloat16"))
            {
                return OpenVinoStaticPackageInspectionResult.Rejected(OpenVinoSupportCode.PackageInconsistentResource);
            }

            string tokenizerClass = RequiredString(tokenizerRoot, "tokenizer_class");
            long tokenizerContext = RequiredPositiveInt64(tokenizerRoot, "model_max_length");
            if (tokenizerClass is not ("PreTrainedTokenizerFast" or "GPT2TokenizerFast") || tokenizerContext != contextLength)
            {
                return OpenVinoStaticPackageInspectionResult.Rejected(OpenVinoSupportCode.TokenizerUnsupported);
            }

            if (!TokenIdentifiersAgree(configRoot, generationRoot) ||
                RequiredPositiveInt64(generationRoot, "max_new_tokens") > 512)
            {
                return OpenVinoStaticPackageInspectionResult.Rejected(OpenVinoSupportCode.TokenizerUnsupported);
            }

            if (!ValidateXml(snapshot, "openvino_model.xml", "openvino_model.bin", ["Parameter", "Select", "Result"]) ||
                !ValidateXml(snapshot, "openvino_tokenizer.xml", "openvino_tokenizer.bin", ["Parameter", "StringTensorUnpack", "Result"]) ||
                !ValidateXml(snapshot, "openvino_detokenizer.xml", "openvino_detokenizer.bin", ["Parameter", "VocabDecoder", "Result"]))
            {
                return OpenVinoStaticPackageInspectionResult.Rejected(OpenVinoSupportCode.PackageInconsistentResource);
            }

            OpenVinoPackageSnapshotEntry model = GetRequired(snapshot, "openvino_model.bin");
            bool hasChatTemplate = snapshot.TryGetEntry("chat_template.json", out _) ||
                snapshot.TryGetEntry("chat_template.jinja", out _);
            return OpenVinoStaticPackageInspectionResult.NativeValidationRequired(new OpenVinoStaticPackageEvidence(
                OpenVinoPackagePolicy.PolicyVersion,
                ComputeManifestDigest(snapshot.Entries),
                model.Sha256,
                model.Length,
                modelType,
                architecture,
                task,
                contextLength,
                precision,
                tokenizerClass,
                snapshot.Entries.Count,
                hasChatTemplate));
        }
        catch (Exception exception) when (exception is JsonException or DecoderFallbackException or XmlException or FormatException or OverflowException or InvalidDataException)
        {
            return OpenVinoStaticPackageInspectionResult.Rejected(OpenVinoSupportCode.PackageInconsistentResource);
        }
    }

    internal static OpenVinoStaticPackageInspectionResult RejectSnapshotFailure(OpenVinoSnapshotFailure failure) =>
        OpenVinoStaticPackageInspectionResult.Rejected(failure switch
        {
            OpenVinoSnapshotFailure.RootMissing => OpenVinoSupportCode.PackageMissingResource,
            OpenVinoSnapshotFailure.Changed => OpenVinoSupportCode.PackageChanged,
            OpenVinoSnapshotFailure.Unreadable => OpenVinoSupportCode.PackageUnreadable,
            OpenVinoSnapshotFailure.ReparsePoint or
            OpenVinoSnapshotFailure.EscapedRoot or
            OpenVinoSnapshotFailure.CaseCollision or
            OpenVinoSnapshotFailure.AlternateDataStream or
            OpenVinoSnapshotFailure.NonRegularArtifact or
            OpenVinoSnapshotFailure.ExecutableOrScript or
            OpenVinoSnapshotFailure.EntryLimitExceeded or
            OpenVinoSnapshotFailure.DepthLimitExceeded => OpenVinoSupportCode.PackageUnsafePath,
            OpenVinoSnapshotFailure.UnrecognizedResource or
            OpenVinoSnapshotFailure.JsonTooLarge or
            OpenVinoSnapshotFailure.XmlTooLarge => OpenVinoSupportCode.PackageInconsistentResource,
            _ => OpenVinoSupportCode.PackageInconsistentResource
        });

    private static JsonDocument ReadJson(
        OpenVinoPackageSnapshot snapshot,
        string name,
        HashSet<string>? allowedProperties)
    {
        return ReadJson(GetRequired(snapshot, name), allowedProperties);
    }

    private static JsonDocument ReadJson(OpenVinoPackageSnapshotEntry entry, HashSet<string>? allowedProperties)
    {
        if (entry.Length <= 0 || entry.Length > OpenVinoPackagePolicy.MaximumJsonBytes || entry.Length > int.MaxValue)
        {
            throw new InvalidDataException("JSON resource length is invalid.");
        }

        byte[] bytes = new byte[checked((int)entry.Length)];
        entry.Stream.Position = 0;
        entry.Stream.ReadExactly(bytes);
        entry.Stream.Position = 0;
        string json = StrictUtf8.GetString(bytes);
        JsonDocument document = JsonDocument.Parse(json, JsonOptions);
        try
        {
            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                throw new InvalidDataException("JSON resource root must be an object.");
            }

            RejectDuplicateProperties(document.RootElement);
            RejectNullValues(document.RootElement);
            if (allowedProperties is not null)
            {
                foreach (JsonProperty property in document.RootElement.EnumerateObject())
                {
                    if (!allowedProperties.Contains(property.Name))
                    {
                        throw new InvalidDataException("JSON resource contains an unrecognized property.");
                    }
                }
            }

            return document;
        }
        catch
        {
            document.Dispose();
            throw;
        }
    }

    private static void RejectDuplicateProperties(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            HashSet<string> names = new(StringComparer.Ordinal);
            foreach (JsonProperty property in element.EnumerateObject())
            {
                if (!names.Add(property.Name))
                {
                    throw new InvalidDataException("JSON resource contains a duplicate property.");
                }

                RejectDuplicateProperties(property.Value);
            }
        }
        else if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (JsonElement item in element.EnumerateArray())
            {
                RejectDuplicateProperties(item);
            }
        }
    }

    private static void RejectNullValues(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Null)
        {
            throw new InvalidDataException("JSON resource contains a null value.");
        }

        if (element.ValueKind == JsonValueKind.Object)
        {
            foreach (JsonProperty property in element.EnumerateObject())
            {
                RejectNullValues(property.Value);
            }
        }
        else if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (JsonElement item in element.EnumerateArray())
            {
                RejectNullValues(item);
            }
        }
    }

    private static void ValidateConfigurationShape(JsonElement config, JsonElement generation, JsonElement tokenizer)
    {
        ValidateConfigPropertyTypes(config);
        _ = RequiredString(config, "model_type");
        _ = RequiredSingleString(config, "architectures");
        _ = RequiredPositiveInt64(config, "vocab_size");
        _ = RequiredPositiveInt64(config, "max_position_embeddings");
        _ = RequiredString(config, "torch_dtype");
        RequireNonNegativeToken(config, "bos_token_id");
        RequireNonNegativeToken(config, "eos_token_id");
        RequireNonNegativeToken(config, "pad_token_id");

        _ = RequiredPositiveInt64(generation, "max_new_tokens");
        RequireBoolean(generation, "do_sample");
        RequireNonNegativeToken(generation, "bos_token_id");
        RequireNonNegativeToken(generation, "eos_token_id");
        RequireNonNegativeToken(generation, "pad_token_id");

        _ = RequiredPositiveInt64(tokenizer, "model_max_length");
        _ = RequiredString(tokenizer, "tokenizer_class");
        _ = RequiredString(tokenizer, "bos_token");
        _ = RequiredString(tokenizer, "eos_token");
        _ = RequiredString(tokenizer, "pad_token");
        RequireBoolean(tokenizer, "add_bos_token");
        RequireBoolean(tokenizer, "add_eos_token");
    }

    private static void ValidateConfigPropertyTypes(JsonElement config)
    {
        foreach (JsonProperty property in config.EnumerateObject())
        {
            JsonElement value = property.Value;
            if (string.Equals(property.Name, "architectures", StringComparison.Ordinal))
            {
                _ = RequiredSingleString(config, property.Name);
            }
            else if (ConfigStringProperties.Contains(property.Name))
            {
                _ = RequiredString(value);
            }
            else if (ConfigBooleanProperties.Contains(property.Name))
            {
                if (value.ValueKind is not (JsonValueKind.True or JsonValueKind.False))
                {
                    throw new InvalidDataException("JSON configuration boolean has an invalid type.");
                }
            }
            else if (ConfigFloatingPointProperties.Contains(property.Name))
            {
                if (value.ValueKind != JsonValueKind.Number ||
                    !value.TryGetDouble(out double number) ||
                    !double.IsFinite(number))
                {
                    throw new InvalidDataException("JSON configuration number must be finite.");
                }
            }
            else if (value.ValueKind != JsonValueKind.Number || !value.TryGetInt64(out _))
            {
                throw new InvalidDataException("JSON configuration integer has an invalid type.");
            }
        }
    }

    private static bool TokenIdentifiersAgree(JsonElement config, JsonElement generation)
    {
        long vocabularySize = RequiredPositiveInt64(config, "vocab_size");
        foreach (string property in new[] { "bos_token_id", "eos_token_id", "pad_token_id" })
        {
            long configValue = RequireNonNegativeToken(config, property);
            long generationValue = RequireNonNegativeToken(generation, property);
            if (configValue != generationValue || configValue >= vocabularySize)
            {
                return false;
            }
        }

        return true;
    }

    private static string RequiredSingleString(JsonElement parent, string propertyName)
    {
        if (!parent.TryGetProperty(propertyName, out JsonElement value) || value.ValueKind != JsonValueKind.Array)
        {
            throw new InvalidDataException("JSON property must be a one-item string array.");
        }

        JsonElement.ArrayEnumerator values = value.EnumerateArray();
        if (!values.MoveNext())
        {
            throw new InvalidDataException("JSON property must be a one-item string array.");
        }

        string result = RequiredString(values.Current);
        if (values.MoveNext())
        {
            throw new InvalidDataException("JSON property must be a one-item string array.");
        }

        return result;
    }

    private static string RequiredString(JsonElement parent, string propertyName)
    {
        if (!parent.TryGetProperty(propertyName, out JsonElement value))
        {
            throw new InvalidDataException("JSON string property is missing.");
        }

        return RequiredString(value);
    }

    private static string RequiredString(JsonElement value)
    {
        if (value.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(value.GetString()))
        {
            throw new InvalidDataException("JSON property must be a non-empty string.");
        }

        return value.GetString()!;
    }

    private static long RequiredPositiveInt64(JsonElement parent, string propertyName)
    {
        if (!parent.TryGetProperty(propertyName, out JsonElement value) ||
            value.ValueKind != JsonValueKind.Number ||
            !value.TryGetInt64(out long result) ||
            result <= 0)
        {
            throw new InvalidDataException("JSON property must be a positive Int64.");
        }

        return result;
    }

    private static long RequireNonNegativeToken(JsonElement parent, string propertyName)
    {
        if (!parent.TryGetProperty(propertyName, out JsonElement value) ||
            value.ValueKind != JsonValueKind.Number ||
            !value.TryGetInt64(out long result) ||
            result < 0)
        {
            throw new InvalidDataException("JSON token identifier must be a non-negative Int64.");
        }

        return result;
    }

    private static void RequireBoolean(JsonElement parent, string propertyName)
    {
        if (!parent.TryGetProperty(propertyName, out JsonElement value) ||
            value.ValueKind is not (JsonValueKind.True or JsonValueKind.False))
        {
            throw new InvalidDataException("JSON property must be a boolean.");
        }
    }

    private static bool ValidateXml(
        OpenVinoPackageSnapshot snapshot,
        string xmlName,
        string binName,
        IReadOnlyCollection<string> requiredLayerTypes)
    {
        OpenVinoPackageSnapshotEntry xml = GetRequired(snapshot, xmlName);
        OpenVinoPackageSnapshotEntry binary = GetRequired(snapshot, binName);
        if (xml.Length <= 0 || xml.Length > OpenVinoPackagePolicy.MaximumXmlBytes || binary.Length <= 0)
        {
            return false;
        }

        XmlReaderSettings settings = new()
        {
            CloseInput = false,
            DtdProcessing = DtdProcessing.Prohibit,
            IgnoreComments = false,
            IgnoreProcessingInstructions = false,
            MaxCharactersFromEntities = 0,
            MaxCharactersInDocument = OpenVinoPackagePolicy.MaximumXmlBytes,
            ValidationType = ValidationType.None,
            XmlResolver = null
        };
        HashSet<string> observedTypes = new(StringComparer.Ordinal);
        bool validRoot = false;
        bool insideConstant = false;
        xml.Stream.Position = 0;
        using StreamReader text = new(xml.Stream, StrictUtf8, detectEncodingFromByteOrderMarks: false, 64 * 1024, leaveOpen: true);
        using XmlReader reader = XmlReader.Create(text, settings);
        while (reader.Read())
        {
            if (reader.NodeType == XmlNodeType.DocumentType)
            {
                return false;
            }

            if (reader.NodeType == XmlNodeType.Element)
            {
                if (reader.Depth == 0)
                {
                    validRoot = string.Equals(reader.LocalName, "net", StringComparison.Ordinal) &&
                        !string.IsNullOrWhiteSpace(reader.GetAttribute("name"));
                }

                if (reader.HasAttributes)
                {
                    while (reader.MoveToNextAttribute())
                    {
                        if (reader.LocalName is "schemaLocation" or "noNamespaceSchemaLocation" ||
                            reader.Value.Contains("://", StringComparison.OrdinalIgnoreCase) ||
                            reader.Value.StartsWith("file:", StringComparison.OrdinalIgnoreCase))
                        {
                            return false;
                        }
                    }

                    reader.MoveToElement();
                }

                if (string.Equals(reader.LocalName, "layer", StringComparison.Ordinal))
                {
                    string? layerType = reader.GetAttribute("type");
                    insideConstant = string.Equals(layerType, "Const", StringComparison.Ordinal);
                    if (!string.IsNullOrEmpty(layerType))
                    {
                        observedTypes.Add(layerType);
                    }
                }
                else if (insideConstant && string.Equals(reader.LocalName, "data", StringComparison.Ordinal))
                {
                    if (!long.TryParse(reader.GetAttribute("offset"), NumberStyles.None, CultureInfo.InvariantCulture, out long offset) ||
                        !long.TryParse(reader.GetAttribute("size"), NumberStyles.None, CultureInfo.InvariantCulture, out long size) ||
                        offset < 0 || size < 0 || offset > binary.Length || size > binary.Length - offset)
                    {
                        return false;
                    }
                }
            }
            else if (reader.NodeType == XmlNodeType.EndElement && string.Equals(reader.LocalName, "layer", StringComparison.Ordinal))
            {
                insideConstant = false;
            }
        }

        xml.Stream.Position = 0;
        return validRoot && requiredLayerTypes.All(observedTypes.Contains);
    }

    private static OpenVinoPackageSnapshotEntry GetRequired(OpenVinoPackageSnapshot snapshot, string name)
    {
        if (!snapshot.TryGetEntry(name, out OpenVinoPackageSnapshotEntry entry))
        {
            throw new InvalidDataException("Required package resource is absent.");
        }

        return entry;
    }

    private static string ComputeManifestDigest(IEnumerable<OpenVinoPackageSnapshotEntry> entries)
    {
        using IncrementalHash manifest = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        foreach (OpenVinoPackageSnapshotEntry entry in entries.OrderBy(static entry => entry.RelativeName, StringComparer.Ordinal))
        {
            string tuple = entry.RelativeName + '\0' +
                entry.Length.ToString(CultureInfo.InvariantCulture) + '\0' +
                entry.Sha256 + '\n';
            manifest.AppendData(Encoding.UTF8.GetBytes(tuple));
        }

        return Convert.ToHexString(manifest.GetHashAndReset()).ToLowerInvariant();
    }
}
