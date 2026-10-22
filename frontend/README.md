# Delivergate Desktop

The optional C# WPF client for the Delivergate assessment. It uses the Go API
in the repository root. The interface starts with standard Windows controls
and a simple white background.

## Current stage: F1 - Project setup

Implemented:

- .NET 10 WPF project for Windows
- A resizable welcome window and working Close button
- API server address loaded from `appsettings.json`
- A clear startup error if the configuration is missing or invalid
- Build and IDE output excluded from Git

The window displays the configured address; it does not check connectivity yet.
API requests are F2. Login, role permissions, and CRUD screens come afterwards.

## Requirements

- Windows with the .NET 10 SDK
- VS Code or another C# editor

The `global.json` in this folder selects a stable .NET 10 SDK when commands
are run here. No third-party UI packages are needed for F1.

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

The backend does not need to be running for this initial window. From F2,
start it with `go run .` in a separate terminal at the repository root.

## Files to understand

| File | Purpose |
| --- | --- |
| `Delivergate.Desktop.csproj` | Sets the target framework and enables WPF |
| `App.xaml` / `App.xaml.cs` | Loads settings and opens the main window |
| `Configuration/AppSettings.cs` | Reads and validates the API address |
| `Views/MainWindow.xaml` | Defines the window layout |
| `Views/MainWindow.xaml.cs` | Supplies settings to the view and handles Close |
| `appsettings.json` | Stores the API address |

Folders for services, models, and view models will be added as they are needed.

## F1 manual check

1. Build the project and confirm there are no errors.
2. Run it and check the title and configured API address.
3. Resize the window; text and the Close button should remain visible.
4. Click Close; the app should exit and return control to the terminal.
5. Run `git status --short` and confirm that `bin` and `obj` are not listed.
