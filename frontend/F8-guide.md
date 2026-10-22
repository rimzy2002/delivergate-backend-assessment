# F8 - Interface polish

This stage improves presentation without changing the backend or permission rules.
The background stays white, with quiet grey table stripes and standard Windows
buttons. There are no new UI libraries, decorative illustrations, or fake metrics.

## What changed and why

- `App.xaml` contains shared control styles. For example, changing the minimum
  button height here applies to Save, Refresh, and Cancel in both modules.
- Tables have taller rows, clearer headers, and alternating row backgrounds so
  it is easier to follow a record across several columns.
- The initial window is wider to give the inventory columns room. The existing
  scrollable layout and table scrollbars remain available on smaller windows.
- Inventory and Customers show an indeterminate progress bar while `IsBusy` is
  true. It means a request is running, not a percentage of work completed.
- Short introductions explain each management screen.

Native control templates are retained: keyboard navigation, focus indicators,
selection states, and disabled states are not replaced with custom templates.
Validation messages, draft recovery, confirmation dialogs and server-side role
checks continue to work as before. Styling is not authorization.

## Automated verification

From the repository root:

```powershell
dotnet build frontend/Delivergate.Desktop/Delivergate.Desktop.csproj -c Release
dotnet run --project frontend/Delivergate.ConnectionChecks/Delivergate.ConnectionChecks.csproj
```

The build checks compiled XAML. The check runner tests the actual services and
view models, not rendered pixels. No shop records are changed by its default run.

## Manual UI acceptance checklist

These checks must still be performed in the running Windows app; a successful
build does not prove visual layout or accessibility at every display scale.

1. Open the app at normal size, then resize to its minimum size. Scroll to every
   action and use horizontal table scrolling when columns do not fit.
2. Check at 100% and 150% Windows display scaling for clipped labels or controls.
3. Use Tab and Shift+Tab through login and management forms. Confirm visible focus.
4. Sign in with each role. Confirm the same allowed actions as F6/F7; cashier
   cannot create items, and only owner can delete items or customers.
5. Refresh each list. Confirm the progress bar disappears on success or error.
6. Search for an absent record and clear the search. Check the result summary.
7. Open an edit, make an invalid value, and Save. Check the error and retained draft.
8. Cancel an edit; cancel a delete confirmation. Neither should change records.
9. Sign out during a request. No previous user's data should appear after logout.
10. Verify keyboard focus, selected rows and disabled controls in Windows high
    contrast mode; the application is not yet certified for all accessibility settings.

Live write verification still has the F7 Application Control limitation documented
in `F7-guide.md`. This stage does not change Windows security policy or claim to
have completed the blocked tests. Use disposable data for manual CRUD testing.
