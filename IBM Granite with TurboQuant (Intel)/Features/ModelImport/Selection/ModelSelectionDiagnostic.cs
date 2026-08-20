namespace GraniteEdgeAI.Features.ModelImport.Selection;

internal sealed class ModelSelectionDiagnostic
{
    internal ModelSelectionDiagnostic(string code, string message)
    {
        Code = code;
        Message = message;
    }

    internal string Code { get; }

    internal string Message { get; }
}
