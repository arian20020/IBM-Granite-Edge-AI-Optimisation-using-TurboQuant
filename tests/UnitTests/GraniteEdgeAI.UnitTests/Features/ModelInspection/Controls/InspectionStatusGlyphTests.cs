using GraniteEdgeAI.Features.ModelInspection.Controls;
using GraniteEdgeAI.Features.ModelInspection.Models;
using GraniteEdgeAI.Features.ModelInspection.Presentation;
using GraniteEdgeAI.Features.ModelInspection.ViewModels;
using GraniteEdgeAI.Features.Onboarding;
using GraniteEdgeAI.Features.Onboarding.Controls;
using Microsoft.UI.Composition;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Automation.Peers;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Hosting;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.VisualStudio.TestTools.UnitTesting.AppContainer;
using System.Reflection;
using Windows.Foundation;
using XamlPath = Microsoft.UI.Xaml.Shapes.Path;

namespace GraniteEdgeAI.UnitTests.Features.ModelInspection.Controls;

[TestClass]
[DoNotParallelize]
public sealed class InspectionStatusGlyphTests
{
    [UITestMethod]
    [TestCategory("WinUI")]
    [DataRow(InspectionStatusGlyphKind.Success, 22d)]
    [DataRow(InspectionStatusGlyphKind.Success, 30d)]
    [DataRow(InspectionStatusGlyphKind.Success, 36d)]
    [DataRow(InspectionStatusGlyphKind.Success, 40d)]
    [DataRow(InspectionStatusGlyphKind.Warning, 22d)]
    [DataRow(InspectionStatusGlyphKind.Warning, 30d)]
    [DataRow(InspectionStatusGlyphKind.Error, 30d)]
    [DataRow(InspectionStatusGlyphKind.Error, 36d)]
    [DataRow(InspectionStatusGlyphKind.Information, 30d)]
    [DataRow(InspectionStatusGlyphKind.Waiting, 30d)]
    [DataRow(InspectionStatusGlyphKind.Waiting, 36d)]
    [DataRow(InspectionStatusGlyphKind.NotComplete, 36d)]
    [DataRow(InspectionStatusGlyphKind.Active, 30d)]
    public async Task EveryApprovedKindAndSurface_UsesCenteredVectorGeometry(
        InspectionStatusGlyphKind kind,
        double surfaceSize)
    {
        string kindName = kind.ToString();
        InspectionStatusGlyph glyph = CreateGlyph(
            kind,
            surfaceSize,
            stageNumber: "3",
            isMotionEnabled: kind == InspectionStatusGlyphKind.Active);
        var loaded = new TaskCompletionSource<bool>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        glyph.Loaded += (_, _) => loaded.TrySetResult(true);
        var window = new Window { Content = glyph };

        try
        {
            window.Activate();
            await loaded.Task.WaitAsync(TimeSpan.FromSeconds(10));
            glyph.UpdateLayout();

            Assert.AreEqual(
                AccessibilityView.Raw,
                AutomationProperties.GetAccessibilityView(glyph));
            Assert.AreEqual(surfaceSize, glyph.Width, 0.001d);
            Assert.AreEqual(surfaceSize, glyph.Height, 0.001d);

            Viewbox vectorViewbox = Descendants(glyph)
                .OfType<Viewbox>()
                .Single(element => Equals(element.Tag, "StatusGlyphVectorViewbox"));
            Canvas logicalCanvas = Descendants(glyph)
                .OfType<Canvas>()
                .Single(element => Equals(element.Tag, "StatusGlyphLogicalCanvas"));
            Assert.AreEqual(24d, logicalCanvas.Width, 0.001d);
            Assert.AreEqual(24d, logicalCanvas.Height, 0.001d);
            Rect vectorBounds = logicalCanvas
                .TransformToVisual(glyph)
                .TransformBounds(new Rect(0d, 0d, 24d, 24d));
            Assert.AreEqual(surfaceSize / 2d, CenterX(vectorBounds), 0.5d);
            Assert.AreEqual(surfaceSize / 2d, CenterY(vectorBounds), 0.5d);
            Assert.AreEqual(surfaceSize, vectorViewbox.ActualWidth, 0.5d);
            Assert.AreEqual(surfaceSize, vectorViewbox.ActualHeight, 0.5d);

            FrameworkElement kindRoot = Descendants(glyph)
                .OfType<FrameworkElement>()
                .Single(element => Equals(element.Tag, $"GlyphKind:{kindName}"));
            Assert.AreEqual(Visibility.Visible, kindRoot.Visibility);
            Shape[] shapes = Descendants(kindRoot).OfType<Shape>().ToArray();
            Assert.IsNotEmpty(shapes, $"{kindName} must use vector geometry.");
            Assert.HasCount(0, Descendants(kindRoot).OfType<SymbolIcon>());
            Assert.HasCount(0, Descendants(kindRoot).OfType<FontIcon>());
            Assert.HasCount(0, Descendants(kindRoot).OfType<ProgressRing>());

            TextBlock[] decorativeText = Descendants(kindRoot)
                .OfType<TextBlock>()
                .ToArray();
            if (kindName == "Waiting")
            {
                Assert.HasCount(1, decorativeText);
                Assert.AreEqual("3", decorativeText[0].Text);
                Assert.AreEqual(
                    AccessibilityView.Raw,
                    AutomationProperties.GetAccessibilityView(decorativeText[0]));
                Assert.AreEqual(
                    string.Empty,
                    AutomationProperties.GetName(decorativeText[0]));
            }
            else
            {
                Assert.HasCount(
                    0,
                    decorativeText,
                    "StageNumber is the only decorative-text exception.");
            }

            switch (kindName)
            {
                case "Success":
                    AssertSuccessGeometry(kindRoot);
                    break;
                case "Warning":
                    AssertWarningGeometry(kindRoot);
                    break;
                case "Error":
                case "Information":
                    AssertOpticalCenter(kindRoot, kindName);
                    break;
                case "NotComplete":
                    AssertOpticalCenter(kindRoot, kindName);
                    AssertNotCompleteGeometry(kindRoot);
                    break;
                case "Active":
                    AssertActiveGeometryAndMotion(glyph, kindRoot);
                    break;
            }

            AssertSemanticBrushes(kindRoot, kindName);
            if (kindName != "Active")
            {
                SetProperty(glyph, "IsMotionEnabled", true);
                Assert.AreEqual(
                    false,
                    GetProperty(glyph, "IsPrecisionOrbitRunning"),
                    $"{kindName} must remain static when motion is enabled.");
                Assert.AreEqual(
                    0f,
                    OrbitVisual(glyph).RotationAngleInDegrees,
                    0.01f);
            }
        }
        finally
        {
            window.Content = null;
            window.Close();
        }
    }

    [UITestMethod]
    [TestCategory("WinUI")]
    public void DependencyProperties_ValidateApprovedValuesAndNormalizeStageNumber()
    {
        var glyph = new InspectionStatusGlyph();

        Assert.AreEqual("Success", GetProperty(glyph, "Kind")!.ToString());
        Assert.AreEqual(30d, GetProperty(glyph, "SurfaceSize"));
        Assert.AreEqual(string.Empty, GetProperty(glyph, "StageNumber"));
        Assert.AreEqual(false, GetProperty(glyph, "IsMotionEnabled"));

        glyph.SetValue(
            InspectionStatusGlyph.KindProperty,
            InspectionStatusGlyphKind.Error);
        glyph.SetValue(InspectionStatusGlyph.SurfaceSizeProperty, 40d);
        glyph.SetValue(InspectionStatusGlyph.StageNumberProperty, " 12 ");
        glyph.SetValue(InspectionStatusGlyph.IsMotionEnabledProperty, true);
        Assert.AreEqual("Error", GetProperty(glyph, "Kind")!.ToString());
        Assert.AreEqual(40d, glyph.Width, 0.001d);
        Assert.AreEqual(40d, glyph.Height, 0.001d);
        Assert.AreEqual("1", GetProperty(glyph, "StageNumber"));
        Assert.AreEqual(true, GetProperty(glyph, "IsMotionEnabled"));
        Assert.AreEqual(
            Visibility.Visible,
            KindRoot(glyph, "Error").Visibility,
            "Kind changes must update the existing control instance.");

        AssertRejected(() => glyph.SetValue(
            InspectionStatusGlyph.KindProperty,
            (InspectionStatusGlyphKind)99));
        Assert.AreEqual("Error", GetProperty(glyph, "Kind")!.ToString());
        foreach (double invalidSize in new[]
        {
            double.NaN,
            double.PositiveInfinity,
            double.NegativeInfinity,
            0d,
            21d,
            24d,
            31d,
            41d
        })
        {
            AssertRejected(() =>
                glyph.SetValue(
                    InspectionStatusGlyph.SurfaceSizeProperty,
                    invalidSize));
            Assert.AreEqual(40d, GetProperty(glyph, "SurfaceSize"));
        }

        glyph.SetValue(InspectionStatusGlyph.StageNumberProperty, null);
        Assert.AreEqual(string.Empty, GetProperty(glyph, "StageNumber"));
    }

    [UITestMethod]
    [TestCategory("WinUI")]
    public async Task LoadedAndUnloaded_RepeatedTransitionsOwnOneOrbitIdempotently()
    {
        InspectionStatusGlyph glyph = CreateGlyph(
            InspectionStatusGlyphKind.Active,
            30d,
            stageNumber: string.Empty,
            isMotionEnabled: true);
        var window = new Window();

        try
        {
            await AttachAndWaitAsync(window, glyph);
            Assert.AreEqual(true, GetProperty(glyph, "IsPrecisionOrbitRunning"));
            Assert.AreEqual(1, GetProperty(glyph, "PrecisionOrbitStartCount"));
            Assert.AreEqual(0, GetProperty(glyph, "PrecisionOrbitStopCount"));

            SetProperty(glyph, "Kind", GetProperty(glyph, "Kind"));
            SetProperty(glyph, "SurfaceSize", 30d);
            SetProperty(glyph, "IsMotionEnabled", true);
            Assert.AreEqual(1, GetProperty(glyph, "PrecisionOrbitStartCount"));

            await DetachAndWaitAsync(window, glyph);
            Assert.AreEqual(false, GetProperty(glyph, "IsPrecisionOrbitRunning"));
            Assert.AreEqual(1, GetProperty(glyph, "PrecisionOrbitStopCount"));
            Assert.AreEqual(0f, OrbitVisual(glyph).RotationAngleInDegrees, 0.01f);

            await AttachAndWaitAsync(window, glyph);
            Assert.AreEqual(true, GetProperty(glyph, "IsPrecisionOrbitRunning"));
            Assert.AreEqual(2, GetProperty(glyph, "PrecisionOrbitStartCount"));

            SetProperty(glyph, "IsMotionEnabled", false);
            SetProperty(glyph, "IsMotionEnabled", false);
            Assert.AreEqual(false, GetProperty(glyph, "IsPrecisionOrbitRunning"));
            Assert.AreEqual(2, GetProperty(glyph, "PrecisionOrbitStopCount"));
            Assert.AreEqual(0f, OrbitVisual(glyph).RotationAngleInDegrees, 0.01f);
        }
        finally
        {
            window.Content = null;
            window.Close();
        }
    }

    [UITestMethod]
    [TestCategory("WinUI")]
    public async Task BoundUses_ReplaceStockStatusMarksAtEveryApprovedSurface()
    {
        var progressRows = new InspectionProgressRows();
        progressRows.Reset(new ModelInspectionRenderKey(1, 0));
        var progress = new InspectionContentCard
        {
            Presentation = InitialInspectionProgressPresentationFactory.Create(
                progressRows)
        };
        var disclosure = new InspectionContentCard
        {
            Presentation = new InspectionContentCardPresentation
            {
                Mode = InspectionContentCardMode.Warnings,
                SectionTitle = "Review findings",
                DisclosureStatus = InspectionContentStatus.Warning,
                DisclosureSummary = "One warning",
                DisclosureVisibility = Visibility.Visible,
                Items = Array.Empty<InspectionContentItemPresentation>()
            }
        };
        var model = new InspectionModelCard
        {
            Presentation = new InspectionModelCardPresentation
            {
                DisplayMode = InspectionModelCardMode.Detailed,
                ModelName = "Safe model",
                InspectionChecksSummary = "One check passed",
                InspectionDetailsVisibility = Visibility.Visible,
                IsInspectionDetailsExpanded = true,
                InspectionChecks =
                [
                    new InspectionCheckPresentation
                    {
                        Title = "Package",
                        Detail = "Verified.",
                        Status = InspectionCheckStatus.Passed,
                        StatusText = "Passed",
                        AutomationName = "Package. Passed."
                    }
                ]
            }
        };
        var outcomePresentation = new InspectionOutcomePresentation
        {
            Kind = InspectionOutcomePresentationKind.Ready,
            Tone = InspectionOutcomeTone.Success,
            GlyphKind = InspectionStatusGlyphKind.Success,
            Title = "Ready",
            Message = "Inspection complete.",
            AutomationName = "Ready. Inspection complete."
        };

        var outcome = new InspectionOutcomeCard
        {
            Presentation = outcomePresentation
        };
        var onboarding = new OnboardingStageIndicator
        {
            CurrentStage = OnboardingStage.CheckHardwareFit
        };
        var host = new StackPanel();
        host.Children.Add(progress);
        host.Children.Add(disclosure);
        host.Children.Add(model);
        host.Children.Add(outcome);
        host.Children.Add(onboarding);
        var loaded = new TaskCompletionSource<bool>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        host.Loaded += (_, _) => loaded.TrySetResult(true);
        var window = new Window
        {
            Content = new ScrollViewer { Content = host }
        };

        try
        {
            window.Activate();
            await loaded.Task.WaitAsync(TimeSpan.FromSeconds(10));
            host.UpdateLayout();

            AssertSurface(model, 22d, "Success");
            AssertSurface(progress, 30d, "Waiting");
            AssertSurface(disclosure, 30d, "Warning");
            AssertSurface(onboarding, 36d, "Success");
            AssertSurface(outcome, 40d, "Success");

            DependencyObject[] affected =
            [
                .. Descendants(progress),
                .. Descendants(disclosure),
                .. Descendants(model),
                .. Descendants(outcome),
                .. Descendants(onboarding)
            ];
            Symbol[] forbiddenSymbols =
            [
                Symbol.Accept,
                Symbol.Important,
                Symbol.Cancel,
                Symbol.Help,
                Symbol.Clock
            ];
            Assert.IsFalse(affected
                .OfType<SymbolIcon>()
                .Any(icon => forbiddenSymbols.Contains(icon.Symbol)));
            Assert.HasCount(0, affected.OfType<ProgressRing>());
            Assert.IsFalse(affected
                .OfType<TextBlock>()
                .Any(text =>
                    text.Text is "\u2713" or "\u2715" or "\u2016"));

            foreach (InspectionStatusGlyph statusGlyph in affected
                .OfType<InspectionStatusGlyph>())
            {
                Assert.AreEqual(
                    AccessibilityView.Raw,
                    AutomationProperties.GetAccessibilityView(statusGlyph));
                Assert.AreEqual(
                    string.Empty,
                    AutomationProperties.GetName(statusGlyph));
            }
        }
        finally
        {
            window.Content = null;
            window.Close();
        }
    }

    private static void AssertSuccessGeometry(FrameworkElement kindRoot)
    {
        XamlPath check = Tagged<XamlPath>(kindRoot, "Mark:SuccessCheck");
        Assert.AreEqual(PenLineCap.Round, check.StrokeStartLineCap);
        Assert.AreEqual(PenLineCap.Round, check.StrokeEndLineCap);
        PathGeometry geometry = Assert.IsInstanceOfType<PathGeometry>(check.Data);
        Assert.HasCount(1, geometry.Figures);
        PathFigure figure = geometry.Figures[0];
        Assert.HasCount(1, figure.Segments);
        PolyLineSegment segment = Assert.IsInstanceOfType<PolyLineSegment>(
            figure.Segments[0]);
        Assert.HasCount(2, segment.Points);
        Point[] endpoints = [figure.StartPoint, segment.Points[^1]];
        Assert.IsTrue(endpoints.All(point =>
            point.X > 0d && point.X < 24d && point.Y > 0d && point.Y < 24d));
    }

    private static void AssertWarningGeometry(FrameworkElement kindRoot)
    {
        XamlPath triangle = Tagged<XamlPath>(kindRoot, "Mark:WarningTriangle");
        Rect triangleBounds = triangle.Data.Bounds;
        Assert.AreEqual(
            12d - triangleBounds.Left,
            triangleBounds.Right - 12d,
            0.01d,
            "The warning triangle must be horizontally symmetric.");
        XamlPath stem = Tagged<XamlPath>(kindRoot, "Mark:WarningStem");
        Ellipse dot = Tagged<Ellipse>(kindRoot, "Mark:WarningDot");
        Assert.AreEqual(12d, CenterX(stem.Data.Bounds), 0.01d);
        Assert.AreEqual(12d, CenterX(ShapeBounds(dot)), 0.01d);
    }

    private static void AssertOpticalCenter(
        FrameworkElement kindRoot,
        string kindName)
    {
        Shape[] marks = Descendants(kindRoot)
            .OfType<Shape>()
            .Where(shape => shape.Tag is string tag &&
                tag.StartsWith("Mark:", StringComparison.Ordinal))
            .ToArray();
        Assert.IsNotEmpty(marks);
        Rect bounds = marks
            .Select(ShapeBounds)
            .Aggregate(Union);
        Assert.AreEqual(12d, CenterX(bounds), 1d, $"{kindName} horizontal centre");
        Assert.AreEqual(12d, CenterY(bounds), 1d, $"{kindName} vertical centre");
    }

    private static void AssertNotCompleteGeometry(FrameworkElement kindRoot)
    {
        XamlPath left = Tagged<XamlPath>(
            kindRoot,
            "Mark:NotCompleteBarLeft");
        XamlPath right = Tagged<XamlPath>(
            kindRoot,
            "Mark:NotCompleteBarRight");
        Assert.AreEqual(PenLineCap.Round, left.StrokeStartLineCap);
        Assert.AreEqual(PenLineCap.Round, left.StrokeEndLineCap);
        Assert.AreEqual(PenLineCap.Round, right.StrokeStartLineCap);
        Assert.AreEqual(PenLineCap.Round, right.StrokeEndLineCap);
        Assert.AreEqual(
            12d - CenterX(left.Data.Bounds),
            CenterX(right.Data.Bounds) - 12d,
            0.01d,
            "NotComplete pause bars must be symmetric around the optical centre.");
        Assert.AreEqual(
            CenterY(left.Data.Bounds),
            CenterY(right.Data.Bounds),
            0.01d);
    }

    private static void AssertActiveGeometryAndMotion(
        FrameworkElement glyph,
        FrameworkElement kindRoot)
    {
        Ellipse track = Tagged<Ellipse>(kindRoot, "ActiveTrack");
        XamlPath arc = Tagged<XamlPath>(kindRoot, "ActiveArc");
        Assert.AreSame(Resource("InspectionBorderStrongBrush"), track.Stroke);
        Assert.AreSame(Resource("InspectionPrimaryBlueBrush"), arc.Stroke);
        Assert.AreEqual(PenLineCap.Round, arc.StrokeStartLineCap);
        Assert.AreEqual(PenLineCap.Round, arc.StrokeEndLineCap);
        Assert.AreEqual(true, GetProperty(glyph, "IsPrecisionOrbitRunning"));
        ScalarKeyFrameAnimation animation =
            Assert.IsInstanceOfType<ScalarKeyFrameAnimation>(
                GetProperty(glyph, "PrecisionOrbitAnimation"));
        Assert.AreEqual(TimeSpan.FromMilliseconds(1050), animation.Duration);
        Assert.AreEqual(AnimationIterationBehavior.Forever, animation.IterationBehavior);
        CompositionEasingFunction easing =
            Assert.IsInstanceOfType<CompositionEasingFunction>(
                GetProperty(glyph, "PrecisionOrbitEasing"));
        StringAssert.Contains(easing.GetType().Name, "Linear");

        SetProperty(glyph, "IsMotionEnabled", false);
        Assert.AreEqual(false, GetProperty(glyph, "IsPrecisionOrbitRunning"));
        Assert.AreEqual(0f, OrbitVisual(glyph).RotationAngleInDegrees, 0.01f);
    }

    private static void AssertSemanticBrushes(
        FrameworkElement kindRoot,
        string kindName)
    {
        string brushKey = kindName switch
        {
            "Success" => "InspectionSuccessTextBrush",
            "Warning" => "InspectionWarningTextBrush",
            "Error" => "InspectionErrorTextBrush",
            "Information" or "Active" => "InspectionPrimaryBlueBrush",
            "Waiting" => "InspectionTextSecondaryMutedBrush",
            "NotComplete" => "InspectionTextSecondaryStrongBrush",
            _ => throw new AssertFailedException($"Unknown glyph kind: {kindName}")
        };
        Brush semanticBrush = Assert.IsInstanceOfType<Brush>(Resource(brushKey));
        Brush[] markBrushes = Descendants(kindRoot)
            .OfType<Shape>()
            .SelectMany(shape => new[] { shape.Fill, shape.Stroke })
            .Where(brush => brush is not null)
            .Cast<Brush>()
            .ToArray();
        if (kindName == "Waiting")
        {
            markBrushes =
            [
                .. markBrushes,
                .. Descendants(kindRoot)
                    .OfType<TextBlock>()
                    .Select(text => text.Foreground)
                    .Where(brush => brush is not null)
            ];
        }

        Assert.IsTrue(
            markBrushes.Any(brush => ReferenceEquals(brush, semanticBrush)),
            $"{kindName} must use {brushKey}.");

        ResourceDictionary highContrast = Application.Current.Resources
            .MergedDictionaries
            .Select(dictionary => dictionary.ThemeDictionaries.ContainsKey(
                    "HighContrast")
                ? dictionary.ThemeDictionaries["HighContrast"] as
                    ResourceDictionary
                : null)
            .FirstOrDefault(dictionary => dictionary?.ContainsKey(brushKey) == true)
            ?? throw new AssertFailedException(
                $"High Contrast must define semantic glyph brush {brushKey}.");
        Assert.IsInstanceOfType<SolidColorBrush>(highContrast[brushKey]);
    }

    private static InspectionStatusGlyph CreateGlyph(
        InspectionStatusGlyphKind kind,
        double surfaceSize,
        string stageNumber,
        bool isMotionEnabled)
    {
        return new InspectionStatusGlyph
        {
            Kind = kind,
            SurfaceSize = surfaceSize,
            StageNumber = stageNumber,
            IsMotionEnabled = isMotionEnabled
        };
    }

    private static object? GetProperty(object target, string name) =>
        target.GetType()
            .GetProperty(
                name,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)!
            .GetValue(target);

    private static void SetProperty(object target, string name, object? value) =>
        target.GetType()
            .GetProperty(
                name,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)!
            .SetValue(target, value);

    private static void AssertRejected(Action action)
    {
        Exception exception = Assert.Throws<Exception>(action);
        while (exception is TargetInvocationException target &&
            target.InnerException is not null)
        {
            exception = target.InnerException;
        }

        Assert.IsInstanceOfType<ArgumentOutOfRangeException>(exception);
    }

    private static async Task AttachAndWaitAsync(
        Window window,
        FrameworkElement element)
    {
        var loaded = new TaskCompletionSource<bool>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        RoutedEventHandler handler = (_, _) => loaded.TrySetResult(true);
        element.Loaded += handler;
        try
        {
            window.Content = element;
            window.Activate();
            await loaded.Task.WaitAsync(TimeSpan.FromSeconds(10));
            element.UpdateLayout();
        }
        finally
        {
            element.Loaded -= handler;
        }
    }

    private static async Task DetachAndWaitAsync(
        Window window,
        FrameworkElement element)
    {
        var unloaded = new TaskCompletionSource<bool>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        RoutedEventHandler handler = (_, _) => unloaded.TrySetResult(true);
        element.Unloaded += handler;
        try
        {
            window.Content = null;
            await unloaded.Task.WaitAsync(TimeSpan.FromSeconds(10));
        }
        finally
        {
            element.Unloaded -= handler;
        }
    }

    private static void AssertSurface(
        DependencyObject root,
        double size,
        string kindName)
    {
        InspectionStatusGlyph[] matches = Descendants(root)
            .OfType<InspectionStatusGlyph>()
            .Where(element =>
                Math.Abs(element.Width - size) < 0.01d &&
                string.Equals(element.Kind.ToString(), kindName,
                    StringComparison.Ordinal))
            .ToArray();
        Assert.IsNotEmpty(
            matches,
            $"Expected a {size}px {kindName} status glyph in {root.GetType().Name}.");
    }

    private static FrameworkElement KindRoot(
        DependencyObject root,
        string kindName) => Descendants(root)
            .OfType<FrameworkElement>()
            .Single(element => Equals(element.Tag, $"GlyphKind:{kindName}"));

    private static T Tagged<T>(DependencyObject root, string tag)
        where T : FrameworkElement => Descendants(root)
            .OfType<T>()
            .Single(element => Equals(element.Tag, tag));

    private static Microsoft.UI.Composition.Visual OrbitVisual(
        FrameworkElement glyph) =>
        ElementCompositionPreview.GetElementVisual(
            Tagged<FrameworkElement>(glyph, "PrecisionOrbitRotationTarget"));

    private static object Resource(string key) =>
        Application.Current.Resources[key];

    private static Rect ShapeBounds(Shape shape) => shape switch
    {
        XamlPath path => path.Data.Bounds,
        Ellipse ellipse => new Rect(
            Canvas.GetLeft(ellipse),
            Canvas.GetTop(ellipse),
            ellipse.Width,
            ellipse.Height),
        _ => throw new AssertFailedException(
            $"Unsupported semantic vector shape: {shape.GetType().Name}")
    };

    private static Rect Union(Rect left, Rect right)
    {
        double x = Math.Min(left.Left, right.Left);
        double y = Math.Min(left.Top, right.Top);
        double rightEdge = Math.Max(left.Right, right.Right);
        double bottomEdge = Math.Max(left.Bottom, right.Bottom);
        return new Rect(x, y, rightEdge - x, bottomEdge - y);
    }

    private static double CenterX(Rect bounds) =>
        bounds.Left + (bounds.Width / 2d);

    private static double CenterY(Rect bounds) =>
        bounds.Top + (bounds.Height / 2d);

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
