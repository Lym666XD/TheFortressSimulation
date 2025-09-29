HumanFortress Controls (v1)

Movement & Camera
- WASD: Pan camera (hold Shift for faster)
- Mouse Move: Move cursor to hovered tile
- Mouse Wheel: Change Z-level (Ctrl + Wheel = Zoom)

Panels & Menus
- F1..F7: Open management drawers (toggle)
- Z / X / C: Open Orders / Zones / Build quick menus
- Tab / Shift+Tab: Cycle tabs inside drawers
- ESC: Back/close (from drawer root = close drawer; from no drawer = pause menu)

Overlays & Debug
- F9: Cycle overlays (Walkability, MovementCost[FP], Traffic, Connectivity, PathDisplay, FlowField, RampMask)
- ` or ~ or F12: Toggle debug menu
- Right-Click: Cancel current tool / close submenu

Notes
- Mouse can click the bottom-left F1..F7 icons and bottom-center Z/X/C icons to open the same menus.
- Drawer height ~70% of screen; quick menus appear along the bottom.

Path Tools
- F10: Two-step path tool
  - First press sets Start at cursor (records current Z)
  - Second press sets Goal at cursor（可跨 Z）并求解路径
  - Ctrl+F10: 清除当前路径
  - PathDisplay 模式会在底部显示 len 与总成本（定点 FP=10，1 位小数）
