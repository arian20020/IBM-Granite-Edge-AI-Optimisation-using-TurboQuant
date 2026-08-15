using System.Diagnostics;
using System.Net;
using System.Net.Sockets;

return await RunAsync(args).ConfigureAwait(false);

static async Task<int> RunAsync(string[] arguments)
{
    if (arguments.SequenceEqual(["--version"], StringComparer.Ordinal))
    {
        Console.Out.WriteLine("llmfit 1.1.9");
        return 0;
    }

    if (!arguments.SequenceEqual(["--no-dashboard", "--json", "system"], StringComparer.Ordinal))
    {
        return 64;
    }

    string modePath = Path.Combine(Directory.GetCurrentDirectory(), "fake-mode.txt");
    if (!File.Exists(modePath))
    {
        return 64;
    }

    string mode = await File.ReadAllTextAsync(modePath).ConfigureAwait(false);
    return mode switch
    {
        "success" => WriteSuccess(),
        "nonzero" => WriteNonzero(),
        "large-output" => await WriteLargeOutputAsync().ConfigureAwait(false),
        "sleep" => await SleepAsync().ConfigureAwait(false),
        "spawn-child" => await SpawnChildAsync().ConfigureAwait(false),
        "sleep-child" => await SleepAsync().ConfigureAwait(false),
        "dashboard" => await ListenOnDashboardPortAsync().ConfigureAwait(false),
        _ => 64,
    };
}

static int WriteSuccess()
{
    Console.Out.WriteLine(
        "{\"system\":{\"total_ram_gb\":32,\"available_ram_gb\":16," +
        "\"cpu_cores\":8,\"cpu_name\":\"Fixture CPU\",\"has_gpu\":false," +
        "\"gpu_count\":0,\"gpu_name\":null,\"gpus\":[]}}");
    return 0;
}

static int WriteNonzero()
{
    Console.Error.WriteLine("fake llmfit requested a nonzero exit");
    return 23;
}

static async Task<int> WriteLargeOutputAsync()
{
    Task standardOutput = WriteBytesAsync(Console.OpenStandardOutput(), (byte)'O');
    Task standardError = WriteBytesAsync(Console.OpenStandardError(), (byte)'E');
    await Task.WhenAll(standardOutput, standardError).ConfigureAwait(false);
    return 0;
}

static async Task WriteBytesAsync(Stream stream, byte value)
{
    const int TotalBytes = 2 * 1024 * 1024;
    byte[] block = new byte[16 * 1024];
    Array.Fill(block, value);

    for (int written = 0; written < TotalBytes; written += block.Length)
    {
        await stream.WriteAsync(block).ConfigureAwait(false);
    }

    await stream.FlushAsync().ConfigureAwait(false);
}

static async Task<int> SleepAsync()
{
    await Task.Delay(TimeSpan.FromMinutes(5)).ConfigureAwait(false);
    return 0;
}

static async Task<int> SpawnChildAsync()
{
    string? processPath = Environment.ProcessPath;
    if (string.IsNullOrWhiteSpace(processPath))
    {
        return 64;
    }

    string childDirectory = Path.Combine(
        Directory.GetCurrentDirectory(),
        "owned-child-" + Guid.NewGuid().ToString("N"));
    Directory.CreateDirectory(childDirectory);
    await File.WriteAllTextAsync(
            Path.Combine(childDirectory, "fake-mode.txt"),
            "sleep-child")
        .ConfigureAwait(false);

    var startInfo = new ProcessStartInfo
    {
        FileName = processPath,
        WorkingDirectory = childDirectory,
        UseShellExecute = false,
        CreateNoWindow = true,
    };
    startInfo.ArgumentList.Add("--no-dashboard");
    startInfo.ArgumentList.Add("--json");
    startInfo.ArgumentList.Add("system");

    using var child = new Process { StartInfo = startInfo };
    if (!child.Start())
    {
        return 64;
    }

    Console.Out.WriteLine(child.Id);
    Console.Out.Flush();
    await Task.Delay(TimeSpan.FromMinutes(5)).ConfigureAwait(false);
    return 0;
}

static async Task<int> ListenOnDashboardPortAsync()
{
    using var listener = new TcpListener(IPAddress.Loopback, 8787);
    listener.Start();
    await Task.Delay(Timeout.InfiniteTimeSpan).ConfigureAwait(false);
    return 0;
}
