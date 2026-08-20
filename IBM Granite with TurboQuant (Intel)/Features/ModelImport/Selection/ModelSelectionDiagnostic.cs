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
        for (int index = 0; index < value.Length; index++)
        {
            char current = value[index];

            if (index < value.Length - 2 &&
                char.IsLetter(current) &&
                value[index + 1] == ':' &&
                (value[index + 2] == '\\' || value[index + 2] == '/'))
            {
                return true;
            }

            if (current is '\\' or '/' &&
                (index == 0 ||
                 (index < value.Length - 1 && current == value[index + 1]) ||
                 !IsPathSegmentCharacter(value[index - 1])))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsPathSegmentCharacter(char value)
    {
        return char.IsLetterOrDigit(value) || value is '_' or '.';
    }
}
