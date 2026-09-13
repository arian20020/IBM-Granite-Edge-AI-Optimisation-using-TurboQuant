using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.VisualStudio.TestTools.UnitTesting.AppContainer;

namespace GraniteEdgeAI.UnitTests.Features.ModelInspection.Visual;

[TestClass]
[DoNotParallelize]
public sealed class ModelInspectionRenderHarnessTests
{
    public TestContext TestContext { get; set; } = null!;

    [UITestMethod]
    [TestCategory("WinUI")]
    public async Task RenderTargetBitmap_Visible1000By700ElementReturnsExpectedDimensions()
    {
        var root = new Grid
        {
            Background = new SolidColorBrush(Colors.White)
        };
        root.Children.Add(new Border
        {
            Width = 32,
            Height = 32,
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top,
            Background = new SolidColorBrush(Colors.Blue)
        });

        await using var host = await WinUiRenderHost.ShowAsync(
            root,
            width: 1000,
            height: 700);

        RenderedFrame frame = await host.CaptureAsync();
        double rasterizationScale = root.XamlRoot.RasterizationScale;
        string attachment = await frame.SavePngAsync(
            "model-inspection-render-harness.png");
        TestContext.AddResultFile(attachment);

        Assert.AreEqual(
            (int)Math.Round(1000d * rasterizationScale),
            frame.Width);
        Assert.AreEqual(
            (int)Math.Round(700d * rasterizationScale),
            frame.Height);
        Assert.AreEqual(1000d, root.ActualWidth, 1d);
        Assert.AreEqual(700d, root.ActualHeight, 1d);
        Assert.AreEqual(1000d, root.XamlRoot.Size.Width, 1d);
        Assert.AreEqual(700d, root.XamlRoot.Size.Height, 1d);
        Assert.IsTrue(File.Exists(attachment));
        Assert.IsGreaterThan(0L, new FileInfo(attachment).Length);
    }

    [TestMethod]
    public void PixelDiff_IdenticalClonePassesAndOnePixelGeometryMutationIsExact()
    {
        Assert.ThrowsExactly<ArgumentException>(() =>
            RenderedFrame.FromBgra8(1, 1, [0x00]));
        Assert.ThrowsExactly<OverflowException>(() =>
            RenderedFrame.FromBgra8(int.MaxValue, 1, []));
        RenderedFrame reference = RenderedFrame.FromBgra8(
            width: 3,
            height: 1,
            pixels:
            [
                0xff, 0x00, 0x00, 0xff,
                0xff, 0xff, 0xff, 0xff,
                0xff, 0xff, 0xff, 0xff
            ]);
        RenderedFrame identicalClone = RenderedFrame.FromBgra8(
            reference.Width,
            reference.Height,
            reference.Bgra8Pixels.Span);
        byte[] mutatedPixels = reference.Bgra8Pixels.ToArray();
        // Move the right edge of the opaque blue region by one pixel.
        reference.Bgra8Pixels.Span.Slice(0, 4).CopyTo(
            mutatedPixels.AsSpan(4, 4));
        RenderedFrame oneOpaquePixelMutation = RenderedFrame.FromBgra8(
            reference.Width,
            reference.Height,
            mutatedPixels);

        PixelDiff identical = PixelDiff.Compare(reference, identicalClone);
        PixelDiff mutated = PixelDiff.Compare(reference, oneOpaquePixelMutation);

        Assert.IsTrue(identical.IsMatch);
        Assert.AreEqual(12, reference.Stride);
        Assert.AreEqual(0, identical.DifferentPixelCount);
        Assert.IsNull(identical.DifferenceBounds);
        Assert.AreEqual(0, identical.MaxChannelDelta);

        Assert.IsFalse(mutated.IsMatch);
        Assert.AreEqual(1, mutated.DifferentPixelCount);
        Assert.AreEqual(
            new PixelDifferenceBounds(1, 0, 1, 1),
            mutated.DifferenceBounds);
        Assert.AreEqual(255, mutated.MaxChannelDelta);
        Assert.AreEqual(reference.Width, mutated.DifferenceFrame.Width);
        Assert.AreEqual(reference.Height, mutated.DifferenceFrame.Height);
        CollectionAssert.AreEqual(
            new byte[]
            {
                0x00, 0x00, 0x00, 0x00,
                0x00, 0x00, 0xff, 0xff,
                0x00, 0x00, 0x00, 0x00
            },
            mutated.DifferenceFrame.Bgra8Pixels.ToArray());
    }
}
