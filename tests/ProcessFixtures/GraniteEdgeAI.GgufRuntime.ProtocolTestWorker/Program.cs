using System.Diagnostics;
using System.Text;

await using Stream standardInput = Console.OpenStandardInput();
await using Stream standardOutput = Console.OpenStandardOutput();
await using Stream standardError = Console.OpenStandardError();
using StreamReader input = new(
    standardInput,
    new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
    detectEncodingFromByteOrderMarks: false,
    leaveOpen: true);
await using StreamWriter output = new(
    standardOutput,
    new UTF8Encoding(encoderShouldEmitUTF8Identifier: false),
    leaveOpen: true)
{
    AutoFlush = true,
};

if (args is ["--child"])
{
    await Task.Delay(Timeout.InfiniteTimeSpan);
    return 0;
}

await output.WriteLineAsync(Environment.GetEnvironmentVariable("G1_TEST_SECRET") is null
    ? "secret=absent"
    : "secret=present");

if (args is ["--spawn-child"])
{
    string executable = Environment.ProcessPath
        ?? throw new InvalidOperationException("The fixture executable is unavailable.");
    using Process child = Process.Start(new ProcessStartInfo
    {
        FileName = executable,
        UseShellExecute = false,
        ArgumentList = { "--child" },
    }) ?? throw new InvalidOperationException("The child fixture did not start.");
    await output.WriteLineAsync($"child={child.Id}");
}

while (await input.ReadLineAsync() is { } line)
{
    await output.WriteLineAsync($"echo={line}");
}

return 0;
