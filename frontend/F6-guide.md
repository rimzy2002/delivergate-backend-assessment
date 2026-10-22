# F6: Inventory create, edit, and delete

## Completed behavior

- Owner: create, edit, delete. Manager: create/edit. Cashier: edit only.
- New item opens an inline draft with category choices and initial quantity zero.
- Edit selected copies the row into draft fields; typing never changes the row itself.
- Save validates, then sends POST /api/items or PATCH /api/items/:id.
- Cancel discards the draft without sending a request.
- Delete shows the item ID/name and requires confirmation (No is the default).
- Successful saves use the server-returned item to update the list, clear filters,
  and select the saved row. Successful deletion removes the row locally.
- The table remains read-only; all edits go through the form and API.

## Validation

Name: 1–150 trimmed characters. Optional description: at most 500 characters.
Category: a real loaded category, never the All categories filter option.
Price: a positive decimal using a dot, such as 125.50. Quantity: a nonnegative
32-bit whole number; fractional/negative quantities are rejected.

Example: quantity `-2` never reaches the backend. Quantity `0` is valid (out of stock).
The Go backend still validates every submitted request; UI validation is for helpful feedback.

## Important code

- InventoryEditing.cs is the editing half of the partial InventoryViewModel class.
  Keeping loading/filtering separate from drafting/saving makes both easier to read.
- CanCreate/CanEdit/CanDelete control buttons AND are checked by the methods.
  Disabled buttons alone are not enough; callers can invoke methods directly.
- ItemInput maps category_id explicitly and sends price/quantity as JSON numbers.
- SaveItemAsync selects POST for a new item and PATCH for an existing ID.
- DeleteItemAsync expects 204 No Content and does not try to parse JSON from it.
- The view handles the confirmation dialog; the model requires confirmed=true and
  rechecks the selected ID and current permission before sending DELETE.

## Error and concurrency review

Only one inventory load/write runs at a time. Save disables Save, Cancel, filters,
and row selection while pending. Form validation/API rejection keeps the draft.
Logout clears drafts and rows; a delayed old result cannot populate a new account.

A timeout/network error/invalid success response/server 500 can have an uncertain
outcome: the server may already have committed the write. The client never retries
automatically. It blocks further writes until Refresh succeeds and asks the user
to check the result first. Cancellation cannot undo an already-sent server write.

The existing backend has no optimistic concurrency/version field; two users
editing the same item can overwrite one another (last successful update wins).
This limitation was not disguised as conflict detection or changed in F6.

## Verification

- Release build: zero warnings/errors.
- 74 controlled editing assertions plus 8 real-backend CRUD/permission assertions.
- All previous 75 controlled connection/auth/navigation/listing checks pass.
- Isolated run totals 157 assertions; Go tests also pass.
- Real writes used a temporary database and throwaway test credentials, not the shop database.
- The test server is stopped after testing; temporary database/logs remain in the
  OS temp folder for diagnosis. No test credentials are committed or printed.
- XAML is compiled; native form/dialog click-through remains a manual check.

From repository root, repeat the isolated checks:

```powershell
.\frontend\scripts\Test-IsolatedInventoryCrud.ps1
```

## Manual walkthrough

Close old desktop windows, start the backend, and run the desktop project.
As owner, open Inventory, create a disposable item, edit it, cancel another edit,
and verify the original stays unchanged. Test invalid fields. Delete only your
disposable item after checking the confirmation ID/name. As manager verify Delete
is disabled; as cashier verify New/Delete are disabled but Edit works.
Never use real records just to test deletion. Next stage: F7 customer workflows.
