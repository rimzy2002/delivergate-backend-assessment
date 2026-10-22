# F10 - Final verification and handoff

Verification date: 23 October 2026 (Asia/Colombo).

## Verified

- `go test ./...` passed (Go reported cached handler/middleware results).
- `frontend/scripts/Test-IsolatedInventoryCrud.ps1 -IncludeCustomers` passed.
  This ran 237 controlled checks plus 8 live inventory and 14 live customer
  checks: 259 checks in total against a temporary backend database.
- Live checks covered owner/manager/cashier workflows, server-side restrictions,
  duplicate customer emails, editing and deletion. The real shop data was not used.
- F9 Release rebuild and packaging passed with zero warnings/errors.
- Postman collection and environment files and ER PDF/PNG/DBML are present.
- No `.env`, `.db`, or `.exe` files are tracked by Git. This targeted check is
  not a comprehensive audit of the repository's entire Git history.

The earlier Application Control restriction did not recur during this F10 run.
No Windows security policy was changed. Older guides describe the limitations at
the time those stages were completed; this report supersedes that live-test status.

## Deliverables

- Repository: https://github.com/rimzy2002/delivergate-backend-assessment
- API collection: `postman/Delivergate-Backend-API.postman_collection.json`
- Local environment: `postman/Delivergate-Local.postman_environment.json`
- ER diagram: `docs/delivergate-er-diagram.pdf` (PNG and DBML also available)
- Desktop source and instructions: `frontend/README.md`, `frontend/RUN-DESKTOP.md`
- Desktop ZIP: generated locally by `frontend/scripts/Publish-Desktop.ps1`;
  ignored by Git, not uploaded as a GitHub Release automatically.

## Remaining human acceptance and submission

1. Extract the desktop ZIP and run it with the backend available.
2. Complete the F8 visual/keyboard/display-scaling checklist. Automated tests
   exercise services/view models, not clicks or rendered layout.
3. Record the WPF demo using the outline in `RUN-DESKTOP.md`, using disposable
   records and hiding passwords/tokens. Upload the video to Drive and verify
   that the reviewer can open it.
4. Verify the repository is accessible to the reviewer. Add the video link to
   the submission document/email alongside the repository and ER diagram.
5. Send the submission to the addresses specified in the assessment. No email
   has been sent by these implementation stages.

## Suggested email text

Subject: Backend Engineer Intern Technical Assessment - Rimzy

Dear Team,

I have completed the backend implementation and the optional C# WPF desktop
client for the technical assessment.

Repository: https://github.com/rimzy2002/delivergate-backend-assessment

The repository includes setup instructions, the Postman API collection, and the
ER diagram in the docs folder. The frontend folder contains the desktop client
and instructions for building and running it.

Demo video: [insert your accessible Google Drive video link before sending]

Thank you for reviewing my submission.

Kind regards,
Rimzy

Do not send this draft with the placeholder still present. Final submission is
not complete until the video and required document/email have been delivered.
