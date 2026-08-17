# LokrModMenu — Conventions

- Register menu entries from your plugin's `Awake()` after declaring `[BepInDependency(LokrModMenuPlugin.Guid)]`.
- Use a unique id prefix (e.g. `character-lab`, `ability-lab`).
- Full-screen tools should **`RegisterBlockingOverlay`** so the shared hotkey closes them cleanly.
- Do not patch `UIMainScreen` for global toggles — use `ModMenuAPI` + config here instead.
