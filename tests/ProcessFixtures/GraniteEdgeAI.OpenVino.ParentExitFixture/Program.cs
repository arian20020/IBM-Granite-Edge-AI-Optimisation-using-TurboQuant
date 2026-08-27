using System.Diagnostics;
using System.Text;
using GraniteEdgeAI.OpenVino.Contracts;

const string packageDigest =
    "b5316ac62e1e846b33ec92e5ad555859238af70ffd6b75aa50500995fbe15372";
const string modelDigest =
    "894dd0aac21e588d5cf78994d90aa0dcba8284626c976a4e0c89c0273b452c1c";

if (args.Length != 2)
{
    return 64;
}

string stage = Path.GetFullPath(args[0]);
string package = Path.GetFullPath(args[1]);
ProcessStartInfo start = new()
{
    FileName = Path.Combine(stage, "OpenVinoOfficial.Worker.exe"),
    WorkingDirectory = stage,
    UseShellExecute = false,
    CreateNoWindow = true,
    RedirectStandardInput = true,
    RedirectStandardOutput = true,
    RedirectStandardError = true,
};
start.ArgumentList.Add("--protocol");
start.ArgumentList.Add(OpenVinoProtocol.OfficialProtocolId);
Process child = Process.Start(start) ?? throw new InvalidOperationException();

string hello = await child.StandardOutput.ReadLineAsync().WaitAsync(TimeSpan.FromSeconds(5))
    ?? throw new InvalidOperationException("The worker closed stdout before hello.");
_ = OpenVinoProtocolJson.DeserializeEvent(Encoding.UTF8.GetBytes(hello));
Guid sessionId = Guid.NewGuid();
await child.StandardInput.WriteLineAsync(Encoding.UTF8.GetString(OpenVinoProtocolJson.Serialize(
    new StartSessionCommand(
        sessionId,
        Guid.NewGuid(),
        package,
        packageDigest,
        modelDigest,
        88,
        new OpenVinoDeviceRequest("CPU"),
        new OpenVinoGenerationLimits(64, 2)))));
await child.StandardInput.FlushAsync();
string started = await child.StandardOutput.ReadLineAsync().WaitAsync(TimeSpan.FromSeconds(5))
    ?? throw new InvalidOperationException("The worker closed stdout before sessionStarted.");
_ = OpenVinoProtocolJson.DeserializeEvent(Encoding.UTF8.GetBytes(started));
await child.StandardInput.WriteLineAsync(Encoding.UTF8.GetString(OpenVinoProtocolJson.Serialize(
    new PromptCommand(sessionId, Guid.NewGuid(), "hello", 2))));
await child.StandardInput.FlushAsync();
string generation = await child.StandardOutput.ReadLineAsync().WaitAsync(TimeSpan.FromSeconds(5))
    ?? throw new InvalidOperationException("The worker closed stdout before generationStarted.");
_ = OpenVinoProtocolJson.DeserializeEvent(Encoding.UTF8.GetBytes(generation));
Console.WriteLine(child.Id);
return 0;
