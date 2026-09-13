#if MODEL_INSPECTION_FIXTURE_GALLERY
using GraniteEdgeAI.ModelInspection.Fixtures;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace GraniteEdgeAI.Features.ModelInspection.DebugFixtures.Gallery;

internal sealed class ModelInspectionFixtureGalleryViewModel :
    INotifyPropertyChanged
{
    private IReadOnlyList<ModelInspectionFixtureListItem> items =
        Array.Empty<ModelInspectionFixtureListItem>();
    private IReadOnlyList<ModelInspectionFixtureListItem> filteredItems =
        Array.Empty<ModelInspectionFixtureListItem>();
    private string searchText = string.Empty;
    private ModelInspectionFixtureCategory? selectedCategory;
    private ModelInspectionFixtureListItem? selectedItem;
    private string validationStatus = "Loading fixture catalogue…";

    public event PropertyChangedEventHandler? PropertyChanged;

    internal IReadOnlyList<ModelInspectionFixtureListItem> Items
    {
        get => items;
        private set
        {
            items = value;
            Notify();
        }
    }

    internal IReadOnlyList<ModelInspectionFixtureListItem> FilteredItems
    {
        get => filteredItems;
        private set
        {
            filteredItems = value;
            Notify();
        }
    }

    internal string SearchText
    {
        get => searchText;
        set
        {
            string normalized = value ?? string.Empty;
            if (string.Equals(searchText, normalized, StringComparison.Ordinal))
            {
                return;
            }

            searchText = normalized;
            Notify();
            ApplyFilter();
        }
    }

    internal ModelInspectionFixtureCategory? SelectedCategory
    {
        get => selectedCategory;
        set
        {
            if (selectedCategory == value)
            {
                return;
            }

            selectedCategory = value;
            Notify();
            ApplyFilter();
        }
    }

    internal ModelInspectionFixtureListItem? SelectedItem
    {
        get => selectedItem;
        set
        {
            if (ReferenceEquals(selectedItem, value))
            {
                return;
            }

            selectedItem = value;
            Notify();
        }
    }

    internal string ValidationStatus
    {
        get => validationStatus;
        set
        {
            string safe = value ?? string.Empty;
            if (string.Equals(validationStatus, safe, StringComparison.Ordinal))
            {
                return;
            }

            validationStatus = safe;
            Notify();
        }
    }

    internal async Task LoadAsync(
        ModelInspectionFixturePackageLoader loader,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(loader);
        ValidatedModelInspectionFixtureCoverageCatalogue loaded;
        try
        {
            loaded = await loader.LoadAsync(cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (ModelInspectionFixtureGalleryLoadException error)
        {
            Items = Array.Empty<ModelInspectionFixtureListItem>();
            FilteredItems = Array.Empty<ModelInspectionFixtureListItem>();
            SelectedItem = null;
            ValidationStatus =
                $"Fixture catalogue unavailable: {error.Diagnostic}";
            throw;
        }
        catch
        {
            Items = Array.Empty<ModelInspectionFixtureListItem>();
            FilteredItems = Array.Empty<ModelInspectionFixtureListItem>();
            SelectedItem = null;
            ValidationStatus = "Fixture catalogue unavailable.";
            throw;
        }

        HashSet<string> n001Ids = loaded.Catalogue.Policy.Value
            .ExternalEvidenceLinks
            .Where(link => string.Equals(
                link.EvidenceId,
                "N-001",
                StringComparison.Ordinal))
            .Select(link => link.FixtureId)
            .ToHashSet(StringComparer.Ordinal);
        ModelInspectionFixtureListItem[] next = loaded.Catalogue.Fixtures
            .OrderBy(fixture => fixture.Id, StringComparer.Ordinal)
            .Select(fixture => new ModelInspectionFixtureListItem(
                fixture,
                n001Ids.Contains(fixture.Id)))
            .ToArray();
        Items = next;
        ApplyFilter();
        ValidationStatus =
            $"Catalogue validated: {next.Length} fixtures.";
    }

    internal void ClearSelection() => SelectedItem = null;

    private void ApplyFilter()
    {
        string query = SearchText.Trim();
        FilteredItems = Items.Where(item =>
                (SelectedCategory is null ||
                 item.Category == SelectedCategory) &&
                (query.Length == 0 ||
                 item.Id.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                 item.FileName.Contains(
                     query,
                     StringComparison.OrdinalIgnoreCase) ||
                 item.TargetCondition.Contains(
                     query,
                     StringComparison.OrdinalIgnoreCase)))
            .ToArray();
    }

    private void Notify([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(
            this,
            new PropertyChangedEventArgs(propertyName));
}
#endif
