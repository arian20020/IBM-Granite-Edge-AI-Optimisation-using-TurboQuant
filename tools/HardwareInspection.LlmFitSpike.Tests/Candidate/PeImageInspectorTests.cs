using HardwareInspection.LlmFitSpike.Candidate;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace HardwareInspection.LlmFitSpike.Tests.Candidate;

[TestClass]
[TestCategory("Deterministic")]
#pragma warning disable CA1707 // Test names intentionally encode the required behavior.
public sealed class PeImageInspectorTests
{
    [TestMethod]
    public void Inspect_ValidAmd64Image_ExtractsMachine()
    {
        using var stream = new MemoryStream(CreatePeImage(0x8664));

        PeImageInspection inspection = PeImageInspector.Inspect(stream);

        Assert.AreEqual((ushort)0x8664, inspection.Machine);
        Assert.AreEqual("AMD64", inspection.MachineName);
    }

    [TestMethod]
    public void Inspect_MissingMzHeader_ThrowsInvalidData()
    {
        byte[] image = CreatePeImage(0x8664);
        image[0] = 0;

        Assert.ThrowsExactly<InvalidDataException>(() => PeImageInspector.Inspect(new MemoryStream(image)));
    }

    [TestMethod]
    [DataRow(0)]
    [DataRow(-1)]
    public void Inspect_NonPositivePeOffset_ThrowsInvalidData(int peOffset)
    {
        byte[] image = CreatePeImage(0x8664);
        BitConverter.GetBytes(peOffset).CopyTo(image, 0x3c);

        Assert.ThrowsExactly<InvalidDataException>(() => PeImageInspector.Inspect(new MemoryStream(image)));
    }

    [TestMethod]
    public void Inspect_PeOffsetOutsideImage_ThrowsInvalidData()
    {
        byte[] image = CreatePeImage(0x8664);
        BitConverter.GetBytes(image.Length).CopyTo(image, 0x3c);

        Assert.ThrowsExactly<InvalidDataException>(() => PeImageInspector.Inspect(new MemoryStream(image)));
    }

    [TestMethod]
    public void Inspect_InvalidPeSignature_ThrowsInvalidData()
    {
        byte[] image = CreatePeImage(0x8664);
        image[0x80] = (byte)'X';

        Assert.ThrowsExactly<InvalidDataException>(() => PeImageInspector.Inspect(new MemoryStream(image)));
    }

    [TestMethod]
    public void Inspect_TruncatedMachineField_DoesNotReadPastLength()
    {
        byte[] image = CreatePeImage(0x8664)[..0x84];
        using var stream = new BoundaryGuardStream(image);

        Assert.ThrowsExactly<InvalidDataException>(() => PeImageInspector.Inspect(stream));
    }

    internal static byte[] CreatePeImage(ushort machine)
    {
        var image = new byte[512];
        image[0] = (byte)'M';
        image[1] = (byte)'Z';
        BitConverter.GetBytes(0x80).CopyTo(image, 0x3c);
        image[0x80] = (byte)'P';
        image[0x81] = (byte)'E';
        image[0x82] = 0;
        image[0x83] = 0;
        BitConverter.GetBytes(machine).CopyTo(image, 0x84);
        return image;
    }

    private sealed class BoundaryGuardStream(byte[] buffer) : MemoryStream(buffer, writable: false)
    {
        public override int Read(byte[] buffer, int offset, int count)
        {
            RejectReadPastEnd(count);
            return base.Read(buffer, offset, count);
        }

        public override int Read(Span<byte> buffer)
        {
            RejectReadPastEnd(buffer.Length);
            return base.Read(buffer);
        }

        public override int ReadByte()
        {
            RejectReadPastEnd(1);
            return base.ReadByte();
        }

        private void RejectReadPastEnd(int count)
        {
            if (Position + count > Length)
            {
                throw new InvalidOperationException("The inspector attempted to read beyond the stream length.");
            }
        }
    }
}
#pragma warning restore CA1707
