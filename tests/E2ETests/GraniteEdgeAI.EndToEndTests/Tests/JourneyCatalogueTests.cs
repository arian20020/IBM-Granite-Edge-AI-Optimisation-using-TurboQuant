using System.Reflection;
using GraniteEdgeAI.EndToEndTests.Journeys;

namespace GraniteEdgeAI.EndToEndTests.Tests;

[TestClass]
public sealed class JourneyCatalogueTests
{
    [TestMethod]
    public void Every_R2_required_journey_is_a_uniquely_categorized_MSTest()
    {
        Dictionary<string, MethodInfo> tests = typeof(JourneyCatalogueTests).Assembly
            .GetTypes()
            .SelectMany(type => type.GetMethods(BindingFlags.Instance | BindingFlags.Public))
            .Where(method => method.GetCustomAttribute<TestMethodAttribute>() is not null)
            .ToDictionary(method => method.Name, StringComparer.Ordinal);

        CollectionAssert.AreEquivalent(JourneyCatalogue.RequiredNames, JourneyCatalogue.RequiredNames.Distinct(StringComparer.Ordinal).ToArray());
        foreach (string name in JourneyCatalogue.RequiredNames)
        {
            Assert.IsTrue(tests.TryGetValue(name, out MethodInfo? method), $"Missing required R2 journey '{name}'.");
            string[] categories = method!.DeclaringType!.GetCustomAttributes<TestCategoryAttribute>()
                .Concat(method.GetCustomAttributes<TestCategoryAttribute>())
                .SelectMany(attribute => attribute.TestCategories)
                .Where(category => category.StartsWith("Native", StringComparison.Ordinal))
                .ToArray();
            Assert.HasCount(1, categories, $"Journey '{name}' must have exactly one native stage category.");
        }
    }
}
