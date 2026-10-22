# F3: Login and session handling

## What changed

The desktop client now signs in through the existing Go API. It has email and
masked password fields, a sign-in button, and clear status messages. After login
it displays name, email, and role, with Verify session and Sign out buttons.
The F2 health check remains available. No backend rules or credentials changed.
Role-based navigation and management screens belong to F4 and later stages.

## Follow the code

1. `Views/MainWindow.xaml.cs` reads the password and immediately clears the PasswordBox.
2. `ViewModels/AuthViewModel.cs` validates required fields and email format.
3. `Services/ApiClient.cs` posts credentials to `/api/auth/login`.
4. The backend checks the password and returns a JWT and user details.
5. ApiClient sends that token to `/api/auth/me` as a Bearer header.
6. Only after verification does `Services/UserSession.cs` store the token and user.
7. Property-change notifications switch `Views/MainWindow.xaml` to the signed-in view.

`Models/AuthModels.cs` describes the JSON response shapes. `ApiException.cs`
provides safe messages for HTTP failures without displaying raw server responses.

## Important concepts with examples

### Async/await and busy state

`await _api.LoginAsync(email, password)` waits for the network without freezing
the UI thread. `IsBusy` disables the form while waiting, so a double-click does
not send two login requests. `finally` enables the controls after success or failure.

### A token is a temporary entry pass

The password is sent for login. Later protected requests use
`Authorization: Bearer <token>` instead of sending the password again.
The token is never displayed or written to a file or log. Health and login
requests do not receive that header. The desktop does not contain the backend's
JWT signing secret; the backend validates the token's signature and expiry.

### Screen state is separate from layout

The view model contains behavior; XAML contains layout. When the session changes,
notifications update `IsSignedIn`, `IsSignedOut`, and the user properties.
Example: successful login hides the password form; Sign out shows it again.
Separating these parts lets tests check behavior without opening Windows.

### Errors are not all the same

- Wrong password: a generic error does not reveal whether the email exists.
- HTTP 401 from `/me`: clear the session and ask the user to sign in again.
- HTTP 403: show permission denied, without treating it as a logout.
- Network failure: show an error but preserve an already valid local session.
- Incomplete or inconsistent identity data: do not create a session.

## Security limits to understand

Tokens are memory-only. Closing the app requires a new login. Passwords are not
saved; the visible input is cleared promptly. Managed password strings cannot
be guaranteed to disappear immediately from RAM.

Logout does not revoke the server token: this backend has no revocation endpoint.
Its JWT expiry is 24 hours. Expiry is detected when a protected request is
rejected, not by a timer. Future protected API methods must apply the same 401
handling. Showing a role in the desktop is not authorization: the backend
enforces access. Use HTTPS for remote APIs; localhost HTTP is development-only.

## Run and test manually

Terminal 1, repository root:

```powershell
go run .
```

Terminal 2, repository root:

```powershell
cd frontend
dotnet run --project .\Delivergate.Desktop\Delivergate.Desktop.csproj
```

Use owner@delivergate.local, manager@delivergate.local, or cashier@delivergate.local
with that role's password from your private root `.env`. Do not use placeholder
text or share passwords in screenshots.

Check empty fields, incorrect password, successful login, Verify session, and
Sign out. Close/reopen the app to confirm it starts signed out. Stop the backend
to test a network error, then restart and retry. Health success alone does not
prove that login works.

## Verification on 22 October 2026

- Release build: zero warnings and errors.
- 12 connection checks including the live backend.
- 21 authentication checks: 17 controlled checks and four live checks.
- Live owner, manager, cashier login and `/me` succeeded; local logout cleared sessions.
- Live wrong-password login was rejected correctly.
- Existing Go tests passed.
- The actual WPF login screen opened and its layout/accessibility labels were inspected.
- Credentials were tested through production API code, not UI automation.
  A full manual login/logout click-through is still recommended.

Controlled checks cover validation, busy/duplicate protection, JSON parsing,
unverified/inconsistent identity, 401/403, network/timeout handling, token isolation,
notifications, recovery, and starting signed out. Timeout and expiry errors are
simulated rather than waiting for actual token expiry.

Run checks from frontend:

```powershell
dotnet run --project .\Delivergate.ConnectionChecks\Delivergate.ConnectionChecks.csproj
```

Live checks are opt-in; see the frontend README for environment variables.
