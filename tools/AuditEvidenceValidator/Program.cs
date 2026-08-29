using System.Text.Json.Nodes;
using Json.Schema;

if (args.Length != 2)
{
    Console.Error.WriteLine("Usage: AuditEvidenceValidator <schema> <instance>");
    return 2;
}

JsonSchema schema = JsonSchema.FromText(await File.ReadAllTextAsync(args[0]));
JsonNode instance = JsonNode.Parse(await File.ReadAllTextAsync(args[1]))
    ?? throw new InvalidDataException("The JSON instance is empty.");
EvaluationResults results = schema.Evaluate(
    instance,
    new EvaluationOptions
    {
        OutputFormat = OutputFormat.List,
        ValidateAgainstMetaSchema = true
    });
if (!results.IsValid)
{
    Console.Error.WriteLine("JSON_SCHEMA_INVALID");
    return 1;
}

Console.WriteLine("JSON_SCHEMA_VALID");
return 0;
