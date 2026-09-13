using System;
using System.Collections.Generic;
using GraniteEdgeAI.Features.ModelHardwareCompatibility.Presentation;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace GraniteEdgeAI.Features.ModelHardwareCompatibility.DebugFixtures;

/// <summary>
/// Lists every screen state and renders the selected one.
///
/// The preview is the real page, not a mock of it, so what a reviewer sees is
/// what a user would see. Only the source of the snapshot differs: a fixture
/// instead of an engine run.
/// </summary>
internal sealed partial class CompatibilityFixtureGalleryPage : Page
{
    private readonly List<CompatibilityFixture> _fixtures = [];
    private CompatibilityPage? _preview;

    public CompatibilityFixtureGalleryPage()
    {
        InitializeComponent();

#if DEBUG
        _fixtures.AddRange(CompatibilityFixtureCatalogue.All);
#endif

        foreach (CompatibilityFixture fixture in _fixtures)
        {
            FixtureList.Items.Add($"{fixture.Id}  ·  {fixture.Title}");
        }

        if (_fixtures.Count > 0)
        {
            FixtureList.SelectedIndex = 0;
        }
    }

    private void OnFixtureSelected(object sender, SelectionChangedEventArgs args)
    {
        int index = FixtureList.SelectedIndex;

        if (index < 0 || index >= _fixtures.Count)
        {
            return;
        }

        // the page is created once and re-shown, mirroring how it behaves in the
        // real flow: a snapshot is applied to a tree that already exists.
        if (_preview is null)
        {
            _preview = new CompatibilityPage { StartAutomatically = false };
            PreviewHost.Content = _preview;
        }

        _preview.ViewModel.ShowFixture(_fixtures[index].Presentation);
    }

    private void GalleryRoot_SizeChanged(object sender, SizeChangedEventArgs args)
    {
        if (args.NewSize.Width < 760d)
        {
            Sidebar.Visibility = Visibility.Collapsed;
            SidebarColumn.Width = new GridLength(0);
            Grid.SetColumn(PreviewHost, 0);
            Grid.SetColumnSpan(PreviewHost, 2);
            return;
        }

        Sidebar.Visibility = Visibility.Visible;
        SidebarColumn.Width = new GridLength(
            args.NewSize.Width < 1100d ? 220d : 300d);
        Grid.SetColumn(PreviewHost, 1);
        Grid.SetColumnSpan(PreviewHost, 1);
    }
}
