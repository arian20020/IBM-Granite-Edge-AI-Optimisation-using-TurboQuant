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
        if (index > 0 && value[index - 1] == ':')
        {
            // Only web URL schemes are allowed at ://; labelled path tokens stay private.
            return index == value.Length - 1 ||
                   value[index + 1] != '/' ||
                   !HasWebUrlScheme(value, index - 1);
        }

        return StartsAfter(value, index, allowColon: false);
    }

    private static bool HasWebUrlScheme(string value, int colonIndex)
    {
        int schemeStart = colonIndex - 1;
        while (schemeStart >= 0 && IsAsciiLetter(value[schemeStart]))
        {
            schemeStart--;
        }

        string scheme = value[(schemeStart + 1)..colonIndex];
        return string.Equals(scheme, "http", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(scheme, "https", StringComparison.OrdinalIgnoreCase);
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
