using System.Diagnostics;
using System.Text;
using GraniteEdgeAI.ModelInspection.Transport;
using GraniteEdgeAI.OpenVino.Contracts;

return await FixtureProgram.RunAsync(args).ConfigureAwait(false);

internal static class FixtureProgram
{
    internal static async Task<int> RunAsync(string[] arguments)
    {
        if (arguments.Length != 2 ||
            arguments[0] != "--protocol" ||
            arguments[1] != OpenVinoProtocol.OfficialProtocolId)
        {
            return 64;
        }

        string scenario = ReadScenario();
        if (scenario == "startup-timeout")
        {
            await Task.Delay(TimeSpan.FromSeconds(5)).ConfigureAwait(false);
        }

        await using Stream input = Console.OpenStandardInput();
        await using Stream output = Console.OpenStandardOutput();
        BoundedUtf8LineReader reader = new(input, OpenVinoProtocol.MaximumLineBytes);
        using BoundedUtf8LineWriter writer = new(output, OpenVinoProtocol.MaximumLineBytes);
        string helloProtocol = scenario == "wrong-protocol"
            ? OpenVinoProtocol.TurboQuantProtocolId
            : OpenVinoProtocol.OfficialProtocolId;
        await WriteAsync(writer, new HelloEvent(helloProtocol)).ConfigureAwait(false);

        if (scenario == "parent-exit")
        {
            return 17;
        }

        if (scenario == "malformed-line")
        {
            await WriteRawAsync(output, Encoding.UTF8.GetBytes("{\n"))
                .ConfigureAwait(false);
            return 0;
        }

        if (scenario == "oversized-line")
        {
            byte[] oversized = new byte[OpenVinoProtocol.MaximumLineBytes + 2];
            Array.Fill(oversized, (byte)'x');
            oversized[^1] = (byte)'\n';
            await WriteRawAsync(output, oversized).ConfigureAwait(false);
            return 0;
        }

        if (scenario == "stderr-overflow")
        {
            await Console.OpenStandardError()
                .WriteAsync(new byte[16 * 1024])
                .ConfigureAwait(false);
            await WriteRawAsync(output, Encoding.UTF8.GetBytes("{\n"))
                .ConfigureAwait(false);
            return 0;
        }

        if (scenario == "child-escape-attempt")
        {
            _ = Process.Start(new ProcessStartInfo("cmd.exe")
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                Arguments = "/d /c ping.exe -t 127.0.0.1 >nul"
            });
            await WriteRawAsync(output, Encoding.UTF8.GetBytes("{\n"))
                .ConfigureAwait(false);
            return 0;
        }

        byte[]? firstLine = await reader.ReadLineAsync(CancellationToken.None)
            .ConfigureAwait(false);
        if (firstLine is null)
        {
            return 65;
        }

        IOpenVinoCommand first = OpenVinoProtocolJson.DeserializeCommand(firstLine);
        if (first is StartInspectionCommand inspection)
        {
            await WriteAsync(
                    writer,
                    new InspectionCompletedEvent(inspection.InspectionRunId))
                .ConfigureAwait(false);
            if (scenario == "stdout-after-terminal")
            {
                await WriteAsync(
                        writer,
                        new InspectionCompletedEvent(inspection.InspectionRunId))
                    .ConfigureAwait(false);
            }

            return 0;
        }

        if (first is not StartSessionCommand start)
        {
            return 66;
        }

        await WriteAsync(writer, new SessionStartedEvent(start.SessionId))
            .ConfigureAwait(false);
        if (scenario == "session-timeout")
        {
            await Task.Delay(TimeSpan.FromSeconds(5)).ConfigureAwait(false);
            return 0;
        }

        int turn = 0;
        while (true)
        {
            byte[]? line = await reader.ReadLineAsync(CancellationToken.None)
                .ConfigureAwait(false);
            if (line is null)
            {
                return 0;
            }

            IOpenVinoCommand command = OpenVinoProtocolJson.DeserializeCommand(line);
            if (command is CancelSessionCommand cancel)
            {
                await WriteAsync(writer, new SessionCancelledEvent(cancel.SessionId))
                    .ConfigureAwait(false);
                return 0;
            }

            if (command is not PromptCommand prompt)
            {
                continue;
            }

            await WriteAsync(
                    writer,
                    new GenerationStartedEvent(prompt.SessionId, prompt.TurnId))
                .ConfigureAwait(false);
            if (scenario is "turn-timeout" or "idle-timeout")
            {
                await Task.Delay(TimeSpan.FromSeconds(5)).ConfigureAwait(false);
                return 0;
            }

            if (scenario == "partial-stop-next" && turn == 0)
            {
                while (true)
                {
                    byte[]? stopLine = await reader
                        .ReadLineAsync(CancellationToken.None)
                        .ConfigureAwait(false);
                    if (stopLine is null)
                    {
                        return 0;
                    }

                    IOpenVinoCommand stop =
                        OpenVinoProtocolJson.DeserializeCommand(stopLine);
                    if (stop is StopTurnCommand)
                    {
                        break;
                    }
                }

                await WriteAsync(
                        writer,
                        new TurnCompletedEvent(prompt.SessionId, prompt.TurnId))
                    .ConfigureAwait(false);
                turn++;
                continue;
            }

            if (scenario == "valid-two-turn-stale" && turn == 0)
            {
                await WriteAsync(
                        writer,
                        new TokenEvent(Guid.NewGuid(), Guid.NewGuid(), 0, "stale"))
                    .ConfigureAwait(false);
            }

            string text = turn == 0 ? "one" : "two";
            await WriteAsync(
                    writer,
                    new TokenEvent(prompt.SessionId, prompt.TurnId, 0, text))
                .ConfigureAwait(false);
            if (scenario == "active-cancel")
            {
                byte[]? cancelLine = await reader
                    .ReadLineAsync(CancellationToken.None)
                    .ConfigureAwait(false);
                if (cancelLine is null ||
                    OpenVinoProtocolJson.DeserializeCommand(cancelLine) is not
                        CancelSessionCommand activeCancel)
                {
                    return 67;
                }

                await WriteAsync(
                        writer,
                        new SessionCancelledEvent(activeCancel.SessionId))
                    .ConfigureAwait(false);
                return 0;
            }

            await WriteAsync(
                    writer,
                    new TurnCompletedEvent(prompt.SessionId, prompt.TurnId))
                .ConfigureAwait(false);
            turn++;
        }
    }

    private static string ReadScenario()
    {
        string path = Path.Combine(AppContext.BaseDirectory, "scenario.txt");
        return File.Exists(path) ? File.ReadAllText(path).Trim() : "valid-two-turn-stale";
    }

    private static ValueTask WriteAsync(
        BoundedUtf8LineWriter writer,
        IOpenVinoEvent @event) =>
        writer.WriteLineAsync(
            OpenVinoProtocolJson.Serialize(@event),
            CancellationToken.None);

    private static async Task WriteRawAsync(Stream output, byte[] payload)
    {
        await output.WriteAsync(payload).ConfigureAwait(false);
        await output.FlushAsync().ConfigureAwait(false);
    }
}
