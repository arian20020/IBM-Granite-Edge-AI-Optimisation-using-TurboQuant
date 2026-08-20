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

            if (IsDriveRoot(value, index))
            {
                return true;
            }

            if (current == '\\' && StartsBackslashRoot(value, index))
            {
                return true;
            }

            if (current == '/' && StartsSlashRoot(value, index))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsDriveRoot(string value, int index)
    {
        return index < value.Length - 2 &&
               IsAsciiLetter(value[index]) &&
               value[index + 1] == ':' &&
               (value[index + 2] == '\\' || value[index + 2] == '/') &&
               (index == 0 || !IsAsciiLetter(value[index - 1]));
    }

    private static bool StartsBackslashRoot(string value, int index)
    {
        return StartsAfter(value, index, allowColon: true);
    }

    private static bool StartsSlashRoot(string value, int index)
    {
        // A colon is intentionally not a slash-root boundary so https:// stays safe.
        return StartsAfter(value, index, allowColon: false);
    }

    private static bool StartsAfter(string value, int index, bool allowColon)
    {
        if (index == 0)
        {
            return true;
        }

        char previous = value[index - 1];
        return char.IsWhiteSpace(previous) ||
               previous is '\'' or '"' or '(' or '[' or '{' or '<' or '=' ||
               (allowColon && previous == ':');
    }

    private static bool IsAsciiLetter(char value)
    {
        return value is >= 'A' and <= 'Z' or >= 'a' and <= 'z';
    }
}
