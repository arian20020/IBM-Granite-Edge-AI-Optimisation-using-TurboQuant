using System.Collections.Generic;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;

namespace GraniteEdgeAI.Features.ModelHardwareCompatibility.Presentation;

/// <summary>
/// Finds theme-scoped resources from code.
///
/// A brush declared inside ThemeDictionaries is invisible to the ordinary
/// <c>Resources[key]</c> indexer: the indexer only searches a dictionary's own
/// entries and its merged dictionaries' entries, never the theme-scoped ones
/// nested within them. Markup does not hit this because {ThemeResource} resolves
/// through a different path, so the mistake compiles cleanly and only fails when
/// the page is constructed.
///
/// This walks the tree the indexer will not: the active theme's dictionary
/// first, then the top level for anything not theme-scoped.
/// </summary>
internal static class CompatibilityResources
{
    internal static Brush Brush(FrameworkElement element, string key) =>
        Find(element, key) as Brush
        ?? new SolidColorBrush(Microsoft.UI.Colors.Transparent);

    internal static T Value<T>(FrameworkElement element, string key, T fallback) =>
        Find(element, key) is T found ? found : fallback;

    private static object? Find(FrameworkElement element, string key)
    {
        string theme = ResolveTheme(element);

        // Walk up from the element: a page merges the dictionary, and a control
        // hosted inside one may rely on its parent's copy.
        for (DependencyObject? node = element; node is not null;
            node = node is FrameworkElement current ? current.Parent : null)
        {
            if (node is not FrameworkElement host)
            {
                continue;
            }

            if (Search(host.Resources, key, theme) is { } found)
            {
                return found;
            }
        }

        return Search(Application.Current?.Resources, key, theme);
    }

    private static string ResolveTheme(FrameworkElement element)
    {
        // Apply() runs in the page constructor, before ActualTheme has caught
        // up with the page's explicit RequestedTheme. Honour the nearest
        // explicit request first so code-created controls do not permanently
        // capture the host application's dark brushes during that interval.
        for (DependencyObject? node = element; node is not null;
            node = node is FrameworkElement current ? current.Parent : null)
        {
            if (node is FrameworkElement host
                && host.RequestedTheme != ElementTheme.Default)
            {
                return host.RequestedTheme == ElementTheme.Dark ? "Dark" : "Light";
            }
        }

        return element.ActualTheme == ElementTheme.Dark ? "Dark" : "Light";
    }

    private static object? Search(ResourceDictionary? dictionary, string key, string theme)
    {
        if (dictionary is null)
        {
            return null;
        }

        if (dictionary.ThemeDictionaries.TryGetValue(theme, out object? themed)
            && themed is ResourceDictionary themeDictionary
            && themeDictionary.TryGetValue(key, out object? themedValue))
        {
            return themedValue;
        }

        foreach (ResourceDictionary merged in dictionary.MergedDictionaries)
        {
            if (Search(merged, key, theme) is { } found)
            {
                return found;
            }
        }

        // Only ask the ordinary indexer after walking merged dictionaries.
        // The indexer also searches those dictionaries, but it resolves their
        // ThemeDictionaries against the host application's current theme. That
        // can still be Dark while this page has explicitly requested Light.
        if (dictionary.TryGetValue(key, out object? direct))
        {
            return direct;
        }

        return null;
    }
}
