# Delivergate Desktop

The optional C# WPF client for the Delivergate assessment. It uses the Go API
in the repository root. The interface starts with standard Windows controls
and a simple white background.

## Current stage: F8 - Interface polish

Implemented:

- .NET 10 WPF project for Windows
- A resizable connection window and working Close button
- API server address loaded from `appsettings.json`
- A clear startup error if the configuration is missing or invalid
- Build and IDE output excluded from Git

Click Check connection to call `GET /health`. The screen reports success,
an unreachable server, a timeout, HTTP errors, or invalid health responses.
The button is disabled during requests, and the window stays responsive.
F3 adds email/password login, server-verified identity, a signed-in view showing
name/email/role, session verification, and local logout. See [the F3 guide](F3-guide.md).
F4 adds Dashboard, Inventory, and Customers navigation with permission summaries
matching the Go backend. All three roles can open both management sections;
their permitted actions differ.
See [the F4 guide](F4-guide.md) for the permission matrix, code explanation, and tests.

F5 loads real inventory and categories using authenticated GET requests. Open
Inventory to load data, search by name, choose a category, or click Refresh.
The read-only table shows ID, name, category, price, and quantity. Selecting a
row displays its description. See [the F5 guide](F5-guide.md).

F6 adds New item, Edit selected, Save/Cancel, and confirmed deletion. Owner can
create/edit/delete, manager can create/edit, and cashier can edit. The backend
still authorizes each request. See [the F6 guide](F6-guide.md) for validation,
error recovery, and the isolated real-backend test script.

F7 adds customer listing, local search by name/email/phone, create/edit forms,
and owner-only confirmed deletion. All roles can view/create/edit customers.
Duplicate email errors retain the draft. See [the F7 guide](F7-guide.md), including
the Windows Application Control limitation affecting live write verification.

## Requirements

F8 keeps the white Windows-style interface and adds consistent control sizing,
clearer table rows and headers, a wider initial workspace, and loading indicators
for inventory and customer requests. See [the F8 guide](F8-guide.md) for the
changes and the manual UI acceptance checklist.

- Windows with the .NET 10 SDK
- VS Code or another C# editor

The `global.json` in this folder selects a stable .NET 10 SDK when commands
are run here. No third-party UI packages are needed.

## Build and run

From the repository root:

```powershell
cd frontend
dotnet restore .\Delivergate.Desktop\Delivergate.Desktop.csproj
dotnet build .\Delivergate.Desktop\Delivergate.Desktop.csproj
dotnet run --project .\Delivergate.Desktop\Delivergate.Desktop.csproj
```

Close the desktop application before rebuilding it. Its executable may be
locked while the application is running.

## Configuration

Edit `Delivergate.Desktop/appsettings.json` before building:

```json
{
  "ApiBaseUrl": "http://localhost:3000/"
}
```

The file is copied beside the executable during build and publish. It contains
only the server address. Never put passwords, access tokens, or JWT signing
secrets here. A missing file, invalid JSON, or an invalid server URL produces
a configuration error and closes the application.

The window can open without the backend. For a successful connection check,
start the backend with `go run .` in a separate terminal at the repository root.

## Files to understand

| File | Purpose |
| --- | --- |
| `Delivergate.Desktop.csproj` | Sets the target framework and enables WPF |
| `App.xaml` / `App.xaml.cs` | Loads settings and opens the main window |
| `Configuration/AppSettings.cs` | Reads and validates the API address |
| `Views/MainWindow.xaml` | Defines the window layout |
| `Views/MainWindow.xaml.cs` | Connects button events to the view model |
| `Models/HealthResponse.cs` | Represents the health response JSON |
| `Services/ApiClient.cs` | Reuses one HTTP client with a 10-second timeout |
| `ViewModels/MainWindowViewModel.cs` | Updates connection status and busy state |
| `ViewModels/AuthViewModel.cs` | Validates login input and exposes authentication screen state |
| `Models/AuthModels.cs` | Represents login and current-user JSON responses |
| `Services/UserSession.cs` | Holds the token and current user in memory only |
| `Services/ApiException.cs` | Reports safe authentication errors with HTTP status codes |
| `Services/RolePermissions.cs` | Defines the UI permission matrix for each role |
| `ViewModels/WorkspaceViewModel.cs` | Guards navigation and resets workspace state on logout |
| `Views/DashboardView.xaml` | Shows the workspace introduction |
| `Views/InventoryView.xaml` / `Views/CustomersView.xaml` | Display tables, search, role-aware forms and confirmation actions |
| `ViewModels/InventoryViewModel.cs` | Loads inventory and applies search/category filters |
| `Models/InventoryModels.cs` | Maps item and category JSON, including category_id |
| `ViewModels/CustomersViewModel.cs` | Customer loading, search, draft validation and write state |
| `Models/CustomerModels.cs` | Customer API response and input types |
| `appsettings.json` | Stores the API address |

All desktop C# files belong under `frontend/Delivergate.Desktop`, not in the
repository root or the backend's Go `models` directory.

## F2 manual check

1. Build the project and confirm there are no errors.
2. Run it and check the title and configured API address.
3. Resize the window; text and the Close button should remain visible.
4. Click Close; the app should exit and return control to the terminal.
5. Run `git status --short` and confirm that `bin` and `obj` are not listed.
6. With the backend running, click Check connection and expect Connected.
7. Stop the backend and check again; expect Cannot reach the backend.
8. Restart the backend and check again; expect Connected without restarting the app.

## Repeatable connection checks

From `frontend`:

```powershell
dotnet run --project .\Delivergate.ConnectionChecks\Delivergate.ConnectionChecks.csproj
```

This dependency-free check runner compiles the actual service and view-model
sources and tests success, failures, busy state, duplicate clicks, and recovery.
It exits with a nonzero code on failure. It does not replace manual WPF UI checks.
With the real Go backend running, add `-- --live` to also check localhost:3000.

The same runner also checks authentication using controlled HTTP responses.
For live authentication, set `OWNER_PASSWORD`, `MANAGER_PASSWORD`, and
`CASHIER_PASSWORD` in the current process environment to match your local
backend settings, and add `-- --live --live-auth`. Never commit those values.
This checks all three seeded roles against the running backend without printing tokens.
Also add `--live-inventory` to read categories/items for each role without changing records.
Use `--live-customer-read` to read customers for each role without changing records.
For isolated real CRUD checks (never the shop database), run from the repo root:

```powershell
.\frontend\scripts\Test-IsolatedInventoryCrud.ps1 -IncludeCustomers
```

This requires Windows to permit Go and the temporary test backend executable.
F7's first isolated run was blocked by Application Control. No policy was changed.

## Session security

- Passwords are masked and cleared from the input on submission; they are not saved.
- Tokens exist only in memory. Closing the app requires a new login next time.
- Login succeeds only after `/api/auth/me` accepts the returned token.
- A 401 from session verification clears the session and shows the sign-in form.
- Logout clears this client's session; it does not revoke an already-issued token
  on the backend. The backend currently expires tokens after 24 hours.
- Expiry is detected on a protected request, not by a background timer.
- Use HTTPS for a remote deployment. HTTP localhost is for local development.
- The backend remains responsible for enforcing role permissions.
