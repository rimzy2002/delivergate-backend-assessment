# F7: Customer management

## What is implemented

- Opening Customers fetches the real customer list using the current Bearer token.
- The table shows ID, name, email, and phone. Selecting a row displays its address.
- Search matches name, email, or phone without case sensitivity. Clear search
  restores the loaded snapshot; Refresh obtains changes from the server.
- Owner, manager, and cashier can create/edit customers. Only owner can delete.
- Create/edit use a separate inline draft. Cancel never sends a request.
- Delete asks for confirmation using the customer's ID/name, with No as default.
- Duplicate email (409) shows a specific message and keeps the entered fields.
- Empty data, no search matches, loading, API/network errors, and retries are handled.
- Logout/account changes clear personal data, selection, search, and draft fields.

## Validation

Required: name (1–150 trimmed characters), valid email (up to 150), and phone
(1–30 trimmed characters). Address is optional, at most 300 characters, and may
be cleared during an edit. Phone is text, preserving leading zeros and + signs.
No invented country-specific phone format is enforced beyond the backend contract.
Email is trimmed and lowercased to match the server's normalization.

Example: ` ALICE@example.com ` is submitted as `alice@example.com`. If that email
belongs to another record, the backend rejects the request. The desktop keeps
the draft so the user can correct just the email instead of retyping everything.

## Code map: what and why

| File | Responsibility |
| --- | --- |
| Models/CustomerModels.cs | Typed customer JSON and request fields |
| Services/ApiClient.cs | GET/POST/PATCH/DELETE and shared protected mutation handling |
| ViewModels/CustomersViewModel.cs | List/search, draft state, validation, permissions, retries |
| Views/CustomersView.xaml | Read-only table, form, search, status messages |
| Views/CustomersView.xaml.cs | Thin click handlers and the delete confirmation dialog |
| Delivergate.ConnectionChecks/CustomerChecks.cs | Controlled regression checks and optional isolated live tests |

## Important design choices

### Search is local, Refresh is remote

The existing backend returns the full list without pagination. Typing filters
the loaded snapshot, rather than sending a request for every character. For a
large database, move filtering/pagination to the server in a later enhancement.
Search uses literal substring matches, not SQL wildcard semantics.

### Drafts do not edit the selected object

BeginEdit copies values into EditName/EditEmail/EditPhone/EditAddress. The table
changes only after a successful server response. Cancel simply clears the draft.
Example: changing a name and clicking Cancel leaves the original row unchanged.

### Permissions are checked twice

The desktop disables unavailable actions and checks permissions again inside
the operation methods. The backend is still the actual security boundary: its
role middleware rejects unauthorized writes even if a client bypasses its UI.
Cashiers can create customers but cannot create inventory; the rules differ.

### One write at a time; do not automatically retry ambiguous writes

Busy state blocks duplicate Save clicks and draft cancellation while a request
is pending. If a write times out or returns an invalid response, it may already
have succeeded on the server. Further writes are blocked until Refresh succeeds.
The message asks the user to verify the result before retrying.

### Session revisions prevent data leaking into the next account

Operations capture the session revision. Logout cancels pending work and clears
the view. A late response belonging to an old revision is discarded. Cancellation
does not undo a write already accepted by the backend.

### Shared mutation helper

F7 reuses the inventory mutation transport for both resources, with typed JSON
inputs and resource-specific messages. Regression tests cover F6 after this
refactor. Delete expects 204 No Content rather than trying to parse an empty body.

## Verification and limitations

- WPF Release build: zero warnings and errors.
- Controlled checks: 88 customer checks plus the previous 149 checks (237 total).
- Live-enabled read-only regression run: 248 checks, including actual login and
  customer/inventory reads for all three roles against the already-running backend.
- Real F6 CRUD was tested successfully on an isolated database before its push.
- F7's new isolated live customer-write test attempt was blocked by Windows
  Application Control when starting the temporary backend. A subsequent Go test
  invocation was also blocked. No security policy was changed or bypassed.
- Therefore F7 live POST/PATCH/DELETE integration remains unverified. The code
  for those checks is included, but a provided test is not a claim it passed.
- Backend source was not changed in F7. Backend tests last passed during F6.
- Native customer form/delete-dialog click-through also remains a manual check.
- No real shop customer records were modified by these F7 checks.

## Repeat the checks

From frontend (controlled checks, no live writes):

```powershell
dotnet run --project .\Delivergate.ConnectionChecks\Delivergate.ConnectionChecks.csproj
```

After an authorized user/admin resolves the Application Control restriction,
run from the repository root:

```powershell
.\frontend\scripts\Test-IsolatedInventoryCrud.ps1 -IncludeCustomers
```

The script creates a temporary database, uses generated test credentials, starts
a test server on a free port, and stops it in finally. It does not use the shop
database. Test records are deleted through the owner API; temp logs/database
remain for diagnosis. Never point live write tests at a production database.

## Manual walkthrough

Close older desktop windows and start the app. Sign in, open Customers, test name,
email and phone search, and Clear search. Use disposable test data for creating
and editing. Try a duplicate email and check the draft stays. Cancel an edit and
verify the row is unchanged. Verify manager/cashier cannot delete; as owner,
delete only the disposable record after checking the ID/name in the dialog.
Finally sign out and ensure no customer details remain visible.

Known backend limitation: no optimistic concurrency/version check; concurrent
edits use last successful write wins. F7 does not claim to solve that limitation.
Next planned stage: F8, UI/error-state polish and broader usability checks.
