using GraniteEdgeAI.GgufQuantization.FakeQuantizer;

_ = typeof(FakeQuantizerMarker);
string[] accepted = ["Q2_K", "Q3_K_M", "Q4_K_M", "Q5_K_M", "Q6_K", "Q8_0"];
bool allowRequantize = args.Length == 4 && args[0] == "--allow-requantize";
int offset = allowRequantize ? 1 : 0;
if (args.Length - offset != 3 || !accepted.Contains(args[offset + 2], StringComparer.Ordinal))
{
    return 2;
}

string source = args[offset];
string output = args[offset + 1];
if (!File.Exists(source) || File.Exists(output))
{
    return 3;
}
if (File.Exists(source + ".delay"))
{
    await Task.Delay(TimeSpan.FromSeconds(5));
}

await using FileStream input = new(source, FileMode.Open, FileAccess.Read, FileShare.Read);
await using FileStream result = new(output, FileMode.CreateNew, FileAccess.Write, FileShare.None);
await input.CopyToAsync(result);
await result.WriteAsync(new byte[] { (byte)'Q' });
await result.WriteAsync(new byte[]
{
    Environment.GetEnvironmentVariable("GRANITE_SECURITY_AUDIT_SENTINEL") is null
        ? (byte)'A'
        : (byte)'P',
});
await result.FlushAsync();
Console.WriteLine("quantize: 100%");
return 0;
