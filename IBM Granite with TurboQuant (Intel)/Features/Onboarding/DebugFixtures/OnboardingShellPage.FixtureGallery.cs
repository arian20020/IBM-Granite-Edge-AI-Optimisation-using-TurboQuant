#if MODEL_INSPECTION_FIXTURE_GALLERY
using GraniteEdgeAI.Features.ModelInspection;
using GraniteEdgeAI.Features.ModelInspection.DebugFixtures.Gallery;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Navigation;
using System;

namespace GraniteEdgeAI.Features.Onboarding;

public sealed partial class OnboardingShellPage
{
    partial void InitializeFixtureGalleryEntry()
    {
        if (Content is not Grid root)
        {
            throw new InvalidOperationException(
                "The onboarding shell requires its root layout grid.");
        }

        root.RowDefinitions.Add(new RowDefinition
        {
            Height = GridLength.Auto
        });
        Grid.SetRow(StageIndicator, 2);

        var button = new Button
        {
            Name = "FixtureGalleryButton",
            Content = "Fixture gallery",
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Center,
            MinWidth = 120,
            MinHeight = 44,
            Margin = new Thickness(24, 8, 24, 8)
        };
        AutomationProperties.SetName(button, "Fixture gallery");
        Grid.SetRow(button, 1);
        root.Children.Add(button);
        button.Click += FixtureGalleryButton_Click;
    }

    private void FixtureGalleryButton_Click(
        object sender,
        RoutedEventArgs eventArguments) => NavigateToFixtureGallery();

    private bool NavigateToFixtureGallery()
    {
        object activation = ModelInspectionFixtureGalleryPage.CreateActivation(
            NavigateToFreshModelImport);
        return NavigateToFixtureGallery(
            activation,
            frame => frame.Navigate(
                typeof(ModelInspectionFixtureGalleryPage),
                activation));
    }

    internal bool NavigateToFixtureGalleryForTesting(
        object expectedActivation,
        Func<Frame, bool> navigator)
    {
        ArgumentNullException.ThrowIfNull(expectedActivation);
        ArgumentNullException.ThrowIfNull(navigator);
        return NavigateToFixtureGallery(expectedActivation, navigator);
    }

    internal bool NavigateToFixtureGalleryForTesting(
        Func<Frame, bool> navigator)
    {
        ArgumentNullException.ThrowIfNull(navigator);
        object? previousContent = StageFrame.Content;
        object? activation = null;
        NavigatingCancelEventHandler guard = (_, eventArguments) =>
        {
            if (eventArguments.SourcePageType !=
                    typeof(ModelInspectionFixtureGalleryPage) ||
                eventArguments.Parameter is null)
            {
                eventArguments.Cancel = true;
                return;
            }

            activation = eventArguments.Parameter;
        };
        bool navigationSucceeded;
        StageFrame.Navigating += guard;
        try
        {
            navigationSucceeded = navigator(StageFrame);
        }
        finally
        {
            StageFrame.Navigating -= guard;
        }

        if (!navigationSucceeded ||
            activation is null ||
            ReferenceEquals(StageFrame.Content, previousContent) ||
            StageFrame.Content is not ModelInspectionFixtureGalleryPage gallery ||
            !gallery.IsActivatedWith(activation))
        {
            return false;
        }

        return CompleteFixtureGalleryNavigation(
            previousContent,
            gallery,
            activation);
    }

    private bool NavigateToFixtureGallery(
        object expectedActivation,
        Func<Frame, bool> navigator)
    {
        object? previousContent = StageFrame.Content;
        bool exactNavigationObserved = false;
        NavigatingCancelEventHandler guard = (_, eventArguments) =>
        {
            if (eventArguments.SourcePageType !=
                    typeof(ModelInspectionFixtureGalleryPage) ||
                !ReferenceEquals(
                    eventArguments.Parameter,
                    expectedActivation))
            {
                eventArguments.Cancel = true;
                return;
            }

            exactNavigationObserved = true;
        };
        bool navigationSucceeded;
        StageFrame.Navigating += guard;
        try
        {
            navigationSucceeded = navigator(StageFrame);
        }
        finally
        {
            StageFrame.Navigating -= guard;
        }

        if (!navigationSucceeded ||
            !exactNavigationObserved ||
            ReferenceEquals(StageFrame.Content, previousContent) ||
            StageFrame.Content is not ModelInspectionFixtureGalleryPage gallery ||
            !gallery.IsActivatedWith(expectedActivation))
        {
            return false;
        }

        return CompleteFixtureGalleryNavigation(
            previousContent,
            gallery,
            expectedActivation);
    }

    private bool CompleteFixtureGalleryNavigation(
        object? previousContent,
        ModelInspectionFixtureGalleryPage gallery,
        object expectedActivation)
    {
        if (!gallery.IsActivatedWith(expectedActivation))
        {
            return false;
        }

        DetachModelImportPage();
        DetachModelInspectionPage();
        if (previousContent is ModelInspectionPage inspection)
        {
            _ = inspection.RetireForFixture();
        }

        StageFrame.BackStack.Clear();
        StageFrame.ForwardStack.Clear();
        return true;
    }
}
#endif
