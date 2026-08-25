#if HARDWARE_INSPECTION_FIXTURE_GALLERY
using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections.Generic;
using System.Linq;

namespace GraniteEdgeAI.Features.HardwareInspection.DebugFixtures;

public sealed partial class HardwareInspectionFixtureGalleryPage : Page
{
    private readonly Func<bool>? closeRequested;

    public HardwareInspectionFixtureGalleryPage()
        : this(closeRequested: null)
    {
    }

    internal HardwareInspectionFixtureGalleryPage(Func<bool>? closeRequested)
    {
        this.closeRequested = closeRequested;
        InitializeComponent();
        Scenarios = HardwareInspectionFixtureCatalogue.Create();
        PreviewPage = new HardwareInspectionPage();
        FixturePreview.Content = PreviewPage;
        ScenarioList.ItemsSource = Scenarios;
        ScenarioList.SelectedIndex = 0;
    }

    public IReadOnlyList<HardwareInspectionFixtureScenario> Scenarios { get; }

    internal HardwareInspectionFixtureScenario SelectedScenario { get; private set; } = null!;

    internal HardwareInspectionPage PreviewPage { get; }

    internal void SelectScenarioForTesting(string id)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        ScenarioList.SelectedItem = Scenarios.Single(item =>
            string.Equals(item.Id, id, StringComparison.Ordinal));
    }

    internal bool CloseForTesting() => closeRequested?.Invoke() ?? false;

    private void ScenarioList_SelectionChanged(
        object sender,
        SelectionChangedEventArgs eventArguments)
    {
        if (ScenarioList.SelectedItem is not HardwareInspectionFixtureScenario scenario)
        {
            return;
        }

        SelectedScenario = scenario;
        PreviewPage.Apply(
            scenario.Presentation,
            scenario.Summary,
            scenario.Details,
            preserveDisclosureState: false);
    }

    private void CloseFixtureGalleryButton_Click(
        object sender,
        Microsoft.UI.Xaml.RoutedEventArgs eventArguments) =>
        _ = CloseForTesting();
}
#endif
