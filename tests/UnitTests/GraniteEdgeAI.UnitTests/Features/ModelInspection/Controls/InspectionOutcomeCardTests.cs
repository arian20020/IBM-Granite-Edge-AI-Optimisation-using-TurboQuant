using GraniteEdgeAI.Features.ModelInspection.Controls;
using GraniteEdgeAI.Features.ModelInspection.Models;
using Microsoft.UI.Xaml;
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
    public async Task Ready_UsesApprovedHeightTypographyAndSuccessResources()
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
            TextBlock title = FindText(control, "Ready for hardware check");
            TextBlock message = FindText(
                control,
                "The package and runtime passed inspection.");

            Assert.AreEqual(82d, card.ActualHeight, 0.01, "ready banner height");
            Assert.AreEqual(40d, iconContainer.ActualWidth, 0.01, "status icon width");
            Assert.AreEqual(40d, iconContainer.ActualHeight, 0.01, "status icon height");
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
        "InspectionSuccessTextStrongBrush")]
    [DataRow(
        (int)InspectionOutcomeTone.Warning,
        "InspectionWarningSurfaceBrush",
        "InspectionWarningBorderBrush",
        "InspectionWarningAccentBrush",
        "InspectionWarningTextStrongBrush")]
    [DataRow(
        (int)InspectionOutcomeTone.Information,
        "InspectionBlueSurfaceBrush",
        "InspectionBlueBorderStrongBrush",
        "InspectionPrimaryBlueBrush",
        "InspectionTextPrimaryBrush")]
    [DataRow(
        (int)InspectionOutcomeTone.Error,
        "InspectionErrorSurfaceBrush",
        "InspectionErrorBorderBrush",
        "InspectionErrorTextBrush",
        "InspectionErrorTextStrongBrush")]
    [DataRow(
        (int)InspectionOutcomeTone.Neutral,
        "InspectionSurfaceMutedBrush",
        "InspectionBorderMutedBrush",
        "InspectionTextSecondaryMutedBrush",
        "InspectionTextPrimaryBrush")]
    public void EveryTone_UsesSharedSemanticResourcesAndVisibleMeaning(
        int toneValue,
        string surfaceKey,
        string borderKey,
        string iconKey,
        string titleKey)
    {
        InspectionOutcomeTone tone = (InspectionOutcomeTone)toneValue;
        InspectionOutcomeCard control = CreateControl(
            tone,
            $"{tone} outcome",
            $"{tone} status explanation");
        Border card = Find<Border>(control, "OutcomeCardBorder");
        Border iconContainer = Find<Border>(control, "OutcomeIconContainer");
        SymbolIcon icon = Find<SymbolIcon>(control, "OutcomeIcon");
        TextBlock title = FindText(control, $"{tone} outcome");

        Assert.AreSame(Resource(surfaceKey), card.Background, tone.ToString());
        Assert.AreSame(Resource(borderKey), card.BorderBrush, tone.ToString());
        Assert.AreSame(Resource(iconKey), iconContainer.Background, tone.ToString());
        Assert.AreSame(Resource(iconKey), iconContainer.BorderBrush, tone.ToString());
        Assert.AreSame(Resource("InspectionSurfaceBrush"), icon.Foreground, tone.ToString());
        Assert.AreSame(Resource(titleKey), title.Foreground, tone.ToString());
        Assert.AreEqual(Visibility.Visible, icon.Visibility, tone.ToString());
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
                IconSymbol = Symbol.Accept,
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
