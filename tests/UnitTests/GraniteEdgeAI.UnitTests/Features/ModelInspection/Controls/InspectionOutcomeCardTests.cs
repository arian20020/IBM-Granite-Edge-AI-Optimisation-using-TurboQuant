using GraniteEdgeAI.Features.ModelInspection.Controls;
using GraniteEdgeAI.Features.ModelInspection.Models;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Automation.Peers;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.VisualStudio.TestTools.UnitTesting.AppContainer;
using Windows.Foundation;

namespace GraniteEdgeAI.UnitTests.Features.ModelInspection.Controls;

[TestClass]
[DoNotParallelize]
public sealed class InspectionOutcomeCardTests
{
    [UITestMethod]
    [TestCategory("WinUI")]
    public async Task Ready_UsesNaturalBalancedGeometryTypographyAndSuccessResources()
    {
        InspectionOutcomeCard control = CreateControl(
            InspectionOutcomeTone.Success,
            "Ready for hardware check",
            "The package and runtime passed inspection.");
        var loaded = new TaskCompletionSource<bool>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        control.Loaded += (_, _) => loaded.TrySetResult(true);
        var window = new Window { Content = control };

        try
        {
            window.Activate();
            await loaded.Task.WaitAsync(TimeSpan.FromSeconds(10));
            ArrangeControl(control, width: 840);
            Border card = Find<Border>(control, "OutcomeCardBorder");
            Border iconContainer = Find<Border>(control, "OutcomeIconContainer");
            InspectionStatusGlyph glyph = Find<InspectionStatusGlyph>(
                control,
                "OutcomeIcon");
            TextBlock title = FindText(control, "Ready for hardware check");
            TextBlock message = FindText(
                control,
                "The package and runtime passed inspection.");

            Grid layout = Find<Grid>(control, "OutcomeLayoutGrid");
            ColumnDefinition leading = layout.ColumnDefinitions[0];
            ContentControl focus = Find<ContentControl>(control, "OutcomeFocusTarget");
            Point titleOrigin = title.TransformToVisual(card).TransformPoint(new Point());
            Point messageOrigin = message.TransformToVisual(card).TransformPoint(new Point());

            Assert.AreEqual(0d, card.MinHeight, 0.01, "the banner must size naturally");
            Assert.AreEqual(2, layout.ColumnDefinitions.Count,
                "the compact outcome is one inline glyph plus left-aligned copy");
            Assert.AreEqual(44d, leading.ActualWidth, 0.01,
                "inline outcome glyph column");
            Assert.AreEqual(HorizontalAlignment.Stretch, focus.HorizontalContentAlignment);
            Assert.AreEqual(
                messageOrigin.X,
                titleOrigin.X,
                1d,
                "outcome title and message share one left edge");
            Assert.AreEqual(TextAlignment.Left, title.TextAlignment, "title alignment");
            Assert.AreEqual(TextAlignment.Left, message.TextAlignment, "message alignment");
            Assert.AreEqual(HorizontalAlignment.Stretch, title.HorizontalAlignment,
                "title fills the copy column");
            Assert.AreEqual(HorizontalAlignment.Stretch, message.HorizontalAlignment,
                "message fills the copy column");
            Assert.AreEqual(40d, iconContainer.ActualWidth, 0.01, "status icon width");
            Assert.AreEqual(40d, iconContainer.ActualHeight, 0.01, "status icon height");
            Assert.AreEqual(22d, glyph.SurfaceSize, 0.01,
                "the approved dependency-property surface is uniformly scaled");
            Viewbox glyphHost = VisibleGlyphHost(iconContainer);
            Assert.AreEqual(20d, glyphHost.ActualWidth, 0.01, "visible glyph width");
            Assert.AreEqual(20d, glyphHost.ActualHeight, 0.01, "visible glyph height");
            Assert.AreEqual(InspectionStatusGlyphKind.Success, glyph.Kind);
            Assert.AreEqual(
                AccessibilityView.Raw,
                AutomationProperties.GetAccessibilityView(glyph));
            Assert.AreEqual(0d, card.CornerRadius.TopLeft, 0.01, "inline outcome radius");
            Assert.AreEqual(new Thickness(24d, 16d, 24d, 16d), card.Padding,
                "compact outcome padding");
            Assert.AreEqual(14d, layout.ColumnSpacing, 0.01d, "outcome column spacing");
            Assert.AreEqual(20d, title.FontSize, 0.01, "outcome title size");
            Assert.AreEqual(12d, message.FontSize, 0.01, "outcome helper size");
            Assert.AreEqual((byte)0, Assert.IsInstanceOfType<SolidColorBrush>(card.Background).Color.A);
            Assert.AreEqual((byte)0, Assert.IsInstanceOfType<SolidColorBrush>(card.BorderBrush).Color.A);
            Assert.AreEqual(new Thickness(0d), card.BorderThickness);
            Assert.AreSame(Resource("InspectionSuccessSurfaceBrush"), iconContainer.Background);
            Assert.AreSame(Resource("InspectionSuccessTextBrush"), iconContainer.BorderBrush);
            Assert.AreEqual(Microsoft.UI.Text.FontWeights.SemiBold, title.FontWeight);
            Assert.AreSame(Resource("InspectionHelperFontFamily"), message.FontFamily);
        }
        finally
        {
            window.Content = null;
            window.Close();
        }
    }

    [UITestMethod]
    [TestCategory("WinUI")]
    [DataRow(
        (int)InspectionOutcomeTone.Success,
        "InspectionSuccessSurfaceBrush",
        "InspectionSuccessBorderBrush",
        "InspectionSuccessTextBrush",
        "InspectionTextPrimaryBrush",
        (int)InspectionStatusGlyphKind.Success)]
    [DataRow(
        (int)InspectionOutcomeTone.Warning,
        "InspectionWarningSurfaceBrush",
        "InspectionWarningBorderBrush",
        "InspectionWarningAccentBrush",
        "InspectionWarningTextStrongBrush",
        (int)InspectionStatusGlyphKind.Warning)]
    [DataRow(
        (int)InspectionOutcomeTone.Information,
        "InspectionBlueSurfaceBrush",
        "InspectionBlueBorderStrongBrush",
        "InspectionPrimaryBlueBrush",
        "InspectionTextPrimaryBrush",
        (int)InspectionStatusGlyphKind.Information)]
    [DataRow(
        (int)InspectionOutcomeTone.Error,
        "InspectionErrorSurfaceBrush",
        "InspectionErrorBorderBrush",
        "InspectionErrorTextBrush",
        "InspectionErrorTextStrongBrush",
        (int)InspectionStatusGlyphKind.Error)]
    [DataRow(
        (int)InspectionOutcomeTone.Neutral,
        "InspectionSurfaceMutedBrush",
        "InspectionBorderMutedBrush",
        "InspectionTextSecondaryMutedBrush",
        "InspectionTextPrimaryBrush",
        (int)InspectionStatusGlyphKind.NotComplete)]
    public void EveryTone_UsesSharedSemanticResourcesAndVisibleMeaning(
        int toneValue,
        string surfaceKey,
        string borderKey,
        string iconKey,
        string titleKey,
        int glyphKindValue)
    {
        InspectionOutcomeTone tone = (InspectionOutcomeTone)toneValue;
        InspectionOutcomeCard control = CreateControl(
            tone,
            $"{tone} outcome",
            $"{tone} status explanation");
        Border card = Find<Border>(control, "OutcomeCardBorder");
        Border iconContainer = Find<Border>(control, "OutcomeIconContainer");
        InspectionStatusGlyph glyph = Find<InspectionStatusGlyph>(
            control,
            "OutcomeIcon");
        TextBlock title = FindText(control, $"{tone} outcome");

        Assert.AreEqual((byte)0,
            Assert.IsInstanceOfType<SolidColorBrush>(card.Background).Color.A,
            "The shared outcome surface remains transparent for every tone.");
        Assert.AreEqual((byte)0,
            Assert.IsInstanceOfType<SolidColorBrush>(card.BorderBrush).Color.A,
            "Tone is carried by the status icon and title, not a second outer border.");
        Assert.AreSame(Resource(surfaceKey), iconContainer.Background, tone.ToString());
        Assert.AreSame(Resource(iconKey), iconContainer.BorderBrush, tone.ToString());
        Assert.AreSame(Resource(titleKey), title.Foreground, tone.ToString());
        Assert.AreEqual(Visibility.Visible, glyph.Visibility, tone.ToString());
        Assert.AreEqual(
            (InspectionStatusGlyphKind)glyphKindValue,
            glyph.Kind,
            tone.ToString());
        Assert.AreEqual(40d, iconContainer.Width, 0.01d, tone.ToString());
        Assert.AreEqual(40d, iconContainer.Height, 0.01d, tone.ToString());
        Assert.AreEqual(22d, glyph.SurfaceSize, 0.01d, tone.ToString());
        Viewbox glyphHost = VisibleGlyphHost(iconContainer);
        Assert.AreEqual(20d, glyphHost.Width, 0.01d, tone.ToString());
        Assert.AreEqual(20d, glyphHost.Height, 0.01d, tone.ToString());
        Assert.AreEqual(
            AccessibilityView.Raw,
            AutomationProperties.GetAccessibilityView(glyph));
        Assert.AreEqual($"{tone} outcome", FindText(control, $"{tone} outcome").Text);
        Assert.AreEqual(
            $"{tone} status explanation",
            FindText(control, $"{tone} status explanation").Text);
    }

    [UITestMethod]
    [TestCategory("WinUI")]
    [TestCategory("DeferredInspectionPresentation")]
    public void LongOutcomeText_WrapsWithoutFixedLineClipping()
    {
        InspectionOutcomeCard control = CreateControl(
            InspectionOutcomeTone.Warning,
            "A long warning heading that must remain readable when text scaling or a narrow client causes several natural lines",
            "This complete warning explanation must remain available without a fixed line cap, even when larger accessible text needs additional vertical space.",
            width: 360);
        Border card = Find<Border>(control, "OutcomeCardBorder");
        TextBlock[] text =
        [
            Find<TextBlock>(control, "OutcomeTitle"),
            Find<TextBlock>(control, "OutcomeMessage")
        ];

        Assert.IsTrue(card.ActualHeight > 82d, "long text must grow the banner");
        Assert.IsTrue(text.All(item => item.MaxLines == 0), "required text cannot be line-capped");
        Assert.IsTrue(text.All(item => item.TextWrapping != TextWrapping.NoWrap));

        double naturalHeight = card.ActualHeight;
        foreach (TextBlock item in text)
        {
            item.FontSize *= 2d;
        }

        ArrangeControl(control, width: 360);

        Assert.IsTrue(
            card.ActualHeight > naturalHeight,
            "the outcome card must grow at 200% equivalent text size");
        Assert.IsTrue(text.All(item =>
            item.ActualHeight + item.Margin.Top + item.Margin.Bottom + 1d >=
                item.DesiredSize.Height));
    }

    private static InspectionOutcomeCard CreateControl(
        InspectionOutcomeTone tone,
        string title,
        string message,
        double width = 840)
    {
        var control = new InspectionOutcomeCard
        {
            Width = width,
            Presentation = new InspectionOutcomePresentation
            {
                Kind = InspectionOutcomePresentationKind.Ready,
                Tone = tone,
                GlyphKind = tone switch
                {
                    InspectionOutcomeTone.Success =>
                        InspectionStatusGlyphKind.Success,
                    InspectionOutcomeTone.Warning =>
                        InspectionStatusGlyphKind.Warning,
                    InspectionOutcomeTone.Information =>
                        InspectionStatusGlyphKind.Information,
                    InspectionOutcomeTone.Error =>
                        InspectionStatusGlyphKind.Error,
                    InspectionOutcomeTone.Neutral =>
                        InspectionStatusGlyphKind.NotComplete,
                    _ => throw new ArgumentOutOfRangeException(nameof(tone), tone, null)
                },
                Title = title,
                Message = message,
                AutomationName = $"{title}. {message}"
            }
        };

        ArrangeControl(control, width);
        return control;
    }

    private static void ArrangeControl(
        InspectionOutcomeCard control,
        double width)
    {
        control.Measure(new Size(width, double.PositiveInfinity));
        control.Arrange(new Rect(
            0,
            0,
            width,
            control.DesiredSize.Height));
        control.UpdateLayout();
    }

    private static object Resource(string key) =>
        Application.Current.Resources[key];

    private static Viewbox VisibleGlyphHost(DependencyObject root) =>
        Descendants(root)
            .OfType<Viewbox>()
            .Single(viewbox =>
                Math.Abs(viewbox.Width - 20d) < 0.01d &&
                Math.Abs(viewbox.Height - 20d) < 0.01d);

    private static T Find<T>(FrameworkElement root, string name)
        where T : DependencyObject =>
        Assert.IsInstanceOfType<T>(root.FindName(name));

    private static TextBlock FindText(DependencyObject root, string text) =>
        Descendants(root)
            .OfType<TextBlock>()
            .Single(candidate => candidate.Text == text);

    private static IEnumerable<DependencyObject> Descendants(
        DependencyObject parent)
    {
        int childCount = VisualTreeHelper.GetChildrenCount(parent);
        for (int index = 0; index < childCount; index++)
        {
            DependencyObject child = VisualTreeHelper.GetChild(parent, index);
            yield return child;
            foreach (DependencyObject descendant in Descendants(child))
            {
                yield return descendant;
            }
        }
    }
}
