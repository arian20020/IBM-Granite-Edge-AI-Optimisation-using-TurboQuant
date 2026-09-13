using System.Xml.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace GraniteEdgeAI.OpenVino.Tests;

[TestClass]
public sealed class CompatibilityXamlGeometryContractTests
{
    private static readonly string?[] ExpectedMemoryAnnotationRowHeights =
        ["Auto", "12", "12", "12", "Auto"];

    private static readonly XNamespace Presentation =
        "http://schemas.microsoft.com/winfx/2006/xaml/presentation";
    private static readonly XNamespace Xaml =
        "http://schemas.microsoft.com/winfx/2006/xaml";

    [TestMethod]
    public void DisclosureCannotChangeTheCompatibilityBayWidthContract()
    {
        XDocument document = XDocument.Load(Path.Combine(
            FindRepositoryRoot(), "IBM Granite with TurboQuant (Intel)",
            "Features", "ModelHardwareCompatibility", "CompatibilityPage.xaml"));

        XElement bay = Named(document, "CompatibilityBay");
        Assert.IsNull(bay.Attribute("Width"));
        Assert.AreEqual("960", (string?)bay.Attribute("MaxWidth"));
        Assert.AreEqual("Stretch", (string?)bay.Attribute("HorizontalAlignment"));

        XElement disclosure = Named(document, "CompatibilityCalculationExpander");
        XElement? disclosureContent = disclosure.Element(Presentation + "Grid");
        Assert.IsNotNull(disclosureContent);
        Assert.AreEqual("Stretch",
            (string?)disclosureContent.Attribute("HorizontalAlignment"));
        Assert.IsNull(disclosure.Attribute("Width"));
        Assert.IsNull(disclosureContent.Attribute("Width"));

        XElement? scrollViewer =
            document.Descendants(Presentation + "ScrollViewer").Single();
        Assert.IsNotNull(scrollViewer);
        Assert.AreEqual("Stretch",
            (string?)scrollViewer.Attribute("HorizontalContentAlignment"));
        XElement centeringHost = Named(document, "CompatibilityViewportCenteringHost");
        Assert.AreSame(scrollViewer, centeringHost.Parent);
        Assert.AreEqual("Stretch",
            (string?)centeringHost.Attribute("HorizontalAlignment"));
        Assert.AreSame(centeringHost, bay.Parent);
        Assert.AreEqual("Disabled",
            (string?)scrollViewer.Attribute("HorizontalScrollMode"));
        Assert.AreEqual("Disabled",
            (string?)scrollViewer.Attribute("HorizontalScrollBarVisibility"));
    }

    [TestMethod]
    public void MemoryThresholdAnnotationsUseFiveIndependentBandsAndNativeDisclosureId()
    {
        XDocument document = XDocument.Load(Path.Combine(
            FindRepositoryRoot(), "IBM Granite with TurboQuant (Intel)",
            "Features", "ModelHardwareCompatibility", "CompatibilityPage.xaml"));

        XElement track = Named(document, "CompatibilityBudgetTrack");
        XElement? annotationGrid = track.Parent;
        Assert.IsNotNull(annotationGrid);
        XElement[] rows = annotationGrid
            .Element(Presentation + "Grid.RowDefinitions")!
            .Elements(Presentation + "RowDefinition")
            .ToArray();
        CollectionAssert.AreEqual(
            ExpectedMemoryAnnotationRowHeights,
            rows.Select(row => (string?)row.Attribute("Height")).ToArray());
        Assert.AreEqual("2", (string?)track.Attribute("Grid.Row"));

        XElement available = Named(document, "CompatibilityAvailableRamMarker");
        XElement minimum = Named(document, "CompatibilityMinimumRamMarker");
        Assert.AreEqual("8", (string?)available.Attribute("Height"));
        Assert.AreEqual("Bottom", (string?)available.Attribute("VerticalAlignment"));
        Assert.IsNull(available.Attribute("StrokeDashArray"));
        Assert.AreEqual("8", (string?)minimum.Attribute("Height"));
        Assert.AreEqual("Top", (string?)minimum.Attribute("VerticalAlignment"));
        Assert.IsNull(minimum.Attribute("StrokeDashArray"));
        Assert.IsNull(Named(document, "CompatibilityAvailableRamLabel").Attribute("Grid.RowSpan"));
        Assert.IsNull(Named(document, "CompatibilityMinimumRamLabel").Attribute("Grid.RowSpan"));

        XElement toggle = document.Descendants(Presentation + "ToggleButton")
            .Single(element => string.Equals(
                (string?)element.Attribute(Xaml + "Name"),
                "ExpanderHeader",
                StringComparison.Ordinal));
        Assert.AreEqual(
            "ExpanderToggleButton",
            (string?)toggle.Attribute(
                XName.Get("AutomationProperties.AutomationId")));
    }

    private static XElement Named(XDocument document, string name)
    {
        XElement? element = document.Descendants().SingleOrDefault(candidate =>
            string.Equals((string?)candidate.Attribute(Xaml + "Name"), name,
                StringComparison.Ordinal));
        Assert.IsNotNull(element);
        return element;
    }

    private static string FindRepositoryRoot()
    {
        DirectoryInfo? current = new(AppContext.BaseDirectory);
        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, "global.json")) &&
                Directory.Exists(Path.Combine(current.FullName,
                    "IBM Granite with TurboQuant (Intel)")))
            {
                return current.FullName;
            }
            current = current.Parent;
        }
        throw new DirectoryNotFoundException("Repository root was not found.");
    }
}
