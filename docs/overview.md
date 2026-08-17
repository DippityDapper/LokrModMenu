# LokrModMenu — Overview

Global **mod menu popup** and shared hotkey entry point. Other plugins register
buttons or submenus via **`ModMenuAPI`** without implementing their own overlay
shell or input polling.

## Hotkeys

Configured in `BepInEx/config/com.lokrmodding.modmenu.cfg`:

| Setting | Default | Purpose |
|---------|---------|---------|
| `Hotkeys.ToggleCharacterLab` | `BackQuote` (`` ` ``) | Primary mod menu toggle — works on Linux/Proton |
| `Hotkeys.AlsoToggleOnF3` | `true` | Also bind bare F3 (often stolen by desktop on Linux) |
| `Hotkeys.ToggleCharacterLabControl/Shift/Alt` | `false` | Optional modifier chords |

Uses **`LokrModAPI.Input.GameInputPoll`** for reliable key detection under Proton.

## Blocking overlays

LokrLab and Ability Lab call
**`ModMenuAPI.RegisterBlockingOverlay`**. While any registered overlay is open,
the mod menu hotkey **closes the overlay first** instead of stacking menus on top.

The hotkey also no-ops while the base-game loading fade (`FadeScreen` with
loading content) or the `transition` / `splashScreen` scenes are up.

## In this folder

- [`architecture.md`](architecture.md) — overlay scene, input handler
- [`layout.md`](layout.md) — file structure
- [`classes.md`](classes.md) — `ModMenuAPI`, `ModMenuOverlay`
- [`conventions.md`](conventions.md) — registration patterns
- [`cross-references.md`](cross-references.md) — dependencies

## Plugin metadata

`LokrModMenuPlugin.cs`: `Guid = "com.lokrmodding.modmenu"`,
`Name = "LoKR Mod Menu"`, `Version = "1.1.1"`,
`[BepInDependency(LokrModAPIPlugin.Guid)]`.
