# SmartNotes tests

A plain console app with its own small assert helpers, so no test framework packages.

```powershell
dotnet run --project tests\SmartNotes.Tests                 # unit tests: storage, settings, backups, autocorrect, snapping, strings
dotnet run --project tests\SmartNotes.Tests -- --e2e        # plus end-to-end tests against the real app
dotnet run --project tests\SmartNotes.Tests -- --e2e Export # only tests whose name contains "Export"
```

The exit code is the number of failed tests.

**Unit tests** run in-process with `SMARTNOTES_DATA_DIR` pointing at `%TEMP%\SmartNotesTests\unit-data`, which is emptied before each test.

**End-to-end tests** start the freshly built `bin\Debug\...\SmartNotes.exe` with a throw-away data folder (`%TEMP%\SmartNotesTests\e2e-<test>`). They drive it through UI Automation: buttons are invoked and text is set through the controls, so the mouse is never moved. A test copy uses its own single-instance mutex and never touches your real notes or the Windows autostart entry. While they run, don't type or use the clipboard:
- A few tests press the global shortcuts Win+Alt+N / A / S.
- One test opens a full-screen grey window over the notes.
- One test moves a note off-screen.
- One test opens the tray menu (it appears where the mouse is).
- One test copies a note as a picture to the clipboard (your clipboard text is put back afterwards).
