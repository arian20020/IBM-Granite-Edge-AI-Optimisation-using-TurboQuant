using System.Text;

namespace GraniteEdgeAI.GgufRuntime.WorkerClient.Windows;

internal static class GgufWindowsCommandLine
{
    internal static char[] Build(string executablePath, IReadOnlyList<string> arguments)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(executablePath);
        ArgumentNullException.ThrowIfNull(arguments);
        if (executablePath.Contains('\0'))
        {
            throw new ArgumentException("The executable path contains a null character.", nameof(executablePath));
        }

        var command = new StringBuilder();
        AppendQuotedArgument(command, executablePath);
        foreach (string argument in arguments)
        {
            if (argument is null || argument.Contains('\0'))
            {
                throw new ArgumentException("A process argument is invalid.", nameof(arguments));
            }

            command.Append(' ');
            AppendQuotedArgument(command, argument);
        }

        command.Append('\0');
        return command.ToString().ToCharArray();
    }

    private static void AppendQuotedArgument(StringBuilder command, string argument)
    {
        command.Append('"');
        int pendingBackslashes = 0;
        foreach (char character in argument)
        {
            if (character == '\\')
            {
                pendingBackslashes++;
                continue;
            }

            if (character == '"')
            {
                command.Append('\\', checked((pendingBackslashes * 2) + 1));
                command.Append('"');
                pendingBackslashes = 0;
                continue;
            }

            command.Append('\\', pendingBackslashes);
            pendingBackslashes = 0;
            command.Append(character);
        }

        command.Append('\\', checked(pendingBackslashes * 2));
        command.Append('"');
    }
}
