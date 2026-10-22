namespace Delivergate.Desktop.Services;

// UI guidance only. The Go API must still authorize every protected operation.
public sealed record RolePermissions(
    bool CanViewInventory, bool CanCreateInventory, bool CanEditInventory,
    bool CanDeleteInventory, bool CanViewCustomers, bool CanCreateCustomers,
    bool CanEditCustomers, bool CanDeleteCustomers)
{
    public static RolePermissions ForRole(string? role) => role switch
    {
        "owner" => new(true, true, true, true, true, true, true, true),
        "manager" => new(true, true, true, false, true, true, true, false),
        "cashier" => new(true, false, true, false, true, true, true, false),
        _ => new(false, false, false, false, false, false, false, false)
    };
}
