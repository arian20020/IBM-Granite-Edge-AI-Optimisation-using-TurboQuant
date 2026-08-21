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
        // nothing else. Everything else is resolved through a port or created by
        // the coordinator, so a caller cannot smuggle in a stale RAM figure or a
        // pre-chosen policy version.
        PropertyInfo[] properties = typeof(CompatibilityRunRequest)
            .GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            .Where(property => property.Name != "EqualityContract")
            .ToArray();

        Assert.AreEqual(1, properties.Length);
        Assert.AreEqual("Context", properties[0].Name);
    }

    [TestMethod]
    [DataRow("RunId")]
    [DataRow("Id")]
    [DataRow("AvailableMemory")]
    [DataRow("SystemMemory")]
    [DataRow("PolicyVersion")]
    [DataRow("Progress")]
    [DataRow("CancellationToken")]
    [DataRow("Hardware")]
    [DataRow("Model")]
    [DataRow("Handoff")]
    public void Request_CarriesNoProhibitedMember(string prohibited)
    {
        bool present = typeof(CompatibilityRunRequest)
            .GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            .Any(property => property.Name.Contains(prohibited, StringComparison.OrdinalIgnoreCase));

        Assert.IsFalse(present, $"The request must not carry {prohibited}.");
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
        // An empty id cannot distinguish one run from another, which is what
        // stale-run rejection depends on.
        Assert.ThrowsExactly<ArgumentException>(() => CompatibilityRunId.From(Guid.Empty));
    }

    [TestMethod]
    public void Finding_CarriesACodeAndNoText()
    {
        // Section 14: no free-form payload reaches a result. Presentation owns
        // the wording; the engine owns the code.
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
