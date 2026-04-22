# MZ Resource Manager

A read-only analysis and inspection tool for **RPG Maker MZ** game projects. Browse your game's database and resources, check where switches, variables, and files are used.

---

## Requirements

- Windows 10 / 11
- [.NET 10 Runtime](https://dotnet.microsoft.com/download)
- An RPG Maker MZ project folder (the folder containing `data/`, `audio/`, `img/`, etc.)

> **Not compatible with RPG Maker MV** projects.

---

## Features

### Search Text (sidebar)

Full-text search across all map event and common event dialogue and commands. Results show the map, event, page, and matching line.

---

### DATABASE (sidebar)

| Section           | Description                                                                                       |
| ----------------- | ------------------------------------------------------------------------------------------------- |
| **Maps**          | Browse all maps, preview rendered map images, export as PNG, and trace incoming teleport links.   |
| **Items**         | List all items with icon, type, and description. Find every event that references each item.      |
| **Switches**      | List all named switches and see where each one is read or written, including plugin parameters.   |
| **Variables**     | List all named variables and trace usage across map events, common events, and plugin parameters. |
| **Common Events** | List all common events and find where each one is called by event command.                        |

---

### RESOURCES (sidebar)

| Section         | Folders / Sources scanned                                                              |
| --------------- | -------------------------------------------------------------------------------------- |
| **Audio**       | `audio/bgm`, `audio/bgs`, `audio/me`, `audio/se`                                       |
| **Sprites**     | `img/characters`, `img/faces`, `img/sv_actors`, `img/sv_enemies`, `img/battlers`       |
| **Animations**  | `img/animations`                                                                       |
| **Backgrounds** | `img/parallaxes`, `img/battlebacks1`, `img/battlebacks2`, `img/titles1`, `img/titles2` |
| **Pictures**    | `img/pictures`                                                                         |
| **System / UI** | `img/system`, `img/icons`                                                              |
| **Tilesets**    | `img/tileset`                                                                          |

For each resource, the panel lists every event command that references it so you can trace usage at a glance.

---

### Tools menu

| Tool                     | Description                                                                            |
| ------------------------ | -------------------------------------------------------------------------------------- |
| **Unused Resource Scan** | Scans audio and image folders for files that are never referenced by any map or event. |
| **Script Book Export**   | Exports all map events and common events to a single plain-text document.              |

---

## How to Use

1. Launch the app (`dotnet run` or run the built executable).
2. Click **File → Open Game Folder…** and select your RPG Maker MZ project root.

---
