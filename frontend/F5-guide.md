# F5: Real inventory listing and filters

## What works

- Opening Inventory loads GET /api/categories and GET /api/items with the current token.
- The read-only table displays ID, name, category, price (two decimal places), and quantity.
- Selecting a row displays the description. Zero-stock items remain visible.
- Search matches part of an item name, ignoring case and surrounding spaces.
- The category dropdown combines with search; All categories removes that restriction.
- Clear filters restores all loaded items. Refresh reloads both endpoints.
- Loading, empty inventory, no matching results, network failure, timeout, HTTP
  errors, and invalid response data have explicit states/messages.
- All three roles can view inventory. Editing and deletion are intentionally unavailable in F5.

## Why filters run locally

The existing backend returns the complete item list without pagination. It
supports a name-search query but no category-filter parameter. F5 loads the list
once, then applies both filters locally rather than pretending an unsupported
server parameter works. Typing does not send a request per character.

Example: search `cone`, choose Chocolate, and see only chocolate-category items
whose names contain `cone`. Both conditions must match. `%` is treated as literal
text in this client, not as a SQL wildcard. Refresh obtains changes made elsewhere.
For a much larger inventory, add server-side category filtering and pagination.

## Important code

### Models/InventoryModels.cs

Typed classes describe item/category JSON. `[JsonPropertyName("category_id")]`
maps the backend's category_id field to C# CategoryId. Decimal represents price
in the desktop model; this does not change the backend's existing numeric storage.
The response wrappers match `{ "items": [...] }` and `{ "categories": [...] }`.
An empty array is valid; a missing/null wrapper is an error rather than fake empty data.

### Services/ApiClient.cs

GetItemsAsync and GetCategoriesAsync use a shared protected-GET helper. It adds
the Bearer header to that request only. A 401 clears the matching session. A late
401 from an old session cannot sign out a newly signed-in account.

### ViewModels/InventoryViewModel.cs

`_allItems` keeps the loaded snapshot; `Items` is the filtered list shown in WPF.
`ApplyFilters()` combines name and category conditions. Selection is cleared if
its item disappears from the filtered results. `IsBusy` prevents duplicate loads.

LoadAsync commits the categories and items together only after both requests
succeed. A failed refresh clears previous rows rather than presenting them as
fresh data. The user can retry with Refresh. Filters are preserved across a
successful refresh unless the selected category no longer exists.

### Session revision and cancellation

UserSession.Revision changes when the account/token/role changes or is cleared.
An inventory request remembers the revision it started with. Logout cancels the
load, clears rows, categories, selection, and filters. A delayed old result is
discarded instead of entering the next user's screen. Ordinary session
verification with the same identity does not erase loaded inventory.

Example: owner starts a slow load, signs out, then cashier signs in. The old
owner response must not populate the cashier's screen. The revision check and
cancellation protect that transition.

### Views/InventoryView.xaml

The DataGrid is read-only, with explicit columns and a bounded height so rows
can scroll. Search and category selection are bound to the view model. The
view's click handlers call Refresh/Clear filters; they contain no networking.
The existing permission summary remains in an expandable section.

## Run and try it

Start `go run .` from the repository root if the backend is not already running.
Close older desktop windows before rebuilding. From frontend:

```powershell
dotnet run --project .\Delivergate.Desktop\Delivergate.Desktop.csproj
```

1. Sign in with a seeded account and its password from the private root .env.
2. Open Inventory. Check rows, category names, prices, and quantities.
3. Search for part of an existing name, then narrow by category.
4. Search for a nonexistent name; expect the no-matches message.
5. Clear filters, select a row, and read its description.
6. Click Refresh. Stop the backend, Refresh again, and check the error/retry behavior.
7. Sign out; the inventory and filters must clear. Another account starts fresh.

If the real database has no items, an empty inventory message is correct.
No records are inserted just to make this screen look populated. F6 adds forms.

## Verification

- Release WPF build: zero warnings and errors (separate ignored F5-check output folder).
- 23 controlled inventory checks passed, including filters, parsing, empty/error
  states, retry, duplicate loads, 401 cleanup, and stale-response protection.
- Regression checks: 11 connection, 17 authentication, and 24 navigation checks passed.
- Live checks: backend health, all three role logins/session checks, invalid
  credentials, and read-only categories/items retrieval for all three roles.
- Complete live-enabled run: 83 checks passed. Existing Go tests also passed.
- Tests exercise the actual API/view-model sources; WPF XAML compiles.
  Native signed-in inventory click-through remains a manual check.
- No inventory/customer records were created, changed, or deleted by F5 tests.

Run the controlled checks from frontend:

```powershell
dotnet run --project .\Delivergate.ConnectionChecks\Delivergate.ConnectionChecks.csproj
```

For live checks, supply the three role passwords through process environment
variables as described in README, then append `-- --live --live-auth --live-inventory`.
Never commit passwords or tokens. Timeout responses are simulated in controlled tests.
