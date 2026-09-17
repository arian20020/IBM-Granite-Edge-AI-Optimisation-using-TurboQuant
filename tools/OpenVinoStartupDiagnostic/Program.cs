using System.ComponentModel;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.ExceptionServices;
using GraniteEdgeAI.HardwareInspection.Foundation.Processes;
using GraniteEdgeAI.ModelInspection.WorkerClient.ProtectedWorker;
using GraniteEdgeAI.OpenVino.Contracts;
using GraniteEdgeAI.OpenVino.WorkerClient;

// Diagnostic host only: calls the unchanged client's protected startup path.
// No model, prompt, session command, installation or trust changes are made.
if (args.Length != 1)
{
    Console.WriteLine("Usage: OpenVinoStartupDiagnostic.exe <absolute OVRuntime folder>");
    return 2;
}
const string digest = "f0089dae967a0b4249238f9bf49db02ba44e111c78b83ebff36778f6d0ddcbe2";
var captured = new List<string>();
var sync = new object();
void Capture(object? sender, FirstChanceExceptionEventArgs e)
{
    lock (sync)
    {
        if (captured.Count >= 40) return;
        string trace = e.Exception.StackTrace ?? "";
        if (!trace.Contains("GraniteEdgeAI", StringComparison.Ordinal)) return;
        string detail = $"{e.Exception.GetType().Name} HRESULT=0x{e.Exception.HResult:X8}";
        if (e.Exception is Win32Exception native) detail += $" WIN32={native.NativeErrorCode}";
        string message = e.Exception.Message;
        captured.Add(detail + " " + message[..Math.Min(message.Length, 512)] + "\n" +
            string.Join("\n", trace.Split('\n').Take(8)));
    }
}
ProtectedWorkerSession? session = null;
IDisposable? closure = null;
TrustedToolOperationEnvironment? environment = null;
int exit = 0;
AppDomain.CurrentDomain.FirstChanceException += Capture;
try
{
    Console.WriteLine("Standalone diagnostic; NOT the installed app's package identity.");
    Console.WriteLine($"OS={Environment.OSVersion}; host={System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture}");
    if (!Path.IsPathFullyQualified(args[0]) || !Directory.Exists(args[0]))
        throw new ArgumentException("An existing absolute OVRuntime folder is required.");
    var installation = OpenVinoOfficialWorkerAuthority.CreateInstallation(args[0], digest);
    var client = new OpenVinoWorkerClient(OpenVinoWorkerClientOptions.CreateDefault(installation));
    var method = typeof(OpenVinoWorkerClient).GetMethod("StartProtectedAsync", BindingFlags.Instance | BindingFlags.NonPublic)
        ?? throw new MissingMethodException("Protected startup entry point unavailable.");
    Console.WriteLine("START: closure verification, private temporary environment, protected process launch");
    var task = (Task)method.Invoke(client, new object[] { CancellationToken.None })!;
    await task;
    var tuple = (ITuple)task.GetType().GetProperty("Result")!.GetValue(task)!;
    session = (ProtectedWorkerSession)tuple[0]!;
    closure = (IDisposable)tuple[2]!;
    environment = (TrustedToolOperationEnvironment)tuple[3]!;
    Console.WriteLine("PROTECTED LAUNCH PASSED");
    using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(5));
    var line = await session.StandardOutput.ReadLineAsync(deadline.Token);
    if (line is null) throw new InvalidOperationException("Worker exited without hello.");
    if (OpenVinoProtocolJson.DeserializeEvent(line) is not HelloEvent hello ||
        hello.ProtocolId != installation.ExpectedProtocolId || hello.BuildEvidence != installation.ExpectedBuildEvidence)
        throw new InvalidOperationException("Worker hello did not match pinned runtime evidence.");
    Console.WriteLine("HELLO VERIFIED (no model was loaded)");
}
catch (Exception error)
{
    exit = 1;
    Console.WriteLine("FAILED: " + error.GetType().Name + ": " + error.Message);
}
finally
{
    // Keep the same ownership order as the app: process, closure, temp directory.
    try
    {
        if (session is not null) await session.DisposeAsync();
    }
    catch (Exception error) { exit = 1; Console.WriteLine("PROCESS CLEANUP FAILED: " + error.Message); }
    try { closure?.Dispose(); }
    catch (Exception error) { exit = 1; Console.WriteLine("CLOSURE CLEANUP FAILED: " + error.Message); }
    try
    {
        environment?.Dispose();
        if (environment is not null && !environment.CleanupSucceeded)
            throw new InvalidOperationException("Temporary directory cleanup could not be verified.");
    }
    catch (Exception error) { exit = 1; Console.WriteLine("TEMP CLEANUP FAILED: " + error.Message); }
    AppDomain.CurrentDomain.FirstChanceException -= Capture;
}
if (exit == 0) Console.WriteLine("CLEANUP VERIFIED");
lock (sync)
{
    Console.WriteLine("First-chance evidence (some exceptions may be handled normally):");
    foreach (string entry in captured) Console.WriteLine(entry);
}
return exit;
