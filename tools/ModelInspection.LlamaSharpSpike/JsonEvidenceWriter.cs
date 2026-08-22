using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace GraniteEdgeAI.Tools.ModelInspection.LlamaSharpSpike;

/// <summary>
/// Writes project-owned feasibility evidence as indented JSON through a
/// temporary file and atomic replacement.
/// </summary>
public sealed class JsonEvidenceWriter
{
    private static readonly JsonSerializerOptions SerializerOptions =
        CreateSerializerOptions();

    /// <summary>
    /// Writes one evidence object and returns the absolute output path.
    /// Existing evidence remains untouched when serialization or writing fails.
    /// </summary>
    public async Task<string> WriteAsync<T>(
        T result,
        string outputPath,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(result);
        ArgumentException.ThrowIfNullOrWhiteSpace(outputPath);
        cancellationToken.ThrowIfCancellationRequested();

        string fullOutputPath = Path.GetFullPath(outputPath);
        string? outputDirectory =
            Path.GetDirectoryName(fullOutputPath);

        if (string.IsNullOrWhiteSpace(outputDirectory))
        {
            throw new InvalidOperationException(
                "The evidence output directory could not be resolved.");
        }

        // Serialize before creating a temporary file so a serialization failure
        // cannot leave partial output or a stale temp artifact.
        string json = JsonSerializer.Serialize(
            result,
            SerializerOptions);

        Directory.CreateDirectory(outputDirectory);

        string temporaryPath =
            fullOutputPath + ".tmp-" + Guid.NewGuid().ToString("N");

        try
        {
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

    private static JsonSerializerOptions CreateSerializerOptions()
    {
        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = true
        };

        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }
}
