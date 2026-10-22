using System.Globalization;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using Delivergate.Desktop.Models;
using Delivergate.Desktop.Services;

namespace Delivergate.Desktop.ViewModels;

public sealed partial class InventoryViewModel
{
    private int? _editingId;
    public bool IsEditing { get; private set; }
    public bool RequiresRefresh { get; private set; }
    public string EditorTitle => _editingId is null ? "Create item" : $"Edit item #{_editingId}";
    public string EditName { get; set; } = "";
    public string EditDescription { get; set; } = "";
    public int EditCategoryId { get; set; }
    public string EditPrice { get; set; } = "";
    public string EditQuantity { get; set; } = "";
    public IReadOnlyList<InventoryCategory> EditorCategories => Categories.Where(c => c.Id > 0).ToArray();
    private RolePermissions Permissions => RolePermissions.ForRole(_api.Session.IsSignedIn ? _api.Session.User?.Role : null);
    public bool CanCreate => CanFilter && Permissions.CanCreateInventory && EditorCategories.Count > 0;
    public bool CanEdit => CanFilter && Permissions.CanEditInventory && SelectedItem is not null;
    public bool CanDelete => CanFilter && Permissions.CanDeleteInventory && SelectedItem is not null;
    public bool CanSave => IsEditing && !IsBusy && !RequiresRefresh &&
        (_editingId is null ? Permissions.CanCreateInventory : Permissions.CanEditInventory);
    public bool CanCancelEdit => IsEditing && !IsBusy;

    public void BeginCreate()
    {
        if (!CanCreate) return;
        ResetEditor();
        IsEditing = true;
        EditCategoryId = EditorCategories[0].Id;
        EditQuantity = "0";
        Message = "Enter item details. Price must be positive and quantity cannot be negative.";
        NotifyAll();
    }

    public void BeginEdit()
    {
        if (!CanEdit || SelectedItem is not { } item) return;
        _editingId = item.Id;
        EditName = item.Name;
        EditDescription = item.Description;
        EditCategoryId = item.CategoryId;
        EditPrice = item.Price.ToString(CultureInfo.InvariantCulture);
        EditQuantity = item.Quantity.ToString(CultureInfo.InvariantCulture);
        IsEditing = true;
        Message = "Edit the fields and choose Save, or Cancel to discard the draft.";
        NotifyAll();
    }

    public void CancelEdit()
    {
        if (!CanCancelEdit) return;
        ResetEditor();
        Message = "Changes cancelled. No request was sent.";
        NotifyAll();
    }

    private void ResetEditor()
    {
        IsEditing = false;
        _editingId = null;
        EditName = EditDescription = EditPrice = EditQuantity = "";
        EditCategoryId = 0;
    }

    public async Task SaveAsync()
    {
        if (!CanSave) return;
        string name = EditName.Trim();
        string description = EditDescription.Trim();
        string? error = name.Length is 0 or > 150 ? "Enter an item name (1–150 characters)."
            : description.Length > 500 ? "Description must be 500 characters or fewer."
            : !EditorCategories.Any(c => c.Id == EditCategoryId) ? "Choose a valid category."
            : null;
        bool validPrice = decimal.TryParse(EditPrice.Trim(), NumberStyles.AllowDecimalPoint,
            CultureInfo.InvariantCulture, out decimal price) && price > 0;
        bool validQuantity = int.TryParse(EditQuantity.Trim(), NumberStyles.None,
            CultureInfo.InvariantCulture, out int quantity);
        error ??= !validPrice ? "Enter a positive price using a dot for decimals (example: 125.50)."
            : !validQuantity ? "Quantity must be a whole number from 0 to 2147483647." : null;
        if (error is not null) { Message = error; NotifyAll(); return; }

        int? id = _editingId;
        var input = new ItemInput(name, description, EditCategoryId, price, quantity);
        await MutateAsync(async cancellation =>
        {
            InventoryItem saved = await _api.SaveItemAsync(id, input, cancellation);
            return () =>
            {
                _allItems = _allItems.Where(item => item.Id != saved.Id).Append(saved).OrderByDescending(item => item.Id).ToArray();
                ResetEditor();
                ClearFilters();
                SelectedItem = saved;
                Message = id is null ? "Item created successfully." : "Item updated successfully.";
            };
        });
    }

    public async Task DeleteAsync(int id, bool confirmed)
    {
        if (!confirmed || !CanDelete || SelectedItem?.Id != id) return;
        await MutateAsync(async cancellation =>
        {
            await _api.DeleteItemAsync(id, cancellation);
            return () =>
            {
                _allItems = _allItems.Where(item => item.Id != id).ToArray();
                SelectedItem = null;
                ApplyFilters();
                Message = "Item deleted successfully.";
            };
        });
    }

    private async Task MutateAsync(Func<CancellationToken, Task<Action>> operation)
    {
        int revision = _api.Session.Revision;
        using var cancellation = new CancellationTokenSource();
        _loadCancellation = cancellation;
        IsBusy = true;
        Message = "Saving changes...";
        NotifyAll();
        try
        {
            Action applyResult = await operation(cancellation.Token);
            if (revision == _api.Session.Revision) applyResult();
        }
        catch (Exception ex) when (ex is ApiException or HttpRequestException or OperationCanceledException
                                   or JsonException or InvalidDataException or IOException or NotSupportedException)
        {
            if (revision != _api.Session.Revision) return;
            bool uncertain = ex is not ApiException apiError || (int)apiError.StatusCode >= 500;
            if (uncertain)
            {
                // A timed-out write may still have reached the server. Never retry automatically.
                RequiresRefresh = true;
                IsEditing = false;
                Message = "The result could not be confirmed. The server may have saved the change. Refresh and check the item before trying again.";
            }
            else Message = ex.Message;
        }
        finally
        {
            if (ReferenceEquals(_loadCancellation, cancellation)) _loadCancellation = null;
            if (revision == _api.Session.Revision) { IsBusy = false; NotifyAll(); }
        }
    }
}
