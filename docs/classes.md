# LokrModMenu — Classes

## `LokrModMenuPlugin`

Entry point. Binds hotkey config, registers `GameInputPoll` handlers, runs
`Harmony.PatchAll()`, closes menu on `SceneManager.sceneLoaded`.

## `ModMenuAPI` (public)

Extension surface for other plugins:

```csharp
ModMenuAPI.RegisterButton(id, label, onClick, sortOrder, closeOnClick);
ModMenuAPI.RegisterSubmenu(id, label, buildContent, sortOrder);
ModMenuAPI.RegisterBlockingOverlay(isOpen, close);
ModMenuAPI.Unregister(id);
ModMenuAPI.Open() / Close() / Toggle();
```

- **`RegisterBlockingOverlay`** — list (not a single slot); multiple plugins can register.
- **`HasBlockingOverlayOpen`** — used by toggle: close overlay before opening menu.

## `ModMenuOverlay` (internal)

Persistent scene `LokrModMenu` with main + submenu panels. `Open()` / `Toggle()`
refuse to open during the base-game loading fade or the `transition` /
`splashScreen` scenes. `Open()` also blocks foreign EventSystems;
`ForceClose()` on scene load.

## `ModMenuInputHandler`

`MonoBehaviour` on mod-menu EventSystem for escape/back navigation.
