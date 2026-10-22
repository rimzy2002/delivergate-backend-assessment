using System.ComponentModel;
using Delivergate.Desktop.Services;

namespace Delivergate.Desktop.ViewModels;

public enum WorkspacePage { Dashboard, Inventory, Customers }

public sealed record PermissionRow(string Action, bool IsAllowed)
{
    public string Status => IsAllowed ? "Allowed" : "Not allowed";
}

public sealed class WorkspaceViewModel : INotifyPropertyChanged
{
    private readonly UserSession _session;
    private int? _userId;
    private string? _role;

    public WorkspaceViewModel(UserSession session)
    {
        _session = session;
        _session.Changed += OnSessionChanged;
        OnSessionChanged();
    }

    public WorkspacePage? SelectedPage { get; private set; }
    public RolePermissions Permissions => RolePermissions.ForRole(
        _session.IsSignedIn ? _session.User?.Role : null);
    public bool CanOpenWorkspace => Permissions.CanViewInventory || Permissions.CanViewCustomers;
    public bool IsDashboard => CanOpenWorkspace && SelectedPage == WorkspacePage.Dashboard;
    public bool IsInventory => CanOpenWorkspace && SelectedPage == WorkspacePage.Inventory;
    public bool IsCustomers => CanOpenWorkspace && SelectedPage == WorkspacePage.Customers;
    public string UserName => _session.User?.Name ?? "";
    public string Role => _session.User?.Role ?? "";
    public string PageTitle => SelectedPage switch
    {
        WorkspacePage.Dashboard => "Dashboard",
        WorkspacePage.Inventory => "Inventory",
        WorkspacePage.Customers => "Customers",
        _ => "Sign in to continue"
    };

    public IReadOnlyList<PermissionRow> InventoryPermissions =>
    [
        new("View items", Permissions.CanViewInventory),
        new("Create items", Permissions.CanCreateInventory),
        new("Edit items", Permissions.CanEditInventory),
        new("Delete items", Permissions.CanDeleteInventory)
    ];

    public IReadOnlyList<PermissionRow> CustomerPermissions =>
    [
        new("View customers", Permissions.CanViewCustomers),
        new("Create customers", Permissions.CanCreateCustomers),
        new("Edit customers", Permissions.CanEditCustomers),
        new("Delete customers", Permissions.CanDeleteCustomers)
    ];

    public bool Navigate(WorkspacePage page)
    {
        bool allowed = page switch
        {
            WorkspacePage.Dashboard => CanOpenWorkspace,
            WorkspacePage.Inventory => Permissions.CanViewInventory,
            WorkspacePage.Customers => Permissions.CanViewCustomers,
            _ => false
        };
        if (!allowed) return false;
        SelectedPage = page;
        NotifyAll();
        return true;
    }

    private void OnSessionChanged()
    {
        int? id = _session.User?.Id;
        string? role = _session.User?.Role;
        if (!CanOpenWorkspace)
            SelectedPage = null;
        else if (SelectedPage is null || id != _userId || role != _role)
            SelectedPage = WorkspacePage.Dashboard;

        // A normal /me refresh preserves the page. A new account/role starts fresh.
        _userId = id;
        _role = role;
        NotifyAll();
    }

    public event PropertyChangedEventHandler? PropertyChanged;
    private void NotifyAll() => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(null));
}
