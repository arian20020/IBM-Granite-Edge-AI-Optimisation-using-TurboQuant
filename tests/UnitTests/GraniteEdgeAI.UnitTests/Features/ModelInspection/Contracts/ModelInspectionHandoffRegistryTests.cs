using GraniteEdgeAI.Features.ModelInspection.Contracts;
using GraniteEdgeAI.Features.ModelInspection.Handoff;
using GraniteEdgeAI.UnitTests.Features.ModelInspection.Presentation;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace GraniteEdgeAI.UnitTests;

[TestClass]
public sealed class ModelInspectionHandoffRegistryTests
{
    private static readonly Guid ModelRunId =
        Guid.Parse("22222222-2222-4222-8222-222222222222");

    private static readonly Guid HardwareRunId =
        Guid.Parse("33333333-3333-4333-8333-333333333333");

    [TestMethod]
    public void Issue_RequiresCurrentEligibleModelRunAndAllowsOneLiveHandoff()
    {
        var registry = new ModelInspectionHandoffRegistry();
        ModelInspectionExecutionResult terminal = CreateReadyTerminal();

        Assert.IsFalse(registry.TryIssue(
            ModelRunId,
            terminal,
            out _));

        registry.ActivateModelRun(ModelRunId);

        Assert.IsTrue(registry.TryIssue(
            ModelRunId,
            terminal,
            out ModelInspectionHandoff? handoff));
        Assert.IsNotNull(handoff);
        Assert.AreEqual(ModelRunId, handoff.ModelInspectionRunId);
        Assert.AreEqual(
            ModelInspectionHandoffLifecycleState.Issued,
            registry.GetState(handoff.ModelInspectionHandoffId));
        Assert.IsFalse(registry.TryIssue(
            ModelRunId,
            terminal,
            out _));
        Assert.IsFalse(registry.TryIssue(
            Guid.Parse("44444444-4444-4444-8444-444444444444"),
            terminal,
            out _));
    }

    [TestMethod]
    public void RegisterIssued_AcceptsOnlyAnExactCurrentProjectedHandoff()
    {
        var registry = new ModelInspectionHandoffRegistry();
        Assert.IsTrue(ModelInspectionHandoffProjector.TryProject(
            ModelRunId,
            ModelRunId,
            CreateReadyTerminal(),
            out ModelInspectionHandoff? handoff));
        Assert.IsNotNull(handoff);

        Assert.IsFalse(registry.TryRegisterIssued(handoff));
        registry.ActivateModelRun(ModelRunId);
        Assert.IsTrue(registry.TryRegisterIssued(handoff));
        Assert.IsTrue(
            registry.TryRegisterIssued(handoff),
            "Re-registering the exact current Issued value must support one explicit navigation retry.");
        var altered = new ModelInspectionHandoff(
            handoff.SchemaVersion,
            handoff.ModelInspectionHandoffId,
            handoff.ModelInspectionRunId,
            handoff.Outcome,
            "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb",
            handoff.ModelLengthBytes);
        Assert.IsFalse(registry.TryRegisterIssued(altered));
    }

    [TestMethod]
    public void Bind_IsAtomicAndRejectsDuplicateWrongOrAlteredClaims()
    {
        (ModelInspectionHandoffRegistry registry, ModelInspectionHandoff handoff) =
            CreateIssued();

        Assert.IsFalse(registry.TryBindToHardwareRun(
            handoff,
            Guid.Parse("44444444-4444-4444-8444-444444444444"),
            Guid.Parse("55555555-5555-4555-8555-555555555555"),
            out _));
        var altered = new ModelInspectionHandoff(
            handoff.SchemaVersion,
            handoff.ModelInspectionHandoffId,
            handoff.ModelInspectionRunId,
            handoff.Outcome,
            "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb",
            handoff.ModelLengthBytes);
        Assert.IsFalse(registry.TryBindToHardwareRun(
            altered,
            ModelRunId,
            HardwareRunId,
            out _));
        Assert.IsFalse(registry.TryBindToHardwareRun(
            handoff,
            ModelRunId,
            handoff.ModelInspectionHandoffId,
            out _));
        Assert.IsTrue(registry.TryBindToHardwareRun(
            handoff,
            ModelRunId,
            HardwareRunId,
            out ModelInspectionHandoffClaim claim));
        Assert.AreEqual(HardwareRunId, claim.ProductHardwareRunId);
        Assert.AreEqual(
            ModelInspectionHandoffLifecycleState.BoundToHardwareRun,
            registry.GetState(handoff.ModelInspectionHandoffId));
        Assert.IsFalse(registry.TryBindToHardwareRun(
            handoff,
            ModelRunId,
            Guid.Parse("66666666-6666-4666-8666-666666666666"),
            out _));

    }

    [TestMethod]
    public void Rollback_SucceedsOnlyForTheExactUnstartedClaim()
    {
        (ModelInspectionHandoffRegistry registry, ModelInspectionHandoff handoff) =
            CreateIssued();
        Assert.IsTrue(registry.TryBindToHardwareRun(
            handoff,
            ModelRunId,
            HardwareRunId,
            out ModelInspectionHandoffClaim claim));
        var wrongClaim = new ModelInspectionHandoffClaim(
            claim.ModelInspectionHandoffId,
            claim.ModelInspectionRunId,
            claim.ProductHardwareRunId,
            Guid.NewGuid());

        Assert.IsFalse(registry.TryRollbackBeforeHardwareStart(wrongClaim));
        Assert.IsTrue(registry.TryRollbackBeforeHardwareStart(claim));
        Assert.AreEqual(
            ModelInspectionHandoffLifecycleState.Issued,
            registry.GetState(handoff.ModelInspectionHandoffId));
        Assert.IsFalse(registry.TryRollbackBeforeHardwareStart(claim));
    }

    [TestMethod]
    public void StartedClaim_CannotRollbackAndMayTransferOnlyOnce()
    {
        (ModelInspectionHandoffRegistry registry, ModelInspectionHandoff handoff) =
            CreateIssued();
        Assert.IsTrue(registry.TryBindToHardwareRun(
            handoff,
            ModelRunId,
            HardwareRunId,
            out ModelInspectionHandoffClaim claim));

        Assert.IsTrue(registry.TryMarkHardwareStarted(claim));
        Assert.IsFalse(registry.TryMarkHardwareStarted(claim));
        Assert.IsFalse(registry.TryRollbackBeforeHardwareStart(claim));
        Assert.AreEqual(
            ModelInspectionHandoffLifecycleState.BoundToHardwareRun,
            registry.GetState(handoff.ModelInspectionHandoffId));
        Assert.IsNull(
            typeof(ModelInspectionHandoffRegistry).GetMethod(
                "TryTransferToBlock3",
                System.Reflection.BindingFlags.Instance |
                System.Reflection.BindingFlags.NonPublic),
            "Block 3 transfer must not exist before the full route predicate is implemented.");
    }

    [TestMethod]
    public void NewModelRun_InvalidatesEveryPriorLiveHandoff()
    {
        (ModelInspectionHandoffRegistry registry, ModelInspectionHandoff handoff) =
            CreateIssued();

        registry.ActivateModelRun(
            Guid.Parse("44444444-4444-4444-8444-444444444444"));

        Assert.AreEqual(
            ModelInspectionHandoffLifecycleState.Invalidated,
            registry.GetState(handoff.ModelInspectionHandoffId));
        Assert.IsFalse(registry.TryBindToHardwareRun(
            handoff,
            ModelRunId,
            HardwareRunId,
            out _));
    }

    [TestMethod]
    public void ExplicitReissue_UsesNewHandoffAndHardwareRunIdentities()
    {
        (ModelInspectionHandoffRegistry registry, ModelInspectionHandoff handoff) =
            CreateIssued();
        Assert.IsTrue(registry.TryBindToHardwareRun(
            handoff,
            ModelRunId,
            HardwareRunId,
            out ModelInspectionHandoffClaim claim));
        Assert.IsTrue(registry.TryMarkHardwareStarted(claim));

        Assert.IsTrue(ModelInspectionHandoffProjector.TryProject(
            ModelRunId,
            ModelRunId,
            CreateReadyTerminal(),
            out ModelInspectionHandoff? replacement));
        Assert.IsNotNull(replacement);
        Assert.IsTrue(registry.TryAcceptReissue(
            handoff.ModelInspectionHandoffId,
            replacement));

        Assert.AreNotEqual(
            handoff.ModelInspectionHandoffId,
            replacement.ModelInspectionHandoffId);
        Assert.AreEqual(ModelRunId, replacement.ModelInspectionRunId);
        Assert.AreEqual(
            ModelInspectionHandoffLifecycleState.Invalidated,
            registry.GetState(handoff.ModelInspectionHandoffId));
        Assert.AreEqual(
            ModelInspectionHandoffLifecycleState.Issued,
            registry.GetState(replacement.ModelInspectionHandoffId));
        Assert.IsFalse(registry.TryBindToHardwareRun(
            replacement,
            ModelRunId,
            HardwareRunId,
            out _));
        Assert.IsTrue(registry.TryBindToHardwareRun(
            replacement,
            ModelRunId,
            Guid.Parse("77777777-7777-4777-8777-777777777777"),
            out _));
    }

    private static (
        ModelInspectionHandoffRegistry Registry,
        ModelInspectionHandoff Handoff) CreateIssued()
    {
        var registry = new ModelInspectionHandoffRegistry();
        registry.ActivateModelRun(ModelRunId);
        Assert.IsTrue(registry.TryIssue(
            ModelRunId,
            CreateReadyTerminal(),
            out ModelInspectionHandoff? handoff));
        Assert.IsNotNull(handoff);
        return (registry, handoff);
    }

    private static ModelInspectionExecutionResult CreateReadyTerminal() =>
        ModelInspectionExecutionResult.Completed(
            PresentationTestData.CreateResult(ModelInspectionOutcome.Ready));
}
