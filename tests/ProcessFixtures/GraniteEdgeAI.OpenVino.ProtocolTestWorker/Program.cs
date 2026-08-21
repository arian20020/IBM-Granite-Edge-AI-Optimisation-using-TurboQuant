using System.Diagnostics;
using System.Security.Cryptography;
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

        if (Environment.GetEnvironmentVariable("GRANITE_OPENVINO_FIXTURE_CHILD") ==
            "1")
        {
            await Task.Delay(TimeSpan.FromMinutes(5)).ConfigureAwait(false);
            return 0;
        }

        string scenario = ReadScenario();
        Process? cleanupInventoryChild = scenario == "cleanup-inventory"
            ? StartContainedChildAndWriteInventory()
            : null;
        if (scenario == "startup-timeout")
        {
            await Task.Delay(TimeSpan.FromSeconds(5)).ConfigureAwait(false);
        }

        if (scenario == "split-startup-timeout")
        {
            await Task.Delay(TimeSpan.FromMilliseconds(1800)).ConfigureAwait(false);
        }

        await using Stream input = Console.OpenStandardInput();
        await using Stream output = Console.OpenStandardOutput();
        BoundedUtf8LineReader reader = new(input, OpenVinoProtocol.MaximumLineBytes);
        using BoundedUtf8LineWriter writer = new(output, OpenVinoProtocol.MaximumLineBytes);
        string helloProtocol = scenario == "wrong-protocol"
            ? OpenVinoProtocol.TurboQuantProtocolId
            : OpenVinoProtocol.OfficialProtocolId;
        OpenVinoBuildEvidence helloEvidence = scenario == "wrong-build-evidence"
            ? BuildEvidence() with { WorkerManifestDigest = new string('2', 64) }
            : BuildEvidence();
        await WriteAsync(writer, new HelloEvent(helloProtocol, helloEvidence))
            .ConfigureAwait(false);

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
            using Process child = StartContainedChildAndWriteInventory();
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
            if (cleanupInventoryChild is not null)
            {
                cleanupInventoryChild.Kill();
                await cleanupInventoryChild.WaitForExitAsync()
                    .ConfigureAwait(false);
                cleanupInventoryChild.Dispose();
            }

            await WriteInspectionSuccessAsync(writer, inspection)
                .ConfigureAwait(false);
            if (scenario == "stdout-after-terminal")
            {
                await WriteAsync(
                        writer,
                        InspectionCompleted(inspection))
                    .ConfigureAwait(false);
            }

            return 0;
        }

        if (first is not StartSessionCommand start)
        {
            return 66;
        }

        if (scenario == "split-startup-timeout")
        {
            await Task.Delay(TimeSpan.FromMilliseconds(1800)).ConfigureAwait(false);
        }

        await WriteAsync(
                writer,
                new SessionStartedEvent(
                    start.SessionId,
                    start.Device.DeviceId,
                    [start.Device.DeviceId],
                    OpenVinoProtocol.OfficialProtocolId,
                    BuildEvidence()))
            .ConfigureAwait(false);
        if (scenario == "blocked-cancel-write")
        {
            WriteMarker("stdin-abandoned");
            await Task.Delay(TimeSpan.FromSeconds(5)).ConfigureAwait(false);
            return 0;
        }
        if (scenario == "blocked-prompt-write")
        {
            await Task.Delay(TimeSpan.FromSeconds(5)).ConfigureAwait(false);
            return 0;
        }
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
                if (scenario == "ignore-cancel")
                {
                    await Task.Delay(TimeSpan.FromSeconds(5)).ConfigureAwait(false);
                    return 0;
                }

                await WriteAsync(writer, new SessionCancelledEvent(cancel.SessionId))
                    .ConfigureAwait(false);
                if (scenario == "session-stdout-after-terminal")
                {
                    await WriteAsync(
                            writer,
                            new SessionCancelledEvent(cancel.SessionId))
                        .ConfigureAwait(false);
                }

                if (scenario == "cancel-delayed-exit")
                {
                    await Task.Delay(TimeSpan.FromMilliseconds(400))
                        .ConfigureAwait(false);
                    WriteMarker("cancel-exited");
                }

                return 0;
            }

            if (command is CloseSessionCommand close)
            {
                await WriteAsync(writer, new SessionCompletedEvent(close.SessionId))
                    .ConfigureAwait(false);
                WriteMarker("close-exited");
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
            if (scenario is "active-external-cancel" or
                "active-slow-cancel-exit" or
                "active-failed-cancel")
            {
                WriteMarker("generation-started");
                byte[]? cancelLine = await reader
                    .ReadLineAsync(CancellationToken.None)
                    .ConfigureAwait(false);
                if (cancelLine is null ||
                    OpenVinoProtocolJson.DeserializeCommand(cancelLine) is not
                        CancelSessionCommand externalCancel)
                {
                    return 68;
                }

                IOpenVinoEvent terminal = scenario == "active-failed-cancel"
                    ? new SessionFailedEvent(
                        externalCancel.SessionId,
                        OpenVinoSupportCode.RuntimeProtocolFailed)
                    : new SessionCancelledEvent(externalCancel.SessionId);
                await WriteAsync(writer, terminal).ConfigureAwait(false);
                if (scenario == "active-failed-cancel")
                {
                    return 0;
                }

                TimeSpan exitDelay = scenario == "active-slow-cancel-exit"
                    ? TimeSpan.FromSeconds(5)
                    : TimeSpan.FromMilliseconds(400);
                await Task.Delay(exitDelay)
                    .ConfigureAwait(false);
                WriteMarker("cancel-exited");
                return 0;
            }

            if (scenario == "active-ignore-cancel")
            {
                WriteMarker("generation-started");
                _ = await reader.ReadLineAsync(CancellationToken.None)
                    .ConfigureAwait(false);
                await Task.Delay(TimeSpan.FromSeconds(5)).ConfigureAwait(false);
                return 0;
            }

            if (scenario is "turn-timeout" or "idle-timeout")
            {
                await Task.Delay(TimeSpan.FromSeconds(5)).ConfigureAwait(false);
                return 0;
            }

            if (scenario == "stale-idle-timeout")
            {
                Task<byte[]?> cancelRead = reader
                    .ReadLineAsync(CancellationToken.None)
                    .AsTask();
                for (int stale = 0; stale < 8; stale++)
                {
                    Task tick = Task.Delay(TimeSpan.FromMilliseconds(75));
                    Task winner = await Task.WhenAny(cancelRead, tick)
                        .ConfigureAwait(false);
                    if (winner == cancelRead)
                    {
                        byte[]? cancelPayload = await cancelRead.ConfigureAwait(false);
                        if (cancelPayload is not null &&
                            OpenVinoProtocolJson.DeserializeCommand(cancelPayload) is
                                CancelSessionCommand staleCancel)
                        {
                            await WriteAsync(
                                    writer,
                                    new SessionCancelledEvent(staleCancel.SessionId))
                                .ConfigureAwait(false);
                        }

                        return 0;
                    }

                    await WriteAsync(
                            writer,
                            new TokenEvent(
                                Guid.NewGuid(),
                                Guid.NewGuid(),
                                0,
                                "stale"))
                        .ConfigureAwait(false);
                    if (stale == 4)
                    {
                        WriteMarker("stale-kept-alive");
                    }
                }

                await Task.Delay(TimeSpan.FromSeconds(5)).ConfigureAwait(false);
                return 0;
            }

            if ((scenario == "partial-stop-next" && turn == 0) ||
                scenario == "partial-stop-each")
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
                        new TurnCompletedEvent(
                            prompt.SessionId,
                            prompt.TurnId,
                            1,
                            0,
                            OpenVinoTurnDisposition.Stopped))
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
                    new TurnCompletedEvent(
                        prompt.SessionId,
                        prompt.TurnId,
                        1,
                        1,
                        OpenVinoTurnDisposition.Completed))
                .ConfigureAwait(false);
            turn++;
        }
    }

    private static string ReadScenario()
    {
        string path = Path.Combine(AppContext.BaseDirectory, "scenario.txt");
        return File.Exists(path) ? File.ReadAllText(path).Trim() : "valid-two-turn-stale";
    }

    private static void WriteMarker(string name) => File.WriteAllText(
        Path.Combine(AppContext.BaseDirectory, name + ".marker"),
        "complete");

    private static Process StartContainedChildAndWriteInventory()
    {
        string executable = Environment.ProcessPath
            ?? throw new InvalidOperationException(
                "The fixture process path is unavailable.");
        ProcessStartInfo childStart = new(executable)
        {
            UseShellExecute = false,
            CreateNoWindow = true
        };
        childStart.ArgumentList.Add("--protocol");
        childStart.ArgumentList.Add(OpenVinoProtocol.OfficialProtocolId);
        childStart.Environment["GRANITE_OPENVINO_FIXTURE_CHILD"] = "1";
        Process child = Process.Start(childStart)
            ?? throw new InvalidOperationException(
                "The contained fixture child did not start.");
        File.WriteAllLines(
            Path.Combine(AppContext.BaseDirectory, "process-inventory.txt"),
            [
                "root=" + Environment.ProcessId,
                "child=" + child.Id
            ]);
        return child;
    }

    private static ValueTask WriteAsync(
        BoundedUtf8LineWriter writer,
        IOpenVinoEvent @event) =>
        writer.WriteLineAsync(
            OpenVinoProtocolJson.Serialize(@event),
            CancellationToken.None);

    private static async Task WriteInspectionSuccessAsync(
        BoundedUtf8LineWriter writer,
        StartInspectionCommand inspection)
    {
        await WriteAsync(
                writer,
                new InspectionStartedEvent(inspection.InspectionRunId))
            .ConfigureAwait(false);
        foreach (OpenVinoInspectionStage stage in new[]
        {
            OpenVinoInspectionStage.ManifestVerified,
            OpenVinoInspectionStage.MainModelParsed,
            OpenVinoInspectionStage.TokenizerParsed,
            OpenVinoInspectionStage.DetokenizerParsed
        })
        {
            await WriteAsync(
                    writer,
                    new InspectionProgressEvent(
                        inspection.InspectionRunId,
                        stage))
                .ConfigureAwait(false);
        }

        await WriteAsync(writer, InspectionCompleted(inspection))
            .ConfigureAwait(false);
    }

    private static InspectionCompletedEvent InspectionCompleted(
        StartInspectionCommand inspection) => new(
        inspection.InspectionRunId,
        inspection.PackageManifestDigest,
        inspection.ModelSha256,
        inspection.ModelLengthBytes,
        true,
        true,
        true,
        BuildEvidence());

    private static OpenVinoBuildEvidence BuildEvidence()
    {
        string root = Path.GetDirectoryName(Environment.ProcessPath)
            ?? throw new InvalidOperationException(
                "fixture executable root is unavailable");
        string manifestDigest = Convert.ToHexString(SHA256.HashData(
            File.ReadAllBytes(Path.Combine(root, "worker-manifest.json"))))
            .ToLowerInvariant();
        return new OpenVinoBuildEvidence(
            "fixture-runtime-2026.3.0",
            "fixture-genai-2026.3.0.0",
            "fixture-tokenizers-2026.3.0.0",
            manifestDigest);
    }

    private static async Task WriteRawAsync(Stream output, byte[] payload)
    {
        await output.WriteAsync(payload).ConfigureAwait(false);
        await output.FlushAsync().ConfigureAwait(false);
    }
}
