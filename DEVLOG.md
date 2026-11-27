# SmartNotes - Developer Log & Technical Specification

## Project Overview
SmartNotes is a high-performance, lightweight Windows desktop sticky notes application built with C# and WPF on .NET 10. It is engineered to live permanently on the desktop canvas below all other open applications, games, and windows, featuring the signature Obsidian Dark + Neon design language of **MacroMaster** and **SoundSwitcher**.

---

## Ideas backlog (suggested 2026-11-26, not started)

The quick wins from the same list and **note history** are done (see the changelog entries for v2.6.0). These are the remaining bigger ones. All three top picks (note history, a bring-to-front hotkey, app-linked notes) are done. Items marked *(done)* are described in the entries below.

**Bigger features (a few days each)**
- **Reminders** *(done, unreleased)*. "Remind me…" on a note (time, optional repeat). At that time the note comes forward over the open apps and glows.
- **Daily checklists** *(done, unreleased)*. "Repeat daily" on a checklist: the ticks reset each morning, with a small streak count.
- **Finish the Westeros theme.** Planned below ("Skins"); the reference board `docs/references/westeros-board.html` is waiting for picks (surface, house colours, title font). Sigils drawn in code, no show logo or artwork.
- **A sync folder of your choice.** Settings option to keep the data in OneDrive / Dropbox / a NAS, watching it for changes made on another PC. The merge already exists (`MergePendingNotes`: by note id, newest `ModifiedAt` wins) and `ModifiedAt` only changes on real edits since 2.5.0. Keeps "no account, no cloud": it is the user's own folder.

**Bold ideas and new themes**
- **App-linked notes** *(done, unreleased)*. A note can be linked to a program: it shows on top only while that program is in front and hides otherwise (key bindings over a game, snippets over an editor, a checklist over tax software).
- **Living themes** *(done, unreleased)*. Opt-in, animating only while a note is focused or hovered so idle CPU stays at zero: a shooting star over Astral, the Elden Ring rune slowly glowing, a Cyberpunk glitch when a note saves, a wax seal pressed onto a Medieval note when it is locked.
- **Sketch mode** *(done, unreleased)*. A fourth note mode for pen / mouse drawing: WPF's `InkCanvas` brings pressure, eraser and highlighter; the strokes are saved with the note.
- **New original themes** *(done, unreleased)*, each with its own per-note emblem like the runes and constellations:
  - **Deep Sea**: a bioluminescent abyss, a different glowing creature per note (jellyfish, anglerfish lure, nautilus).
  - **Blueprint**: cyan grid paper and white technical lettering; the title sits in a drawing title block with the note's ID as the drawing number.
  - **Ink & Washi**: rice paper and brush strokes, a red name seal (hanko) per note with its first letter.
  - **Terminal**: green or amber phosphor, faint scanlines, `[x]` checkboxes and a blinking block cursor.

---

## Architecture & Subsystems

1. **Desktop Sticking & Native Win32 Subsystem (`Core/Native/`)**:
   - `Win32Api.cs`: P/Invoke declarations for `user32.dll` and `dwmapi.dll` (`SetWindowPos`, `GetWindowLong`, `SetWindowLong`, `RegisterHotKey`, `UnregisterHotKey`, `DwmSetWindowAttribute`).
   - `DesktopWindowManager.cs`: Manages window Z-order enforcement and `HwndSource` message hooks.
     - Automatically asserts `HWND_BOTTOM = (IntPtr)1` on `WM_ACTIVATE` (inactive) and `WM_KILLFOCUS`.
     - Traps `WM_WINDOWPOSCHANGING` so when foreground programs receive focus, note windows stay pinned below them.
     - Applies `WS_EX_TOOLWINDOW` via `GWL_EXSTYLE` to prevent taskbar and Alt+Tab clutter while remaining 100% interactive on the desktop surface.
   - `WindowBlurHelper.cs`: Configures DWM attributes (`DWMWA_USE_IMMERSIVE_DARK_MODE` and `DWMWCP_DONOTROUND`) so WPF handles crisp per-pixel transparency and antialiased corner rounding without OS window artifact halos.
   - `GlobalHotKeyManager.cs`: System-wide hotkey engine using `RegisterHotKey` for instant shortcuts (`Win+Alt+N` new note, `Win+Alt+A` all notes to the front, `Win+Alt+S` show / hide). Windows itself owns `Win+Alt+B D G K M R T` (date and time, HDR, Game Bar, microphone).
   - `TrayManager.cs`: Windows Notification Area (System Tray) icon manager with custom right-click context menu, tooltip updates, balloon tips, and lifecycle management.

2. **Persistence & Service Layer (`Core/Services/` & `Core/Models/`)**:
   - `NoteItem.cs`: Full data model capturing `Id` (Guid), `Title`, `Content`, `Checklist` (`List<TodoCheckItem>`), `IsChecklistMode`, `(X, Y)` desktop coordinates, `Width`, `Height`, `ColorKey`, `PinMode` (`DesktopStuck`, `AlwaysOnTop`, `Normal`), `IsLocked`, `Opacity`, `FontSize`, `CreatedAt`, and `ModifiedAt`.
   - `NoteColorTheme.cs`: Dynamic palette generator supporting 8 neon obsidian themes (Amber, Emerald, Violet, Cyan, Rose, Obsidian, Gold, Mint).
   - `NoteStorageService.cs`: Manages JSON serialization/deserialization to `%APPDATA%\SmartNotes\notes.json` with debounced write operations, safe lock concurrency, welcome defaults, and JSON export/import.
   - `SettingsService.cs`: Manages `%APPDATA%\SmartNotes\settings.json` and syncs startup configuration with `AutoStartManager`.
   - `AutoStartManager.cs`: Windows Registry manager for `HKCU\Software\Microsoft\Windows\CurrentVersion\Run\SmartNotes`.

3. **UI, Controls & Theme Engine (`UI/`)**:
   - `DarkTheme.xaml`: Deep Obsidian palette (`#0D1017`, `#131824`, `#161D2E`) with thick 3px vector borders, high-contrast neon switches, buttons, textboxes, scrollbars, and ClearType typography defaults.
   - `LucideIcon.cs` & `LucideIcons.xaml`: High-precision vector icon control rendering Lucide SVG geometry paths with configurable size and stroke thickness (`2.6` - `2.8`).
   - `ColorKeyToBrushConverter.cs`: IValueConverter binding color keys to dynamic XAML SolidColorBrush instances.
   - `StickyNoteWindow.xaml` / `.xaml.cs`: Interactive desktop sticky note window with true single-border rounded geometry, text/checklist toggling, color palette popup, lock controls, pin mode switcher, and continuous position/dimension persistence.
   - The Notes Hub window was removed in 1.1.0. Notes are managed from each note's ⋯ menu and the tray menu (trash, arrange, bring to front).
   - `SettingsDialog.xaml` / `.xaml.cs`: Preference configuration modal for autostart, hotkeys, default styling, and stickiness mode.

---

## Technical Specifications & Sticking Mechanics

### 1. True Desktop Layering
When `PinMode == NotePinMode.DesktopStuck`:
- The note window is placed at `HWND_BOTTOM` via `SetWindowPos(hwnd, HWND_BOTTOM, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE | SWP_SHOWWINDOW)`.
- When the user clicks on the note, it gains input focus to accept typing and drag movements.
- When focus shifts to any other window (browser, IDE, game), `WM_ACTIVATE` / `WM_KILLFOCUS` and `WM_WINDOWPOSCHANGING` automatically enforce bottom Z-order, preventing sticky notes from ever occluding foreground applications.

### 2. Multi-Monitor Coordinate Persistence
- Saved `X` and `Y` coordinates accurately map across virtual screen bounds, supporting primary and secondary monitors with differing DPI scale factors (`PerMonitorV2`).

### 3. Debounced Auto-Save
- Modifications to text content, task checklist items, note position (`LocationChanged`), and dimensions (`SizeChanged`) trigger a 400ms debounced timer, avoiding excessive disk I/O while ensuring zero data loss.

## Note Skins (Cyberpunk, Elden Ring)
Skins change the whole look of a note (silhouette, surface, fonts, icon colours) and are chosen per note in the palette popup ("Note Style") or as the default for new notes in Settings.

**How it works (replaces the abandoned ControlTemplate-swap plan from the `SmartNotes_theme` branch):**
- There is still exactly one `StickyNoteWindow` layout, so every feature works in every skin. A skin is a `NoteSkin` entry (`Core/Models/NoteSkin.cs`): insets, fonts, panel shape, chrome ink colours and a `Tint` function that re-colours the note's own `NoteColorTheme`.
- `StickyNoteWindow.ApplySkinVisuals()` writes those values into window-level `Skin*` resources that the XAML consumes via `DynamicResource`.
- Skins with `CustomArt = true` switch the rounded card off and paint their own non-rectangular body into `SkinArtHost` (`UI/Skins/CyberpunkArt.cs`, `UI/Skins/EldenScrollArt.cs`), redrawn on resize. The art constants must match the skin's `ContentInset` / `HeaderTopPad`.
- Bundled OFL fonts live in `Assets/Fonts` (each with its licence file) and are embedded as resources.
- Each skin can name an icon pack (`NoteSkin.IconPack`). `LucideIcon.Pack` is inherited down the note surface and resolves `{Pack}Icon_{key}` / `{Pack}IconFill_{key}` from `UI/Icons/SkinIcons.xaml`, falling back to Lucide for anything the pack does not define. Menus, popups and Settings stay on Lucide.
- To add a skin: add a `NoteSkin` to `NoteSkin.All`, an art class if it needs its own shape, a button in the popup and a `Skin_*` localization key.
- Cyberpunk cells (header groups, checklist/copy rows, input row) use `UI/Skins/CutBorder.cs`, a `Border` that chamfers two corners when `Cut > 0` and renders as a normal Border otherwise.

## App chrome: design tokens and menus
- **Design tokens** live at the top of `UI/Themes/DarkTheme.xaml` ("DESIGN TOKENS"): one accent (`AccentBrush`, teal like the app icon, plus hover / pressed / on-accent), surface brushes (`MenuBgBrush`, `HoverBgBrush`, `ControlBgBrush`, `ControlBorderBrush`), one border weight (`StrokeThin`), two radii (`RadiusControl`, `RadiusPanel`) and a type scale (`FontSizeCaption` / `Body` / `Label` / `Title`). Use these instead of literal values. Amber, emerald and red remain as status colours only (warning, success, danger).
- **Menus** are all built with `UI/Controls/AppMenu.cs` (`Item`, `Sub`, `Heading`, `Separator`, tones Normal / Accent / Danger / Muted, `isChecked`) and styled by the ContextMenu / MenuItem styles in `DarkTheme.xaml`. The tray menu (`Core/Native/TrayManager.cs`) is now a WPF menu too: the tray icon is still a WinForms `NotifyIcon`, and a hidden 1px host window gives the menu a foreground window so it closes on an outside click.
- **Themed menus** (2026-11-16/17): every skin except Classic restyles the note's "…" and right-click menus, its tooltips and the colour popup, as on `docs/references/themed-menus-board.html` (Elden Ring = option A "Grace menu", Cyberpunk / Medieval / Paper Sticky / Astral = proposals 1–4). How it works:
  - `UI/Themes/SkinMenuBase.xaml` holds the templates (ContextMenu, MenuItem, menu separator, ToolTip, popup choice / primary buttons, slider, switch). Every value in them is a `Menu*` token read by DynamicResource.
  - Each skin's `UI/Themes/*Menu.xaml` (`GraceMenu`, `CyberpunkMenu`, `MedievalMenu`, `PaperMenu`, `AstralMenu`) defines the tokens and merges the base. A token a skin leaves out falls back to the Classic value in `DarkTheme.xaml`.
  - `UI/Controls/SkinMenus.cs` maps skin → dictionary. `ApplyToPanel(window)` merges it into the note window (popup and tooltips follow); `Apply(menu)` merges it into each ContextMenu, which lives outside the window's tree.
  - `AppMenu` leaves Normal-tone icons uncoloured so they inherit the row's icon colour (and its hover colour); Danger uses `MenuDangerBrush`.
  - Cut corners (Cyberpunk) come from `CutBorder` with `MenuPanelCut` / `MenuChoiceCut`; Paper's glue strip is `MenuTopStripHeight`.
  - Define tokens as real brushes/styles, not `<StaticResource>` alias entries: those resolved to the wrong value in testing.
- **Sketch mode** (2026-11-27, unreleased): a fourth mode button (pencil) in the header; `UI/Windows/StickyNoteWindow.Sketch.cs`. The note's minimum width went from 260 to 280 for the extra button.
  - `InkCanvas` (pen pressure for free) with a toolbar: pen (2.6 px, smoothed), highlighter (14 × 5 rectangle tip), eraser (by point), five colours (the note's accent, its text colour, red, blue, green), Clear. Locked notes can't be drawn on.
  - **Stored in notes.json**: `NoteItem.Sketch` = the strokes in Ink Serialized Format, compressed, base64 (`UI/Controls/SketchData`). The plan said a separate file per note; inline was chosen so the trash, the backups before updates, safe saving and (later) the sync folder carry drawings with no extra code. A typical doodle is a few KB.
  - **Counts as content**: a stroke revision counter is part of `ContentFingerprint` (drawing changes "modified" and starts a new earlier version after a pause); `NoteVersion.Sketch` keeps the drawing in Earlier Versions, and restoring brings it back. Clear first saves the drawing as an earlier version, then empties it ("Cleared, kept in Earlier Versions"), so there is no confirmation dialog.
  - Copy All Content copies a sketch note as a picture; Export as Markdown writes a PNG next to the .md and links it.
  - Tests: 3 unit tests (round trip, unreadable data, versions) and 1 end-to-end test (drawing shown at start, Clear saves an earlier version, mode switches saved).
- **Living themes** (2026-11-27, unreleased): Settings > Note Appearance > Living Themes (`AppSettings.LivingThemes`, off by default). `UI/Windows/StickyNoteWindow.Living.cs`. Two canvases in the note: `LivingLayer` behind the content, `LivingOverlay` in front (both not hit-testable).
  - **Only while focused or hovered** (`UpdateLiving` on Activated / Deactivated / MouseEnter / MouseLeave / visibility, skin and size changes): Astral launches a shooting star every 3–8 s (a gradient streak falling to the left, 0.75 s); Elden Ring and Deep Sea get a breathing radial glow over the rune (`EldenRingArt.RunePlacement`) or the creature's light (`DeepSeaArt.CreaturePlacement` / `GlowPoint`: the bell, the lure, the shell). When the note is left alone the timer stops and the glow is removed, so no animation keeps running.
  - **Events**: Cyberpunk glitches when an edit is saved (content jumps sideways in discrete steps for 220 ms, a few accent slices flicker; not on moves or resizes). Medieval: locking presses a wax seal onto the lower rod (`UI/Skins/WaxSealArt`: uneven wax in the note's seal colour, raised rim, the title's first letter in UnifrakturMaguntia, or a lily; scale 1.7 → 1 with a slight overshoot); it stays while locked and lifts off on unlock.
  - Checked with `--render <folder> living` (the renderer puts these windows on screen at 1 % opacity, because WPF only advances animations for windows it renders).
- **Four new themes: Terminal, Blueprint, Ink & Washi, Deep Sea** (2026-11-27, unreleased). Built from the ideas backlog without a pick board (the user asked to go ahead with the DEVLOG); `dotnet run --project tests\SmartNotes.Tests -- --render <folder> skins|creatures|menu` shows them.
  - **Terminal** (`TerminalArt`, `TerminalMenu.xaml`): CRT bezel, black glass with a phosphor bloom, scanlines, a dashed header rule with a `tty/<id>` label, a power LED. The phosphor is the note colour (amber note = amber terminal), and with `NoteSkin.InkFromTheme` every icon and helper text on the glass follows it. `NoteSkin.TextCheckboxes`: ticks are "[ ]" / "[x]" (the checkbox template shows a text mark through the `SkinBoxCheckVisibility` / `SkinTextCheckVisibility` resources). `NoteSkin.BlockCursor`: a block after the footer time that blinks (DispatcherTimer, 530 ms) only while the note is active, so nothing runs when idle. Fonts VT323 / Share Tech Mono / Silkscreen. Menus: green, the selection in inverse video.
  - **Blueprint** (`BlueprintArt`, `BlueprintMenu.xaml`): blue paper (a violet note prints like a diazo blueline), 10 px and 50 px grids, a white double border with zone ticks and centring marks. The title row is framed as the drawing's title block: "TITLE" cell, and on the right "DWG NO. SN-<id>" and the note's creation date (`NoteSkin.TitleReserveRight` keeps the title text clear of that cell; the painter gets the title row's position from `StickyNoteWindow.TitleBand`). A first version put the block in the lower right, over the tasks; moved to the title row.
  - **Ink & Washi** (`InkWashiArt`, `InkWashiMenu.xaml`): rice paper with deckled edges, fibres and a soft shadow; a sumi brush stroke under the header in the note colour as ink, with dry-brush streaks; a faint ensō in the note colour; a vermilion hanko beside the title with its first letter (印 without a title), centred on the glyph's outline. It repaints 300 ms after the title stops changing (`NoteSkin.ArtShowsTitle`). EB Garamond / IM Fell / Macondo.
  - **Deep Sea** (`DeepSeaArt`, `DeepSeaMenu.xaml`): dark teal water with light beams from above, a wavering thermocline under the header, marine snow and a column of bubbles, a luminous rim, and a creature in the lower right: jellyfish (scalloped bell, oral arms, tentacles fading out), anglerfish (dark fish, bright lure on its stalk) or nautilus (logarithmic spiral with chamber walls and stripes). Audiowide / Zen Dots / Metamorphous titles.
  - **Emblems** (`UI/Skins/Emblems.cs`): one picker in the colour popup for every skin with per-note emblems (Automatic + tiles): Astral constellations and Deep Sea creatures. Stored in `NoteItem.Emblems` (skin key → emblem key). The old `ConstellationKey` field is still read into it (`LegacyConstellationKey`, never written). The Great Runes stay separate because a rune also sets the note's colours.
  - Shared: `UI/Skins/ArtKit.cs` (brushes, blends, seeded noise, text), `SkinArt.Build` now takes the note and the title row.
  - Tests: 5 unit tests (names in both languages, content inside the visible body, old constellation field read and not written, emblems per skin and automatic, seal letter).
- **Shaped skins: snapping, corners, Settings preview** (2026-11-27, unreleased), the last open items of 2026-11-12:
  - `NoteSkin.VisualInset`: how far each skin's visible body sits inside its window (Paper 4/3/7/10, Cyberpunk 4, Medieval 9/2 for the rods' finials, Elden Ring and Astral 5). `StickyNoteWindow.GetVisibleScreenRect()` / `VisualInsetPx` (physical pixels, per-monitor DPI); `Core/Native/ScreenRects` deflates and inflates.
  - **Snapping and docking** use the visible bodies: `App.GetOtherNoteRects`, `NoteClusterEngine.GetConnectedCluster`, and in `WM_MOVING` the moving note is deflated, snapped, and inflated again. Two Medieval scrolls used to stop 18 px apart (and so did not count as a stack); now their rods touch.
  - **Corners**: Windows gives fully transparent pixels of a layered window no mouse input, which is why the cut-away corners (Cyberpunk, Paper's fold) could not be grabbed. The corner and top/bottom resize zones now sit on the visible body and are filled with `#01000000` (alpha 1, invisible but hit-testable).
  - **Settings > Note Appearance preview**: shaped skins paint their body with the real painter behind the mini note (`UI/Skins/SkinArt.Build`, now shared with the note window), with the header row in the painted header strip.
  - `tests/SmartNotes.Tests/NoteRenderer.cs`: `dotnet run --project tests\SmartNotes.Tests -- --render <folder> [skins|features|menu|settings]` draws real note windows (off-screen, with the unit-test data folder) to PNG files plus a contact sheet. `AssemblyInfo.cs` gives the test project access to the window's named elements (`InternalsVisibleTo`).
- **Reminders and daily checklists** (2026-11-27, unreleased):
  - **Rules** (`Core/Models/NoteReminder.cs`, `NoteSchedule`, no UI so they test with any clock): `NoteItem.Reminder` = `At` (next time), `Repeat` (None, Daily, Mon–Fri, Weekly, Monthly), `SnoozeUntil`, `Anchor` (first time of a series; later times are counted from it, so a reminder on the 31st is on Feb 28 and back on Mar 31). When it rings: a snooze is used up, a one-off is gone, a repeat moves to its next time after now (missed repeats ring once, not one by one).
  - **Clock** (`App.CheckNoteClock`): one Background-priority `DispatcherTimer` that sleeps until the next reminder or day start, at most 60 s; also runs on wake from sleep (`SystemEvents.PowerModeChanged`), on a clock change, at startup (so reminders missed while the PC was off ring then) and when a reminder is set or snoozed.
  - **Ringing** (`StickyNoteWindow.FireReminder`): a banner at the top of the note ("Reminder · 14:30", "10 min" snooze, ✓ done) with a glow in the note colour that pulses for about a minute, then stays steady (no animation keeps running). The note floats (topmost, no focus taken, `DesktopWindowManager.SetFloating`) even if it is stuck to the desktop, hidden with Win+Alt+S or linked to a program that is not in front, until Done or Snooze. A tray message names the note. The reminder state is saved at once (`SaveNoteState`; `RequestSave` does nothing before the window has loaded, which is the case at startup: found by the end-to-end test).
  - **Menu**: ⋯ > Remind Me: the current reminder (muted), In 1 Hour, This Evening (18:00, before 17:00 only), Tomorrow Morning (09:00), Pick a Time… (`UI/Windows/ReminderDialog`: day for the next two weeks, hour, minute in 5-minute steps, repeat, and a line "Reminds you Fri 28 Nov 09:00"; a time that has passed moves to the next day), Remove Reminder. Snooze length is `AppSettings.SnoozeMinutes` (10). Duplicates don't copy the reminder.
  - **Footer**: a bell with "14:30" / "Fri 09:00" / "28 Nov" while a reminder is set.
  - **Daily checklists**: ⋯ > Repeat Daily (checklist mode). `NoteSchedule.ResetDaily`: when a new day has started (`AppSettings.DayStartHour`, 4:00, so ticks after midnight count for the day before), every task is unticked; `Streak` goes up if every task was done on the day before, and back to 0 after a day with an open task or a skipped day. The footer shows a repeat icon, or a flame with the streak (today counts once everything is ticked). The reset is a real edit, so after the night's pause yesterday's ticks become an earlier version.
  - Tests: 7 unit tests (one-off, repeats and missed days, monthly 31st, snooze, saving, daily reset and streak, 4 am) and 3 end-to-end tests (a reminder missed while off rings at start and snoozes; set, change and remove from the menu and the dialog; a daily list from yesterday is unticked at start with streak 3).
- **App-linked notes** (2026-11-27, unreleased): "Show Only With" in the note's ⋯ menu (below Opacity).
  - **Data**: `NoteItem.LinkedApp` (exe file name, "Code.exe"; empty = not linked) and `LinkedAppName` (for the menu and footer). Matching is by exe name, case-insensitive (`Core/Models/AppLink.cs`). Duplicates keep the link.
  - **Tracking**: `Core/Native/ForegroundWatcher.cs` now owns the `EVENT_SYSTEM_FOREGROUND` hook (skips SmartNotes' own windows); `ShowDesktopWatcher` and `App.OnForegroundChanged` both listen. `ProgramWindows.ExeNameOf` reads the exe via `QueryFullProcessImageName` (`PROCESS_QUERY_LIMITED_INFORMATION`, so elevated programs work too). Store apps run inside `ApplicationFrameHost.exe`; the frame's child window from another process gives the real app.
  - **Behaviour** (`App.ApplyAppLinks`): the linked program comes to the front → the note appears topmost without taking focus (`ShowWithoutFocus`: `ShowActivated = false`, `SWP_NOACTIVATE`); any other program → hidden. Clicking into the note is a SmartNotes window, so no event and it stays. At startup a linked note only appears if its program is in front. Win+Alt+A, a new / duplicated / restored note and Show all still show it, so a note linked to a program that is never open can be reached and unlinked. While linked the effective pin mode is Always on Top (`DesktopWindowManager.SetAppLinked`), so Win+D handling and "send to bottom" leave it alone; the saved `PinMode` comes back on unlink. The pin button is disabled with a tooltip; the footer reads "Only with Visual Studio Code".
  - **Menu**: "Always (not linked)", then every program with an Alt+Tab-style window (visible, unowned, not a tool window, titled, not cloaked; desktop and SmartNotes left out), one per exe, named by the exe's FileDescription. The list is built when the submenu opens. A linked program that is not running is listed (checked) so it can be seen.
  - **Limits**: exclusive full-screen games draw over every window; the note shows over borderless / windowed games only.
  - Tests: 3 unit tests (matching, which notes hide, exe of a window) and 2 end-to-end tests (hidden at startup, shown over its program without stealing focus, hidden over a PowerShell window; linking and unlinking from the real menu). Full run: 77/79. The two failures were `History_EditsInTheSameRound_DoNotPileUpVersions` (it fails the same way on the code before this change: the note got ". ab", i.e. keys typed during the run landed in it) and a one-off UI Automation "element not available" in the new menu test, which passed on its own runs.
- **v2.6.0** (2026-11-26), note history: "Earlier Versions" submenu in the note's ⋯ menu, below Duplicate Note. Autosave writes within 400 ms and `notes.json.bak` only keeps the previous save, so typing over a note could not be undone after a restart.
  - **When a version is kept** (`StickyNoteWindow.SaveNoteState`): on the first content change after `NoteHistoryService.EditPause` (10 min) without edits, the content as last saved (`_lastSavedVersion`, an independent copy because task and copy rows are edited in place) becomes a version. Content = title, text, mode, tasks, copy rows (the same fingerprint as "modified"); moving, resizing and colours don't count. Before every restore, the current content is kept too, so a restore can be undone from the same list.
  - **Storage** (`NoteHistoryService`): `<data>\history\note_<id>.json`, newest first, at most 20 versions, none older than 30 days, an identical newest version is not stored twice, atomic writes. The history is deleted when the note leaves the trash for good (emptied, expired after 48 h, permanent delete), never for a note that is still active.
  - **Menu**: entries read "Today 14:32   start of the text…" (yesterday / date otherwise); the tooltip shows the whole version; click restores and shows "Version restored". Greyed out for locked notes.
  - Tests: 6 unit tests (order, duplicates, limits, tasks and copy rows, trash rules) and 2 end-to-end tests (edit after a pause → restore from the real menu → undo entry; edits in one round add no versions).
- **v2.6.0** (2026-11-26), quick wins:
  - **Hotkeys**: `Win+Alt+A` brings all notes to the front (and un-hides them). Show / hide moved from `Win+Alt+D` to `Win+Alt+S`: Windows owns `Win+Alt+D` (date and time), so `RegisterHotKey` always failed for it. A probe on Windows 11 24H2 found `B D F G K M R T W Y` taken. A shortcut that is taken is now reported once in a tray message (`ReportedHotkeyConflicts` remembers what was reported).
  - **Lost notes come back** (`ScreenPlacement`): a note whose 40 px title strip has less than 80×20 px on any monitor's work area is moved onto the nearest one, at startup, when notes are shown again and 1.5 s after `WM_DISPLAYCHANGE`.
  - **Arrange** (`NoteArranger`): keeps sizes, moves docked stacks as one (`NoteClusterEngine`), leaves locked notes, rows with a 30 px cascade when the screen is full.
  - **Checklist**: done tasks struck through and dimmed; "3/7" in the footer; ticks via `Checked`/`Unchecked` (a keyboard or accessibility tick was not saved with `Click`).
  - **Copy as Picture** (⋯ menu): `RenderTargetBitmap` of `RootGrid` at ≥2×, PNG plus a bitmap on white; retries for 3 s when the clipboard is held (Windows clipboard history needs a moment for large pictures).
  - **Updates**: the zip is checked against GitHub's published SHA-256 (`digest` of the release asset); a read-only app folder is detected before downloading.
  - **German autocorrect list** (nciht, udn, garnicht, standart, …).
  - **Autostart**: `SettingsService.MayChangeAutoStart` now has a test. Cleanup on the development PC: a test program from 2026-11-19 (`dragtest.exe`, built before the 2.3.1 rule) had registered itself as "SmartNotes" in `HKCU\…\Run`; the entry and the old test programs were removed. Never part of a release.
- **v2.5.1** (2026-11-24), decisions after the bug hunt:
  - Locked notes cannot be deleted (× and "Delete Note" disabled, tooltip says to unlock first).
  - The Settings switch "New notes stick to the desktop" (was "Desktop Sticking", never read) sets `DefaultPinMode` for new notes; on by default.
  - Removed: "Add to Dictionary" and the custom dictionary button (never worked; WPF custom dictionaries leak spell-checker registrations), the Win+Alt+H "Notes Hub" row (never built), and unused settings/fields (ConfirmDelete, EnableSoundCues, hotkey strings, KeepBehindAllWindows, IsCollapsed, Tags, JSON import/export, LocateNote, IsStartupEnabled/SetStartup, RebuildContextMenu).
  - README lists lock, export, safe saving and autostart. 51 tests pass.
- **v2.5.0** (2026-11-24), bug hunt with a new test suite:
  - **Tests**: `tests/SmartNotes.Tests`, a dependency-free console runner (see `tests/README.md`). 35 unit tests (storage, trash, damaged/locked files, backups, settings, autocorrect, snapping, strings, export names, updates) and 15 end-to-end tests that start the real exe with a temp `SMARTNOTES_DATA_DIR` and drive it through UI Automation. Before the fixes 15 of 43 failed; now 50/50 pass.
  - **No note loss**: `DeleteNote` writes the trash copy first and keeps the note if that fails; `Load` skips null entries, repairs null lists, survives a broken trash folder and never lets a later save overwrite `notes.json` after a failed load; `notes.pending.json` (edits from a session where `notes.json` stayed locked) is merged on the next start; `Save` returns false and the note shows "Not saved, retrying..." and retries.
  - **Startup**: if settings, notes or the tray cannot be set up, an error is shown and the app exits (it used to stay as an invisible process that blocked every later start). Optional steps and each note window start independently. `settings.json` nulls are repaired.
  - **Spell check** no longer runs `Set-WinUserLanguageList` (it added languages and keyboard layouts to Windows). English typo fixes only for English text (German "im" became "I'm"); read-only text is never rewritten; corrections keep Ctrl+Z. `custom_dict.lex` is deliberately not attached via WPF `CustomDictionaries`: WPF leaks a Windows spell-checker registration and a temp file per text box on every run ("Add to Dictionary" was removed in 2.5.1).
  - **Lock** also covers task and copy rows. **Export** suggests a valid file name (`NoteExport.SuggestFileName`) and exports what is on screen. Settings from a note now go through `App.OpenSettingsDialog` (hotkey switch applies), open while notes are hidden, and Cancel undoes a previewed language. New / restored notes un-hide hidden notes.
  - **Updates** download with a 5 minute timeout (15 s failed below ~1 Mbit/s). `AppLog` writes recovered errors, taken hotkeys and update problems to `startup.log`.
  - **Autostart**: the Run entry is repaired when it points to a missing exe; a "Disabled" set in Task Manager is left alone.
  - Polish: German plurals ("Notizen"), real version in the Settings header, localized row tooltips and messages, "(Default)" on 16 pt, "modified" only changes on real edits, welcome note without `**`. RELEASE_GUIDE.md removed (the release workflow builds and uploads the zip).
- **v2.4.0** (2026-11-23), issue #1 follow-up and issue #3:
  - **Issue #1 follow-up**: after Win+D, clicking note 1 and then note 2 hid note 1. Every deactivation (`WM_ACTIVATE` inactive, `WM_KILLFOCUS`, `WM_ACTIVATEAPP`, WPF `Deactivated`) sent a Desktop Stuck note to `HWND_BOTTOM`, which is behind the raised Progman. Now `DesktopWindowManager.LowerAfterDeactivate` waits until the switch has settled (Background dispatcher priority) and only lowers the note when the foreground left SmartNotes. Focus on another SmartNotes window keeps the z-order. Focus on the desktop leaves placement to `ShowDesktopWatcher`.
  - **Issue #3, Bring All to Front raised only one note**: `BringAllNotesToFront` activated the notes one after another, so each dropped back when the next took focus. And while another app holds the foreground, Windows ignores raising our windows. Now one note takes the foreground first, then all are raised with `RaiseWithoutActivating` (`HWND_TOP`, `SWP_NOACTIVATE`). A second launch calls `AllowSetForegroundWindow(ASFW_ANY)` before signalling, so the running copy may take the foreground.
  - Side effect: Desktop Stuck notes that are above other apps stay there while you click between notes, and go back behind once another app takes focus.
  - Verified with the Debug build against the previous commit, using a temp `SMARTNOTES_DATA_DIR`, three notes and a script (real Win+D, mouse clicks, a WinForms window from another process as "other app"; visibility = `WindowFromPoint` at each note's centre). Old build: note 1 hidden under Progman after note 1 → note 2; Bring All raised no note. New build: all three notes stay visible through the clicks, Bring All raises all three (with SmartNotes focused or not), and clicking the other app puts them back behind it.
- **Choosable Astral constellation** (2026-11-19, in 2.4.0): `NoteItem.ConstellationKey` (empty = picked from the note id as before, so existing notes keep their sky). In the Astral skin the colour popup shows a Constellation row: Automatic plus one tile per figure in `AstralArt.Figures` (tiles drawn by `AstralArt.RenderTile` in the note colour). New / duplicated notes start on Automatic. Adding a figure to `Figures` (and its German name to `NamesDe`) adds a tile.
- **Physical drag for checklist tasks and copy items** (2026-11-19, in 2.4.0):
  - `UI/Controls/ListItemDragController<T>` replaces the checklist-only reorder code (which just swapped indices with `ObservableCollection.Move`) and now drives both lists. Copy rows got the same six-dot grip.
  - The grabbed row's card is lifted into `DragGhostWindow`: a borderless, click-through (`WS_EX_TRANSPARENT | WS_EX_NOACTIVATE | WS_EX_TOOLWINDOW`), top-most window positioned in physical pixels with `SetWindowPos`. It follows the cursor anywhere on screen on a stiff spring, tilts with sideways speed, and has a drop shadow. The row's slot stays open as a gap; other rows glide into place (FLIP: layout offset before/after the move, animated `TranslateTransform`).
  - Release inside the note: the ghost springs (under-damped, slight bounce) into the slot and the order is saved. Cursor more than 16 px outside the note: the rows return to the original order, the ghost shrinks / fades a bit, and on release it flies home with nothing changed.
  - Starts from the grip or row background (4 px any direction) or from the text (6 px, mostly vertical; sideways stays a text selection). Buttons and the checkbox are excluded. Disabled on locked notes.
  - The snapshot renders the whole items panel with `RenderTargetBitmap` and crops the card out of it. A `VisualBrush` snapshot only worked for the first drag in a session and came out blank afterwards.
  - Verified with the Debug build and a temp `SMARTNOTES_DATA_DIR`, driven by a mouse script: grip drags in both modes, repeated drags, a drag far outside the note (snapped back, order unchanged), a drag started on text, and a plain click still placing the caret.
- **Note safety across updates** (2026-11-19, in 2.3.1):
  - `DataBackupService.SnapshotIfNewVersion` runs in `App.OnStartup` before any service loads. On the first start of a new version (`last_version.txt` differs) it copies notes.json, notes.json.bak and settings.json to `<data>\backups\<date>-before-v<version>[-from-v<old>]\`. The newest 10 snapshots are kept.
  - `NoteStorageService`: a *locked* notes.json (antivirus / backup / sync) is retried 8 × 250 ms and is no longer treated as corrupt and moved aside. If it is still unreadable, a snapshot is taken and `_loadIncomplete` stops saves from overwriting it: saves merge with the file once it is readable, and until then go to `notes.pending.json`. Before this, a load failure meant the next save replaced the real notes and the save after that replaced the .bak.
  - `SettingsService`: an unreadable settings.json is snapshotted before the first save replaces it with defaults.
  - The autostart entry (`AutoStartManager.SyncStartupRegistration`) is now only synced from a process named SmartNotes.exe, and never with `SMARTNOTES_DATA_DIR` set. Test / screenshot programs that create a `SettingsService` had re-pointed the user's HKCU Run entry to their own exe (happened once on 2026-11-19; SmartNotes repairs it on its next start).
  - `SMARTNOTES_DATA_DIR` also gives a separate single-instance mutex / events (`AppPaths.InstanceSuffix`), so a test copy never signals or quits the user's SmartNotes.
  - **`SmartNotes.exe --exit`** asks the running instance to quit normally (OnExit flushes every note). `Launch SmartNotes.bat` uses it before `dotnet publish`: the running app locked SmartNotes.exe/.dll in `release\`, so the publish failed with file-in-use errors and the old version kept running. It never force-kills. If the running copy is too old to know `--exit` (≤ 2.3.0), it asks the user to quit from the tray. It only starts the app if the build succeeded.
  - Verified end-to-end with the Debug build and a temp data folder: the snapshot is created on first start; `--exit` quits within a second with the notes intact; with notes.json held open exclusively, nothing is moved aside, an "unreadable-notes" snapshot is taken and notes.json is unchanged afterwards. The Run entry was untouched by the test.
- **v2.3.1** (2026-11-19), from the user's first real test of 2.2 / 2.3:
  - **Checklist reorder**: dragging by the grip worked, but the user dragged the task text, which only selected it. Now a press anywhere on a task becomes a drag once the mouse moves clearly up or down (more than 8 px and more than sideways); the checkbox and × are excluded. The list then takes the mouse from the text box. Bug found on the way: `LostMouseCapture` bubbles, so the text box losing the mouse ended the drag at once; the handler now only reacts when the list itself loses capture. The grip is larger and brighter. Verified with real mouse input (SendInput-style test on a throw-away note) on Classic, Elden Ring and Cyberpunk.
  - **Win+D on Windows 11 24H2 (build 26200), two monitors**, measured with a z-order probe: `Progman` is the only top-level desktop window (WorkerW is now its child). Show Desktop raises Progman *above most topmost windows*, including `Shell_SecondaryTrayWnd`, so the second screen's taskbar disappears behind the wallpaper. That is Windows' own behaviour: it happened with no SmartNotes window visible. The 2.2 `ShowDesktopWatcher` does place a Desktop Stuck note directly above Progman during Show Desktop (note #19, Progman #20), and back at the bottom afterwards.
- **v2.3.0** (2026-11-19):
  - **Astral is its own theme now**, not the old Elden look. It has a night-sky panel (navy, a nebula in the note colour, a seeded star field, a dotted ecliptic arc) and one constellation per note in the lower right: Cassiopeia, Ursa Major, Cygnus, Orion or Lyra, picked from the note id and kept clear of the footer / input row. It has a silver frame with four-point sparkles, its own `Astral` icon pack (star-on-needle pin, constellation colour button, star bullets) and a starlight recolour. Its fonts are Cinzel / Metamorphous / Zen Dots and EB Garamond / Almendra / Oxanium.
  - **Icon pack fallbacks**: `IconPack = "Medieval|Elden"` tries Medieval's glyphs first, then Elden's, then Lucide. Medieval uses it to keep its dye-flask colour button while Elden shows a rune.
  - **`Core/AppPaths.cs`**: one place for the data folder. The `SMARTNOTES_DATA_DIR` environment variable overrides it, so screenshot / test runs never touch the real `%APPDATA%\SmartNotes`.
  - **README screenshots** are now real windows: a throw-away program (not in the repo) sets `SMARTNOTES_DATA_DIR` to a temp folder, opens actual `StickyNoteWindow`s with sample notes, opens their "…" menu and colour popup, turns spell check off and renders them.
- **v2.2.0** (2026-11-19, released):
  - **Issue #1, Win+D hides Desktop Stuck notes**: Show Desktop raises the shell desktop window (Progman / WorkerW) and makes it foreground, so notes at HWND_BOTTOM ended up behind it. `Core/Native/ShowDesktopWatcher.cs` (started in `App.OnStartup`) hooks `EVENT_SYSTEM_FOREGROUND` for other processes. When the desktop becomes foreground, it places stuck notes directly above the desktop window (and re-applies twice, 160 ms apart, for shell animations). When any other window does, it sends them back to the bottom. Verified with a real Win+D press in 2.3.1 and 2.4.0 (see those entries).
  - **Issue #2, reorder checklist items**: a six-dot grip on each task. Dragging it moves the task live (the ItemsControl holds the mouse capture, the target index comes from row midpoints, and the list auto-scrolls near the edges); saved on release. Disabled while the note is locked.
  - **Silent updates**: `UpdateService.StartAutomaticUpdates` checks 45 s after start, then every 6 h. It downloads and extracts a newer release into `%TEMP%\SmartNotes_Update` with no UI, and restarts into it only when the user has been idle ≥ 3 min (`Win32Api.GetIdleTime`) and no note is focused. The manual "Check for Updates…" keeps its dialogs. **v2.1.0 has no automatic check at all**, so existing users reach 2.2.0 only through the manual menu item; silent updating starts from 2.2.0.
  - The release workflow now also uploads `SmartNotes-win-x64.zip` (the README download link). Version bumped to 2.2.0 in the csproj.
  - The README was rewritten for the themes; images in `docs/images/` (rendered with the real painters off-screen).
- **Elden Ring colour button** (2026-11-17): the "flask of dye" glyph (`EldenIcon_palette`) is now a small Great Rune, a ring crossed by a staff with a diamond tip, since the button opens the rune picker. Astral shares it.
- **Skin fonts** are selectable: each `NoteSkin` lists `TitleFonts` / `TextFonts` (`SkinFontOption`: bundled family, weight, size correction). The choice is stored per skin in `AppSettings.SkinTitleFonts` / `SkinTextFonts`, picked in Settings > Note Appearance > Style Fonts, and resolved in `StickyNoteWindow.ApplySkinVisuals()`. To offer another font: drop a static (non-variable) TTF into `Assets/Fonts` and add one `SkinFontOption` line.

### Skins as of 2026-11-12 (evening)
- **Classic** – default; header button groups no longer boxed, even corners, no resize grip glyph.
- **Paper Sticky** (`Paper`, `PaperStickyArt`) – light sticky note in the note colour, dark ink, glue strip, folded corner.
- **Cyberpunk** (`Cyberpunk`, `CyberpunkArt`).
- **Elden Ring** (`EldenRing`, `EldenRingArt` + `RuneArt`) – the game's menu look, roughened 2026-11-13: murky olive panel with two-scale grain, dark mottling, mist banks, scratches and dust, grime along the edge, a chipped pale-gold double frame with notched corners, motes. A parchment variant was tried and rejected the same day: the user found it lost the Elden Ring feel (parchment lives on in **Medieval**).
- **Great Runes** (2026-11-13): in the Elden Ring skin the colour popup shows nine Great Runes instead of colours (Godrick, Radahn, Rykard, Morgott, Mohg, Malenia, Unborn/Rennala, Miquella, and the title-screen Elden Ring emblem). The choice is stored per note in `NoteItem.RuneKey`; `ColorKey` is kept for the other skins. `GreatRune` (Core/Models) holds the list and builds the note theme from the rune colour; `NoteSkin.UsesRunes` switches this on. `RuneArt` draws each rune as glowing embers into a cached bitmap with additive blending, bottom right of the note and on the picker buttons. It is a C# port of the renderer in `docs/references/great-runes-board.html`, where every shape and detail (petals, flames, star flares, thick/thin regions) was tuned with the user against in-game references; change both together. The runes are our own drawings; the user decided not to ship the game's images (2026-11-13) because the repository and releases are public.
- **Astral** (`Astral`, `AstralArt`) – started as the clean, pre-roughening dark Elden Ring look; themed in 2.3.0 (night sky, one constellation per note, its own `Astral` icon pack).
- **Westeros** (planned): reference board `docs/references/westeros-board.html` (surface direction, house colours for the picker, title font). Waiting for the user's picks; sigils to be drawn in code, no show logo or artwork.
- **Medieval** (`Medieval`, `EldenScrollArt`) – the parchment scroll that used to be called "Elden Ring". `App.MigrateSkinKeys()` renames stored `EldenRing` keys to `Medieval` once (`AppSettings.SkinDataVersion`).
- Painters are chosen by `NoteSkin.ArtKey`; the popup's style buttons are generated from `NoteSkin.All`; `SideResizeInset` / `SideResizeWidth` put the side resize zones on visible pixels of shaped skins.
- Franchise themes: build the look (shapes, palette, type, ornaments) from references, but do not ship a franchise's logo, artwork, screenshots or paid fonts — the repository and releases are public.
## TODO (open work as of 2026-11-12)

### Done on 2026-11-12
- Skin fonts: Elden Ring defaults to Grenze Gotisch + Eagle Lake, Cyberpunk to Orbitron + VT323; 17 extra faces bundled; font picker in Settings.
- App-wide UI overhaul: design tokens, WPF tray menu on the shared menu component, calmer Classic note (2px card border, 1px inner borders, mode icons and add buttons in the note colour), settings / dialogs / palette popup moved onto the tokens.
- Footer pin emoji replaced by a skin-aware icon.
- New app icon and the pick-board skill (earlier).

### Still open
- **Click through before a release** (what the end-to-end tests don't cover): tray menu opens at the cursor and closes on an outside click or Escape; three-dot and right-click menus in every skin; changing fonts in Settings and saving; switching skins on an open note; resizing shaped skins; Win+D with Desktop Stuck notes.
- Variable fonts are not usable in WPF: only static TTFs work as skin fonts.

## Changelog & Evolution

### Version 1.0.0
- **Initial Architecture & Framework**:
  - Implemented core WPF on .NET 10 project structure with Windows Forms tray integration.
  - Built `NoteStorageService` and `SettingsService` for `%APPDATA%\SmartNotes\` JSON persistence.
  - Implemented `DesktopWindowManager` for Win32 `HWND_BOTTOM` sticking and toolwindow behavior.
  - Created `StickyNoteWindow` with drag handle, title bar, multiline text editor, and interactive task checklist mode.
  - Created `NotesHubWindow` for centralized note search, management, and desktop arrangement.
  - Implemented system tray integration with `TrayManager` and global hotkey engine (`Win+Alt+N`, `Win+Alt+H`, `Win+Alt+D`).
  - Added vector Lucide icons (`LucideIcon.cs`, `LucideIcons.xaml`) and 8 neon color themes.
  - Generated multi-resolution `app.ico` and `app.png` vector assets via `generate_icon.py`.

### Version 1.1.0
- **True Rounded Corners & Black Rectangle Elimination**:
  - Removed separate outer shadow border (`NoteShadowBorder`) that caused a black rectangular artifact behind rounded corners in WPF layered transparent windows (`AllowsTransparency="True"`).
  - Switched to a clean single-border outer frame (`NoteCardBorder`) with `CornerRadius="16"` and `ClipToBounds="True"`, ensuring seamless, true rounded glass corners.
- **Thickened 3px Vector Borders & Line Weight (Anti-Pixelation)**:
  - Upgraded all outer and inner borders across the entire app from `1.5px` to **`3px`** (and **`2.5px`** for card items, checklist borders, and switches).
  - Increased Lucide vector icon stroke thickness to **`2.6` – `2.8`**, delivering razor-sharp, crisp vector graphics on high-DPI displays.
- **Header Simplification**:
  - Removed the 3 center dots from the note header bar, creating a clean, unobstructed drag surface.
- **Enlarged Typography System-Wide**:
  - Increased note body text to **`16px`** by default.
  - Increased note titles to **`16.5px Bold`**.
  - Increased checklist item text to **`15.5px`**.
  - Scaled up global Window base font to **`15.5px`** and TextBlock base to **`15px`**.
  - Upgraded Notes Hub headings, search bar, and status labels to **`15px – 21px`** for optimal clarity and readability.
- **Resolved WinForms & WPF Namespace Ambiguities**:
  - Created `GlobalUsings.cs` to cleanly alias WPF `Button`, `TextBox`, `CheckBox`, `MenuItem`, `Color`, `Brushes`, `MessageBox`, and `Clipboard`, preventing compiler conflicts when linking WinForms `NotifyIcon`.

### Version 1.1.1 - Linked Group Movement & Stack Leader Mechanics
- **Classic 2D Drawing-App Color Picker & Popup Redesign**:
  - Replaced the single-slider color picker with a professional 2D Saturation / Value gradient canvas + Hue spectrum bar (identical to Figma, Photoshop, Paint.NET, and Procreate).
  - Eliminates awkward empty space on the right of the palette popup with a clean $220\text{px}$ symmetrical layout.
  - Interactive crosshair target thumb on the 2D canvas allows full 0-100% saturation and brightness adjustment with live mouse dragging.
  - Rainbow spectrum track enables fluid 0-360° hue selection with instant real-time swatch preview, Hex code input (`#RRGGBB`), and 8 neon presets in a centered $4\times 2$ grid.
- **Topmost Stack Leader Drag Handle (`NoteClusterEngine.GetStackLeader`)**:
  - In any connected stack or cluster of docked notes, only the **highest point (topmost note)** acts as the leader handle to drag the entire cluster.
  - **Natural Single-Note Peeling / Separation**: Dragging any lower or subordinate note in the stack performs normally, moving *only* that single note and allowing users to effortlessly pull, peel, and separate notes away from the group with standard dragging (zero modifier keys required).
- **Strict Exterior Edge Docking (`NoteClusterEngine.AreNotesAdjacent`)**:
  - Refined adjacency algorithms to evaluate strict exterior perimeter contact ($6\text{px}$ threshold with $\ge 20\text{px}$ collinear overlap), completely ignoring internal overlaps or stacked Z-order intersections.
- **Breakaway Modifier Support**:
  - `Alt` or `Ctrl` key remains supported as an instant breakaway override when dragging any note.
- **New Note Spawn Z-Order & Smart Adjacent Placement**:
  - Resolved issue where newly spawned notes (`[ + ]` button) were placed underneath existing notes due to `OnWindowLoaded` overriding `BringToFront()` with `HWND_BOTTOM`.
  - New notes now spawn in front with active focus (`startInForeground = true`), docked immediately adjacent to the right of the active note (or cascaded if reaching screen bounds).
- **Physical Pixel & High-DPI Coordinate Alignment**:
  - Replaced DIP bounding calculations with native Win32 `GetWindowRect` and `TransformFromDevice` matrices, guaranteeing precise edge contact detection and jitter-free multi-window translation across all monitor scale factors (100%, 125%, 150%, 200%).
- **Resize Grip Inward Repositioning & Obsidian Styling**:
  - Added dedicated `ResizeGrip` styling in `DarkTheme.xaml` with `Margin="0,0,10,10"`, shifting the bottom-right resize symbol 10px up and 10px left cleanly inside the window's rounded corner geometry.
  - Implemented crisp vector diagonal grip lines (`1.5px` stroke with round caps) matching the obsidian muted text palette (`#7888A4`) and neon amber hover glow.
- **Notes Hub Initialization & Event Lifecycle Fix**:
  - Resolved `NullReferenceException` in `NotesHubWindow` by setting dependencies (`_storageService`, `_settingsService`, etc.) prior to `InitializeComponent()`, preventing premature XAML `Checked` event triggers from accessing uninitialized services.
  - Added null-safety guards in `RefreshNotesList()` and `TxtSearch_TextChanged`.
- **Outer Border Thickened to 5px & Black Pixel Artifact Elimination**:
  - Upgraded outer window borders across `StickyNoteWindow`, `NotesHubWindow`, and `SettingsDialog` from 3px to **`5px`** (`BorderThickness="5"` with `CornerRadius="16"`).
  - Eliminated black pixels outside the rounded outer line and transparent wallpaper gaps on the inside by rendering the 5px border with an internal 1px content overlap (`Margin="-1"` on child grid with `CornerRadius="12,12,0,0"` header).
  - The outer perimeter is now 100% drawn by the vibrant neon/obsidian `BorderBrush`, guaranteeing razor-sharp, artifact-free rounded corners on high-DPI displays.

### Version 1.2.0
- **Dedicated Top Header Compartment for Quick Copy**:
  - Introduced modular, rounded obsidian compartments (`BorderThickness="2"`, `CornerRadius="8"`) across the sticky note top header bar:
    1. **Left Compartment**: `[ + New Note ]` and `[ 📌 Pin / Stick Mode ]`.
    2. **Separated Copy Compartment**: Dedicated capsule with `[ 📋 Copy Note ]` button supporting 1-click clipboard copying and global `Ctrl+Shift+C` shortcut.
    3. **Tools Compartment**: Color theme picker `[ 🎨 ]`, Checklist/Text mode toggle `[ 📝 ]`, and Position Lock `[ 🔒 ]`.
    4. **Window Actions**: More menu `[ ⋯ ]` and Close note `[ ✕ ]`.
  - Added instant visual feedback animation on copy: the icon transitions dynamically to a glowing neon emerald checkmark (`✓`) with updated tooltip before smoothly resetting after 1.5 seconds.
  - Automatically handles both rich plain/markdown text notes and formatted todo task checklists (`- [x] Done` / `- [ ] Pending`) with title preservation.
  - All compartment borders dynamically adopt the active neon color theme (Amber, Emerald, Violet, Cyan, Rose, Obsidian, Gold, Mint).
- **System Tray Icon Scaled & High-DPI Optimization**:
  - Maximized squircle usable canvas area from 90px down to **18px** margin, scaling the sticky note pad and glowing pin geometry to fill over 96% of the icon frame.
  - Added native pre-resampled **20x20** (125% DPI) and **24x24** (150% DPI) raster frames to `Assets/app.ico`.
  - Configured `TrayManager.cs` to request `SystemInformation.SmallIconSize`, ensuring crisp, bold, and prominent display in the Windows system tray alongside all other tray icons.

### Version 1.2.1
- **Interactive Copy Compartments Mode (Per-Line Copy Buttons)**:
  - Added a 3rd note mode: **Copy Compartments / Quick Snippets** (`NoteViewMode.CopyCompartments`).
  - Implemented dedicated top toolbar mode switcher:
    - `[ 📝 Plain Text ]` (`BtnTextMode`, Amber highlight)
    - `[ ☑️ Checklist Tasks ]` (`BtnChecklistMode`, Emerald highlight)
    - `[ 📋 Copy Compartments ]` (`BtnCopyMode`, Cyan highlight)
  - In Copy Compartments mode, each row is a distinct separated card containing:
    1. Snippet text input box.
    2. **Dedicated `[ 📋 ]` Copy button directly on the right in the same line**. Clicking it instantly copies that line's text to the clipboard and animates the icon to a green neon checkmark (`✓`) for 1.2 seconds.
    3. `[ ✕ ]` Delete button.
  - Added bottom input field `[ + Add new copy item (Press Enter)... ]` with green `[ + ]` button to rapidly append new snippet rows.
### Version 1.1.0 - The Magnetic Sticking & Docking Update (2026-09-19)
- **Magnetic Sticky Note Snapping & Docking (`MagneticSnapEngine.cs`)**:
  - Implemented real-time hardware-accelerated magnetic snapping via Win32 `WM_MOVING` (0x0216) window message hooks.
  - When dragging any sticky note near another note on the desktop, notes magnetically "click" and snap together:
    - **Edge Sticking**: Snaps Left-to-Right, Right-to-Left, Top-to-Bottom, and Bottom-to-Top flush.
    - **Edge Alignment**: Top-to-Top, Bottom-to-Bottom, Left-to-Left, and Right-to-Right edge alignments.
    - **Center Guideline Snapping**: Centers automatically align along both Horizontal and Vertical center axes.
    - **Desktop Screen Edge Snapping**: Snaps to monitor work area boundaries (Left, Top, Right, Bottom).
  - **Fluid Breakaway / Unsnapping**: Calculating magnetic displacement relative to the physical mouse cursor position (`GetCursorPos`) ensures that pulling the cursor slightly past the snap threshold (22px) smoothly releases and unsnaps the note with zero sticking or trapping.
- **Desktop Sticking & HWND_BOTTOM Z-Order Architecture**:
  - Notes configured in `DesktopStuck` mode remain permanently on the desktop wallpaper (`HWND_BOTTOM`), never occluding open applications on startup or when working in other programs.
  - Interactive activation brings the active note forward during typing/dragging and immediately restores bottom placement upon focus loss.
- **Custom Neon Color Picker**:
  - Added a 9th custom rainbow gradient circle with built-in 0–360° Hue slider, Hex code input (`#RRGGBB`), real-time swatch preview, and dynamic obsidian palette generation (`NoteColorTheme.FromCustomHex`).
- **Live Typing & Auto-Save Indicator**:
  - Added new `LucideIcon_pen-line` vector geometry resource in `LucideIcons.xaml`.
  - Added dynamic `TypingIndicatorContainer` to `StickyNoteWindow.xaml` bottom footer row.
  - Displays `✏️ Typing...` matching the active neon theme glow accent while typing, and transitions smoothly to `✓ Saved` upon completion.
- **Interactive Entry Box Focus & Caret Fixes**:
  - Blinking glowing caret (`|`) enabled across all text and entry boxes with unfrozen brushes.
  - Placeholders automatically hide upon focusing any entry box (`GotFocus`) and reappear only on `LostFocus` if empty.
- **RAM Optimization & Safe Resource Management**:
  - Removed separate `NotesHubWindow` background manager, saving system resources.
  - Embedded Settings directly into every note's 3-dot menu (`BtnMore`) for instant access without a separate dashboard window.
  - Replaced aggressive working-set purging with safe idle garbage collection cycles.

### Version 1.1.3 - Stack Leader Docking, 2D Drawing Color Picker & Auto-Updater (2026-09-20)
- **Automatic GitHub Update Engine (`UpdateService.cs`)**:
  - Automatically queries `https://api.github.com/repos/OlfJD/SmartNotes/releases/latest` on application startup.
  - Compares semantic versioning (`latestVer > CurrentVersion`) and prompts the user with an intuitive update modal and release notes.
  - Performs 1-click downloading, temporary extraction, non-blocking batch script swap, and automated process restart.
  - Added manual "Check for Updates..." triggers in both the System Tray context menu (`TrayManager.cs`) and Note 3-dot overflow menu (`StickyNoteWindow.xaml.cs`).
- **Topmost Stack Leader Drag Handle (`NoteClusterEngine.cs`)**:
  - Docked notes form dynamic spatial clusters based on physical edge adjacency.
  - Only the **highest note (Stack Leader)** moves the entire cluster when dragged by its header bar.
  - Lower and middle notes peel and detach away independently with standard cursor movement, eliminating cluster locking traps.
- **Classic 2D Saturation / Value Color Canvas + Hue Spectrum**:
  - Replaced single-slider color picker with a professional 2D Saturation/Value gradient canvas and 1D Hue spectrum track.
  - Real-time crosshair drag tracking, live swatch preview, #Hex input, and 8 centered Obsidian neon presets.
- **Smart Note Placement & Z-Order Fix**:
  - New notes (`+`) spawn in the foreground adjacent to the active note without getting obscured behind existing windows.
- **High-DPI Coordinate Normalization & 5px Vector Borders**:
  - Replaced DIP calculations with Win32 physical pixel rectangles, ensuring jitter-free docking on multi-monitor setups with mixed DPI scaling.
  - Thickened outer window frame to 5px with anti-artifact inner overlap, completely eliminating black pixels outside rounded corners.
- **GitHub Preview Banner & Documentation**:
  - Added full visual preview screenshot (`Assets/preview.png`) to `README.md` showcasing Note Modes and Stack Leader docking mechanics.

### Version 1.1.4 - RAM Footprint Optimization & 48-Hour Temporary Trash Recovery (2026-09-20)
- **Ultra-Low Physical RAM Footprint & Working Set Engine (`MemoryOptimizer.cs`)**:
  - Implemented native Win32 `SetProcessWorkingSetSize` P/Invoke (`kernel32.dll`), instructing the Windows kernel to purge unreferenced working set pages from physical RAM.
  - Comprehensive Gen-2 garbage collection pipeline with Large Object Heap (LOH) compaction (`GCSettings.LargeObjectHeapCompactionMode = GCLargeObjectHeapCompactionMode.CompactOnce`) and finalizer queue drainage (`GC.WaitForPendingFinalizers()`).
  - Configured Workstation GC (`<ServerGarbageCollection>false</ServerGarbageCollection>`, `<ConcurrentGarbageCollection>true</ConcurrentGarbageCollection>`) in `SmartNotes.csproj` for minimal baseline heap allocation.
  - Automatic scheduled memory trim runs 2 seconds after startup (after JIT compilation & XAML parsing settle), on every note window close/delete, and on a 60-second low-priority application idle dispatcher timer.
  - Reduced idle memory footprint from **~72 MB down to ~15 MB – 20 MB** (and down to **< 2 MB** in deep idle).
- **Frozen WPF Resource & Brush Architecture (`NoteColorTheme.cs`, `DarkTheme.xaml`, `StickyNoteWindow.xaml.cs`)**:
  - Pre-created and frozen (`.Freeze()`) all static and dynamic theme brushes (`BgBrush`, `HeaderBgBrush`, `BorderBrush`, `PrimaryBrush`, `GlowBrush`, `TextPrimaryBrush`, `TextMutedBrush`).
  - Added `xmlns:po="http://schemas.microsoft.com/winfx/2006/xaml/presentation/options"` and `po:Freeze="True"` across `DarkTheme.xaml` palettes.
  - Eliminated repetitive `SolidColorBrush` allocations and WPF dependency change-tracking listener overhead.
- **48-Hour Temporary Trash & Note Recovery Subsystem (`NoteStorageService.cs`)**:
  - Introduced local disk trash storage at `%APPDATA%\SmartNotes\trash\`.
  - When notes are deleted (`[ ✕ ]` or "Delete Note"), they are safely archived to `%APPDATA%\SmartNotes\trash\note_{id}.json` with `DeletedAt` timestamps and offloaded from active in-memory collections, saving RAM.
  - Built automatic 48-Hour Time-To-Live (TTL) auto-purge (`PurgeExpiredTrash()`), automatically deleting archived files older than 48 hours on startup and during trash operations.
  - Automatic migration on load moves any legacy deleted notes from `notes.json` into the 48h temporary trash directory.
- **System Tray "Recently Deleted (48h)" Context Submenu (`TrayManager.cs`)**:
  - Added dedicated **"🗑️ Recently Deleted (X)"** submenu to the Windows notification area tray icon:
    - Lists each deleted note by title or preview snippet with dynamic time remaining (e.g. `Restore: "Meeting Notes" (47h left)`). Clicking instantly restores the note to the desktop with all original coordinates, modes, and styling.
    - **"↺ Restore All Notes"**: 1-click batch recovery of all deleted notes.
    - **"🗑️ Empty Trash Now"**: Permanently wipes all temporary trash files on demand.
    - **"📁 Open Trash Folder in Explorer..."**: Opens `%APPDATA%\SmartNotes\trash\` directly in Windows File Explorer.
    - Added instant **"⚡ Optimize Memory (RAM)"** tray action with balloon tip feedback.
- **Settings & Preferences Dashboard Integration (`SettingsDialog.xaml` / `.xaml.cs`)**:
  - Added **"TEMPORARY TRASH & RECOVERY (48H RETENTION)"** section displaying live trash counts, "Open Trash Folder in Explorer", and "Empty Trash Now".
  - Added **"MEMORY & PERFORMANCE"** card displaying live physical working set metrics in MB with 1-click "⚡ Optimize RAM Now" button.
- **Self-Healing Windows Autostart Registration & System32 Working Directory Hardening**:
  - Dynamic binary path resolution with automatic registry synchronization on settings load/save.
  - Hardened crash logging to `AppDomain.CurrentDomain.BaseDirectory` / `%APPDATA%`, preventing working directory permission failures when Windows boots from `C:\Windows\System32`.

### Version 1.1.5 - Multi-Directional 8-Way Window Resizing, Unfocused Auto-Transparency & Appearance Controls (2026-09-21)
- **8-Way Multi-Directional Window Resizing Overlays (`StickyNoteWindow.xaml`, `StickyNoteWindow.xaml.cs`, `Win32Api.cs`)**:
  - Added dedicated perimeter hit-zones (`ResizeOverlayGrid`) on all 4 borders (`Top`, `Bottom`, `Left`, `Right`) and 4 corners (`TopLeft`, `TopRight`, `BottomLeft`, `BottomRight`).
  - Integrated native Win32 `WM_SYSCOMMAND` + `SC_SIZE` (`0xF001` - `0xF008`) modal resizing loops via `ReleaseCapture` and `SendMessage`.
  - Full bidirectional cursor feedback (`SizeNS`, `SizeWE`, `SizeNWSE`, `SizeNESW`) across all outer edges.
  - Resizing hit-zones automatically disable when notes are locked (`IsLocked`).
- **Unfocused Auto-Transparency & Hover Preview Animation (`StickyNoteWindow.xaml.cs`, `AppSettings.cs`, `SettingsDialog.xaml`)**:
  - Implemented automatic smooth opacity transitions on focus change (`Activated` / `Deactivated`).
  - When notes lose input focus, they smoothly fade to translucent opacity (`55%` of configured opacity) via hardware-accelerated WPF `DoubleAnimation` easing curves.
  - Returning focus instantly and smoothly restores full opaque/configured opacity.
  - Hovering mouse over an unfocused note temporarily brightens the note for reading without requiring input activation.
  - `SaveNoteState()` maintains `_userConfiguredOpacity` separately to ensure saving while unfocused never permanently lowers the note's base opacity.
- **Integrated Transparency & Dimming Customization Controls (`StickyNoteWindow.xaml`, `SettingsDialog.xaml`, `DarkTheme.xaml`)**:
  - Integrated dedicated **"Transparency & Dimming"** section directly inside the **Colors & Themes Popup**:
    - **Note Opacity Slider**: Directly tune active note opacity (30% - 100%) with live percentage readout.
    - **Dim When Unfocused Switch**: Quickly toggle unfocused auto-dimming on/off per note context.
    - **Unfocused Opacity Slider**: Tune inactive dim factor (15% - 90%) with instant live preview across all desktop notes.
  - Added Unfocused Opacity slider to **Settings & Preferences** modal.
  - Added Dim When Unfocused toggle in note header **More Options (`...`)** `Opacity` submenu.
  - Built custom vector `ModernSlider` style in `DarkTheme.xaml`.

### Version 2.0.0 - Native Language Proofing, Live Autocorrect Engine & Smart Typography (2026-09-28)
- **Native WPF & Windows Spell Checking Engine (`TextProofingService.cs`, `StickyNoteWindow.xaml`, `StickyNoteWindow.xaml.cs`)**:
  - Integrated real-time red squiggly underlines on misspelled words across note body text (`TxtContent`), titles (`TxtTitle`), checklist items, and quick-copy snippet fields.
  - Multi-language proofing support with 12 international languages (`en-US`, `en-GB`, `en-CA`, `en-AU`, `de-DE`, `es-ES`, `fr-FR`, `it-IT`, `pt-PT`, `nl-NL`, `pl-PL`, `sv-SE`).
  - Persistent custom user dictionary at `%APPDATA%\SmartNotes\custom_dict.lex` with seamless "Add to Dictionary" and "Ignore All" context menu actions.
- **Real-Time Autocorrect Engine (`TextProofingService.cs`, `AutocorrectRule.cs`)**:
  - Instant typo fixes as you type (e.g., `teh` → `the`, `dont` → `don't`, `cant` → `can't`, `recieve` → `receive`, `seperate` → `separate`, `definately` → `definitely`, `goverment` → `government`, etc.) with intelligent casing preservation (`TEH` → `THE`, `Teh` → `The`, `teh` → `the`).
  - Automatic sentence capitalization: automatically capitalizes the first letter of sentences and standalone `i` → `I`.
  - Smart typography symbol replacements (`->` to `→`, `<-` to `←`, `=>` to `⇒`, `!=` to `≠`, `--` to `—`, `---` to `—`, `(c)` to `©`, `(r)` to `®`, `(tm)` to `™`, `+-` to `±`, `...` to `…`, `1/2` to `½`, `1/4` to `¼`, `3/4` to `¾`, `>=` to `≥`, `<=` to `≤`).
  - Instant Backspace Undo UX: Pressing `Backspace` immediately after an autocorrect reverts the word back to what was originally typed.
  - Custom user autocorrect rules loaded and saved to `%APPDATA%\SmartNotes\autocorrect_rules.json`.
- **Obsidian Dark Theme Context Menus (`DarkTheme.xaml`, `StickyNoteWindow.xaml.cs`)**:
  - High-contrast neon dark right-click context menu displaying top spelling suggestions with checkmark accents, "Add to Dictionary", "Ignore All", Cut/Copy/Paste/Select All, and nested Proofing & Language configuration submenus.
  - Razor-sharp vector Lucide icons for all menu commands.

### Version 2.0.1 - Complete Settings & Preferences Dashboard Overhaul (2026-09-28)
- **Modern Sidebar Rail & Categorized Layout (`SettingsDialog.xaml`, `SettingsDialog.xaml.cs`)**:
  - Completely redesigned the settings experience from a single cramped scroll list to an expansive modern dashboard (800x600).
  - Added a dedicated **Left Sidebar Navigation Rail** (Width: 220px) with custom glowing active indicator pills and razor-sharp Lucide vector icons:
    1. 🖥️ **Desktop & System** (`monitor`, Amber accent)
    2. 🎨 **Note Appearance** (`palette`, Emerald accent)
    3. ⌨️ **Global Hotkeys** (`keyboard`, Cyan accent)
    4. ✍️ **Text Proofing** (`globe`, Violet accent)
    5. 🗑️ **Trash & Recovery** (`trash-2`, Rose accent)
    6. ⚡ **Memory & Performance** (`cpu`, Amber accent)
- **Note Appearance Visual Palette & Real-Time Note Preview**:
  - Integrated an interactive 8-theme visual color swatch selector (Amber, Emerald, Violet, Cyan, Rose, Obsidian, Gold, Mint) directly alongside the theme combobox.
  - Implemented a live **Note Card Preview** right within the settings panel that immediately renders dynamic theme border colors, header backgrounds, font sizing, and typography as settings are adjusted.
- **Custom Dark Theme Controls Engine (`DarkTheme.xaml`)**:
  - Implemented full custom Obsidian Dark styles for `ComboBox`, `ComboBoxItem`, `ToggleButton`, and `ModernSwitch`, eliminating bright default Win32 controls.
  - Features obsidian surfaces (`#131824`), glowing focus/hover borders (`#FBBF24`), and smooth slide popups.

### Version 2.0.2 - Context Menu Interception, Alt+Space Fix & ClearType High-DPI Engine (2026-09-29)
- **Obsidian Context Menu Tunneling Interception (`StickyNoteWindow.xaml`, `StickyNoteWindow.xaml.cs`)**:
  - Attached `PreviewMouseRightButtonDown` and `PreviewMouseRightButtonUp` tunneling event handlers on all note textboxes.
  - Setting `e.Handled = true` in tunneling right-click completely intercepts and prevents WPF's internal `TextEditorSpelling` from ever popping up its default white Win32 menu.
- **Alt+Space Windows System Menu Suppression (`StickyNoteWindow.xaml.cs`, `DesktopWindowManager.cs`)**:
  - Intercepted `WM_SYSCOMMAND` with `SC_KEYMENU` (`0xF100`) inside `DesktopWindowManager.WndProc`.
  - Suppressed `Alt+Space` and `e.SystemKey == Key.Space` in `PreviewKeyDown`, eliminating the unwanted default Windows titlebar system menu.
- **Ultra-HD ClearType Subpixel Rendering Engine (`DarkTheme.xaml`, `LucideIcon.cs`)**:
  - Decoupled drop shadow rasterization from solid content containers, enabling full RGB subpixel `ClearTypeHint="Enabled"` across transparent context menus and submenus.
  - Enforced `TextOptions.TextFormattingMode="Display"` and `TextOptions.TextHintingMode="Fixed"` to guarantee razor-sharp text on 96 DPI / 1080p monitors.
  - Enforced `SnapsToDevicePixels="True"` and `UseLayoutRounding="True"` across all Lucide vector canvas, path, and viewbox hierarchies.
  - Replaced menu separators with crisp 1px device lines (`#222D42`).

### Version 2.0.3 - Modern Obsidian Alert Dialogs, Thread-Safe Updater & Globe Vector Icons (2026-09-29)
- **Modern Obsidian MessageBox Dialog (`ModernMessageBox.xaml`, `ModernMessageBox.xaml.cs`)**:
  - Designed and built a custom dark Obsidian modal replacing all standard white Win32 `MessageBox.Show` dialogs across the app.
  - Features animated neon badge icons (`sparkles` for updates, `check-check` for success, `zap` for alerts, `eye-off` for warnings), draggable glass header bar with close button, and styled Obsidian Amber/Emerald action buttons.
  - Supports keyboard shortcuts (`Enter` to confirm, `Escape` to dismiss/cancel).
- **Thread-Safe UI Dispatcher Marshaling (`ModernMessageBox.xaml.cs`, `UpdateService.cs`)**:
  - Implemented automatic UI thread marshaling (`Application.Current.Dispatcher.Invoke`) inside `ModernMessageBox.Show`, allowing background async update tasks and thread pool workers to safely launch dialogs without cross-thread exceptions.
- **Razor-Sharp Globe Vector Icon (`LucideIcons.xaml`, `StickyNoteWindow.xaml.cs`, `SettingsDialog.xaml`)**:
  - Replaced the cramped `spell-check` icon in context menus and settings navigation with the official international `globe` icon with fine-tuned stroke thickness (`1.9`) for maximum visual clarity.
- **Dynamic Windows Spell Check Dictionary Detection (`TextProofingService.cs`, `SettingsDialog.xaml.cs`)**:
  - Added `GetInstalledSpellCheckLanguages()` and `IsLanguageSupported(langCode)` in `TextProofingService` querying Windows COM `ISpellCheckerFactory`.
  - Added live status indicators and guidance in Settings explaining how Windows Language packs activate proofing dictionaries.
- **Application Metadata & Version Synchronization (`SmartNotes.csproj`, `UpdateService.cs`, `TrayManager.cs`, `StickyNoteWindow.xaml.cs`)**:
  - Synchronized application versioning to `2.0.3` across project assembly attributes and updater menus.

### Version 2.0.4 - Zero-RAM Multi-Language Architecture & Background Memory Trimming (2026-09-29)
- **Zero-RAM Multi-Language & Spell Proofing Architecture**:
  - Clarified and documented memory mechanics: Language packs and dictionaries installed in Windows (or localized resource files) remain dormant on disk in `C:\Windows\System32\` and take **0 MB of RAM** when inactive.
  - Windows Spell Checking only initializes the lexicon for the note's active language tag (`de-DE` or `en-US`) when actively typing, ensuring multi-language support has zero RAM penalty.
- **Automatic Background Memory Trimming Engine (`MemoryOptimizer.cs`, `AppSettings.cs`)**:
  - Implemented real Win32 `EmptyWorkingSet(process.Handle)` and .NET Large Object Heap (LOH) compaction (`GCSettings.LargeObjectHeapCompactionMode = CompactOnce`).
  - Added background idle timer (`DispatcherTimer`) that automatically flushes unreferenced memory pages every 3 minutes when SmartNotes is idle.
  - Added persistent `AutoOptimizeMemory` boolean setting in `AppSettings.cs`.
- **Modern Settings Memory Management UI (`SettingsDialog.xaml`, `SettingsDialog.xaml.cs`)**:
  - Replaced the confusing static button with an **"Automatic Background RAM Trimming"** modern switch toggle (`ChkAutoTrimMemory`).
  - Added a dedicated "Trim RAM Now" interactive action with real-time Before / After / Freed MB telemetry calculation.
  - Added clear architectural callout explaining the Zero-RAM idle model for language dictionaries.
- **Accurate System Tray RAM Balloon Notifications (`TrayManager.cs`)**:
  - Updated tray "Optimize Memory (RAM)" action to calculate and report the exact amount of physical memory freed (e.g. `Freed 42.1 MB! Current usage: 28.4 MB`) instead of generic/misleading text.

### Version 2.1.0 - Full App-Wide Localization, Obsidian Dark ToolTips & Startup Language Setup (2026-09-29)
- **Zero-RAM Multi-Language Subsystem (`LocalizationService.cs`)**:
  - Comprehensive bilingual support for **English (`en`)** (Default) and **German (`de`)** (Deutsch).
  - Extended translations across the entire application: all 6 panels in Settings, sidebar navigation, headers/footers, note pin modes (*"📌 Am Desktop fixiert"*, *"📌 Immer im Vordergrund"*), buttons, placeholders, right-click proofing menus, updater dialogs, and tray notifications.
  - Zero RAM penalty: dictionaries stay lightweight and load on demand.
- **Real-Time Dynamic UI Translation (`SettingsDialog.xaml`, `SettingsDialog.xaml.cs`)**:
  - Changing the UI language in Settings immediately updates all visible text blocks, switch descriptions, slider labels, status badges, and action buttons in real time without requiring an application restart.
- **Custom Obsidian Dark ToolTips (`DarkTheme.xaml`)**:
  - Replaced the default Windows Win32 white square hover tooltips with an implicit WPF `<Style TargetType="{x:Type ToolTip}">`.
  - Styled with acrylic deep dark background (`#131824`), sleek border (`#2C3D5E`, 1.5px), rounded corners (`CornerRadius="8"`), high-contrast ClearType typography, and soft ambient drop shadow.
- **First-Run & Version 2.1 Update Language Prompt Modal (`LanguagePromptDialog.xaml`, `LanguagePromptDialog.xaml.cs`)**:
  - Implemented an Obsidian Dark modal prompt that triggers on first application launch or when upgrading to Version 2.1.
  - Automatically queries Windows system culture (`CultureInfo.InstalledUICulture`). If the user's OS is German (or non-English), it prompts:
    - *"Switch to German (Deutsch)"* (switches UI and proofing dictionary immediately)
    - *"Keep English (Default)"*
  - Persists preference and records `LastLanguagePromptVersion = "2.1.0"` in `AppSettings.cs` to avoid nagging.
- **Full Updater & Modal Dialog Localization (`UpdateService.cs`, `ModernMessageBox.xaml.cs`)**:
  - Localized all update notices ("SmartNotes is Up to Date!", "New Version Available!", etc.) and button actions (*"Got It" / "Verstanden"*, *"Yes, Update" / "Ja, Jetzt aktualisieren"*, *"Cancel" / "Abbrechen"*).
- **Full Sticky Note Context Menu & More Options Localization (`StickyNoteWindow.xaml.cs`, `LocalizationService.cs`)**:
  - Localized the entire sticky note header "More Options" (`...`) context menu:
    - Font Size submenu: *"Font Size ({0}pt)"* / *"Schriftgröße ({0}pt)"*, *"Increase Font Size (+)"* / *"Schriftgröße vergrößern (+)"*, *"Decrease Font Size (-)"* / *"Schriftgröße verkleinern (-)"*, *"Reset Font Size (16pt)"* / *"Schriftgröße zurücksetzen (16pt)"*.
    - Opacity submenu: *"Opacity ({0}%)"* / *"Deckkraft ({0}%)"*, preset swatches (*"100% Solid"* / *"100% Vollständig sichtbar"*, *"90% Crisp"* / *"90% Klar"*, *"80% Glass"* / *"80% Glas"*, *"65% Translucent"* / *"65% Durchscheinend"*, *"50% Stealth"* / *"50% Dezent"*), and unfocused dimming toggle (*"Dim When Unfocused"* / *"Bei Inaktivität abdunkeln"*).
    - Text proofing submenu & toggle items (*"Spell Check Underlines"* / *"Rechtschreibprüfung"*, *"Autocorrect Common Typos"* / *"Häufige Tippfehler korrigieren"*, *"Auto-Capitalize Sentences"* / *"Satzanfänge großschreiben"*, *"Smart Symbols: -> to →"* / *"Sonderzeichen: -> zu →"*, *"Proofing Language ({0})"* / *"Wörterbuch-Sprache ({0})"*).
    - Note actions: *"Copy All Content (Ctrl+Shift+C)"* / *"Gesamten Inhalt kopieren (Strg+Umschalt+C)"*, *"Export as Markdown (.md)"* / *"Als Markdown exportieren (.md)"*, *"Duplicate Note"* / *"Notiz duplizieren"*, *"Clear Completed Tasks"* / *"Erledigte Aufgaben entfernen"*, *"Settings & Preferences..."* / *"Einstellungen & Optionen..."*, *"Check for Updates..."* / *"Nach Updates suchen..."*, *"Delete Note"* / *"Notiz löschen"*.
  - Localized all color popup controls, theme preset tooltips, live modified time indicators (*"Just now"* / *"Gerade eben"*, *"Xm ago"* / *"Vor X Min."*, *"Xh ago"* / *"Vor X Std."*), typing/saved badges, markdown export dialogs, and system tray balloon alerts.




### Unreleased (after 2.1.0) - Data Safety & Stability Fixes (2026-11-04)
- **Notes could be wiped by a damaged `notes.json` (`Core/Services/NoteStorageService.cs`)**: saves overwrote the file in place, so a crash, forced kill or power loss mid-write left it truncated. On the next launch the parse failed, the list was empty, and `Load()` treated that as a first run and **overwrote the file with the welcome notes**, so every note was lost.
  - Saves are now atomic (`Core/Services/AtomicFile.cs`: write `.tmp`, then `File.Replace`) and keep the previous version as `notes.json.bak`.
  - If `notes.json` can't be read, it's moved aside as `notes.corrupt-<timestamp>.json` (never overwritten), and notes are recovered from `notes.json.bak` when possible.
  - Welcome notes are only created on a genuine first run. Before, they also came back 48 h after the user deleted every note and the trash expired.
  - Trash files and `settings.json` use the same atomic write.
- **Last edits lost on exit**: changes made within the 400 ms save debounce were dropped when quitting from the tray, updating, or shutting down Windows. `App.OnExit` now flushes every note's pending save (`StickyNoteWindow.FlushPendingSave`).
- **Updater killed the app mid-save**: `apply_update.bat` force-killed SmartNotes 1 s after launch. It now waits up to ~15 s for a clean exit and only force-kills as a last resort.
- **Updater version can't drift**: `UpdateService.CurrentVersion` is read from the assembly version (csproj `<Version>`) instead of a hard-coded `2.1.0` that had to be bumped by hand.
- **Tray menu leak (`Core/Native/TrayManager.cs`)**: every right-click and state change built a new `ContextMenuStrip` and two fonts without disposing the old ones. Because the rebuild ran *after* WinForms had already opened the old menu, it was also wasted work. There's now one menu that repopulates itself in `Opening`, so it's always current.
- A stray UI exception no longer crashes the app (`DispatcherUnhandledException` is logged and marked handled), so unsaved notes survive it.
- `startup.log` (appended on every launch) is reset once it exceeds 512 KB and now includes the date.
