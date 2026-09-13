// the drop handler's testable request boundary is platform-neutral, but the
// same production file also contains an unused WinUI adapter. This one type
// compiles that adapter while retaining the real Windows SDK projections.
namespace Microsoft.UI.Xaml
{
    internal sealed class DragEventArgs
    {
        internal Windows.ApplicationModel.DataTransfer.DataPackageView DataView => null!;
        internal Windows.ApplicationModel.DataTransfer.DataPackageOperation AcceptedOperation { get; set; }
        internal Windows.ApplicationModel.DataTransfer.DragOperationDeferral GetDeferral() => null!;
    }
}

namespace Windows.ApplicationModel.DataTransfer
{
    internal sealed class DragOperationDeferral
    {
        internal void Complete() { }
    }
}
