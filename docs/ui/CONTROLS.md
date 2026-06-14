# HumanFortress Controls

Updated: 2026-06-12
Status: current player-facing summary

## Camera And Map

- `W` / `A` / `S` / `D`: pan camera.
- Hold `Shift`: faster camera pan.
- Mouse move: move the map cursor to the hovered tile.
- Mouse wheel: change Z level.
- `Ctrl` + mouse wheel: zoom when supported by the current view.

## Simulation

- `Space`: pause or resume simulation.
- `-`: slow simulation speed.
- `=` / `+`: increase simulation speed.
- `Esc`: back or close the current UI layer; when no UI layer is active, opens/closes pause flow depending on state.

## Management Drawers

- `F1`..`F7`: open/toggle management drawers.
- Click bottom-left `F1`..`F7` buttons: same as keyboard.
- `Tab` / `Shift+Tab`: cycle tabs inside an open drawer.

Current drawer mapping may change during UI refactor. Treat on-screen labels as authoritative.

## Quick Menus

- `Z`: Orders menu.
- `X`: Zones menu.
- `C`: Build menu.
- Click bottom-center `Z` / `X` / `C` buttons: same as keyboard.
- Right-click: cancel current tool or close current submenu.
- `,`: clear current designations for the active designation mode where supported.

## Debug And Overlays

- `F9`: cycle navigation/debug overlays.
- `` ` `` / `~` / `F12`: toggle debug menu when enabled.
- `F10`: path debug tool where enabled.
- `Ctrl+F10`: clear current path debug display where enabled.

## Notes

- Camera and cursor are separate. Camera moves with keyboard; cursor follows mouse hover.
- UI commands should enqueue simulation commands rather than directly mutating world state.
- Some buttons are placeholders during development and may show a toast instead of performing gameplay work.
