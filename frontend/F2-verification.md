# F2 verification

Verified on 22 October 2026.

## Correction

The manually added desktop files were outside the WPF project: in the
repository root, root Views folder, and the Go models folder. The SDK therefore
continued building the F1 desktop files. Their contents were preserved and
moved into frontend/Delivergate.Desktop. The duplicate root appsettings.json
was removed; the identical desktop configuration remains in the project.

## Results

- Release WPF build: passed, zero warnings and zero errors.
- Connection check runner: all 11 controlled checks passed.
- Live Go backend GET /health: passed (12 checks including the live request).
- Existing Go tests: passed.
- Actual WPF window: opened with the correct API address and Check connection button.
- Actual offline check: showed the expected error and allowed another attempt.
- Online recovery: passed in the controlled check runner. Final real-window
  recovery click was blocked by a Windows firewall prompt after starting Go;
  no security settings were changed. Repeat manually after dismissing the prompt.

Controlled checks cover a healthy response, the request URL/method/header,
network failure, timeout exception handling, HTTP 404/500, malformed/null JSON,
missing/unhealthy status, busy state, duplicate requests, and recovery.
The timeout check simulates cancellation; it is not a timed network integration test.

## Run again

From frontend:

```powershell
dotnet build .\Delivergate.Desktop\Delivergate.Desktop.csproj -c Release
dotnet run --project .\Delivergate.ConnectionChecks\Delivergate.ConnectionChecks.csproj
```

Start `go run .` from the repository root, then add `-- --live` to the check
runner command to include the real backend. No credentials are needed for health checks.
