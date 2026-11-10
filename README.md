# Squarely

A keyboard- and drag-driven window manager for Windows, written in F#, modelled on
[Rectangle](https://rectangleapp.com) for macOS. It lives in the system tray.

## Features

- **Keyboard shortcuts** for halves, quarters, thirds, two-thirds, fourths, sixths, maximize,
  almost-maximize, maximize height, larger/smaller, center, move to edge, restore, and next/previous display.
- **Repeat to cycle sizes**: press Left Half again for ⅔, again for ⅓. First Third steps across the screen.
- **Drag to snap**: drag a window to an edge or corner and a translucent preview shows where it will land.
  Top edge maximizes, sides give halves, corners give quarters, the bottom edge gives thirds.
  Drag a snapped window away and it returns to its original size.
- **Restore** puts a window back exactly where it was before Squarely first moved it.
- **Multi-monitor & mixed-DPI aware**: tiled windows keep their tiling when sent to another display.
- **Portrait screens** get thirds/fourths as rows instead of columns.
- **Gaps** between windows (configurable).
- Tray menu with every action, launch at login, and a toggle for Windows' built-in Snap.

## Default shortcuts

Rectangle's defaults, with `Ctrl+Alt` standing in for `Ctrl+Option` and `Win` for `Cmd`.

| Action | Shortcut | | Action | Shortcut |
|---|---|---|---|---|
| Left / Right Half | `Ctrl+Alt+←` / `→` | | Maximize | `Ctrl+Alt+Enter` |
| Top / Bottom Half | `Ctrl+Alt+↑` / `↓` | | Maximize Height | `Ctrl+Alt+Shift+↑` |
| Top Left / Top Right | `Ctrl+Alt+U` / `I` | | Larger / Smaller | `Ctrl+Alt+=` / `-` |
| Bottom Left / Bottom Right | `Ctrl+Alt+J` / `K` | | Center | `Ctrl+Alt+C` |
| First / Center / Last Third | `Ctrl+Alt+D` / `F` / `G` | | Restore | `Ctrl+Alt+Backspace` |
| First / Last Two Thirds | `Ctrl+Alt+E` / `T` | | Next / Previous Display | `Ctrl+Alt+Win+→` / `←` |

All other actions are unbound by default.

## Configuration

Settings live in `%APPDATA%\Squarely\config.json` (tray menu → *Edit Shortcuts & Settings…*, then
*Reload Settings*):

```json
{
  "cycleSizes": true,
  "dragToSnap": true,
  "gapSize": 0,
  "shortcuts": {
    "LeftHalf": "Ctrl+Alt+Left",
    "CenterHalf": "Ctrl+Alt+Shift+C",
    "TopLeftSixth": ""
  }
}
```

Shortcuts are `Modifier+Modifier+Key`, with modifiers `Ctrl`, `Alt`, `Shift`, `Win`. Keys are letters, digits,
`Left`/`Right`/`Up`/`Down`, `Enter`, `Backspace`, `=`, `-`, `F1`–`F24`, or any
[`Keys`](https://learn.microsoft.com/dotnet/api/system.windows.forms.keys) name. An empty string unbinds.
If a shortcut is already taken by another app, Squarely tells you at startup.

## Build & run

Requires the .NET 7+ SDK.

```powershell
dotnet run --project src            # run
dotnet test                         # unit tests for layout math and shortcut parsing
dotnet publish src -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true -o publish
```

## Known limitations

- Windows of apps running **as administrator** can't be moved unless Squarely is also elevated (Windows UIPI).
- With **Windows' built-in Snap** on, both Squarely and Windows react to edge drags. Turn one off from the tray menu.
- On keyboard layouts where `AltGr` = `Ctrl+Alt` (German, Polish, …), the defaults can shadow characters like
  `€` on `AltGr+E`. Rebind those in the config.

## Layout

| File | Role |
|---|---|
| `src/Native.fs` | Win32 P/Invoke declarations |
| `src/Geometry.fs` | Pure layout math: actions, tiling, cycling, snap zones (unit tested) |
| `src/Config.fs` | Settings + shortcut parsing, JSON persistence |
| `src/WindowOps.fs` | Read/write window frames, compensating for invisible DWM borders |
| `src/Manager.fs` | Runs actions; per-window history for Restore and cycling |
| `src/WinEvents.fs`, `Hotkeys.fs` | System event hooks and global hotkeys |
| `src/Overlay.fs`, `DragSnap.fs` | Drag-to-snap preview and behaviour |
| `src/App.fs` | Tray icon, menu, entry point |
