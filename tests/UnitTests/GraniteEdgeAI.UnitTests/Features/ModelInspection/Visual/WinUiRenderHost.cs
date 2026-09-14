using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using System.Collections.Concurrent;
using System.Runtime.InteropServices;
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

    internal static async Task ResizeClientAsync(
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

        root.LayoutUpdated += ObserveLayout;
        try
        {
            double scale = root.XamlRoot.RasterizationScale;
            var requestedClient = new SizeInt32(
                (int)Math.Round(width * scale),
                (int)Math.Round(height * scale));
            using var sizeAllowance = new NativeTestWindowSizeAllowance(window, requestedClient);
            window.AppWindow.ResizeClient(requestedClient);
            ObserveLayout(null, EventArgs.Empty);
            try
            {
                await arranged.Task.WaitAsync(UiTimeout);
            }
            catch (TimeoutException error)
            {
                throw new TimeoutException(
                    $"Test window did not reach {width}x{height} effective client pixels. " +
                    $"Layout={root.ActualWidth:0.##}x{root.ActualHeight:0.##}, " +
                    $"native client={window.AppWindow.ClientSize.Width}x{window.AppWindow.ClientSize.Height}, " +
                    $"rasterization scale={root.XamlRoot.RasterizationScale:0.##}.", error);
            }
            root.UpdateLayout();
        }
        finally
        {
            root.LayoutUpdated -= ObserveLayout;
        }
    }

    // Hosted Windows desktops can be smaller than a tested responsive endpoint.
    // Override only the native test window's monitor-derived maximum tracking
    // size during resize. XamlRoot and AdaptiveTrigger still see the requested
    // real client dimensions; no element widths or application states are faked.
    private sealed class NativeTestWindowSizeAllowance : IDisposable
    {
        private const uint WmGetMinMaxInfo = 0x0024;
        private static readonly ConcurrentDictionary<nuint, SizeInt32> RequestedSizes = new();
        private static readonly SubclassProcedure Callback = ObserveNativeSizing;
        private static int _nextId;
        private readonly nint _windowHandle;
        private readonly nuint _id;

        internal NativeTestWindowSizeAllowance(Window window, SizeInt32 requestedClient)
        {
            _windowHandle = WinRT.Interop.WindowNative.GetWindowHandle(window);
            _id = checked((nuint)Interlocked.Increment(ref _nextId));
            SizeInt32 outer = window.AppWindow.Size;
            SizeInt32 client = window.AppWindow.ClientSize;
            RequestedSizes[_id] = new SizeInt32(
                checked(requestedClient.Width + Math.Max(0, outer.Width - client.Width)),
                checked(requestedClient.Height + Math.Max(0, outer.Height - client.Height)));
            if (!SetWindowSubclass(_windowHandle, Callback, _id, 0))
            {
                RequestedSizes.TryRemove(_id, out _);
                throw new InvalidOperationException("Could not install the native test window size allowance.");
            }
        }

        public void Dispose()
        {
            bool removed = RemoveWindowSubclass(_windowHandle, Callback, _id);
            RequestedSizes.TryRemove(_id, out _);
            if (!removed)
            {
                throw new InvalidOperationException("Could not remove the native test window size allowance.");
            }
        }

        private static nint ObserveNativeSizing(
            nint window, uint message, nuint wParam, nint lParam, nuint id, nuint referenceData)
        {
            nint result = DefSubclassProc(window, message, wParam, lParam);
            if (message == WmGetMinMaxInfo && RequestedSizes.TryGetValue(id, out SizeInt32 requested))
            {
                NativeMinMaxInfo limits = Marshal.PtrToStructure<NativeMinMaxInfo>(lParam);
                limits.MaximumTrackSize.X = Math.Max(limits.MaximumTrackSize.X, requested.Width);
                limits.MaximumTrackSize.Y = Math.Max(limits.MaximumTrackSize.Y, requested.Height);
                Marshal.StructureToPtr(limits, lParam, false);
            }
            return result;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct NativePoint
        {
            internal int X;
            internal int Y;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct NativeMinMaxInfo
        {
            internal NativePoint Reserved;
            internal NativePoint MaximumSize;
            internal NativePoint MaximumPosition;
            internal NativePoint MinimumTrackSize;
            internal NativePoint MaximumTrackSize;
        }

        [UnmanagedFunctionPointer(CallingConvention.Winapi)]
        private delegate nint SubclassProcedure(
            nint window, uint message, nuint wParam, nint lParam, nuint id, nuint referenceData);

        [DllImport("comctl32.dll", ExactSpelling = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool SetWindowSubclass(
            nint window, SubclassProcedure callback, nuint id, nuint referenceData);

        [DllImport("comctl32.dll", ExactSpelling = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool RemoveWindowSubclass(nint window, SubclassProcedure callback, nuint id);

        [DllImport("comctl32.dll", ExactSpelling = true)]
        private static extern nint DefSubclassProc(nint window, uint message, nuint wParam, nint lParam);
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
