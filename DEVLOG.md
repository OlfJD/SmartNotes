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

### Version 1.1.5 - Multi-Directional 8-Way Window Resizing & Unfocused Auto-Transparency (2026-09-21)
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
  - Added a "Transparent When Unfocused" toggle switch in **Settings & Preferences** under Desktop & System.
