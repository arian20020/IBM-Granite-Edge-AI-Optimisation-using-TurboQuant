namespace GraniteEdgeAI.GgufRuntime.FakeCli;

internal enum FakeCliScenario
{
    TwoTurn,
    Slow,
    IgnoreStop,
    MalformedOutput,
    StandardErrorFlood,
    EarlyExit,
    Hang,
}

internal sealed record FakeCliOptions(FakeCliScenario Scenario)
{
    internal static bool TryParse(string[] args, out FakeCliOptions? options)
    {
        options = null;
        if (args.Length != 2 || !args[0].Equals("--scenario", StringComparison.Ordinal))
        {
            return false;
        }

        FakeCliScenario? scenario = args[1] switch
        {
            "two-turn" => FakeCliScenario.TwoTurn,
            "slow" => FakeCliScenario.Slow,
            "ignore-stop" => FakeCliScenario.IgnoreStop,
            "malformed-output" => FakeCliScenario.MalformedOutput,
            "stderr-flood" => FakeCliScenario.StandardErrorFlood,
            "early-exit" => FakeCliScenario.EarlyExit,
            "hang" => FakeCliScenario.Hang,
            _ => null,
        };
        if (scenario is null)
        {
            return false;
        }

        options = new FakeCliOptions(scenario.Value);
        return true;
    }
}
