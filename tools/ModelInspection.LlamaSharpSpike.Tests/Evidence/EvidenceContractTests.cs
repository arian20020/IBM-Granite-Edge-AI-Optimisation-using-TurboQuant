using System.Reflection;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.Json.Serialization;
using GraniteEdgeAI.ModelInspection.LlamaSharp.ModelProbe;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace GraniteEdgeAI.Tools.ModelInspection.LlamaSharpSpike.Tests;

/// <summary>
/// Verifies that structured probe evidence remains versioned, privacy-safe and
/// independent from LLamaSharp, native handles, streams and UI types.
/// </summary>
[TestClass]
[TestCategory("Deterministic")]
public sealed class EvidenceContractTests
{
    [TestMethod]
    public void VocabOnlyResult_UsesExpectedSchemaAndNullableUnavailableValues()
    {
        var result = new VocabOnlyModelProbeResult();
        var modelEvidence = new VocabOnlyRuntimeModelEvidence();

        Assert.AreEqual("1.1", result.SchemaVersion);
        Assert.AreEqual("VocabOnly", result.ProbeMode);
        Assert.IsTrue(result.VocabOnlyRequested);
        Assert.IsNull(modelEvidence.RuntimeReportedSizeBytes);
        Assert.IsNull(modelEvidence.ParameterCount);
        Assert.IsNull(modelEvidence.ContextSize);
        Assert.IsNull(modelEvidence.EmbeddingSize);
        Assert.IsNull(modelEvidence.LayerCount);
        Assert.IsNull(modelEvidence.HeadCount);
        Assert.IsNull(modelEvidence.KvHeadCount);
    }

    [TestMethod]
    public void VocabOnlyResultGraph_ContainsOnlyProjectOwnedOrFrameworkSafeTypes()
    {
        Type[] forbiddenExactTypes =
        {
            typeof(IntPtr),
            typeof(UIntPtr),
            typeof(Stream),
            typeof(Exception)
        };

        foreach (Type type in WalkProjectTypes(typeof(VocabOnlyModelProbeResult)))
        {
            string assemblyName = type.Assembly.GetName().Name ?? string.Empty;

            Assert.IsFalse(
                assemblyName.StartsWith("LLama", StringComparison.OrdinalIgnoreCase),
                $"Evidence type {type.FullName} belongs to LLamaSharp assembly {assemblyName}.");
            Assert.IsFalse(
                typeof(SafeHandle).IsAssignableFrom(type),
                $"Evidence type {type.FullName} must not be a native SafeHandle.");
            Assert.IsFalse(
                forbiddenExactTypes.Contains(type),
                $"Evidence type {type.FullName} is not safe for structured evidence.");

            string? typeNamespace = type.Namespace;
            Assert.IsFalse(
                typeNamespace?.StartsWith(
                    "Microsoft.UI.Xaml",
                    StringComparison.Ordinal) == true,
                $"Evidence type {type.FullName} must not depend on XAML.");
        }
    }

    [TestMethod]
    public void VocabOnlyResultGraph_DoesNotExposeSensitivePropertyNames()
    {
        string[] forbiddenNames =
        {
            "ModelPath",
            "CanonicalModelPath",
            "ChatTemplateText",
            "NativeHandle",
            "NativePointer"
        };

        PropertyInfo[] properties = WalkProjectTypes(
                typeof(VocabOnlyModelProbeResult))
            .SelectMany(type => type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
            .ToArray();

        foreach (string forbiddenName in forbiddenNames)
        {
            Assert.IsFalse(
                properties.Any(property => string.Equals(
                    property.Name,
                    forbiddenName,
                    StringComparison.OrdinalIgnoreCase)),
                $"Evidence graph must not expose property '{forbiddenName}'.");
        }
    }

    [TestMethod]
    public void SerializedResult_DoesNotContainFullChatTemplateOrModelPathProperty()
    {
        const string template =
            "{% for message in messages %}{{ message.content }}{% endfor %}";
        ChatTemplateEvidence templateEvidence =
            ChatTemplateEvidenceFactory.Create(
                new Dictionary<string, string>
                {
                    ["tokenizer.chat_template"] = template
                });

        var result = new VocabOnlyModelProbeResult
        {
            CompletionStatus = VocabOnlyProbeCompletionStatus.Succeeded,
            ModelEvidence = new VocabOnlyRuntimeModelEvidence
            {
                Architecture = "granite",
                ChatTemplate = templateEvidence
            }
        };

        string json = JsonSerializer.Serialize(
            result,
            new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                Converters = { new JsonStringEnumConverter() }
            });

        Assert.IsFalse(
            json.Contains(template, StringComparison.Ordinal));
        Assert.IsFalse(
            json.Contains("\"modelPath\"", StringComparison.OrdinalIgnoreCase));
        StringAssert.Contains(json, "\"sha256\"");
    }

    private static IEnumerable<Type> WalkProjectTypes(Type root)
    {
        var pending = new Stack<Type>();
        var visited = new HashSet<Type>();
        pending.Push(root);

        while (pending.Count > 0)
        {
            Type current = Normalize(pending.Pop());

            if (!visited.Add(current))
            {
                continue;
            }

            yield return current;

            foreach (PropertyInfo property in current.GetProperties(
                BindingFlags.Public | BindingFlags.Instance))
            {
                foreach (Type candidate in Expand(property.PropertyType))
                {
                    Type normalized = Normalize(candidate);

                    if (normalized.Namespace?.StartsWith(
                            "GraniteEdgeAI.",
                            StringComparison.Ordinal) == true)
                    {
                        pending.Push(normalized);
                    }
                    else
                    {
                        yield return normalized;
                    }
                }
            }
        }
    }

    private static IEnumerable<Type> Expand(Type type)
    {
        Type normalized = Normalize(type);
        yield return normalized;

        if (normalized.IsArray)
        {
            Type? elementType = normalized.GetElementType();
            if (elementType is not null)
            {
                yield return Normalize(elementType);
            }
        }

        if (normalized.IsGenericType)
        {
            foreach (Type argument in normalized.GetGenericArguments())
            {
                yield return Normalize(argument);
            }
        }
    }

    private static Type Normalize(Type type)
    {
        return Nullable.GetUnderlyingType(type) ?? type;
    }
}
