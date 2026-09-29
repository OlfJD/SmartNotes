<div align="center">

# 📝 SmartNotes

**High-Performance Desktop Sticky Notes for Windows with Win32 Permanent Layer Sticking, Multi-Language Proofing & Magnetic Stack Docking**

[![Download Latest Release](https://img.shields.io/badge/Download-Latest_Release_(Windows)-06B6D4?style=for-the-badge&logo=windows&logoColor=white)](https://github.com/OlfJD/SmartNotes/releases/latest/download/SmartNotes-win-x64.zip)
[![GitHub Release](https://img.shields.io/github/v/release/OlfJD/SmartNotes?style=for-the-badge&color=10B981)](https://github.com/OlfJD/SmartNotes/releases/latest)
[![Platform](https://img.shields.io/badge/Platform-Windows_10_%2F_11-3B82F6?style=for-the-badge&logo=windows)](https://github.com/OlfJD/SmartNotes)
[![Framework](https://img.shields.io/badge/.NET-10.0_WPF-8B5CF6?style=for-the-badge&logo=dotnet)](https://dotnet.microsoft.com/)

### 📥 [👉 Click Here to Download SmartNotes for Windows (.zip)](https://github.com/OlfJD/SmartNotes/releases/latest/download/SmartNotes-win-x64.zip)

*(Standalone portable app — no installation required, simply extract and run!)*

<br/>

<img src="Assets/preview.png" alt="SmartNotes Desktop Preview" width="800" />

</div>

---

## ⚡ Quick Start for Users

1. **[Download the Latest Release (.zip)](https://github.com/OlfJD/SmartNotes/releases/latest/download/SmartNotes-win-x64.zip)**.
2. **Extract** the ZIP folder anywhere on your PC (e.g. `Desktop` or `Documents`).
3. Double-click **`SmartNotes.exe`** (or `Launch SmartNotes.bat`) to start!

---

## ✨ Key Features

### 🌍 App-Wide Multi-Language Localization (v2.1)
- **Bilingual English (`en`) & German (`de`) UI**: Fully translated Settings & Preferences dashboard, note context menus, transparency popups, tray menus, timestamps (*"Just now"* / *"Gerade eben"*), and updater alerts.
- **Dynamic Real-Time UI Switching**: Change display languages in Settings with instant live re-rendering across all open notes without restarting.
- **Startup System Detection & Prompt**: Automatically detects your Windows locale (`CultureInfo.InstalledUICulture`) on first launch and offers 1-click language setup.

### ✍️ Native Text Proofing, Spell Checking & Autocorrect (v2.0 - v2.1)
- **Real-Time Spell Checking**: Red squiggly underlines on misspelled words across note text, titles, checklists, and copy compartments.
- **12 International Proofing Dictionaries**: Seamlessly switch between English (US/UK/CA/AU), German (`de-DE`), Spanish, French, Italian, Portuguese, Dutch, Polish, and Swedish.
- **Intelligent Autocorrect As You Type**: Automatically fixes 150+ common typos (e.g., `teh` → `the`, `dont` → `don't`, `recieve` → `receive`, `seperate` → `separate`) with casing preservation (`TEH` → `THE`).
- **Instant Backspace Undo UX**: Pressing `Backspace` immediately after an autocorrect reverts the word back to your exact typed characters.
- **Smart Typography & Symbol Replacements**: Converts shortcuts on the fly (`->` to `→`, `<-` to `←`, `=>` to `⇒`, `!=` to `≠`, `--` to `—`, `(c)` to `©`, `(r)` to `®`, `+-` to `±`, etc.).
- **Auto-Capitalization**: Automatically capitalizes sentence beginnings and standalone `i` → `I`.
- **Custom User Dictionary & Rules**: Add words directly via the context menu (`custom_dict.lex`) or define text expansion macros in `%APPDATA%\SmartNotes\autocorrect_rules.json`.

### ⚙️ Modern Settings & Preferences Dashboard
- **6-Category Sidebar Navigation**:
  1. 🖥️ **Desktop & System**: Language selection, Windows startup, background transparency, and desktop sticking.
  2. 🎨 **Note Appearance**: Default theme palettes, font scaling, and interactive live card preview.
  3. ⌨️ **Global Hotkeys**: System-wide keyboard shortcut toggles.
  4. ✍️ **Text Proofing**: Spellcheck, proofing dictionaries, autocorrect, sentence capitalization, and symbol rules.
  5. 🗑️ **Trash & Recovery**: 48-hour accidental deletion protection and explorer management.
  6. ⚡ **Memory & Performance**: Live RAM telemetry, auto-trimming switches, and cache compaction.
- **Interactive Live Note Preview**: Renders theme colors, typography scaling, and border weights in real time as you adjust settings.

### ⚡ Zero-RAM Idle Architecture & Memory Optimizer
- **Zero-RAM Dictionaries**: Inactive language dictionaries, lexicons, and closed notes stay on disk and consume **0 MB of RAM** while idle.
- **Automatic Background Memory Trimming**: Flushes unreferenced memory pages directly back to the Windows OS kernel every 3 minutes when idle.
- **1-Click Manual RAM Optimization**: Interactive RAM optimization with live Before / After / Freed MB telemetry.

### 📌 Permanent Desktop Layer Sticking (`HWND_BOTTOM`)
- **Lives on Desktop Wallpaper**: Notes live on the desktop wallpaper level underneath open browser windows, IDEs, and full-screen games.
- **Never Steals Focus**: When switching between foreground apps, sticky notes remain quietly docked to your desktop.
- **Per-Note Pin Modes**: Toggle between **📌 Desktop Stuck (Behind Apps)**, **📌 Always on Top (Floating)**, or **📌 Normal Window**.

### 🧲 Magnetic Stack Leader Dragging & Docking
- **Magnetic Snapping**: Drag notes next to one another to snap and dock them into clean clusters.
- **Stack Leader Mechanics**: Drag the highest note of any docked cluster to move the entire stack together.
- **Effortless Single-Note Peeling**: Drag any middle or lower note to peel it away independently without shortcuts.

### 🎨 2D Drawing-App Color Picker & Neon Themes
- **2D Saturation / Value Gradient Canvas**: Full 0–100% saturation and brightness adjustment with live mouse dragging.
- **0–360° Hue Rainbow Spectrum Bar**: Smooth color spectrum bar with interactive thumb indicator.
- **8 Signature Neon Obsidian Presets**: Radiant Amber, Neon Emerald, Cyber Violet, Electric Cyan, Coral Rose, Stealth Obsidian, Classic Sticky Gold, and Fresh Mint.

### 📝 3 Specialized Note Modes
1. **📝 Plain Text / Markdown**: Multiline note taking with live typing indicators, auto-saving, and 1-click Markdown (`.md`) export.
2. **☑️ Todo Checklist**: Interactive task checkboxes with strike-through, progress indicators, and "Clear Completed Tasks" action.
3. **📋 Copy Compartments**: Quick-copy snippet rows with dedicated 1-click `[ 📋 ]` copy buttons on every line with green checkmark confirmation.

### 🗑️ 48-Hour Temporary Trash & Note Recovery
- Safeguards against accidental deletions by retaining deleted notes locally for 48 hours before permanent purging.
- Restore individual notes, restore all notes, or open the trash folder in Windows Explorer directly from the system tray or settings.

### 🔄 Seamless In-Place Auto-Updater
- Automatically checks GitHub Releases for new updates.
- 1-click automatic background downloading, extraction, in-place updating, and restart.

---

## ⌨️ Keyboard Shortcuts & Gestures

| Shortcut / Action | Function |
| :--- | :--- |
| `Win + Alt + N` | Spawn a new sticky note at mouse location |
| `Win + Alt + D` | Toggle Show / Hide all sticky notes on desktop |
| `Ctrl + Shift + C` | Copy all content of the active note to clipboard |
| `Ctrl + X` / `Ctrl + C` / `Ctrl + V` | Standard Cut / Copy / Paste |
| `Ctrl + A` | Select all text in current note |
| `Backspace` (after autocorrect) | Undo autocorrect typo fix and restore original text |
| `Enter` (in Checklist / Copy mode) | Insert new task or snippet line |
| `Right-Click` on note text | Open Obsidian Dark context menu with spelling suggestions |
| `Header Drag` | Move note across virtual monitors |
| `Edge / Corner Drag` | 8-way multi-directional window resizing |

---

## 🛠️ Architecture & Technical Specifications

- **Runtime**: C# / WPF on **.NET 10.0** (`net10.0-windows`, `win-x64`).
- **Native Subsystem (`Core/Native/`)**:
  - `Win32Api.cs` & `DesktopWindowManager.cs`: Win32 `HWND_BOTTOM` Z-order enforcement, `WS_EX_TOOLWINDOW` style assignment, and `WM_WINDOWPOSCHANGING` interception.
  - `WindowBlurHelper.cs`: DWM immersive dark mode (`DWMWA_USE_IMMERSIVE_DARK_MODE`) and corner clip enforcement (`DWMWCP_DONOTROUND`).
  - `GlobalHotKeyManager.cs`: System-wide hotkey engine via `RegisterHotKey` / `UnregisterHotKey`.
  - `TrayManager.cs`: Windows Notification Area (System Tray) integration with custom dark context menu and balloon tooltips.
  - `MemoryOptimizer.cs`: Native `EmptyWorkingSet` memory flush and LOH heap compaction.
- **Persistence (`Core/Services/`)**:
  - `NoteStorageService.cs`: Debounced JSON storage to `%APPDATA%\SmartNotes\notes.json`.
  - `SettingsService.cs`: JSON configuration storage to `%APPDATA%\SmartNotes\settings.json`.
  - `LocalizationService.cs`: Zero-RAM on-demand string dictionary provider for English and German.
  - `TextProofingService.cs`: Windows COM `ISpellCheckerFactory` bridge and typo replacement engine.
- **Typography & Vector Rendering**:
  - Per-monitor DPI awareness (`PerMonitorV2`).
  - Subpixel ClearType display formatting (`TextFormattingMode="Display"`, `ClearTypeHint="Enabled"`).
  - High-precision Lucide vector icons (`LucideIcon.cs`).

---

## 💻 Building from Source

### Prerequisites
- Windows 10 or Windows 11 (64-bit)
- [.NET 10.0 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)

### Clone & Run
```bash
# Clone the repository
git clone https://github.com/OlfJD/SmartNotes.git
cd SmartNotes

# Build and run the app
dotnet run
```

### Compile Release Binary
```bash
dotnet publish -c Release
```
The compiled self-contained files will be in `bin/Release/net10.0-windows/win-x64/publish/`.

---

## 📄 License

SmartNotes is open-source software licensed under the **MIT License**. Feel free to use, modify, and distribute it!
