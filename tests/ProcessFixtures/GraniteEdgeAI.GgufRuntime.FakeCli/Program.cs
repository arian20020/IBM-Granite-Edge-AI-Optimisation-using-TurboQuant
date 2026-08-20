using GraniteEdgeAI.GgufRuntime.FakeCli;

if (!FakeCliOptions.TryParse(args, out FakeCliOptions? options))
{
    return 64;
}

if (options!.Scenario == FakeCliScenario.EarlyExit)
{
    return 23;
}

Console.OutputEncoding = System.Text.Encoding.UTF8;
Console.WriteLine("__G1_READY__");
Console.Out.Flush();

if (options.Scenario == FakeCliScenario.Hang)
{
    await Task.Delay(Timeout.InfiniteTimeSpan);
    return 0;
}

if (options.Scenario == FakeCliScenario.MalformedOutput)
{
    Console.WriteLine("__G1_UNKNOWN_CONTROL__");
    Console.Out.Flush();
    return 24;
}

if (options.Scenario == FakeCliScenario.StandardErrorFlood)
{
    for (int index = 0; index < 2_048; index++)
    {
        Console.Error.WriteLine($"diagnostic-{index:D4}");
    }

    Console.Error.Flush();
}

int turn = 0;
while (await Console.In.ReadLineAsync() is { } prompt)
{
    if (prompt.Equals("__G1_STOP__", StringComparison.Ordinal) &&
        options.Scenario != FakeCliScenario.IgnoreStop)
    {
        Console.WriteLine("__G1_RESPONSE_DONE__");
        Console.Out.Flush();
        continue;
    }

    turn++;
    Console.WriteLine("__G1_RESPONSE_START__");
    Console.Out.Flush();
    if (options.Scenario is FakeCliScenario.Slow or FakeCliScenario.IgnoreStop)
    {
        for (int chunk = 0; chunk < 20; chunk++)
        {
            Console.WriteLine($"slow-{turn:D2}-{chunk:D2}");
            Console.Out.Flush();
            await Task.Delay(50);
        }
    }
    else
    {
        Console.WriteLine($"fake-response-{turn:D2}:{prompt}");
        Console.Out.Flush();
    }

    Console.WriteLine("__G1_RESPONSE_DONE__");
    Console.Out.Flush();
}

return 0;
