using GraniteEdgeAI.GgufRuntime.Contracts.Session;

namespace GraniteEdgeAI.GgufRuntime.Contracts.Tests;

[TestClass]
public sealed class GgufSessionStateMachineTests
{
    [TestMethod]
    public void AdvanceReadyToGeneratingToReadyUpdatesCurrentState()
    {
        var machine = new GgufSessionStateMachine(GgufSessionState.Ready);

        machine.AdvanceTo(GgufSessionState.Generating);
        machine.AdvanceTo(GgufSessionState.Ready);

        Assert.AreEqual(GgufSessionState.Ready, machine.Current);
    }

    [TestMethod]
    public void AdvanceCreatedDirectlyToReadyRejectsTransitionWithoutMutation()
    {
        var machine = new GgufSessionStateMachine(GgufSessionState.Created);

        Assert.ThrowsExactly<InvalidOperationException>(
            () => machine.AdvanceTo(GgufSessionState.Ready));

        Assert.AreEqual(GgufSessionState.Created, machine.Current);
    }

    [TestMethod]
    public void AdvanceClosedToStartingRejectsTerminalStateMutation()
    {
        var machine = new GgufSessionStateMachine(GgufSessionState.Closed);

        Assert.ThrowsExactly<InvalidOperationException>(
            () => machine.AdvanceTo(GgufSessionState.Starting));

        Assert.AreEqual(GgufSessionState.Closed, machine.Current);
    }
}
