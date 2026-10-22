# F4: Navigation and role-based workspace

## What is complete

After login the app opens Dashboard. The navigation lets the user switch to
Inventory and Customers without opening additional windows. Each section shows
the actions allowed for the current role. Logout clears the active page and
permissions. Signing in as another account starts on Dashboard again.

The existing login, session verification, logout, and health check remain.
A normal session verification preserves the current page. An account or role
change resets the page. Signed-out and unknown-role navigation is blocked.

F4 does not fetch inventory/customers, display fabricated statistics, or provide
working CRUD buttons. These pages explicitly explain that lists/search are F5,
inventory forms are F6, and customer workflows are F7.

## Permissions checked against main.go

| Action | Owner | Manager | Cashier |
| --- | --- | --- | --- |
| Open Dashboard, Inventory, Customers | Yes | Yes | Yes |
| View inventory | Yes | Yes | Yes |
| Create inventory | Yes | Yes | No |
| Edit inventory | Yes | Yes | Yes |
| Delete inventory | Yes | No | No |
| View/create/edit customers | Yes | Yes | Yes |
| Delete customers | Yes | No | No |

These are UI permissions, not a security boundary. The Go API still checks the
token and allowed role. Future forms should use the same permission properties
to hide/disable restricted actions and still handle backend 401/403 responses.

## Important code and simple examples

### Services/RolePermissions.cs

`ForRole(role)` returns a set of boolean permissions. An unrecognized or missing
role gets no permissions (deny by default).

Example: a cashier gets `CanCreateInventory = false` but
`CanCreateCustomers = true`. We therefore must not hide the entire Customers
section from cashiers. Keeping the matrix in one file prevents different screens
from accidentally implementing different rules.

### ViewModels/WorkspaceViewModel.cs

`SelectedPage` stores the current section using the `WorkspacePage` enum.
`Navigate(page)` checks access before changing it. Using an enum avoids spelling
mistakes such as "Inventroy" in string-based page names.

Example: `Navigate(WorkspacePage.Inventory)` changes the title and visible view
when signed in. The same call returns false after logout, even if called from
code rather than a button.

The view model observes `UserSession.Changed`. Logout sets the page to null and
clears permissions. Another account starts on Dashboard; ordinary `/me`
verification keeps the current page. This avoids carrying the previous user's
workspace state into a new session.

`PropertyChanged` tells WPF to reevaluate its bindings. Here a null property name
means all properties may have changed, which is reasonable for this small model.

### Views/MainWindow.xaml and its code-behind

The shell contains the navigation and embeds three UserControls. Radio buttons
show the selected section. Their click handlers only ask the workspace to
navigate; they do not contain permission rules or network calls.

`BooleanToVisibilityConverter` turns an `IsInventory` value into visible/hidden
UI. Only the selected page is displayed. These bindings keep layout separate
from the navigation logic, making that logic testable without Windows clicks.

### DashboardView, InventoryView, CustomersView

Each section has a separate XAML file. Their `DataContext` is the workspace.
The Inventory/Customers permission rows come from that model, not hard-coded
per-role XAML. F5–F7 can extend these views without rewriting the login screen.

## Verification

- Release build succeeded with zero warnings/errors using a separate ignored
  output folder, because the previous app could still be open.
- All 24 navigation checks passed.
- All 11 connection and 17 authentication controlled checks passed (52 total).
- Existing Go tests passed.
- No live record creation, updates, or deletion are needed for F4 testing.
- Native signed-in page click-through remains a manual check; automated tests
  cover the view model and permission matrix, while the WPF build validates XAML.

From frontend:

```powershell
dotnet run --project .\Delivergate.ConnectionChecks\Delivergate.ConnectionChecks.csproj
```

## Try it manually

1. Close the previous desktop window before rebuilding.
2. Start the backend with `go run .` from the repository root if it is not running.
3. From frontend, run `dotnet run --project .\Delivergate.Desktop\Delivergate.Desktop.csproj`.
4. Sign in as owner and open all three sections. Owner's action permissions should all say Allowed.
5. Verify session while on Customers; it should remain on Customers.
6. Sign out; navigation should disappear and the login form should return.
7. Sign in as manager: both delete permissions should say Not allowed.
8. Sign in as cashier: inventory create/delete and customer delete should say Not allowed.
9. Resize the window; the main area can scroll on smaller windows.

Use passwords from the private root .env. Never include them in screenshots,
documentation, or commits. Next stage: F5, real inventory listing and search.
