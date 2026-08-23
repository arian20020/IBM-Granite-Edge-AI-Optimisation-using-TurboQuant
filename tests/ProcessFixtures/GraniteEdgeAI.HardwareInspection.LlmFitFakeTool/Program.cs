using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Text;

return await RunAsync(args).ConfigureAwait(false);

static async Task<int> RunAsync(string[] arguments)
{
    string modePath = Path.Combine(Directory.GetCurrentDirectory(), "fake-mode.txt");
    string? mode = await ReadClosedModeAsync(modePath).ConfigureAwait(false);
    if (mode is null)
    {
        return 64;
    }

    string controlRoot = Path.GetFullPath(Path.Combine(
        Path.GetTempPath(),
        "GraniteEdgeAI.HardwareInspection.Tests",
        "LlmFitFake",
        mode));
    Directory.CreateDirectory(controlRoot);
    if (arguments.SequenceEqual(["--version"], StringComparer.Ordinal))
    {
        Console.Out.WriteLine(mode == "version-mismatch" ? "llmfit 9.9.9" : "llmfit 1.1.9");
        return 0;
    }

    if (!arguments.SequenceEqual(["--no-dashboard", "--json", "system"], StringComparer.Ordinal))
    {
        return 64;
    }

    if (mode is "sleep" or "sleep-child")
    {
        await File.WriteAllTextAsync(
                Path.Combine(controlRoot, "owned-root-ready.txt"),
                Environment.ProcessId.ToString(CultureInfo.InvariantCulture))
            .ConfigureAwait(false);
    }

    return mode switch
    {
        "success" => WriteSuccess(),
        "invalid-json" => WriteInvalidJson(),
        "nonzero" => WriteNonzero(),
        "large-output" => await WriteLargeOutputAsync().ConfigureAwait(false),
        "sleep" => await SleepAsync().ConfigureAwait(false),
        "spawn-child" => await SpawnChildAsync(controlRoot, waitForCancellation: true).ConfigureAwait(false),
        "spawn-child-exit" => await SpawnChildAsync(controlRoot, waitForCancellation: false).ConfigureAwait(false),
        "assert-in-job" => JobMembership.IsCurrentProcessInJob() ? WriteSuccess() : 91,
        "sleep-child" => await SleepAsync().ConfigureAwait(false),
        "dashboard" => await ListenOnDashboardPortAsync().ConfigureAwait(false),
        _ => 64,
    };
}

static async Task<string?> ReadClosedModeAsync(string modePath)
{
    string[] closedModes =
    [
        "assert-in-job",
        "dashboard",
        "invalid-json",
        "large-output",
        "nonzero",
        "sleep",
        "sleep-child",
        "spawn-child",
        "spawn-child-exit",
        "success",
        "version-mismatch",
    ];

    if (!File.Exists(modePath))
    {
        return null;
    }

    byte[] bytes = await File.ReadAllBytesAsync(modePath).ConfigureAwait(false);
    foreach (string candidate in closedModes)
    {
        if (bytes.AsSpan().SequenceEqual(Encoding.UTF8.GetBytes(candidate + Environment.NewLine)))
        {
            return candidate;
        }
    }

    return null;
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

static async Task<int> SpawnChildAsync(string controlRoot, bool waitForCancellation)
{
    string? processPath = Environment.ProcessPath;
    if (string.IsNullOrWhiteSpace(processPath))
    {
        return 64;
    }

    string childDirectory = Path.Combine(
        controlRoot,
        "owned-child-" + Guid.NewGuid().ToString("N"));
    Directory.CreateDirectory(childDirectory);
    await File.WriteAllTextAsync(
            Path.Combine(childDirectory, "fake-mode.txt"),
            "sleep-child" + Environment.NewLine)
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
    await File.WriteAllTextAsync(
            Path.Combine(controlRoot, "spawn-child-ready.txt"),
            child.Id.ToString(CultureInfo.InvariantCulture))
        .ConfigureAwait(false);
    if (waitForCancellation)
    {
        await Task.Delay(TimeSpan.FromMinutes(5)).ConfigureAwait(false);
    }
    else if (!await WaitForChildObservationAsync(controlRoot).ConfigureAwait(false))
    {
        return 64;
    }

    return 0;
}

static async Task<bool> WaitForChildObservationAsync(string controlRoot)
{
    string marker = Path.Combine(controlRoot, "spawn-child-observed.txt");
    Stopwatch elapsed = Stopwatch.StartNew();
    while (elapsed.Elapsed < TimeSpan.FromSeconds(30))
    {
        if (File.Exists(marker))
        {
            return true;
        }

        await Task.Delay(TimeSpan.FromMilliseconds(20)).ConfigureAwait(false);
    }

    return false;
}

static int WriteInvalidJson()
{
    Console.Out.WriteLine("{\"system\":");
    return 0;
}

static async Task<int> ListenOnDashboardPortAsync()
{
    using var listener = new TcpListener(IPAddress.Loopback, 8787);
    listener.Start();
    await Task.Delay(Timeout.InfiniteTimeSpan).ConfigureAwait(false);
    return 0;
}

internal static class JobMembership
{
    internal static bool IsCurrentProcessInJob()
    {
        using Process current = Process.GetCurrentProcess();
        return IsProcessInJob(current.Handle, IntPtr.Zero, out bool inJob) && inJob;
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsProcessInJob(
        IntPtr process,
        IntPtr job,
        [MarshalAs(UnmanagedType.Bool)] out bool result);
}
