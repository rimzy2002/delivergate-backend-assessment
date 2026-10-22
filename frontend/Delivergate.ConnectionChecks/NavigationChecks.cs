using Delivergate.Desktop.Models;
using Delivergate.Desktop.Services;
using Delivergate.Desktop.ViewModels;

internal static class NavigationChecks
{
    public static void Run()
    {
        int passed = 0;
        void Check(bool condition, string name)
        {
            if (!condition) throw new Exception(name);
            Console.WriteLine($"PASS navigation: {name}");
            passed++;
        }

        var session = new UserSession();
        var workspace = new WorkspaceViewModel(session);
        Check(!workspace.CanOpenWorkspace && !workspace.Navigate(WorkspacePage.Inventory), "signed-out access blocked");

        foreach (string role in new[] { "owner", "manager", "cashier" })
        {
            session.Set("test-token", new AuthUser { Id = 1, Name = "Test User", Email = "test@example.com", Role = role });
            var p = workspace.Permissions;
            Check(p.CanViewInventory && p.CanEditInventory && p.CanViewCustomers && p.CanCreateCustomers && p.CanEditCustomers,
                $"{role} common permissions");
            Check(p.CanCreateInventory == (role != "cashier") && p.CanDeleteInventory == (role == "owner") &&
                p.CanDeleteCustomers == (role == "owner"), $"{role} restricted permissions");
            Check(workspace.IsDashboard, $"{role} starts on Dashboard");
            Check(workspace.Navigate(WorkspacePage.Inventory) && workspace.IsInventory && !workspace.IsDashboard,
                $"{role} Inventory navigation");
            Check(workspace.Navigate(WorkspacePage.Customers) && workspace.IsCustomers && !workspace.IsInventory,
                $"{role} Customers navigation");
            session.Set("test-token", session.User!);
            Check(workspace.IsCustomers, $"{role} session verification preserves page");
        }

        int notifications = 0;
        workspace.PropertyChanged += (_, _) => notifications++;
        Check(!workspace.Navigate((WorkspacePage)99) && workspace.IsCustomers, "unknown page rejected");
        session.Clear();
        Check(workspace.SelectedPage is null && !workspace.IsCustomers && workspace.UserName == "" &&
            !workspace.Permissions.CanViewCustomers && notifications > 0, "logout clears page, identity and permissions");
        session.Set("test-token", new AuthUser { Id = 2, Name = "Other User", Role = "owner" });
        Check(workspace.IsDashboard, "new account starts fresh");
        session.Set("test-token", new AuthUser { Id = 2, Role = "unknown" });
        Check(!workspace.CanOpenWorkspace && workspace.SelectedPage is null && !workspace.Navigate(WorkspacePage.Dashboard),
            "unknown role denied by default");
        Check(workspace.InventoryPermissions.All(p => !p.IsAllowed) && workspace.CustomerPermissions.All(p => !p.IsAllowed),
            "unknown role has no action permissions");
        Console.WriteLine($"All {passed} navigation checks passed.");
    }
}
