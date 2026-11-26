<div align="center">

<img src="Assets/app.png" width="88" alt="SmartNotes icon">

# SmartNotes

Cool and unique free sticky notes for your Windows desktop.\
Notes, to-do lists and checklists right on your desktop. No account, no cloud, everything stays on your PC.

[**Download SmartNotes (.zip)**](https://github.com/OlfJD/SmartNotes/releases/latest/download/SmartNotes-win-x64.zip)

<br>

<img src="docs/images/themes.png" width="760" alt="SmartNotes in its six themes">

</div>

## How it works

- **Extract the zip and run `SmartNotes.exe`**. Your notes stick to the desktop, behind your apps, and stay visible after `Win + D`.
- **Top of a note**: **+** for a new note, the pin to keep it on top, text / checklist / copy-snippet mode, colours and themes, the lock, and **···** for font size, transparency, spell check, export and settings.
- **Tray icon**: click it (or press `Win + Alt + A`) to bring all notes to the front, right-click for new note, show / hide all, arrange, the 48-hour trash, settings and exit.

## Features

- **Six themes**: Classic, Paper Sticky, Cyberpunk, Medieval, Elden Ring (nine Great Runes instead of colours) and Astral (a constellation on every note), each with its own fonts, icons and menus.
- **Three modes**: text, checklist (drag tasks to reorder, done tasks are struck through, "3/7" shows your progress) and copy snippets.
- **Magnetic docking**: notes snap together and move as a stack.
- **Lock a note** so it can't be edited, moved or deleted by accident.
- **Export** a note as Markdown, copy all of it with `Ctrl + Shift + C`, or **copy it as a picture** to share it in a chat.
- **Several screens**: a note left on an unplugged monitor comes back onto a visible one, and *Arrange notes* keeps your sizes and docked stacks.
- **Spell check** in 12 languages, plus autocorrect and your own text shortcuts.
- **Safe saving**: every change is saved right away. If a save fails, the note says so and tries again. Notes are backed up before every update, and **earlier versions** of each note can be brought back from its **···** menu.
- **Quiet updates** in the background, **English and German**, and it **starts with Windows** (turn that off in the tray menu or Task Manager).

## Runes and constellations

In the Elden Ring theme the colour picker offers nine Great Runes instead of colours:

![The nine Great Runes](docs/images/great-runes.png)

In the Astral theme every note gets its own constellation, picked automatically or chosen by you:

![The five constellations](docs/images/constellations.png)

<details>
<summary>Themed menus</summary>

![Themed menus](docs/images/themed-menus.png)

</details>

## Shortcuts

| Keys | Action |
| --- | --- |
| `Win + Alt + N` | New note |
| `Win + Alt + A` | Bring all notes to the front |
| `Win + Alt + S` | Show / hide all notes |
| `Ctrl + Shift + C` | Copy the note's content |
| `Alt` / `Ctrl` + drag | Move one note out of a stack |

## Where are my notes?

In `%AppData%\SmartNotes` (`notes.json`). Copy that folder to back up your notes or move them to another PC.

## Requirements

Windows 10 or 11 with the [.NET 10 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/10.0) (x64).

## Building

```bash
git clone https://github.com/OlfJD/SmartNotes.git
cd SmartNotes
dotnet run
```

Needs the .NET 10 SDK. Tests: `dotnet run --project tests/SmartNotes.Tests` (see [tests/README.md](tests/README.md)). Pushing a `v*` tag publishes a release. Development notes are in [DEVLOG.md](DEVLOG.md).

MIT license. Not affiliated with Elden Ring; all theme art is drawn in code.
