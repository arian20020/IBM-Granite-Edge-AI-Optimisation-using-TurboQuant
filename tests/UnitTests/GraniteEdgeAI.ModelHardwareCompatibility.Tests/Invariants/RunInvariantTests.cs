using System.Reflection;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Contracts;
using GraniteEdgeAI.ModelHardwareCompatibility.Core.Application.Ports;

namespace GraniteEdgeAI.ModelHardwareCompatibility.Tests.Invariants;

/// <summary>
/// Properties of a run that must hold however the lifecycle is later extended.
/// </summary>
[TestClass]
public sealed class RunInvariantTests
{
    [TestMethod]
    public void EveryPortShipsAnUnavailableImplementation()
    {
        // The whole engine is buildable and testable before any owner publishes a
        // contract precisely because each seam has a typed refusal.
        Assert.IsNotNull(UnavailablePorts.Gateway());
        Assert.IsNotNull(UnavailablePorts.ModelFacts());
        Assert.IsNotNull(UnavailablePorts.HardwareFacts());
        Assert.IsNotNull(UnavailablePorts.MemoryProbe());
        Assert.IsNotNull(UnavailablePorts.VerificationRunner());
    }

    [TestMethod]
    public void NoUnavailablePortReportsSuccess()
    {
        Assert.IsFalse(UnavailablePorts.Gateway().Claim().IsClaimed);
        Assert.IsFalse(UnavailablePorts.ModelFacts().Resolve("x").IsEstablished);
        Assert.IsFalse(UnavailablePorts.HardwareFacts().Resolve("x").IsEstablished);
        Assert.IsFalse(UnavailablePorts.MemoryProbe().Probe().IsEstablished);
    }

    [TestMethod]
    public void EveryUnavailablePortNamesItsReason()
    {
        // A refusal with the zero reason would be an unknown reported as nothing,
        // which the design forbids everywhere else.
        Assert.AreNotEqual(
            PortUnavailableReason.None, UnavailablePorts.Gateway().Claim().Reason);
        Assert.AreNotEqual(
            PortUnavailableReason.None, UnavailablePorts.ModelFacts().Resolve("x").Reason);
        Assert.AreNotEqual(
            PortUnavailableReason.None, UnavailablePorts.HardwareFacts().Resolve("x").Reason);
        Assert.AreNotEqual(
            PortUnavailableReason.None, UnavailablePorts.MemoryProbe().Probe().Reason);
    }

    [TestMethod]
    public void TheVerificationRunnerIsNotMerelyMissingAnAdapter()
    {
        // Runtime verification is another team's to implement, never this
        // feature's — a distinct reason from "no adapter written yet".
        Assert.AreEqual(
            nameof(PortUnavailableReason.RunnerNotRegistered),
            UnavailablePorts.VerificationRunner()
                .Verify(Core.Domain.CompatibilityRunId.New()).Reason.ToString());
    }

    [TestMethod]
    public void TheCoreReferencesNoOwnerFeatureAssembly()
    {
        // This feature owns its own calculation records precisely so it compiles
        // before hardware and model inspection publish anything.
        string[] forbidden = ["HardwareInspection", "ModelInspection"];

        string[] offenders = typeof(CompatibilityRunResult).Assembly
            .GetReferencedAssemblies()
            .Select(reference => reference.Name ?? string.Empty)
            .Where(name => forbidden.Any(f => name.Contains(f, StringComparison.Ordinal)))
            .ToArray();

        Assert.AreEqual(
            0,
            offenders.Length,
            "The compatibility core must not reference an owner feature assembly: "
            + string.Join(", ", offenders));
    }

    [TestMethod]
    public void AnAssessmentCannotBeAttachedAfterConstruction()
    {
        PropertyInfo assessment = typeof(CompatibilityRunResult)
            .GetProperty(
                "Assessment",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)!;

        Assert.IsFalse(
            assessment.CanWrite,
            "Assessment must not be settable, or a cancelled run could acquire one.");
    }

    [TestMethod]
    public void EveryNonCompletedOutcomeCarriesNoAssessment()
    {
        // Partial work surviving into a result would be read as a conclusion the
        // run explicitly failed to reach.
        DateTimeOffset now = DateTimeOffset.UtcNow;

        IReadOnlyList<PolicyIdentity> policies =
        [
            PolicyIdentity.Create(
                "estimator",
                "v1",
                Core.Application.FitAssessment.PolicyProvenance.Provisional)
        ];

        IReadOnlyList<CompatibilityFinding> findings =
        [
            CompatibilityFinding.Create(
                CompatibilityFindingCode.ModelFactsUnavailable, FindingSeverity.Blocking)
        ];

        Assert.IsNull(CompatibilityRunResult
            .Cancelled(Core.Domain.CompatibilityRunId.New(), [], policies, now, now)
            .Assessment);

        Assert.IsNull(CompatibilityRunResult
            .Failed(Core.Domain.CompatibilityRunId.New(), findings, policies, now, now)
            .Assessment);

        Assert.IsNull(CompatibilityRunResult
            .NotEstablished(Core.Domain.CompatibilityRunId.New(), findings, policies, now, now)
            .Assessment);
    }
}
