using System.Buffers.Binary;

namespace GraniteEdgeAI.HardwareInspection.Foundation.TrustedTools;

internal static class PeImageInspector
{
    private const ushort Amd64Machine = 0x8664;
    private const int DosHeaderPointerOffset = 0x3c;

    internal static TrustedToolVerificationFailure? Inspect(Stream stream, PeMachine requiredMachine)
    {
        ArgumentNullException.ThrowIfNull(stream);
        if (!stream.CanRead || !stream.CanSeek || stream.Length < DosHeaderPointerOffset + 4)
        {
            return TrustedToolVerificationFailure.PeInvalid;
        }

        Span<byte> dosHeader = stackalloc byte[DosHeaderPointerOffset + 4];
        stream.Position = 0;
        if (!ReadExactly(stream, dosHeader) || dosHeader[0] != (byte)'M' || dosHeader[1] != (byte)'Z')
        {
            return TrustedToolVerificationFailure.PeInvalid;
        }

        int peOffset = BinaryPrimitives.ReadInt32LittleEndian(dosHeader[DosHeaderPointerOffset..]);
        if (peOffset < DosHeaderPointerOffset + 4 || peOffset > stream.Length - 6)
        {
            return TrustedToolVerificationFailure.PeInvalid;
        }

        Span<byte> peHeader = stackalloc byte[6];
        stream.Position = peOffset;
        if (!ReadExactly(stream, peHeader) ||
            peHeader[0] != (byte)'P' ||
            peHeader[1] != (byte)'E' ||
            peHeader[2] != 0 ||
            peHeader[3] != 0)
        {
            return TrustedToolVerificationFailure.PeInvalid;
        }

        ushort machine = BinaryPrimitives.ReadUInt16LittleEndian(peHeader[4..]);
        return requiredMachine switch
        {
            PeMachine.Amd64 when machine == Amd64Machine => null,
            PeMachine.Amd64 => TrustedToolVerificationFailure.PeArchitectureMismatch,
            _ => TrustedToolVerificationFailure.PeArchitectureMismatch,
        };
    }

    private static bool ReadExactly(Stream stream, Span<byte> buffer)
    {
        int totalRead = 0;
        while (totalRead < buffer.Length)
        {
            int read = stream.Read(buffer[totalRead..]);
            if (read == 0)
            {
                return false;
            }

            totalRead += read;
        }

        return true;
    }
}
