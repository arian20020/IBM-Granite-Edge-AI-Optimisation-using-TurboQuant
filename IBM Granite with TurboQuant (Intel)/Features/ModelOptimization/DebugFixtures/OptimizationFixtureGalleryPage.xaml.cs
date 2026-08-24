using GraniteEdgeAI.Features.ModelOptimization.Presentation;
using Microsoft.UI.Xaml.Controls;

namespace GraniteEdgeAI.Features.ModelOptimization.DebugFixtures;

public sealed partial class OptimizationFixtureGalleryPage : Page
{
    public OptimizationFixtureGalleryPage()
    {
        InitializeComponent();
        foreach (OptimizationFixture fixture in OptimizationFixtureCatalog.All)
        {
            FixtureList.Items.Add(new ListViewItem { Content = fixture.Title, Tag = fixture });
        }

        FixtureList.SelectedIndex = 0;
    }

    internal int FixtureCount => FixtureList.Items.Count;

    internal OptimizationPresentationState? PreviewPresentation => PreviewPage.Presentation;

    private void OnFixtureSelectionChanged(object sender, SelectionChangedEventArgs args)
    {
        if (FixtureList.SelectedItem is ListViewItem { Tag: OptimizationFixture fixture })
        {
            PreviewPage.ApplyPresentation(fixture.Presentation);
        }
    }
}
