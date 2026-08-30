using System.Text.Json;
using GraniteEdgeAI.R4Handoff.Validation;
using Json.Schema;

namespace GraniteEdgeAI.R4Handoff.Validator;

internal static class Program
{
    private const int MaximumDocumentBytes = 1024 * 1024;

    public static int Main(string[] args)
    {
        if (args.Length != 3
            || (args[0] != "receipt" && args[0] != "evidence"))
        {
            Console.Error.WriteLine(
                "Usage: GraniteEdgeAI.R4Handoff.Validator <receipt|evidence> <schema> <document>");
            return 2;
        }

        try
        {
            byte[] schemaBytes = ReadBounded(args[1]);
            byte[] documentBytes = ReadBounded(args[2]);
            JsonSchema schema = JsonSchema.FromText(
                System.Text.Encoding.UTF8.GetString(schemaBytes));
            using JsonDocument document = JsonDocument.Parse(documentBytes);
            bool structureIsValid = schema.Evaluate(document.RootElement).IsValid;
            bool arithmeticIsValid = args[0] == "receipt"
                ? R4HandoffSemanticValidator.HasValidReceiptArithmetic(document.RootElement)
                : R4HandoffSemanticValidator.HasValidEvidenceArithmetic(document.RootElement);
            if (!structureIsValid || !arithmeticIsValid)
            {
                Console.Error.WriteLine("R4 handoff validation failed.");
                return 1;
            }

            Console.WriteLine("R4 handoff validation passed.");
            return 0;
        }
        catch (Exception error) when (
            error is IOException
            or UnauthorizedAccessException
            or JsonException
            or InvalidOperationException
            or ArgumentException)
        {
            Console.Error.WriteLine("R4 handoff validation failed.");
            return 1;
        }
    }

    private static byte[] ReadBounded(string path)
    {
        using FileStream stream = new(
            path,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            bufferSize: 4096,
            FileOptions.SequentialScan);
        if (stream.Length is <= 0 or > MaximumDocumentBytes)
        {
            throw new InvalidDataException("The handoff input is outside its size policy.");
        }

        byte[] content = new byte[checked((int)stream.Length)];
        stream.ReadExactly(content);
        return content;
    }
}
