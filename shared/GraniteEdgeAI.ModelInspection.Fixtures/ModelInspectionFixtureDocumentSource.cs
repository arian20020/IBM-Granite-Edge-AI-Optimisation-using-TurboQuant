using System.Collections.Immutable;

namespace GraniteEdgeAI.ModelInspection.Fixtures;

public sealed record ModelInspectionFixtureDocumentSource(
    string FileName,
    ReadOnlyMemory<byte> Utf8Json);

public sealed class ModelInspectionFixtureValidationException : Exception
{
    internal ModelInspectionFixtureValidationException(
        string fileName,
        string jsonPath,
        string ruleCode)
        : base($"{fileName}|{jsonPath}|{ruleCode}")
    {
        FileName = fileName;
        JsonPath = jsonPath;
        RuleCode = ruleCode;
    }

    public string FileName { get; }

    public string JsonPath { get; }

    public string RuleCode { get; }
}

public sealed class VerifiedModelInspectionFixtureSchema
{
    internal VerifiedModelInspectionFixtureSchema(
        string fileName,
        int schemaVersion,
        ImmutableArray<byte> rawUtf8,
        string sha256)
    {
        FileName = fileName;
        SchemaVersion = schemaVersion;
        RawUtf8 = rawUtf8;
        Sha256 = sha256;
    }

    public string FileName { get; }

    public int SchemaVersion { get; }

    public ImmutableArray<byte> RawUtf8 { get; }

    public string Sha256 { get; }
}
