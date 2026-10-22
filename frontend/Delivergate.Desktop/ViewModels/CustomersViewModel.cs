using System.ComponentModel;
using System.IO;
using System.Net.Http;
using System.Net.Mail;
using System.Text.Json;
using Delivergate.Desktop.Models;
using Delivergate.Desktop.Services;

namespace Delivergate.Desktop.ViewModels;

public sealed class CustomersViewModel : INotifyPropertyChanged
{
    private readonly ApiClient _api;
    private IReadOnlyList<Customer> _allCustomers = [];
    private CancellationTokenSource? _cancellation;
    private int _revision;
    private int? _editingId;
    private string _searchText = "";
    private Customer? _selectedCustomer;

    public CustomersViewModel(ApiClient api)
    {
        _api = api;
        _revision = api.Session.Revision;
        api.Session.Changed += SessionChanged;
    }

    public IReadOnlyList<Customer> Customers { get; private set; } = [];
    public bool IsBusy { get; private set; }
    public bool IsEditing { get; private set; }
    public bool HasLoaded { get; private set; }
    public bool RequiresRefresh { get; private set; }
    public string Message { get; private set; } = "Open Customers to load records.";
    public string ResultSummary => HasLoaded ? $"Showing {Customers.Count} of {_allCustomers.Count} customers" : "No data loaded";
    public string EditorTitle => _editingId is null ? "Create customer" : $"Edit customer #{_editingId}";
    public string EditName { get; set; } = "";
    public string EditEmail { get; set; } = "";
    public string EditPhone { get; set; } = "";
    public string EditAddress { get; set; } = "";
    private RolePermissions Permissions => RolePermissions.ForRole(_api.Session.IsSignedIn ? _api.Session.User?.Role : null);
    public bool CanLoad => !IsBusy && !IsEditing && Permissions.CanViewCustomers;
    public bool CanBrowse => CanLoad && HasLoaded && !RequiresRefresh;
    public bool CanCreate => CanBrowse && Permissions.CanCreateCustomers;
    public bool CanEdit => CanBrowse && Permissions.CanEditCustomers && SelectedCustomer is not null;
    public bool CanDelete => CanBrowse && Permissions.CanDeleteCustomers && SelectedCustomer is not null;
    public bool CanSave => IsEditing && !IsBusy && !RequiresRefresh &&
        (_editingId is null ? Permissions.CanCreateCustomers : Permissions.CanEditCustomers);
    public bool CanCancelEdit => IsEditing && !IsBusy;

    public string SearchText
    {
        get => _searchText;
        set { _searchText = value ?? ""; ApplyFilter(); }
    }
    public Customer? SelectedCustomer
    {
        get => _selectedCustomer;
        set { _selectedCustomer = value; NotifyAll(); }
    }

    public async Task LoadAsync()
    {
        if (!CanLoad) return;
        HasLoaded = false;
        _allCustomers = [];
        Customers = [];
        SelectedCustomer = null;
        await RunAsync(async cancellation =>
        {
            var loaded = await _api.GetCustomersAsync(cancellation);
            return () =>
            {
                _allCustomers = loaded;
                HasLoaded = true;
                RequiresRefresh = false;
                ApplyFilter();
            };
        }, mutation: false);
    }

    public void ClearSearch() => SearchText = "";

    private void ApplyFilter()
    {
        string query = SearchText.Trim();
        Customers = _allCustomers.Where(customer => query.Length == 0 ||
            customer.Name.Contains(query, StringComparison.OrdinalIgnoreCase) ||
            customer.Email.Contains(query, StringComparison.OrdinalIgnoreCase) ||
            customer.Phone.Contains(query, StringComparison.OrdinalIgnoreCase)).ToArray();
        if (SelectedCustomer is not null && !Customers.Contains(SelectedCustomer)) _selectedCustomer = null;
        if (HasLoaded) Message = _allCustomers.Count == 0 ? "No customers have been added yet."
            : Customers.Count == 0 ? "No customers match your search. Try Clear search."
            : "Customers loaded. Select a row to view its address or edit the record.";
        NotifyAll();
    }

    public void BeginCreate()
    {
        if (!CanCreate) return;
        ResetEditor();
        IsEditing = true;
        Message = "Enter name, email, and phone. Address is optional.";
        NotifyAll();
    }

    public void BeginEdit()
    {
        if (!CanEdit || SelectedCustomer is not { } customer) return;
        _editingId = customer.Id;
        EditName = customer.Name;
        EditEmail = customer.Email;
        EditPhone = customer.Phone;
        EditAddress = customer.Address;
        IsEditing = true;
        Message = "Edit the customer details, then Save or Cancel.";
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
        EditName = EditEmail = EditPhone = EditAddress = "";
    }

    public async Task SaveAsync()
    {
        if (!CanSave) return;
        string name = EditName.Trim();
        string email = EditEmail.Trim().ToLowerInvariant();
        string phone = EditPhone.Trim();
        string address = EditAddress.Trim();
        string? error = name.Length is 0 or > 150 ? "Enter a customer name (1–150 characters)."
            : email.Length > 150 || !MailAddress.TryCreate(email, out var parsed) || parsed.Address != email
                ? "Enter a valid email address (up to 150 characters)."
            : phone.Length is 0 or > 30 ? "Enter a phone number (1–30 characters)."
            : address.Length > 300 ? "Address must be 300 characters or fewer." : null;
        if (error is not null) { Message = error; NotifyAll(); return; }
        int? id = _editingId;
        var input = new CustomerInput(name, email, phone, address);
        await RunAsync(async cancellation =>
        {
            Customer saved = await _api.SaveCustomerAsync(id, input, cancellation);
            return () =>
            {
                _allCustomers = _allCustomers.Where(customer => customer.Id != saved.Id).Append(saved).OrderByDescending(customer => customer.Id).ToArray();
                ResetEditor();
                ClearSearch();
                SelectedCustomer = saved;
                Message = id is null ? "Customer created successfully." : "Customer updated successfully.";
            };
        }, mutation: true);
    }

    public async Task DeleteAsync(int id, bool confirmed)
    {
        if (!confirmed || !CanDelete || SelectedCustomer?.Id != id) return;
        await RunAsync(async cancellation =>
        {
            await _api.DeleteCustomerAsync(id, cancellation);
            return () =>
            {
                _allCustomers = _allCustomers.Where(customer => customer.Id != id).ToArray();
                _selectedCustomer = null;
                ApplyFilter();
                Message = "Customer deleted successfully.";
            };
        }, mutation: true);
    }

    private async Task RunAsync(Func<CancellationToken, Task<Action>> operation, bool mutation)
    {
        int revision = _api.Session.Revision;
        using var cancellation = new CancellationTokenSource();
        _cancellation = cancellation;
        IsBusy = true;
        Message = mutation ? "Saving changes..." : "Loading customers...";
        NotifyAll();
        try
        {
            Action applyResult = await operation(cancellation.Token);
            if (revision == _api.Session.Revision) applyResult();
        }
        catch (Exception ex) when (ex is ApiException or HttpRequestException or OperationCanceledException or
                                       JsonException or InvalidDataException or IOException or NotSupportedException)
        {
            if (revision != _api.Session.Revision) return;
            bool uncertainWrite = mutation && (ex is not ApiException apiError || (int)apiError.StatusCode >= 500);
            if (uncertainWrite)
            {
                RequiresRefresh = true;
                IsEditing = false;
                Message = "The result could not be confirmed. The server may have saved the change. Refresh and check before trying again.";
            }
            else Message = ex switch
            {
                ApiException => ex.Message,
                HttpRequestException => "Cannot reach the backend. Check the server, then click Refresh.",
                OperationCanceledException => "The request timed out. Click Refresh to try again.",
                JsonException => "The server returned invalid customer JSON. Try Refresh or contact the administrator.",
                InvalidDataException => ex.Message,
                _ => "Customers could not be loaded. Click Refresh to try again."
            };
        }
        finally
        {
            if (ReferenceEquals(_cancellation, cancellation)) _cancellation = null;
            if (revision == _api.Session.Revision) { IsBusy = false; NotifyAll(); }
        }
    }

    private void SessionChanged()
    {
        if (_revision != _api.Session.Revision)
        {
            _revision = _api.Session.Revision;
            _cancellation?.Cancel();
            _cancellation = null;
            IsBusy = HasLoaded = RequiresRefresh = false;
            _allCustomers = [];
            Customers = [];
            _selectedCustomer = null;
            _searchText = "";
            ResetEditor();
            Message = "Open Customers to load records.";
        }
        NotifyAll();
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void NotifyAll() => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(null));
}
