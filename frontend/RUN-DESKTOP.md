# Running Delivergate Desktop

This package is the Windows WPF client, not the backend server or an installer.
It requires the .NET 10 **Desktop Runtime** matching the machine architecture
used to build the package. The .NET SDK also provides the runtime for developers.

1. Extract the entire ZIP into a folder. Do not run the EXE inside the ZIP.
2. Start the Go backend using the repository README. Keep it running on port 3000.
3. Open `Delivergate.Desktop.exe` from the extracted folder.
4. Click Check connection, then sign in using your locally configured seed account.
   Obtain the password from your own backend setup, not from this package.
5. Open Inventory or Customers. Available actions depend on the signed-in role.

Keep the DLL, runtime configuration, and `appsettings.json` beside the EXE.
If the API uses another address, edit `ApiBaseUrl` in `appsettings.json`, then
restart the client. Use HTTPS for a remote server. Never put passwords or tokens
in this file. Login tokens stay in memory and are cleared on logout or app exit.

If Windows blocks the executable, do not disable security protections. Ask the
administrator to review it, or build from source in an approved environment.
An unsigned development ZIP is not a signed installer.

For connection errors, check the backend is running and the configured port is
correct. For invalid credentials, check the backend seed configuration. For a
write with an uncertain result, refresh the list before trying again; the server
may already have saved it.

## Short demonstration

Use disposable demonstration records, not real customer details. Show login,
inventory search/category filtering, create/edit, customer search/create/edit,
owner-only deletion confirmation, role restrictions, validation errors, and
logout. Do not record passwords, tokens, `.env`, or real personal information.

The assignment's video link must be created and submitted separately. This ZIP
does not upload a video, send the submission email, or start the backend for you.
