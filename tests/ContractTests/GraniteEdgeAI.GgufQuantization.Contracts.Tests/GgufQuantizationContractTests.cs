using System.Reflection;
using GraniteEdgeAI.GgufQuantization.Contracts;

namespace GraniteEdgeAI.GgufQuantization.Contracts.Tests;

[TestClass]
public sealed class GgufQuantizationContractTests
{
    [TestMethod]
    public void CommandCarriesOnlyClosedIdentityAndOpaqueTokenFields()
    {
        string[] properties = typeof(GgufQuantizationCommand)
            .GetProperties(BindingFlags.Instance | BindingFlags.Public)
            .Select(property => property.Name)
            .Order(StringComparer.Ordinal)
            .ToArray();

        CollectionAssert.AreEqual(
            new[]
            {
                "ConfigurationSha256", "CorrelationId", "OptimizationPlanId",
                "OutputToken", "ProtocolVersion", "RequantizationAuthorizationSha256",
                "RequantizationPolicyVersion", "SourceFormat", "SourceToken",
                "TargetFormat", "ToolManifestSha256",
            },
            properties);
        Assert.IsFalse(properties.Any(property =>
            property.Contains("Path", StringComparison.OrdinalIgnoreCase)
            || property.Contains("File", StringComparison.OrdinalIgnoreCase)
            || property.Contains("Argument", StringComparison.OrdinalIgnoreCase)));
    }

    [TestMethod]
    public void ContractRecordsHaveNoPublicConstructionBypass()
    {
        Assert.AreEqual(0, typeof(GgufQuantizationCommand).GetConstructors().Length);
        Assert.AreEqual(0, typeof(GgufQuantizationEvent).GetConstructors().Length);
    }

    [TestMethod]
    public void CommandRejectsMissingAuthorizationForQuantizedReduction()
    {
        Assert.ThrowsExactly<ArgumentException>(() => CreateCommand(
            GgufQuantizationFormat.Q4KM,
            GgufQuantizationFormat.Q3KM,
            authorization: null));
    }

    [TestMethod]
    public void CommandRejectsAuthorizationWhenNoRequantizationOccurs()
    {
        Assert.ThrowsExactly<ArgumentException>(() => CreateCommand(
            GgufQuantizationFormat.F16,
            GgufQuantizationFormat.Q4KM,
            Digest('a')));
        Assert.ThrowsExactly<ArgumentException>(() => CreateCommand(
            GgufQuantizationFormat.Q4KM,
            GgufQuantizationFormat.Q4KM,
            Digest('a')));
    }

    [TestMethod]
    public void CommandAcceptsTheExactBoundedShapes()
    {
        GgufQuantizationCommand command = CreateCommand(
            GgufQuantizationFormat.Q4KM,
            GgufQuantizationFormat.Q3KM,
            Digest('a'));

        Assert.AreEqual(GgufQuantizationProtocol.CurrentVersion, command.ProtocolVersion);
        Assert.AreEqual(Digest('a'), command.RequantizationAuthorizationSha256);
    }

    [TestMethod]
    public void CommandRejectsNonDownwardOrNonQuantizedTargets()
    {
        Assert.ThrowsExactly<ArgumentException>(() => CreateCommand(
            GgufQuantizationFormat.Q3KM,
            GgufQuantizationFormat.Q4KM,
            authorization: null));
        Assert.ThrowsExactly<ArgumentException>(() => CreateCommand(
            GgufQuantizationFormat.Q4KM,
            GgufQuantizationFormat.Q4KM,
            authorization: null));
        Assert.ThrowsExactly<ArgumentException>(() => CreateCommand(
            GgufQuantizationFormat.F32,
            GgufQuantizationFormat.F16,
            authorization: null));
    }

    [TestMethod]
    public void TerminalEventsEnforceTheirClosedOutcomeShapes()
    {
        Guid correlation = Guid.NewGuid();
        Guid plan = Guid.NewGuid();

        Assert.ThrowsExactly<ArgumentException>(() =>
            GgufQuantizationEvent.Create(
                correlation, plan, Digest('a'), GgufQuantizationEventKind.Completed,
                100, GgufQuantizationSupportCode.None));
        Assert.ThrowsExactly<ArgumentException>(() =>
            GgufQuantizationEvent.Create(
                correlation, plan, Digest('a'), GgufQuantizationEventKind.Failed,
                20, GgufQuantizationSupportCode.None));
        Assert.ThrowsExactly<ArgumentException>(() =>
            GgufQuantizationEvent.Create(
                correlation, plan, Digest('a'), GgufQuantizationEventKind.Started,
                0, GgufQuantizationSupportCode.None, "unexpected-output"));

        GgufQuantizationEvent completed = GgufQuantizationEvent.Create(
            correlation, plan, Digest('a'), GgufQuantizationEventKind.Completed,
            100, GgufQuantizationSupportCode.None, "sealed-output");
        Assert.AreEqual("sealed-output", completed.OutputToken);
    }

    private static GgufQuantizationCommand CreateCommand(
        GgufQuantizationFormat source,
        GgufQuantizationFormat target,
        string? authorization) =>
        GgufQuantizationCommand.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Digest('b'),
            "source-token",
            "output-token",
            source,
            target,
            Digest('c'),
            GgufQuantizationProtocol.RequantizationPolicyVersion,
            authorization);

    private static string Digest(char value) => new(value, 64);
}
