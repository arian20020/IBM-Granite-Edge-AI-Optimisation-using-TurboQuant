using GraniteEdgeAI.ModelInspection.ProtocolTestWorker;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace GraniteEdgeAI.ModelInspection.WorkerProcess.Tests;

/// <summary>
/// Locks the exact fixture-only command-line vocabulary. The production worker
/// has no argument parser and cannot select these abnormal behaviours.
/// </summary>
[TestClass]
public sealed class TestWorkerScenarioParserTests
{
    private static readonly IReadOnlyDictionary<TestWorkerScenario, string>
        ExpectedNames = new Dictionary<TestWorkerScenario, string>
        {
            [TestWorkerScenario.LaunchProbe] = "launch-probe",
            [TestWorkerScenario.HealthyControlledFailure] = "healthy-controlled-failure",
            [TestWorkerScenario.NoHello] = "no-hello",
            [TestWorkerScenario.MalformedHello] = "malformed-hello",
            [TestWorkerScenario.WrongProtocolVersion] = "wrong-protocol-version",
            [TestWorkerScenario.WrongWorkerId] = "wrong-worker-id",
            [TestWorkerScenario.WrongWorkerProcessId] = "wrong-worker-process-id",
            [TestWorkerScenario.WrongRuntimeProfile] = "wrong-runtime-profile",
            [TestWorkerScenario.WrongArchitecture] = "wrong-architecture",
            [TestWorkerScenario.TextBeforeHello] = "text-before-hello",
            [TestWorkerScenario.InvalidUtf8] = "invalid-utf8",
            [TestWorkerScenario.Utf8Bom] = "utf8-bom",
            [TestWorkerScenario.OversizedStdoutLine] = "oversized-stdout-line",
            [TestWorkerScenario.MalformedJson] = "malformed-json",
            [TestWorkerScenario.DuplicateJsonProperty] = "duplicate-json-property",
            [TestWorkerScenario.WrongRequestId] = "wrong-request-id",
            [TestWorkerScenario.ProgressBeforeStarted] = "progress-before-started",
            [TestWorkerScenario.NonMonotonicProgress] = "non-monotonic-progress",
            [TestWorkerScenario.DuplicateTerminal] = "duplicate-terminal",
            [TestWorkerScenario.ExitWithoutTerminal] = "exit-without-terminal",
            [TestWorkerScenario.CrashBeforeHello] = "crash-before-hello",
            [TestWorkerScenario.CrashAfterHello] = "crash-after-hello",
            [TestWorkerScenario.CrashAfterStart] = "crash-after-start",
            [TestWorkerScenario.HangBeforeHello] = "hang-before-hello",
            [TestWorkerScenario.HangAfterHello] = "hang-after-hello",
            [TestWorkerScenario.HangAfterStart] = "hang-after-start",
            [TestWorkerScenario.CooperativeCancellation] = "cooperative-cancellation",
            [TestWorkerScenario.IgnoreCancellation] = "ignore-cancellation",
            [TestWorkerScenario.SpawnChildAndWait] = "spawn-child-and-wait",
            [TestWorkerScenario.ExitRootWithLiveChild] = "exit-root-with-live-child",
            [TestWorkerScenario.FloodStdout] = "flood-stdout",
            [TestWorkerScenario.FloodStderr] = "flood-stderr",
            [TestWorkerScenario.TerminalExitMismatch] = "terminal-exit-mismatch",
            [TestWorkerScenario.EchoEnvironmentKeys] = "echo-environment-keys",
            [TestWorkerScenario.ProbeUnrelatedHandle] = "probe-unrelated-handle"
        };

    [TestMethod]
    public void EveryPublicScenarioHasOneExactKebabCaseName()
    {
        foreach ((TestWorkerScenario scenario, string name) in ExpectedNames)
        {
            bool parsed = TestWorkerScenarioParser.TryParse(
                [name],
                out TestWorkerScenarioRequest? request);

            Assert.IsTrue(parsed, name);
            Assert.IsNotNull(request, name);
            Assert.AreEqual(scenario, request.Scenario, name);
        }
    }

    [DataTestMethod]
    [DataRow("unknown")]
    [DataRow("CrashAfterHello")]
    [DataRow("crash_after_hello")]
    public void UnknownOrNonCanonicalNameIsRejected(string name)
    {
        Assert.IsFalse(
            TestWorkerScenarioParser.TryParse([name], out _));
    }

    [TestMethod]
    public void ExtraArgumentsAreRejectedForOrdinaryScenarios()
    {
        Assert.IsFalse(
            TestWorkerScenarioParser.TryParse(
                ["healthy-controlled-failure", "unexpected"],
                out _));
    }

    [TestMethod]
    public void HandleProbeRequiresOneNumericSafeValue()
    {
        Assert.IsTrue(
            TestWorkerScenarioParser.TryParse(
                ["probe-unrelated-handle", "1234"],
                out TestWorkerScenarioRequest? request));
        Assert.AreEqual(1234L, request?.NumericValue);
        Assert.IsFalse(
            TestWorkerScenarioParser.TryParse(
                ["probe-unrelated-handle", "not-a-handle"],
                out _));
    }
}
