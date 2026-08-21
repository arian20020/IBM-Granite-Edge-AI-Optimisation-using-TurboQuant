using GraniteEdgeAI.Features.OpenVinoRoute;
using GraniteEdgeAI.Features.Prompting;

namespace GraniteEdgeAI.OpenVino.Tests;

[TestClass]
public sealed class PromptRouteContractRedTests
{
    [TestMethod]
    public void RegistryAcceptsBothRouteKindsWithoutRuntimeSpecificContractTypes()
    {
        PromptRouteRegistry registry = new(
        [
            new FakeAdapter(Capability(PromptRouteKind.Gguf, "gguf.local")),
            new FakeAdapter(Capability(PromptRouteKind.OpenVino, "openvino.official"))
        ]);

        Assert.AreEqual("gguf.local",
            registry.GetRequired(PromptRouteKind.Gguf).Capability.RouteId);
        Assert.AreEqual("openvino.official",
            registry.GetRequired(PromptRouteKind.OpenVino).Capability.RouteId);

        Type neutralContract = typeof(IPromptRouteAdapter);
        string[] forbidden = neutralContract.Assembly.GetTypes()
            .Where(type => type.Namespace == "GraniteEdgeAI.Features.Prompting")
            .SelectMany(PublicApiTypes)
            .SelectMany(SignatureTypes)
            .Select(type => $"{type.Namespace}.{type.Name}")
            .Where(name => name.Contains("OpenVino", StringComparison.OrdinalIgnoreCase) ||
                name.Contains("Llama", StringComparison.OrdinalIgnoreCase) ||
                name.Contains("TurboQuant", StringComparison.OrdinalIgnoreCase) ||
                name.Contains("Quantization", StringComparison.OrdinalIgnoreCase))
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        CollectionAssert.AreEqual(Array.Empty<string>(), forbidden,
            string.Join(", ", forbidden));
    }

    [TestMethod]
    public void RegistryFailsClosedOnDuplicateOrMissingRoute()
    {
        PromptRouteCapability capability =
            Capability(PromptRouteKind.OpenVino, "openvino.official");

        Assert.ThrowsExactly<ArgumentException>(() => new PromptRouteRegistry(
            [new FakeAdapter(capability), new FakeAdapter(capability)]));
        PromptRouteRegistry registry = new([new FakeAdapter(capability)]);
        Assert.ThrowsExactly<KeyNotFoundException>(() =>
            registry.GetRequired(PromptRouteKind.Gguf));
    }

    [TestMethod]
    public void OfficialOpenVinoAdapterRegistersOnlyTheApprovedCpuCandidate()
    {
        PromptRouteCapability capability =
            OpenVinoRouteCapability.PromptCapability;

        Assert.AreEqual(PromptRouteKind.OpenVino, capability.Kind);
        Assert.AreEqual("openvino.official", capability.RouteId);
        Assert.AreEqual("openvino.official.cpu", capability.ConfigurationId);
        Assert.AreEqual("CPU", capability.Device);
        Assert.AreEqual("Official MVP", capability.Maturity);
        Assert.AreEqual(4_096, capability.MaximumContextTokens);
        Assert.AreEqual(128, capability.DefaultRequestedNewTokens);
        Assert.AreEqual(128, capability.MaximumRequestedNewTokens);
    }

    private static PromptRouteCapability Capability(
        PromptRouteKind kind,
        string routeId) => new(
            kind,
            routeId,
            $"{routeId}.cpu",
            "test backend",
            "CPU",
            "test",
            4_096,
            128,
            128);

    private static IEnumerable<Type> PublicApiTypes(Type type)
    {
        yield return type;
        foreach (Type item in type.GetProperties().Select(property => property.PropertyType)
                     .Concat(type.GetMethods().Select(method => method.ReturnType))
                     .Concat(type.GetMethods().SelectMany(method =>
                         method.GetParameters().Select(parameter => parameter.ParameterType))))
        {
            yield return item;
        }
    }

    private static IEnumerable<Type> SignatureTypes(Type type)
    {
        yield return type;
        foreach (Type argument in type.GetGenericArguments())
        {
            foreach (Type nested in SignatureTypes(argument))
            {
                yield return nested;
            }
        }
    }

    private sealed class FakeAdapter(PromptRouteCapability capability) :
        IPromptRouteAdapter
    {
        public PromptRouteCapability Capability { get; } = capability;
    }
}
