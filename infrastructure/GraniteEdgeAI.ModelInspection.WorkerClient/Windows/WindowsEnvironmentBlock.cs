using System.Runtime.InteropServices;

namespace GraniteEdgeAI.ModelInspection.WorkerClient;

/// <summary>
/// Owns one sorted UTF-16 Windows environment block terminated by two NUL
/// characters. The unmanaged memory is released exactly once.
/// </summary>
internal sealed class WindowsEnvironmentBlock : IDisposable
{
    private const string FailureMessage =
        "The Model Inspection worker environment block could not be created.";

    private int _characterCount;
    private bool _disposed;

    private WindowsEnvironmentBlock(IntPtr pointer, int characterCount)
    {
        Pointer = pointer;
        _characterCount = characterCount;
    }

    public IntPtr Pointer { get; private set; }

    public static WindowsEnvironmentBlock Create(
        IReadOnlyDictionary<string, string> environment)
    {
        ArgumentNullException.ThrowIfNull(environment);

        try
        {
            List<KeyValuePair<string, string>> entries = [.. environment];
            entries.Sort(static (left, right) =>
                StringComparer.OrdinalIgnoreCase.Compare(left.Key, right.Key));

            HashSet<string> names = new(StringComparer.OrdinalIgnoreCase);
            int characterCount = 1;
            foreach ((string key, string value) in entries)
            {
                ValidateEntry(key, value);
                if (!names.Add(key))
                {
                    throw Failure();
                }

                characterCount = checked(
                    characterCount + key.Length + 1 + value.Length + 1);
            }

            if (entries.Count == 0)
            {
                characterCount = 2;
            }

            char[] characters = new char[characterCount];
            int offset = 0;
            foreach ((string key, string value) in entries)
            {
                string entry = string.Concat(key, "=", value);
                entry.CopyTo(0, characters, offset, entry.Length);
                offset += entry.Length;
                characters[offset++] = '\0';
            }

            characters[offset] = '\0';
            if (entries.Count == 0)
            {
                characters[1] = '\0';
            }

            IntPtr pointer = Marshal.AllocHGlobal(
                checked(characterCount * sizeof(char)));
            try
            {
                Marshal.Copy(characters, 0, pointer, characterCount);
                return new WindowsEnvironmentBlock(pointer, characterCount);
            }
            catch
            {
                Marshal.FreeHGlobal(pointer);
                throw;
            }
        }
        catch (WorkerClientPolicyException)
        {
            throw;
        }
        catch (Exception error) when (
            error is ArgumentException or
            OverflowException or
            OutOfMemoryException)
        {
            throw Failure(error);
        }
    }

    internal char[] CopyCharactersForTests()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        char[] characters = new char[_characterCount];
        Marshal.Copy(Pointer, characters, 0, _characterCount);
        return characters;
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        if (Pointer != IntPtr.Zero)
        {
            char[] zeros = new char[_characterCount];
            Marshal.Copy(zeros, 0, Pointer, _characterCount);
            Marshal.FreeHGlobal(Pointer);
            Pointer = IntPtr.Zero;
        }

        _characterCount = 0;
        _disposed = true;
        GC.SuppressFinalize(this);
    }

    private static void ValidateEntry(string key, string value)
    {
        if (string.IsNullOrWhiteSpace(key) ||
            key.Contains('=') ||
            key.Contains('\0') ||
            value.Contains('\0'))
        {
            throw Failure();
        }
    }

    private static WorkerClientPolicyException Failure(
        Exception? innerException = null)
    {
        WorkerClientFailure failure = new(
            WorkerClientFailureCodes.WorkerEnvironmentPolicyFailed,
            FailureMessage);
        return innerException is null
            ? new WorkerClientPolicyException(failure)
            : new WorkerClientPolicyException(failure, innerException);
    }
}
