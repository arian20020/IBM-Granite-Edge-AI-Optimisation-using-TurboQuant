using System.Reflection;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Contracts;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Domain;

namespace GraniteEdgeAI.ModelHardwareCompatibility.Tests.Application.Contracts;

[TestClass]
public sealed class CompatibilityRunRequestTests
{
    [TestMethod]
    public void Request_CarriesOnlyTheContext()
    {
        // Spec section 7: the request carries the user's context intent and
        // nothing else. everything else is resolved through a port or created by
        // the coordinator, so a caller cannot smuggle in a stale RAM figure or a
        // pre-chosen policy version
        PropertyInfo[] properties = typeof(CompatibilityRunRequest)
            .GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            .Where(property => property.Name != "EqualityContract")
            .ToArray();

        Assert.AreEqual(1, properties.Length);
        Assert.AreEqual("Context", properties[0].Name);
    }

    [TestMethod]
    public void Request_ReachesNoProhibitedType()
    {
        // Matching on member names let a prohibited thing through under an
        // innocent name: a CancellationToken called Token passed every check
        // above. Types cannot be renamed out of a test, so the ban is on what a
        // member IS rather than on what it is called.
        //
        // Followed through the whole reachable graph, because a banned type
        // nested one level inside the context request would be just as smuggled
        // in as one sitting on the request itself
        Type[] prohibited =
        [
            typeof(CancellationToken),
            typeof(CompatibilityRunId),
            typeof(ByteCount),
            typeof(IProgress<>),
            typeof(TimeProvider)
        ];

        HashSet<Type> seen = [];
        List<string> offences = [];

        Walk(typeof(CompatibilityRunRequest), "CompatibilityRunRequest");

        Assert.AreEqual(
            0,
            offences.Count,
            "The request reaches something it must resolve through a port instead: "
            + string.Join(", ", offences));

        void Walk(Type type, string path)
        {
            if (!seen.Add(type))
            {
                return;
            }

            foreach (PropertyInfo property in type.GetProperties(
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
            {
                if (property.Name == "EqualityContract")
                {
                    continue;
                }

                Type carried = property.PropertyType;
                string here = $"{path}.{property.Name}";

                if (prohibited.Any(banned => Matches(banned, carried)))
                {
                    offences.Add($"{here} carries {carried.Name}");
                    continue;
                }

                // Only our own types are walked into. Following the framework's
                // would wander the whole BCL and prove nothing about this
                // contract.
                if (carried.Assembly == typeof(CompatibilityRunRequest).Assembly)
                {
                    Walk(carried, here);
                }
            }
        }

        static bool Matches(Type banned, Type carried) =>
            banned.IsGenericTypeDefinition
                ? carried.IsGenericType
                    && carried.GetGenericTypeDefinition() == banned
                : banned.IsAssignableFrom(carried);
    }

    [TestMethod]
    public void RunId_IsUniquePerRun()
    {
        Assert.AreNotEqual(CompatibilityRunId.New().Value, CompatibilityRunId.New().Value);
    }

    [TestMethod]
    public void RunId_RoundTripsAnExplicitValue()
    {
        Guid value = Guid.NewGuid();

        Assert.AreEqual(value, CompatibilityRunId.From(value).Value);
    }

    [TestMethod]
    public void RunId_RejectsAnEmptyValue()
    {
        // an empty id cannot distinguish one run from another, which is what
        // stale-run rejection depends on
        Assert.ThrowsExactly<ArgumentException>(() => CompatibilityRunId.From(Guid.Empty));
    }

    [TestMethod]
    public void Finding_CarriesACodeAndNoText()
    {
        // Section 14: no free-form payload reaches a result. Presentation owns
        // the wording; the engine owns the code
        PropertyInfo[] strings = typeof(CompatibilityFinding)
            .GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            .Where(property => property.PropertyType == typeof(string))
            .ToArray();

        Assert.AreEqual(0, strings.Length);
    }

    [TestMethod]
    public void Finding_RejectsAnUnspecifiedCode()
    {
        Assert.ThrowsExactly<ArgumentException>(
            () => CompatibilityFinding.Create(
                CompatibilityFindingCode.Unspecified, FindingSeverity.Information));
    }

    [TestMethod]
    public void Finding_RejectsAnUnspecifiedSeverity()
    {
        Assert.ThrowsExactly<ArgumentException>(
            () => CompatibilityFinding.Create(
                CompatibilityFindingCode.UncalibratedEstimate, FindingSeverity.Unspecified));
    }

    [TestMethod]
    public void PolicyIdentity_RejectsABlankName()
    {
        Assert.ThrowsExactly<ArgumentException>(
            () => PolicyIdentity.Create(
                "   ", "v1", GraniteEdgeAI.ModelHardwareCompatibility.Core.Application
                    .FitAssessment.PolicyProvenance.Provisional));
    }

    [TestMethod]
    public void PolicyIdentity_RejectsABlankVersion()
    {
        Assert.ThrowsExactly<ArgumentException>(
            () => PolicyIdentity.Create(
                "estimator", "  ", GraniteEdgeAI.ModelHardwareCompatibility.Core.Application
                    .FitAssessment.PolicyProvenance.Provisional));
    }
}
