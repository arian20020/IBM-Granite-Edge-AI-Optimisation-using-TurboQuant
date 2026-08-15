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
            ColumnDefinition trailing = layout.ColumnDefinitions[2];
            ContentControl focus = Find<ContentControl>(control, "OutcomeFocusTarget");
            Point titleOrigin = title.TransformToVisual(card).TransformPoint(new Point());
            Point messageOrigin = message.TransformToVisual(card).TransformPoint(new Point());

            Assert.AreEqual(0d, card.MinHeight, 0.01, "the banner must size naturally");
            Assert.AreEqual(leading.ActualWidth, trailing.ActualWidth, 0.01,
                "the copy must be balanced by equal outer columns");
            Assert.AreEqual(HorizontalAlignment.Stretch, focus.HorizontalContentAlignment);
            Assert.AreEqual(
                card.ActualWidth / 2d,
                titleOrigin.X + (title.ActualWidth / 2d),
                1d,
                "title centre");
            Assert.AreEqual(
                card.ActualWidth / 2d,
                messageOrigin.X + (message.ActualWidth / 2d),
                1d,
                "message centre");
            Assert.AreEqual(40d, iconContainer.ActualWidth, 0.01, "status icon width");
            Assert.AreEqual(40d, iconContainer.ActualHeight, 0.01, "status icon height");
            Assert.AreEqual(40d, glyph.SurfaceSize, 0.01, "glyph surface");
            Assert.AreEqual(InspectionStatusGlyphKind.Success, glyph.Kind);
            Assert.AreEqual(
                AccessibilityView.Raw,
                AutomationProperties.GetAccessibilityView(glyph));
            Assert.AreEqual(12d, card.CornerRadius.TopLeft, 0.01, "shared card radius");
            Assert.AreEqual(14d, title.FontSize, 0.01, "outcome title size");
            Assert.AreEqual(12d, message.FontSize, 0.01, "outcome helper size");
            Assert.AreSame(Resource("InspectionSuccessSurfaceBrush"), card.Background);
            Assert.AreSame(Resource("InspectionSuccessBorderBrush"), card.BorderBrush);
            Assert.AreSame(Resource("InspectionStrongBodyFontFamily"), title.FontFamily);
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
        "InspectionSuccessTextStrongBrush",
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

        Assert.AreSame(Resource(surfaceKey), card.Background, tone.ToString());
        Assert.AreSame(Resource(borderKey), card.BorderBrush, tone.ToString());
        Assert.AreSame(Resource(surfaceKey), iconContainer.Background, tone.ToString());
        Assert.AreSame(Resource(iconKey), iconContainer.BorderBrush, tone.ToString());
        Assert.AreSame(Resource(titleKey), title.Foreground, tone.ToString());
        Assert.AreEqual(Visibility.Visible, glyph.Visibility, tone.ToString());
        Assert.AreEqual(
            (InspectionStatusGlyphKind)glyphKindValue,
            glyph.Kind,
            tone.ToString());
        Assert.AreEqual(40d, glyph.SurfaceSize, 0.01d, tone.ToString());
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
