using System.Text;
using System.Text.Json;

namespace GraniteEdgeAI.Tools.ModelInspection.LlamaSharpSpike;

/// <summary>
/// Writes one native-backend smoke result as local JSON evidence.
/// </summary>
public sealed class SmokeEvidenceWriter
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    /// <summary>
    /// Writes evidence through a temporary file and atomically replaces any
    /// previous local result at the same path.
    /// </summary>
    /// <returns>The absolute path of the written JSON file.</returns>
    public async Task<string> WriteAsync(
        NativeBackendSmokeResult result,
        string outputPath,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(result);
        ArgumentException.ThrowIfNullOrWhiteSpace(outputPath);

        string fullOutputPath = Path.GetFullPath(outputPath);
        string? outputDirectory =
            Path.GetDirectoryName(fullOutputPath);

        if (string.IsNullOrWhiteSpace(outputDirectory))
        {
            throw new InvalidOperationException(
                "The smoke evidence output directory could not be resolved.");
        }

        Directory.CreateDirectory(outputDirectory);

        string temporaryPath =
            fullOutputPath + ".tmp-" + Guid.NewGuid().ToString("N");

        try
        {
            string json = JsonSerializer.Serialize(
                result,
                SerializerOptions);

            await File.WriteAllTextAsync(
                temporaryPath,
                json,
                new UTF8Encoding(
                    encoderShouldEmitUTF8Identifier: false),
                cancellationToken);

            File.Move(
                temporaryPath,
                fullOutputPath,
                overwrite: true);

            return fullOutputPath;
        }
        finally
        {
            if (File.Exists(temporaryPath))
            {
                File.Delete(temporaryPath);
            }
        }
    }
}
