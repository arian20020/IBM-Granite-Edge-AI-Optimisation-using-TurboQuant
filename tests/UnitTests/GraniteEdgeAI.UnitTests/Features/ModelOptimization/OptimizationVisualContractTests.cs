using GraniteEdgeAI.Features.ModelOptimization;
using GraniteEdgeAI.UnitTests.Features.ModelOptimization.Fixtures;
using System.Linq;
using Microsoft.UI.Xaml;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.VisualStudio.TestTools.UnitTesting.AppContainer;

namespace GraniteEdgeAI.UnitTests.Features.ModelOptimization;

[TestClass]
public sealed class OptimizationVisualContractTests
{
    [UITestMethod]
    public void GalleryCoversEveryFixtureOnTheModernLightCanvas()
    {
        OptimizationFixtureGalleryPage gallery = new();

        Assert.AreEqual(13, gallery.FixtureCount);
        Assert.IsNotNull(gallery.PreviewPresentation);
        Assert.AreEqual(ElementTheme.Light, gallery.RequestedTheme);
    }

    [UITestMethod]
    public void EveryFixtureRendersThroughTheCanonicalPage()
    {
        OptimizationPage page = new();

        foreach (OptimizationFixture fixture in OptimizationFixtureCatalog.All)
        {
            page.ApplyPresentation(fixture.Presentation);
            Assert.AreSame(fixture.Presentation, page.Presentation, fixture.Id);
        }

        Assert.AreEqual(13, OptimizationFixtureCatalog.All.Select(item => item.Id).Distinct().Count());
    }
}
