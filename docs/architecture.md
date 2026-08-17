# LokrModMenu — Architecture

## Overlay model

Same pattern as Character Lab / Ability Lab: one persistent Unity scene built at
first use, toggled with `SetActive`. Foreign `EventSystem`s disabled while open so
input reaches the menu.

## Registration flow

1. Dependent plugin `Awake()` calls `ModMenuAPI.RegisterButton` or `RegisterSubmenu`.
2. Character Lab / Ability Lab also call `RegisterBlockingOverlay`.
3. On `ModMenuOverlay.Open()`, buttons rebuilt from `ModMenuAPI.Entries` (sorted).

## Hotkey flow

1. `LokrModMenuPlugin.RegisterHotkeys()` → `GameInputPoll.Register("ToggleModMenu", ...)`.
2. `ModMenuOverlay.Toggle()` → no-op on the loading fade / transition /
   splash scenes; if a blocking overlay is open, close it; else toggle menu.

## Scene change

`SceneManager.sceneLoaded` → `ModMenuOverlay.ForceClose()` restores foreign EventSystems.
