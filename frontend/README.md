# Delivergate Desktop

The optional C# WPF client for the Delivergate assessment. It uses the Go API
in the repository root. The interface starts with standard Windows controls
and a simple white background.

## Current stage: F2 - Backend connection

Implemented:

- .NET 10 WPF project for Windows
- A resizable connection window and working Close button
- API server address loaded from `appsettings.json`
- A clear startup error if the configuration is missing or invalid
- Build and IDE output excluded from Git

Click Check connection to call `GET /health`. The screen reports success,
an unreachable server, a timeout, HTTP errors, or invalid health responses.
The button is disabled during requests, and the window stays responsive.
Login, role permissions, and CRUD screens come afterwards.

## Requirements

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
