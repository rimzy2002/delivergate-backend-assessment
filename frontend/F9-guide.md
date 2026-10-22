# F9 - Release preparation

F9 adds a repeatable packaging script and instructions for the reviewer.
The script stops on a failed check or build, uses a new output folder each time,
and includes the runtime configuration and API address alongside the executable.
The ZIP is framework-dependent: the target PC needs the .NET 10 Desktop Runtime.
It is not a self-contained or signed installer.

The SHA256 printed by the script identifies the exact generated ZIP. The archive
is ignored by Git; source and instructions are committed, generated binaries are
not. The script neither changes the shop database nor pushes files to GitHub.

Run from the repository root:

```powershell
.\frontend\scripts\Publish-Desktop.ps1
```

Close any running desktop build before packaging to avoid locked output files.
If the script fails, do not distribute its partial output. Fix the reported issue
and rerun; a new directory is used without deleting the old one.

## Acceptance boundary

Automated service/view-model checks and compiled XAML are covered by the script.
Manually run the extracted package, complete the F8 UI checklist, and test real
CRUD against disposable backend data before calling the release fully accepted.
The earlier Application Control restriction on isolated backend write tests is
not bypassed by this script. Packaging success does not prove live API writes.

Use `RUN-DESKTOP.md` as the demonstration outline. Record the WPF demo, upload it
to your chosen Drive folder, verify reviewer access, and include its link with
the repository, Postman collection and ER diagram in the final submission.
No video or email is automatically created or sent by F9.
