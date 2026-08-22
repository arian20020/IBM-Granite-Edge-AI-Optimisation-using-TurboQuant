using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Windows.Foundation;
using Windows.Graphics;
using Windows.Storage.Streams;

namespace GraniteEdgeAI.UnitTests.Features.ModelInspection.Visual;

public sealed class WinUiRenderHost : IAsyncDisposable
{
    private static readonly TimeSpan UiTimeout = TimeSpan.FromSeconds(10);

    private readonly Window _window;
    private bool _disposed;

    private WinUiRenderHost(Window window, FrameworkElement root)
    {
        _window = window;
        Root = root;
    }

    public FrameworkElement Root { get; }

    public static async Task<WinUiRenderHost> ShowAsync(
        FrameworkElement root,
        int width,
        int height)
    {
        ArgumentNullException.ThrowIfNull(root);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(width);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(height);
        if (root.Parent is not null)
        {
            throw new ArgumentException(
                "The render root must not already have a visual parent.",
                nameof(root));
        }

        var loaded = new TaskCompletionSource<bool>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        void OnLoaded(object sender, RoutedEventArgs args) =>
            loaded.TrySetResult(true);
        var window = new Window();

        try
        {
            root.Loaded += OnLoaded;
            window.Content = root;
            window.Activate();
            await loaded.Task.WaitAsync(UiTimeout);
            root.Loaded -= OnLoaded;

            await ResizeClientAsync(window, root, width, height);

            return new WinUiRenderHost(window, root);
        }
        catch
        {
            root.Loaded -= OnLoaded;
            window.Content = null;
            window.Close();
            throw;
        }
    }

    public async Task<RenderedFrame> CaptureAsync()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        await WaitForDispatcherTurnAsync(Root.DispatcherQueue);
        await WaitForCompositionFrameAsync();
        await WaitForDispatcherTurnAsync(Root.DispatcherQueue);
        await WaitForCompositionFrameAsync();

        var bitmap = new RenderTargetBitmap();
        int width = checked((int)Math.Round(Root.ActualWidth));
        int height = checked((int)Math.Round(Root.ActualHeight));
        await bitmap.RenderAsync(Root, width, height);
        IBuffer pixelBuffer = await bitmap.GetPixelsAsync();
        byte[] pixels = new byte[checked((int)pixelBuffer.Length)];
        using (DataReader reader = DataReader.FromBuffer(pixelBuffer))
        {
            reader.ReadBytes(pixels);
        }

        return RenderedFrame.FromBgra8(
            bitmap.PixelWidth,
            bitmap.PixelHeight,
            pixels);
    }

    public ValueTask DisposeAsync()
    {
        if (!_disposed)
        {
            _disposed = true;
            _window.Content = null;
            _window.Close();
        }

        return ValueTask.CompletedTask;
    }

    private static async Task ResizeClientAsync(
        Window window,
        FrameworkElement root,
        int width,
        int height)
    {
        var arranged = new TaskCompletionSource<bool>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        void ObserveLayout(object? sender, object args)
        {
            if (Math.Abs(root.ActualWidth - width) <= 1d &&
                Math.Abs(root.ActualHeight - height) <= 1d)
            {
                arranged.TrySetResult(true);
            }
        }

        void ObserveRoot(
            XamlRoot sender,
            XamlRootChangedEventArgs eventArguments) =>
            ObserveLayout(sender, eventArguments);

        root.LayoutUpdated += ObserveLayout;
        root.XamlRoot.Changed += ObserveRoot;
        try
        {
            double scale = root.XamlRoot.RasterizationScale;
            window.AppWindow.ResizeClient(new SizeInt32(
                (int)Math.Round(width * scale),
                (int)Math.Round(height * scale)));
            ObserveLayout(null, EventArgs.Empty);
            await arranged.Task.WaitAsync(UiTimeout);
            root.UpdateLayout();
        }
        finally
        {
            root.LayoutUpdated -= ObserveLayout;
            root.XamlRoot.Changed -= ObserveRoot;
        }
    }

    private static async Task WaitForDispatcherTurnAsync(
        DispatcherQueue dispatcherQueue)
    {
        var dispatched = new TaskCompletionSource<bool>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        if (!dispatcherQueue.TryEnqueue(() => dispatched.TrySetResult(true)))
        {
            throw new InvalidOperationException(
                "The render host dispatcher rejected a frame boundary.");
        }

        await dispatched.Task.WaitAsync(UiTimeout);
    }

    private static async Task WaitForCompositionFrameAsync()
    {
        var rendered = new TaskCompletionSource<bool>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        void OnRendering(object? sender, object args)
        {
            CompositionTarget.Rendering -= OnRendering;
            rendered.TrySetResult(true);
        }

        CompositionTarget.Rendering += OnRendering;
        try
        {
            await rendered.Task.WaitAsync(UiTimeout);
        }
        finally
        {
            CompositionTarget.Rendering -= OnRendering;
        }
    }
}
