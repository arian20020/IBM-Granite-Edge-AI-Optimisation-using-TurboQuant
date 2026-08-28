using System.Runtime.InteropServices;

namespace GraniteEdgeAI.HardwareInspection.Foundation.Processes;

internal sealed class TrustedToolEnvironmentBlock : IDisposable
{
    private const int MaximumCharacters = 32_767;
    private int _characterCount;

    private TrustedToolEnvironmentBlock(IntPtr pointer, int characterCount)
    {
        Pointer = pointer;
        _characterCount = characterCount;
    }

    internal IntPtr Pointer { get; private set; }

    internal static TrustedToolEnvironmentBlock Create(
        IReadOnlyDictionary<string, string> environment)
    {
        ArgumentNullException.ThrowIfNull(environment);
        try
        {
            KeyValuePair<string, string>[] entries = environment
                .OrderBy(static pair => pair.Key, StringComparer.OrdinalIgnoreCase)
                .ToArray();
            int characterCount = 1;
            var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach ((string key, string value) in entries)
            {
                if (string.IsNullOrWhiteSpace(key)
                    || key.Contains('=')
                    || key.Contains('\0')
                    || value.Contains('\0')
                    || !names.Add(key))
                {
                    throw new InvalidOperationException(
                        "The trusted hardware tool environment could not be secured.");
                }

                characterCount = checked(characterCount + key.Length + value.Length + 2);
            }

            if (characterCount > MaximumCharacters)
            {
                throw new InvalidOperationException(
                    "The trusted hardware tool environment could not be secured.");
            }

            char[] characters = new char[characterCount];
            int offset = 0;
            foreach ((string key, string value) in entries)
            {
                string item = string.Concat(key, "=", value);
                item.CopyTo(0, characters, offset, item.Length);
                offset += item.Length;
                characters[offset++] = '\0';
            }

            characters[offset] = '\0';
            IntPtr pointer = Marshal.AllocHGlobal(checked(characterCount * sizeof(char)));
            try
            {
                Marshal.Copy(characters, 0, pointer, characterCount);
                return new TrustedToolEnvironmentBlock(pointer, characterCount);
            }
            catch
            {
                Marshal.FreeHGlobal(pointer);
                throw;
            }
        }
        catch (InvalidOperationException)
        {
            throw;
        }
        catch (Exception exception) when (
            exception is ArgumentException or OverflowException or OutOfMemoryException)
        {
            throw new InvalidOperationException(
                "The trusted hardware tool environment could not be secured.");
        }
    }

    public void Dispose()
    {
        if (Pointer == IntPtr.Zero)
        {
            return;
        }

        Marshal.Copy(new char[_characterCount], 0, Pointer, _characterCount);
        Marshal.FreeHGlobal(Pointer);
        Pointer = IntPtr.Zero;
        _characterCount = 0;
    }
}
