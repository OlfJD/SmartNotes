# SmartNotes - Sleek Desktop Sticky Notes for Windows

A lightweight, modern, obsidian-dark sticky notes application designed to live permanently on your Windows desktop surface beneath all open programs, games, and active applications.

Engineered with the same signature design language as **MacroMaster** and **SoundSwitcher** — featuring translucent obsidian glass styling, vivid neon accents, crisp Lucide vector icons, high-DPI typography, checklist modes, and rock-solid Win32 desktop sticking.

---

### Key Features

- **Permanent Desktop Layer Sticking (Below All Applications)**:
  - Notes live on the desktop wallpaper level (`HWND_BOTTOM`).
  - When opening Chrome, games, IDEs, or full-screen apps, SmartNotes stays quietly beneath them without popping over or stealing focus.
  - When switching to the desktop (or pressing `Win+D`), notes are right there on your desktop.
  - Per-note Pin Mode toggle: Switch between **📌 Desktop Stuck (Behind Apps)**, **📌 Always on Top (Floating)**, or **📌 Normal Window**.

- **Exact Coordinate & Dimension Persistence**:
  - Automatically remembers every note's exact `(X, Y)` position, `Width`, `Height`, font size, opacity, and color.
  - Changes save continuously in the background to `%APPDATA%\SmartNotes\notes.json`.

- **Autostart with Windows**:
  - Automatically launches on system boot via Windows Registry (`HKCU\Software\Microsoft\Windows\CurrentVersion\Run\SmartNotes`).
  - Restores all active notes across single or multi-monitor setups.

- **Interactive Checklist & Markdown / Text Modes**:
  - 1-click toggle between freeform text and interactive Todo task checklists.
  - Check off completed items with smooth strikethrough styling.
  - Add tasks instantly by pressing `Enter`.

- **8 Vibrant Obsidian Neon Themes**:
  - Radiant Amber (`#F59E0B`)
  - Neon Emerald (`#10B981`)
  - Cyber Violet (`#8B5CF6`)
  - Electric Cyan (`#06B6D4`)
  - Coral Rose (`#F43F5E`)
  - Stealth Obsidian (`#3B82F6`)
  - Classic Sticky Gold (`#EAB308`)
  - Fresh Mint (`#14B8A6`)

- **Smart Notes Hub & Desktop Manager**:
  - Central management dashboard to view all notes in one place.
  - Real-time instant search across titles, contents, and checklist items.
  - Quick note duplication, recovery from Trash Bin, and "Arrange Notes on Desktop" tiling.
  - Export and Import all notes to JSON or individual notes to Markdown (`.md`).

- **Global System Shortcuts**:
  - `Win + Alt + N` : Spawn a new sticky note anywhere on your desktop.
  - `Win + Alt + H` : Open / Toggle the Notes Hub Dashboard.
  - `Win + Alt + D` : Toggle Show / Hide all sticky notes on desktop.

- **System Tray Integration**:
  - Lives in the Windows notification area tray with quick right-click menu: New Note, Notes Hub, Show/Hide, Tile/Arrange, Startup Toggle, and Settings.

---

### How to Run

1. Double-click [**`SmartNotes.exe`**](file:///C:/Users/Levi/Desktop/SmartNotes/SmartNotes.exe) or run [**`Launch SmartNotes.bat`**](file:///C:/Users/Levi/Desktop/SmartNotes/Launch%20SmartNotes.bat).
2. Your desktop sticky notes will appear on your desktop wallpaper.
3. Click **`+`** in any note header to spawn a new note, click **🎨** to change themes, or press **`Win+Alt+N`** from anywhere in Windows!
4. Access the **Notes Hub** at any time from the system tray icon or by pressing **`Win+Alt+H`**.
