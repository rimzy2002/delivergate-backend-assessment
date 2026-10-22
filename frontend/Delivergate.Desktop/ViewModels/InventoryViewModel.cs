using System.ComponentModel;
using System.IO;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Text.Json;
using Delivergate.Desktop.Models;
using Delivergate.Desktop.Services;

namespace Delivergate.Desktop.ViewModels;

public sealed class InventoryViewModel : INotifyPropertyChanged
{
    private readonly ApiClient _api;
    private IReadOnlyList<InventoryItem> _allItems = [];
    private string _searchText = "";
    private int _selectedCategoryId;
    private int _revision;
    private CancellationTokenSource? _loadCancellation;
    private InventoryItem? _selectedItem;

    public InventoryViewModel(ApiClient api)
    {
        _api = api;
        _revision = api.Session.Revision;
        api.Session.Changed += SessionChanged;
    }

    public IReadOnlyList<InventoryItem> Items { get; private set; } = [];
    public IReadOnlyList<InventoryCategory> Categories { get; private set; } = [];
    public bool IsBusy { get; private set; }
    public bool HasLoaded { get; private set; }
    public string Message { get; private set; } = "Open Inventory to load items.";
    public bool CanLoad => !IsBusy && _api.Session.IsSignedIn &&
        RolePermissions.ForRole(_api.Session.User?.Role).CanViewInventory;
    public bool CanFilter => CanLoad && HasLoaded;
    public string ResultSummary => HasLoaded ? $"Showing {Items.Count} of {_allItems.Count} items" : "No data loaded";

    public string SearchText
    {
        get => _searchText;
        set { _searchText = value ?? ""; ApplyFilters(); }
    }
    public int SelectedCategoryId
    {
        get => _selectedCategoryId;
        set { _selectedCategoryId = value; ApplyFilters(); }
    }
    public InventoryItem? SelectedItem
    {
        get => _selectedItem;
        set { _selectedItem = value; Notify(); }
    }

    public void ClearFilters()
    {
        _searchText = "";
        _selectedCategoryId = 0;
        ApplyFilters();
    }

    public async Task LoadAsync()
    {
        if (!CanLoad) return;
        int revision = _api.Session.Revision;
        using var cancellation = new CancellationTokenSource();
        _loadCancellation = cancellation;
        IsBusy = true;
        HasLoaded = false;
        _allItems = [];
        Items = [];
        SelectedItem = null;
        Message = "Loading inventory...";
        NotifyAll();
        try
        {
            // Commit both results together: do not show a partial or stale load.
            var categories = await _api.GetCategoriesAsync(cancellation.Token);
            cancellation.Token.ThrowIfCancellationRequested();
            if (revision != _api.Session.Revision) return;
            var items = await _api.GetItemsAsync(cancellation.Token);
            if (revision != _api.Session.Revision) return;
            Categories = new[] { new InventoryCategory { Id = 0, Name = "All categories" } }
                .Concat(categories.OrderBy(category => category.Name)).ToArray();
            if (!Categories.Any(category => category.Id == _selectedCategoryId)) _selectedCategoryId = 0;
            _allItems = items;
            HasLoaded = true;
            ApplyFilters();
        }
        catch (Exception ex) when (ex is ApiException or HttpRequestException or OperationCanceledException
                                       or JsonException or InvalidDataException or IOException or NotSupportedException)
        {
            if (revision != _api.Session.Revision || cancellation.IsCancellationRequested) return;
            Message = ex switch
            {
                ApiException => ex.Message,
                HttpRequestException => "Cannot reach the backend. Check the server, then click Refresh.",
                OperationCanceledException => "The request timed out. Click Refresh to try again.",
                JsonException => "The server returned invalid inventory JSON. Try Refresh or contact the administrator.",
                InvalidDataException => ex.Message,
                _ => "Inventory could not be loaded. Click Refresh to try again."
            };
        }
        finally
        {
            if (ReferenceEquals(_loadCancellation, cancellation)) _loadCancellation = null;
            if (revision == _api.Session.Revision) { IsBusy = false; NotifyAll(); }
        }
    }

    private void ApplyFilters()
    {
        string search = SearchText.Trim();
        Items = _allItems.Where(item =>
            (search.Length == 0 || item.Name.Contains(search, StringComparison.OrdinalIgnoreCase)) &&
            (SelectedCategoryId == 0 || item.CategoryId == SelectedCategoryId)).ToArray();
        if (SelectedItem is not null && !Items.Contains(SelectedItem)) SelectedItem = null;
        if (HasLoaded) Message = _allItems.Count == 0 ? "No inventory items have been added yet."
            : Items.Count == 0 ? "No items match your search and category. Try Clear filters."
            : "Inventory loaded. Select a row to read its description.";
        NotifyAll();
    }

    private void SessionChanged()
    {
        if (_revision != _api.Session.Revision)
        {
            _revision = _api.Session.Revision;
            _loadCancellation?.Cancel();
            _loadCancellation = null;
            IsBusy = false;
            HasLoaded = false;
            _allItems = [];
            Items = [];
            Categories = [];
            _selectedItem = null;
            _searchText = "";
            _selectedCategoryId = 0;
            Message = "Open Inventory to load items.";
        }
        NotifyAll();
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void Notify([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    private void NotifyAll() => Notify(null);
}
