using System.Buffers.Binary;

namespace HardwareInspection.LlmFitSpike.Candidate;

internal sealed record PeImageInspection(ushort Machine, string MachineName);

internal static class PeImageInspector
{
    private const int PeOffsetLocation = 0x3c;
    private const int PeSignatureLength = 4;
    private const int MachineLength = 2;

    public static PeImageInspection Inspect(string path)
    {
        using FileStream stream = File.OpenRead(path);
        return Inspect(stream);
    }

    public static PeImageInspection Inspect(Stream stream)
    {
        ArgumentNullException.ThrowIfNull(stream);
        if (!stream.CanRead || !stream.CanSeek)
        {
            throw new InvalidDataException("The PE image must be readable and seekable.");
        }

        EnsureAvailable(stream, 0, 2);
        stream.Position = 0;
        if (stream.ReadByte() != 'M' || stream.ReadByte() != 'Z')
        {
            throw new InvalidDataException("The image does not contain an MZ header.");
        }

        Span<byte> offsetBytes = stackalloc byte[sizeof(int)];
        ReadExactlyAt(stream, PeOffsetLocation, offsetBytes);
        int peOffset = BinaryPrimitives.ReadInt32LittleEndian(offsetBytes);
        if (peOffset <= 0)
        {
            throw new InvalidDataException("The PE header offset must be positive.");
        }

        EnsureAvailable(stream, peOffset, PeSignatureLength + MachineLength);
        Span<byte> signature = stackalloc byte[PeSignatureLength];
        ReadExactlyAt(stream, peOffset, signature);
        if (!signature.SequenceEqual("PE\0\0"u8))
        {
            throw new InvalidDataException("The PE signature is invalid.");
        }

        Span<byte> machineBytes = stackalloc byte[MachineLength];
        ReadExactlyAt(stream, peOffset + PeSignatureLength, machineBytes);
        ushort machine = BinaryPrimitives.ReadUInt16LittleEndian(machineBytes);
        return new PeImageInspection(machine, GetMachineName(machine));
    }

    private static string GetMachineName(ushort machine)
    {
        return machine switch
        {
            0x8664 => "AMD64",
            0x014c => "I386",
            _ => $"0x{machine:X4}",
        };
    }

    private static void ReadExactlyAt(Stream stream, long offset, Span<byte> buffer)
    {
        EnsureAvailable(stream, offset, buffer.Length);
        stream.Position = offset;
        stream.ReadExactly(buffer);
    }

    private static void EnsureAvailable(Stream stream, long offset, int count)
    {
        if (offset < 0 || count < 0 || offset > stream.Length || count > stream.Length - offset)
        {
            throw new InvalidDataException("The PE image is truncated.");
        }
    }
}
