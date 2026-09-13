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
            [TestWorkerScenario.CompletionAfterCancellation] = "completion-after-cancellation",
            [TestWorkerScenario.IgnoreCancellation] = "ignore-cancellation",
            [TestWorkerScenario.SpawnChildAndWait] = "spawn-child-and-wait",
            [TestWorkerScenario.ExitRootWithLiveChild] = "exit-root-with-live-child",
            [TestWorkerScenario.FloodStdout] = "flood-stdout",
            [TestWorkerScenario.FloodStderr] = "flood-stderr",
            [TestWorkerScenario.TerminalExitMismatch] = "terminal-exit-mismatch",
            [TestWorkerScenario.EchoEnvironmentKeys] = "echo-environment-keys",
            [TestWorkerScenario.ProbeUnrelatedHandle] = "probe-unrelated-handle",
            [TestWorkerScenario.ObserveParentIdentity] = "observe-parent-identity",
            [TestWorkerScenario.ChildProcessWait] = "child-process-wait"
        };

    [TestMethod]
    public void EveryPublicScenarioHasOneExactProtocolSelector()
    {
        foreach ((TestWorkerScenario scenario, string name) in ExpectedNames)
        {
            long version = scenario == TestWorkerScenario.ProbeUnrelatedHandle
                ? 1234
                : 1;
            string[] arguments = FixtureArguments(name, version);

            bool parsed = TestWorkerScenarioParser.TryParse(
                arguments,
                out TestWorkerScenarioRequest? request);

            Assert.IsTrue(parsed, name);
            Assert.IsNotNull(request, name);
            Assert.AreEqual(scenario, request.Scenario, name);
        }
    }

    [TestMethod]
    [DataRow("modelinspection.fixture.unknown/1")]
    [DataRow("modelinspection.fixture.CrashAfterHello/1")]
    [DataRow("modelinspection.fixture.crash_after_hello/1")]
    public void UnknownOrNonCanonicalNameIsRejected(string name)
    {
        Assert.IsFalse(
            TestWorkerScenarioParser.TryParse(["--protocol", name], out _));
        Assert.IsFalse(
            TestWorkerScenarioParser.TryParse([name], out _));
    }

    [TestMethod]
    public void ExtraArgumentsAreRejectedForOrdinaryScenarios()
    {
        Assert.IsFalse(
            TestWorkerScenarioParser.TryParse(
                [
                    "--protocol",
                    "modelinspection.fixture.healthy-controlled-failure/1",
                    "unexpected"
                ],
                out _));
    }

    [TestMethod]
    public void HandleProbeRequiresOneNumericSafeValue()
    {
        Assert.IsTrue(
            TestWorkerScenarioParser.TryParse(
                FixtureArguments("probe-unrelated-handle", 1234),
                out TestWorkerScenarioRequest? request));
        Assert.AreEqual(1234L, request?.NumericValue);
        Assert.IsFalse(
            TestWorkerScenarioParser.TryParse(
                [
                    "--protocol",
                    "modelinspection.fixture.probe-unrelated-handle/not-a-handle"
                ],
                out _));
    }

    [TestMethod]
    public void CooperativeCancellationAcceptsOnlyOneBoundedHelloDelay()
    {
        Assert.IsTrue(
            TestWorkerScenarioParser.TryParse(
                FixtureArguments("cooperative-cancellation", 700),
                out TestWorkerScenarioRequest? request));
        Assert.AreEqual(700L, request?.NumericValue);

        foreach (string invalid in new[] { "0", "5001", "not-a-delay" })
        {
            Assert.IsFalse(
                TestWorkerScenarioParser.TryParse(
                    [
                        "--protocol",
                        $"modelinspection.fixture.cooperative-cancellation/{invalid}"
                    ],
                    out _),
                invalid);
        }

        Assert.IsFalse(
            TestWorkerScenarioParser.TryParse(
                [
                    "--protocol",
                    "modelinspection.fixture.cooperative-cancellation/700",
                    "unexpected"
                ],
                out _));
    }

    private static string[] FixtureArguments(string scenario, long version) =>
    [
        "--protocol",
        $"modelinspection.fixture.{scenario}/{version}"
    ];
}
