# SmartNotes - Developer Log & Technical Specification

## Project Overview
SmartNotes is a high-performance, lightweight Windows desktop sticky notes application built with C# and WPF on .NET 10. It is engineered to live permanently on the desktop canvas below all other open applications, games, and windows, featuring the signature Obsidian Dark + Neon design language of **MacroMaster** and **SoundSwitcher**.

---

## Architecture & Subsystems

1. **Desktop Sticking & Native Win32 Subsystem (`Core/Native/`)**:
   - `Win32Api.cs`: P/Invoke declarations for `user32.dll` and `dwmapi.dll` (`SetWindowPos`, `GetWindowLong`, `SetWindowLong`, `RegisterHotKey`, `UnregisterHotKey`, `DwmSetWindowAttribute`).
   - `DesktopWindowManager.cs`: Manages window Z-order enforcement and `HwndSource` message hooks.
     - Automatically asserts `HWND_BOTTOM = (IntPtr)1` on `WM_ACTIVATE` (inactive) and `WM_KILLFOCUS`.
     - Traps `WM_WINDOWPOSCHANGING` so when foreground programs receive focus, note windows stay pinned below them.
     - Applies `WS_EX_TOOLWINDOW` via `GWL_EXSTYLE` to prevent taskbar and Alt+Tab clutter while remaining 100% interactive on the desktop surface.
   - `WindowBlurHelper.cs`: Configures DWM attributes (`DWMWA_USE_IMMERSIVE_DARK_MODE` and `DWMWCP_DONOTROUND`) so WPF handles crisp per-pixel transparency and antialiased corner rounding without OS window artifact halos.
   - `GlobalHotKeyManager.cs`: System-wide hotkey engine using `RegisterHotKey` for instant shortcuts (`Win+Alt+N`, `Win+Alt+H`, `Win+Alt+D`).
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
   - `NotesHubWindow.xaml` / `.xaml.cs`: Central dashboard featuring real-time full-text search, category tabs (All Notes, Checklists, Trash Bin), desktop tiling arrangement, and backup tools.
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

---

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

### Version 1.1.1
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
- **Checklist Input & Plus Button Click Fixes**:
  - **Click-swallowing bug resolved**: Removed premature `_desktopWindowManager.SetInteracting(false)` call from `Input_LostFocus`, which was triggering `HWND_BOTTOM` and deactivating the window before mouse clicks on `BtnAddTask` or checkboxes could complete.
  - **Placeholder overlap fixed**: Attached `TextChanged` event handlers to both `TxtNewTask` and `TxtNewCopyItem` to dynamically show/hide the prompt placeholder when typing.
  - Added keyboard `Enter` submission support on entry inputs, automatically focusing the input field for continuous, seamless item entry.





