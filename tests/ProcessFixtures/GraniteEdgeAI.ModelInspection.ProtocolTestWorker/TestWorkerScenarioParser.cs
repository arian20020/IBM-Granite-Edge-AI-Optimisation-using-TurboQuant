using System.Globalization;

namespace GraniteEdgeAI.ModelInspection.ProtocolTestWorker;

/// <summary>
/// Parses the exact test-only kebab-case vocabulary. Unknown or extra values
/// fail closed and Program returns the conventional usage exit code 64.
/// </summary>
internal static class TestWorkerScenarioParser
{
    private const string ProtocolSelector = "--protocol";
    private const string FixtureRoutePrefix = "modelinspection.fixture.";

    private static readonly Dictionary<string, TestWorkerScenario> Scenarios =
        new(StringComparer.Ordinal)
        {
            ["launch-probe"] = TestWorkerScenario.LaunchProbe,
            ["healthy-controlled-failure"] = TestWorkerScenario.HealthyControlledFailure,
            ["no-hello"] = TestWorkerScenario.NoHello,
            ["malformed-hello"] = TestWorkerScenario.MalformedHello,
            ["wrong-protocol-version"] = TestWorkerScenario.WrongProtocolVersion,
            ["wrong-worker-id"] = TestWorkerScenario.WrongWorkerId,
            ["wrong-worker-process-id"] = TestWorkerScenario.WrongWorkerProcessId,
            ["wrong-runtime-profile"] = TestWorkerScenario.WrongRuntimeProfile,
            ["wrong-architecture"] = TestWorkerScenario.WrongArchitecture,
            ["text-before-hello"] = TestWorkerScenario.TextBeforeHello,
            ["invalid-utf8"] = TestWorkerScenario.InvalidUtf8,
            ["utf8-bom"] = TestWorkerScenario.Utf8Bom,
            ["oversized-stdout-line"] = TestWorkerScenario.OversizedStdoutLine,
            ["malformed-json"] = TestWorkerScenario.MalformedJson,
            ["duplicate-json-property"] = TestWorkerScenario.DuplicateJsonProperty,
            ["wrong-request-id"] = TestWorkerScenario.WrongRequestId,
            ["progress-before-started"] = TestWorkerScenario.ProgressBeforeStarted,
            ["non-monotonic-progress"] = TestWorkerScenario.NonMonotonicProgress,
            ["duplicate-terminal"] = TestWorkerScenario.DuplicateTerminal,
            ["exit-without-terminal"] = TestWorkerScenario.ExitWithoutTerminal,
            ["crash-before-hello"] = TestWorkerScenario.CrashBeforeHello,
            ["crash-after-hello"] = TestWorkerScenario.CrashAfterHello,
            ["crash-after-start"] = TestWorkerScenario.CrashAfterStart,
            ["hang-before-hello"] = TestWorkerScenario.HangBeforeHello,
            ["hang-after-hello"] = TestWorkerScenario.HangAfterHello,
            ["hang-after-start"] = TestWorkerScenario.HangAfterStart,
            ["cooperative-cancellation"] = TestWorkerScenario.CooperativeCancellation,
            ["completion-after-cancellation"] = TestWorkerScenario.CompletionAfterCancellation,
            ["ignore-cancellation"] = TestWorkerScenario.IgnoreCancellation,
            ["spawn-child-and-wait"] = TestWorkerScenario.SpawnChildAndWait,
            ["exit-root-with-live-child"] = TestWorkerScenario.ExitRootWithLiveChild,
            ["flood-stdout"] = TestWorkerScenario.FloodStdout,
            ["flood-stderr"] = TestWorkerScenario.FloodStderr,
            ["terminal-exit-mismatch"] = TestWorkerScenario.TerminalExitMismatch,
            ["echo-environment-keys"] = TestWorkerScenario.EchoEnvironmentKeys,
            ["probe-unrelated-handle"] = TestWorkerScenario.ProbeUnrelatedHandle,
            ["observe-parent-identity"] = TestWorkerScenario.ObserveParentIdentity,
            ["child-process-wait"] = TestWorkerScenario.ChildProcessWait
        };

    internal static bool TryParse(string[] args, out TestWorkerScenarioRequest? request)
    {
        request = null;
        if (args.Length != 2 ||
            !string.Equals(args[0], ProtocolSelector, StringComparison.Ordinal) ||
            !TryParseIdentifier(
                args[1],
                out TestWorkerScenario scenario,
                out long version))
        {
            return false;
        }

        if (scenario == TestWorkerScenario.ProbeUnrelatedHandle)
        {
            request = new TestWorkerScenarioRequest(scenario, version);
            return true;
        }

        if (scenario == TestWorkerScenario.CooperativeCancellation &&
            version != 1)
        {
            if (version > 5_000)
            {
                return false;
            }

            request = new TestWorkerScenarioRequest(
                scenario,
                version);
            return true;
        }

        if (version != 1)
        {
            return false;
        }

        request = new TestWorkerScenarioRequest(scenario);
        return true;
    }

    private static bool TryParseIdentifier(
        string identifier,
        out TestWorkerScenario scenario,
        out long version)
    {
        scenario = default;
        version = default;
        if (!identifier.StartsWith(
                FixtureRoutePrefix,
                StringComparison.Ordinal))
        {
            return false;
        }

        int separator = identifier.LastIndexOf('/');
        if (separator <= FixtureRoutePrefix.Length ||
            separator == identifier.Length - 1)
        {
            return false;
        }

        string scenarioName = identifier[
            FixtureRoutePrefix.Length..separator];
        string versionText = identifier[(separator + 1)..];
        return Scenarios.TryGetValue(scenarioName, out scenario) &&
            long.TryParse(
                versionText,
                NumberStyles.None,
                CultureInfo.InvariantCulture,
                out version) &&
            version > 0 &&
            string.Equals(
                versionText,
                version.ToString(CultureInfo.InvariantCulture),
                StringComparison.Ordinal);
    }
}
