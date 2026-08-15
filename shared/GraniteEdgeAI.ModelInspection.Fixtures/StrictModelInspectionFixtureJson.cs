using System.Collections;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using System.Text.RegularExpressions;

namespace GraniteEdgeAI.ModelInspection.Fixtures;

public static partial class StrictModelInspectionFixtureJson
{
    public const int MaximumDocumentBytes = 256 * 1024;
    public const int MaximumDepth = 32;
    public const int MaximumStringLength = 4096;
    public const int MaximumDisplayNameLength = 160;
    public const int MaximumDisplayFileNameLength = 128;
    public const int MaximumCollectionLength = 128;

    private static readonly JsonSerializerOptions SerializerOptions = CreateOptions();

    internal static T Deserialize<T>(ModelInspectionFixtureDocumentSource source)
    {
        ValidateAndScan(source);

        try
        {
            T? value = JsonSerializer.Deserialize<T>(source.Utf8Json.Span, SerializerOptions);
            if (value is null)
            {
                throw Failure(source, "$", "json.null-root");
            }

            ValidateNullability(source, value);
            return value;
        }
        catch (ModelInspectionFixtureValidationException)
        {
            throw;
        }
        catch (JsonException exception)
        {
            throw Failure(source, NormalizePath(exception.Path), "json.invalid");
        }
        catch (NotSupportedException)
        {
            throw Failure(source, "$", "json.unsupported-type");
        }
    }

    internal static JsonDocument ParseDocument(
        ModelInspectionFixtureDocumentSource source)
    {
        ValidateAndScan(source);

        try
        {
            return JsonDocument.Parse(
                source.Utf8Json,
                new JsonDocumentOptions
                {
                    AllowTrailingCommas = false,
                    CommentHandling = JsonCommentHandling.Disallow,
                    MaxDepth = MaximumDepth
                });
        }
        catch (JsonException exception)
        {
            throw Failure(source, NormalizePath(exception.Path), "json.invalid");
        }
    }

    internal static void ValidateAndScan(
        ModelInspectionFixtureDocumentSource source)
    {
        if (source is null)
        {
            throw new ModelInspectionFixtureValidationException(
                "<invalid-filename>",
                "$",
                "document.null");
        }

        int length = source.Utf8Json.Length;
        if (length is <= 0 or > MaximumDocumentBytes)
        {
            throw Failure(source, "$", "json.byte-limit");
        }

        JsonReaderOptions options = new()
        {
            AllowTrailingCommas = false,
            CommentHandling = JsonCommentHandling.Disallow,
            MaxDepth = MaximumDepth
        };
        Utf8JsonReader reader = new(source.Utf8Json.Span, options);
        Stack<HashSet<string>> objectScopes = new();
        bool sawRoot = false;

        try
        {
            while (reader.Read())
            {
                if (!sawRoot)
                {
                    sawRoot = true;
                    if (reader.TokenType != JsonTokenType.StartObject)
                    {
                        throw Failure(source, "$", "json.root-object");
                    }
                }

                switch (reader.TokenType)
                {
                    case JsonTokenType.StartObject:
                        objectScopes.Push(new HashSet<string>(StringComparer.Ordinal));
                        break;

                    case JsonTokenType.PropertyName:
                        {
                            string? propertyName = reader.GetString();
                            if (propertyName is null ||
                                objectScopes.Count == 0 ||
                                !objectScopes.Peek().Add(propertyName))
                            {
                                throw Failure(source, "$", "json.duplicate-property");
                            }

                            break;
                        }

                    case JsonTokenType.EndObject:
                        if (objectScopes.Count == 0)
                        {
                            throw Failure(source, "$", "json.invalid-scope");
                        }

                        objectScopes.Pop();
                        break;
                }
            }
        }
        catch (ModelInspectionFixtureValidationException)
        {
            throw;
        }
        catch (JsonException)
        {
            throw Failure(source, "$", "json.invalid");
        }

        if (!sawRoot || objectScopes.Count != 0)
        {
            throw Failure(source, "$", "json.incomplete");
        }
    }

    internal static ModelInspectionFixtureValidationException Failure(
        ModelInspectionFixtureDocumentSource source,
        string jsonPath,
        string ruleCode) =>
        new(
            SafeDiagnosticFileName(source?.FileName),
            NormalizePath(jsonPath),
            ruleCode);

    internal static string SafeDiagnosticFileName(string? fileName)
    {
        if (string.IsNullOrEmpty(fileName) ||
            fileName.Length > MaximumDisplayFileNameLength ||
            ModelInspectionFixturePrivacyRules.ContainsExplicitIdentityMarker(fileName) ||
            !(fileName.Equals(
                  "model-inspection-fixture.schema.json",
                  StringComparison.Ordinal) ||
              fileName.Equals(
                  "model-inspection-fixture-coverage-policy.json",
                  StringComparison.Ordinal) ||
              DiagnosticFixtureFileNameRegex().IsMatch(fileName)))
        {
            return "<invalid-filename>";
        }

        return fileName;
    }

    private static void ValidateNullability<T>(
        ModelInspectionFixtureDocumentSource source,
        T root)
    {
        var nullability = new NullabilityInfoContext();
        HashSet<object> visited = new(ReferenceEqualityComparer.Instance);
        Visit(root);

        void Visit(object? value)
        {
            if (value is null || value is string || value.GetType().IsValueType)
            {
                return;
            }

            if (!visited.Add(value))
            {
                return;
            }

            if (value is IDictionary dictionary)
            {
                foreach (DictionaryEntry entry in dictionary)
                {
                    Visit(entry.Key);
                    Visit(entry.Value);
                }

                return;
            }

            if (value is IEnumerable enumerable)
            {
                foreach (object? item in enumerable)
                {
                    Visit(item);
                }

                return;
            }

            foreach (PropertyInfo property in value.GetType()
                         .GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                if (property.GetIndexParameters().Length != 0)
                {
                    continue;
                }

                object? propertyValue = property.GetValue(value);
                NullabilityInfo info = nullability.Create(property);
                bool permitsNull = Nullable.GetUnderlyingType(property.PropertyType) is not null ||
                    (!property.PropertyType.IsValueType &&
                     info.ReadState != NullabilityState.NotNull);
                if (propertyValue is null && !permitsNull)
                {
                    throw Failure(source, "$", "json.null-member");
                }

                Visit(propertyValue);
            }
        }
    }

    private static string NormalizePath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path) ||
            path.Length > 256 ||
            path.Any(character =>
                !(character is >= 'A' and <= 'Z' or
                  >= 'a' and <= 'z' or
                  >= '0' and <= '9' or
                  '$' or '.' or '[' or ']' or '\'' or '_' or '-')))
        {
            return "$";
        }

        return path;
    }

    private static JsonSerializerOptions CreateOptions()
    {
        var resolver = new DefaultJsonTypeInfoResolver();
        resolver.Modifiers.Add(typeInfo =>
        {
            if (typeInfo.Kind == JsonTypeInfoKind.Object &&
                typeInfo.Type.Assembly == typeof(ModelInspectionFixtureDescriptor).Assembly)
            {
                foreach (JsonPropertyInfo property in typeInfo.Properties)
                {
                    property.IsRequired = !IsOptionalStartupProperty(
                        typeInfo.Type,
                        property.Name);
                }
            }
        });
        JsonSerializerOptions options = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = false,
            UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
            AllowTrailingCommas = false,
            ReadCommentHandling = JsonCommentHandling.Disallow,
            MaxDepth = MaximumDepth,
            NumberHandling = JsonNumberHandling.Strict,
            TypeInfoResolver = resolver
        };
        options.Converters.Add(new StrictFixtureEnumConverterFactory());
        return options;
    }

    private static bool IsOptionalStartupProperty(
        Type declaringType,
        string propertyName) =>
        declaringType == typeof(ModelInspectionExpectedContentRegion) &&
        propertyName is "startupStatus" or "startupVisible" or
            "startupActive";

    [GeneratedRegex(
        @"^MI-[0-9]{3}-[a-z0-9]+(?:-[a-z0-9]+)*\.fixture\.json$",
        RegexOptions.CultureInvariant)]
    private static partial Regex DiagnosticFixtureFileNameRegex();

    private sealed class StrictFixtureEnumConverterFactory : JsonConverterFactory
    {
        public override bool CanConvert(Type typeToConvert) => typeToConvert.IsEnum;

        public override JsonConverter CreateConverter(
            Type typeToConvert,
            JsonSerializerOptions options)
        {
            Type converterType = typeof(StrictFixtureEnumConverter<>).MakeGenericType(typeToConvert);
            return (JsonConverter)Activator.CreateInstance(converterType)!;
        }
    }

    private sealed class StrictFixtureEnumConverter<TEnum> : JsonConverter<TEnum>
        where TEnum : struct, Enum
    {
        private static readonly IReadOnlyDictionary<string, TEnum> FromJson =
            Enum.GetValues<TEnum>().ToDictionary(ToJsonName, value => value, StringComparer.Ordinal);

        private static readonly IReadOnlyDictionary<TEnum, string> ToJson =
            FromJson.ToDictionary(pair => pair.Value, pair => pair.Key);

        public override TEnum Read(
            ref Utf8JsonReader reader,
            Type typeToConvert,
            JsonSerializerOptions options)
        {
            if (reader.TokenType != JsonTokenType.String)
            {
                throw new JsonException();
            }

            string? text = reader.GetString();
            if (text is null || !FromJson.TryGetValue(text, out TEnum value))
            {
                throw new JsonException();
            }

            return value;
        }

        public override void Write(
            Utf8JsonWriter writer,
            TEnum value,
            JsonSerializerOptions options)
        {
            if (!ToJson.TryGetValue(value, out string? text))
            {
                throw new JsonException();
            }

            writer.WriteStringValue(text);
        }

        private static string ToJsonName(TEnum value)
        {
            if (typeof(TEnum) == typeof(ModelInspectionFixtureSetupStepKind))
            {
                return value.ToString() switch
                {
                    nameof(ModelInspectionFixtureSetupStepKind.ReleaseServiceCheckpoint) =>
                        "release-service-checkpoint",
                    nameof(ModelInspectionFixtureSetupStepKind.InvokeDisclosure) => "invoke-disclosure",
                    nameof(ModelInspectionFixtureSetupStepKind.InvokeCancel) => "invoke-cancel",
                    nameof(ModelInspectionFixtureSetupStepKind.InvokeRetry) => "invoke-retry",
                    nameof(ModelInspectionFixtureSetupStepKind.InvokeRestart) => "invoke-restart",
                    nameof(ModelInspectionFixtureSetupStepKind.InvokeChooseAnother) => "invoke-choose-another",
                    nameof(ModelInspectionFixtureSetupStepKind.ReleaseStaleProgress) => "release-stale-progress",
                    nameof(ModelInspectionFixtureSetupStepKind.SubmitStaleResultSnapshot) =>
                        "submit-stale-result-snapshot",
                    nameof(ModelInspectionFixtureSetupStepKind.ReleaseStaleMotion) => "release-stale-motion",
                    nameof(ModelInspectionFixtureSetupStepKind.ReleaseStaleAnnouncement) =>
                        "release-stale-announcement",
                    nameof(ModelInspectionFixtureSetupStepKind.Observe) => "observe",
                    _ => throw new InvalidOperationException()
                };
            }

            return JsonNamingPolicy.CamelCase.ConvertName(value.ToString());
        }
    }
}

internal static partial class ModelInspectionFixturePrivacyRules
{
    internal static bool ContainsExplicitIdentityMarker(string text) =>
        ExplicitIdentityMarkerRegex().IsMatch(text);

    [GeneratedRegex(
        @"(?:%[^%\r\n]+%|\$\{[^}\r\n]+\}|\$env:[A-Za-z_][A-Za-z0-9_]*|\$(?:HOME|USER|USERNAME|USERPROFILE|COMPUTERNAME|MACHINENAME|HOSTNAME)\b|\{(?:user[ _-]?name|user[ _-]?profile|computer[ _-]?name|machine[ _-]?name|run[ _-]?user)\}|(?<![A-Za-z0-9])(?:user[ _-]?name|user[ _-]?profile|computer[ _-]?name|machine[ _-]?name|run[ _-]?user)(?![A-Za-z0-9])|(?<![A-Za-z0-9])~(?![A-Za-z0-9]))",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex ExplicitIdentityMarkerRegex();
}
