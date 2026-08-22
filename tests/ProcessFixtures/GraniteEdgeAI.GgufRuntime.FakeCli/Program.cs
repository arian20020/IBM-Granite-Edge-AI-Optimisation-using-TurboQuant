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
var historyRoles = new System.Text.StringBuilder();
while (await Console.In.ReadLineAsync() is { } startFrame)
{
    if (startFrame.Equals("G1START", StringComparison.Ordinal))
    {
        break;
    }

    string[] parts = startFrame.Split(' ', 3, StringSplitOptions.None);
    if (parts.Length != 3 ||
        !parts[0].Equals("G1TURN", StringComparison.Ordinal) ||
        (parts[1] != "U" && parts[1] != "A"))
    {
        return 65;
    }

    _ = Decode(parts[2]);
    historyRoles.Append(parts[1]);
}

Console.WriteLine("G1READY");
Console.Out.Flush();

if (options.Scenario == FakeCliScenario.Hang)
{
    await Task.Delay(Timeout.InfiniteTimeSpan);
    return 0;
}

if (options.Scenario == FakeCliScenario.MalformedOutput)
{
    Console.WriteLine("G1UNKNOWN");
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
while (await Console.In.ReadLineAsync() is { } frame)
{
    if (frame.Equals("G1STOP", StringComparison.Ordinal) &&
        options.Scenario != FakeCliScenario.IgnoreStop)
    {
        Console.WriteLine("G1DONE");
        Console.Out.Flush();
        continue;
    }

    if (!frame.StartsWith("G1PROMPT ", StringComparison.Ordinal))
    {
        return 66;
    }

    string prompt = Decode(frame[9..]);

    turn++;
    Console.WriteLine("G1RESPONSE");
    Console.Out.Flush();
    if (options.Scenario is FakeCliScenario.Slow or FakeCliScenario.IgnoreStop)
    {
        for (int chunk = 0; chunk < 20; chunk++)
        {
            WriteDelta($"slow-{turn:D2}-{chunk:D2}");
            Console.Out.Flush();
            await Task.Delay(50);
        }
    }
    else
    {
        string history = historyRoles.Length == 0
            ? string.Empty
            : $"history={historyRoles}:";
        WriteDelta($"fake-response-{turn:D2}:{history}{prompt}");
        Console.Out.Flush();
    }

    Console.WriteLine("G1DONE");
    Console.Out.Flush();
}

return 0;

static string Decode(string content) =>
    System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(content));

static void WriteDelta(string content) => Console.WriteLine(
    $"G1DELTA {Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(content))}");
