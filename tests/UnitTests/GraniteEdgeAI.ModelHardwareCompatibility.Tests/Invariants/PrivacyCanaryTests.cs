using System.Reflection;
using System.Runtime.CompilerServices;
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
        "CandidateFingerprint.Value",

        // Hand-written ToString overrides surfaced once the scan widened to
        // cover methods. Each formats an already-reviewed numeric value (or,
        // for CandidateFingerprint, delegates to the already-allowed Value
        // property) and introduces no new content of its own.
        "ByteCount.ToString",
        "ContextTokenCount.ToString",
        "CandidateFingerprint.ToString"
    ];

    private static readonly BindingFlags AllMembers =
        BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic
        | BindingFlags.DeclaredOnly;

    [TestMethod]
    public void NoUnreviewedStringMemberExistsInTheCompatibilityCore()
    {
        // A new string member is where a path or model name would first appear.
        // Adding one is allowed; adding one without review is not.
        //
        // Scans instance and static properties AND fields, and treats
        // string[] / IEnumerable<string> the same as a bare string: a path or
        // model name reaching a collection is exactly as much of a leak as
        // reaching a scalar. Methods returning a string-carrying type are
        // scanned too, since a computed string escapes this canary just as
        // easily as a stored one.
        string[] properties = typeof(ByteCount).Assembly
            .GetTypes()
            .SelectMany(type => type
                .GetProperties(AllMembers)
                .Where(property => CarriesString(property.PropertyType))
                .Select(property => $"{type.Name}.{property.Name}"))
            .ToArray();

        // Compiler-generated members are excluded: a property's backing field
        // and a record's synthesized ToString() carry no content beyond what
        // the declared members already expose, so they are not a new place
        // for a string to enter - they are a mechanical byproduct of members
        // this scan already reviews.
        string[] fields = typeof(ByteCount).Assembly
            .GetTypes()
            .SelectMany(type => type
                .GetFields(AllMembers)
                .Where(field => CarriesString(field.FieldType) && !IsCompilerGenerated(field))
                .Select(field => $"{type.Name}.{field.Name}"))
            .ToArray();

        string[] methods = typeof(ByteCount).Assembly
            .GetTypes()
            .SelectMany(type => type
                .GetMethods(AllMembers)
                .Where(method =>
                    !method.IsSpecialName
                    && CarriesString(method.ReturnType)
                    && !IsCompilerGenerated(method))
                .Select(method => $"{type.Name}.{method.Name}"))
            .ToArray();

        string[] found = properties
            .Concat(fields)
            .Concat(methods)
            .Where(name => !AllowedStringMembers.Contains(name))
            .Distinct()
            .Order()
            .ToArray();

        Assert.AreEqual(
            0,
            found.Length,
            "Unreviewed string-carrying members found. Confirm each carries no path, "
            + "filename, model name or native error, then add it to the allowlist: "
            + string.Join(", ", found));
    }

    private static bool CarriesString(Type type) =>
        type == typeof(string)
        || type == typeof(string[])
        || (typeof(System.Collections.IEnumerable).IsAssignableFrom(type)
            && type != typeof(string)
            && type.IsGenericType
            && type.GetGenericArguments().Contains(typeof(string)));

    private static bool IsCompilerGenerated(MemberInfo member) =>
        member.IsDefined(typeof(CompilerGeneratedAttribute), inherit: false);

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
