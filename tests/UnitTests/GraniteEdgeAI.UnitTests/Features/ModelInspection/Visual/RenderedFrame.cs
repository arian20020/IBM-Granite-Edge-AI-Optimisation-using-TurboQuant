using Windows.Graphics.Imaging;
using Windows.Storage;

namespace GraniteEdgeAI.UnitTests.Features.ModelInspection.Visual;

public sealed class RenderedFrame
{
    private readonly byte[] _pixels;

    private RenderedFrame(int width, int height, int stride, byte[] pixels)
    {
        Width = width;
        Height = height;
        Stride = stride;
        _pixels = pixels;
    }

    public int Width { get; }

    public int Height { get; }

    public int Stride { get; }

    public ReadOnlyMemory<byte> Bgra8Pixels => _pixels;

    public static RenderedFrame FromBgra8(
        int width,
        int height,
        ReadOnlySpan<byte> pixels)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(width);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(height);
        int stride = checked(width * 4);
        int expectedLength = checked(stride * height);
        if (pixels.Length != expectedLength)
        {
            throw new ArgumentException(
                $"Expected {expectedLength} BGRA bytes for {width} x {height}, " +
                $"but received {pixels.Length}.",
                nameof(pixels));
        }

        return new RenderedFrame(width, height, stride, pixels.ToArray());
    }

    public async Task<string> SavePngAsync(string fileName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);
        if (!string.Equals(
                fileName,
                Path.GetFileName(fileName),
                StringComparison.Ordinal) ||
            !string.Equals(
                Path.GetExtension(fileName),
                ".png",
                StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException(
                "The PNG attachment must use a leaf .png file name.",
                nameof(fileName));
        }

        StorageFile file = await ApplicationData.Current.TemporaryFolder
            .CreateFileAsync(fileName, CreationCollisionOption.ReplaceExisting);
        using Windows.Storage.Streams.IRandomAccessStream stream =
            await file.OpenAsync(FileAccessMode.ReadWrite);
        BitmapEncoder encoder = await BitmapEncoder.CreateAsync(
            BitmapEncoder.PngEncoderId,
            stream);
        encoder.SetPixelData(
            BitmapPixelFormat.Bgra8,
            BitmapAlphaMode.Premultiplied,
            (uint)Width,
            (uint)Height,
            96,
            96,
            _pixels);
        await encoder.FlushAsync();

        return file.Path;
    }
}
