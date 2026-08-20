using System.Reflection;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Estimation;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.FitAssessment;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Domain;

namespace GraniteEdgeAI.ModelHardwareCompatibility.Tests.Invariants;

/// <summary>
/// Section 14 forbids any path, filename, model name, hostname, device
/// identifier or raw tool output entering a C1 type. The cheapest durable
/// enforcement is to know every string member in the library by name.
/// </summary>
[TestClass]
public sealed class PrivacyCanaryTests
{
    private static readonly HashSet<string> AllowedStringMembers =
    [
        "SafetyPolicy.PolicyVersion",
        "EstimatorPolicy.PolicyVersion",
        "CompatibilityCandidate.SupportEntryId",
        "RouteConfiguration.CanonicalDescriptor",
        "GgufRouteConfiguration.CanonicalDescriptor",
        "CandidateFingerprint.Value"
    ];

    [TestMethod]
    public void NoUnreviewedStringMemberExistsInTheCompatibilityCore()
    {
        // A new string member is where a path or model name would first appear.
        // Adding one is allowed; adding one without review is not.
        string[] found = typeof(ByteCount).Assembly
            .GetTypes()
            .SelectMany(type => type
                .GetProperties(
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic
                    | BindingFlags.DeclaredOnly)
                .Where(property => property.PropertyType == typeof(string))
                .Select(property => $"{type.Name}.{property.Name}"))
            .Where(name => !AllowedStringMembers.Contains(name))
            .Where(name => !name.Contains("EqualityContract", StringComparison.Ordinal))
            .Order()
            .ToArray();

        Assert.AreEqual(
            0,
            found.Length,
            "Unreviewed string members found. Confirm each carries no path, "
            + "filename, model name or native error, then add it to the allowlist: "
            + string.Join(", ", found));
    }

    [TestMethod]
    public void EveryAllowedStringValue_IsFreeOfPathLikeContent()
    {
        string[] forbidden = ["\\", "/", ":", ".gguf", ".."];

        string[] samples =
        [
            SafetyPolicy.ProvisionalV1().PolicyVersion,
            EstimatorPolicy.ProvisionalV1().PolicyVersion
        ];

        foreach (string sample in samples)
        {
            foreach (string fragment in forbidden)
            {
                Assert.IsFalse(
                    sample.Contains(fragment, StringComparison.OrdinalIgnoreCase),
                    $"'{sample}' contains path-like content '{fragment}'.");
            }
        }
    }
}
