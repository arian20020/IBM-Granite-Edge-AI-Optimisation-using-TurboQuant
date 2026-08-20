using System;

namespace GraniteEdgeAI.Features.ModelImport.Selection;

internal sealed class ModelSelectionDiagnostic
{
    internal ModelSelectionDiagnostic(string code, string message)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        ArgumentException.ThrowIfNullOrWhiteSpace(message);

        if (ContainsRootedPath(message))
        {
            throw new ArgumentException(
                "Diagnostic messages must not contain local paths.",
                nameof(message));
        }

        Code = code;
        Message = message;
    }

    internal string Code { get; }

    internal string Message { get; }

    private static bool ContainsRootedPath(string value)
    {
        if (value.Contains(@"\\", System.StringComparison.Ordinal))
        {
            return true;
        }

        for (int index = 0; index < value.Length - 2; index++)
        {
            if (char.IsLetter(value[index]) &&
                value[index + 1] == ':' &&
                (value[index + 2] == '\\' || value[index + 2] == '/'))
            {
                return true;
            }
        }

        return false;
    }
}
