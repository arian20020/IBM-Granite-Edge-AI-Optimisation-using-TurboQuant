using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;

return await RunAsync(args).ConfigureAwait(false);

static async Task<int> RunAsync(string[] arguments)
{
    string? mode = await ReadClosedModeAsync(Path.Combine(
        Directory.GetCurrentDirectory(),
        "fake-mode.txt")).ConfigureAwait(false);
    if (mode is null)
    {
        return 64;
    }

    string controlRoot = Path.GetFullPath(Path.Combine(
        Path.GetTempPath(),
        "GraniteEdgeAI.HardwareInspection.Tests",
        "LlamaCppProbeFake",
        mode));
    Directory.CreateDirectory(controlRoot);

    if (arguments.SequenceEqual(["identity", "--format", "json-v1"], StringComparer.Ordinal))
    {
        WriteUtf8Line(mode == "identity-mismatch"
            ? FixtureProtocol.IdentityMismatchJson
            : FixtureProtocol.IdentityJson);
        return 0;
    }

    if (!arguments.SequenceEqual(["capabilities", "--format", "json-v1"], StringComparer.Ordinal))
    {
        return 64;
    }

    return mode switch
    {
        "success" => WriteCapabilities(),
        "identity-mismatch" => 64,
        "invalid-json" => WriteInvalidJson(),
        "nonzero" => 23,
        "large-output" => await WriteLargeOutputAsync().ConfigureAwait(false),
        "sleep" or "child-sleep" => await SleepAsync(controlRoot).ConfigureAwait(false),
        "assert-in-job" => JobMembership.IsCurrentProcessInJob() ? WriteCapabilities() : 91,
        "spawn-child" => await SpawnChildAsync(controlRoot, waitForCancellation: true).ConfigureAwait(false),
        "spawn-child-exit" => await SpawnChildAsync(controlRoot, waitForCancellation: false).ConfigureAwait(false),
        _ => 64,
    };
}

static async Task<string?> ReadClosedModeAsync(string path)
{
    string[] modes =
    [
        "assert-in-job",
        "child-sleep",
        "identity-mismatch",
        "invalid-json",
        "large-output",
        "nonzero",
        "sleep",
        "spawn-child",
        "spawn-child-exit",
        "success",
    ];
    if (!File.Exists(path))
    {
        return null;
    }

    byte[] bytes = await File.ReadAllBytesAsync(path).ConfigureAwait(false);
    return modes.FirstOrDefault(candidate =>
        bytes.AsSpan().SequenceEqual(Encoding.UTF8.GetBytes(candidate + Environment.NewLine)));
}

static int WriteCapabilities()
{
    WriteUtf8Line(FixtureProtocol.CapabilitiesJson);
    return 0;
}

static int WriteInvalidJson()
{
    WriteUtf8Line("{\"schemaVersion\":");
    return 0;
}

static void WriteUtf8Line(string value)
{
    byte[] bytes = Encoding.UTF8.GetBytes(value + "\n");
    Console.OpenStandardOutput().Write(bytes);
}

static async Task<int> WriteLargeOutputAsync()
{
    Task stdout = WriteBytesAsync(Console.OpenStandardOutput(), (byte)'O');
    Task stderr = WriteBytesAsync(Console.OpenStandardError(), (byte)'E');
    await Task.WhenAll(stdout, stderr).ConfigureAwait(false);
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

static async Task<int> SleepAsync(string controlRoot)
{
    await File.WriteAllTextAsync(
        Path.Combine(controlRoot, "owned-root-ready.txt"),
        Environment.ProcessId.ToString(CultureInfo.InvariantCulture)).ConfigureAwait(false);
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

    string childDirectory = Path.Combine(controlRoot, "owned-child-" + Guid.NewGuid().ToString("N"));
    Directory.CreateDirectory(childDirectory);
    await File.WriteAllTextAsync(
        Path.Combine(childDirectory, "fake-mode.txt"),
        "child-sleep" + Environment.NewLine).ConfigureAwait(false);

    var startInfo = new ProcessStartInfo
    {
        FileName = processPath,
        WorkingDirectory = childDirectory,
        UseShellExecute = false,
        CreateNoWindow = true,
    };
    startInfo.ArgumentList.Add("capabilities");
    startInfo.ArgumentList.Add("--format");
    startInfo.ArgumentList.Add("json-v1");
    using var child = new Process { StartInfo = startInfo };
    if (!child.Start())
    {
        return 64;
    }

    await File.WriteAllTextAsync(
        Path.Combine(controlRoot, "spawn-child-ready.txt"),
        child.Id.ToString(CultureInfo.InvariantCulture)).ConfigureAwait(false);
    if (waitForCancellation)
    {
        await Task.Delay(TimeSpan.FromMinutes(5)).ConfigureAwait(false);
    }

    return WriteCapabilities();
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

internal static class FixtureProtocol
{
    internal const string IdentityJson = "{\"schemaVersion\":1,\"probeIdentity\":\"granite-edge-hardware-llamacpp-capabilities/1\",\"managedPackage\":\"LLamaSharp\",\"managedVersion\":\"0.27.0\",\"backendPackage\":\"LLamaSharp.Backend.Cpu\",\"backendVersion\":\"0.27.0\",\"llamaSharpCommit\":\"7cbbc45e421d55794d5050d126e0b96511007007\",\"mappedLlamaCppCommit\":\"3f7c29d318e317b63f54c558bc69803963d7d88c\",\"runtimeIdentifier\":\"win-x64\"}";
    internal const string IdentityMismatchJson = "{\"schemaVersion\":1,\"probeIdentity\":\"granite-edge-hardware-llamacpp-capabilities/1\",\"managedPackage\":\"LLamaSharp\",\"managedVersion\":\"9.9.9\",\"backendPackage\":\"LLamaSharp.Backend.Cpu\",\"backendVersion\":\"0.27.0\",\"llamaSharpCommit\":\"7cbbc45e421d55794d5050d126e0b96511007007\",\"mappedLlamaCppCommit\":\"3f7c29d318e317b63f54c558bc69803963d7d88c\",\"runtimeIdentifier\":\"win-x64\"}";
    internal const string CapabilitiesJson = "{\"schemaVersion\":1,\"probeIdentity\":\"granite-edge-hardware-llamacpp-capabilities/1\",\"backends\":[\"cpu\"],\"devices\":[{\"ordinal\":0,\"bufferType\":\"Fixture CPU Buffer\"}]}";
}
