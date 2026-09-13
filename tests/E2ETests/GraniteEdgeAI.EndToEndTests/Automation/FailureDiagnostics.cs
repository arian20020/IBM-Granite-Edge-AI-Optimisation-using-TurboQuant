using System.Drawing;
using System.Drawing.Imaging;
using System.Text;
using System.Windows.Automation;

namespace GraniteEdgeAI.EndToEndTests.Automation;

internal static class FailureDiagnostics
{
    internal static void Capture(AutomationElement window, string outputDirectory)
    {
        Directory.CreateDirectory(outputDirectory);
        CaptureScreenshot(window, Path.Combine(outputDirectory, "window.png"));
        File.WriteAllText(Path.Combine(outputDirectory, "automation-tree.txt"), CaptureTree(window, maxDepth: 8));
    }

    private static void CaptureScreenshot(AutomationElement window, string path)
    {
        object current = window.Current;
        object bounds = current.GetType().GetProperty("BoundingRectangle")!.GetValue(current)!;
        Type boundsType = bounds.GetType();
        double left = (double)boundsType.GetProperty("Left")!.GetValue(bounds)!;
        double top = (double)boundsType.GetProperty("Top")!.GetValue(bounds)!;
        double width = (double)boundsType.GetProperty("Width")!.GetValue(bounds)!;
        double height = (double)boundsType.GetProperty("Height")!.GetValue(bounds)!;
        if (width <= 0 || height <= 0 || double.IsInfinity(width) || double.IsInfinity(height))
        {
            return;
        }

        using Bitmap bitmap = new(checked((int)Math.Ceiling(width)), checked((int)Math.Ceiling(height)));
        using Graphics graphics = Graphics.FromImage(bitmap);
        graphics.CopyFromScreen(checked((int)left), checked((int)top), 0, 0, bitmap.Size);
        bitmap.Save(path, ImageFormat.Png);
    }

    private static string CaptureTree(AutomationElement root, int maxDepth)
    {
        StringBuilder output = new();
        Append(root, 0);
        return output.ToString();

        void Append(AutomationElement element, int depth)
        {
            if (depth > maxDepth)
            {
                return;
            }

            AutomationElement.AutomationElementInformation current = element.Current;
            output.Append(' ', depth * 2).Append(current.ControlType.ProgrammaticName).Append(" id=").Append(PrivacyRedactor.Redact(current.AutomationId)).Append(" name=").AppendLine(PrivacyRedactor.Redact(current.Name));
            AutomationElement? child = TreeWalker.ControlViewWalker.GetFirstChild(element);
            while (child is not null)
            {
                Append(child, depth + 1);
                child = TreeWalker.ControlViewWalker.GetNextSibling(child);
            }
        }
    }
}
