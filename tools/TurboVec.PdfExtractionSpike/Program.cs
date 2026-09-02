using System.Text.Json;

namespace TurboVec.PdfExtractionSpike;

public static class Program
{
    public static int Main(string[] args)
    {
        try
        {
            var values = Parse(args);
            var input = Path.GetFullPath(values["--input"]);
            if (!Path.IsPathFullyQualified(values["--input"])) throw new ArgumentException("input must be absolute");
            var attributes = File.GetAttributes(input);
            if ((attributes & FileAttributes.ReparsePoint) != 0) throw new ArgumentException("input links are not allowed");
            var request = new PdfExtractionRequest(
                input, values["--expected-sha256"], int.Parse(values["--max-pages"]),
                int.Parse(values["--max-scalars"]), 100L * 1024 * 1024);
            var result = new PdfExtractor().Extract(request, CancellationToken.None);
            Console.Out.WriteLine(JsonSerializer.Serialize(result, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower }));
            return result.Succeeded ? 0 : 3;
        }
        catch (Exception error)
        {
            var code = error is ArgumentException ? "invalid_arguments" : "worker_failure";
            Console.Out.WriteLine(JsonSerializer.Serialize(new { succeeded = false, failure_code = code }));
            Console.Error.WriteLine(error.GetType().Name[..Math.Min(64, error.GetType().Name.Length)]);
            return 2;
        }
    }

    private static Dictionary<string, string> Parse(string[] args)
    {
        var allowed = new HashSet<string>(StringComparer.Ordinal) { "--input", "--expected-sha256", "--max-pages", "--max-scalars" };
        if (args.Length != 8) throw new ArgumentException("four arguments are required");
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        for (var index = 0; index < args.Length; index += 2)
        {
            if (!allowed.Contains(args[index]) || !result.TryAdd(args[index], args[index + 1])) throw new ArgumentException("unknown or repeated argument");
        }
        return result;
    }
}
