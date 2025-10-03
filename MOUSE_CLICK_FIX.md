# Mouse Click System Fix Documentation

## Issues Fixed

### 1. F3/F4 Button Position Swap
**Problem**: Clicking F3 button opened F4 panel (PlacementManagement), and clicking F4 opened F3 panel (Work).

**Root Cause**: The code used arithmetic enum casting `(DrawerId)(slot + 1)` which assumed sequential enum values. After inserting `PlacementManagement` between `Stock` and `Work`, the enum values became non-sequential:

```csharp
public enum DrawerId
{
    None,                    // 0
    Creature,                // 1  (F1)
    Stock,                   // 2  (F2)
    PlacementManagement,     // 3  (F4 - NEW!)
    Work,                    // 4  (F3 - WRONG!)
    Military,                // 5  (F5)
    Country,                 // 6  (F6)
    World,                   // 7  (F7)
    Log                      // 8  (F8)
}
```

When `slot = 2` (F3 button), the code calculated `(DrawerId)(2 + 1) = (DrawerId)3 = PlacementManagement`, but F3 should open `Work`.

**Solution**: Created explicit mapping arrays instead of arithmetic calculation:

```csharp
// Explicit mapping: slot -> DrawerId (F1-F8 order)
var slotMap = new DrawerId[]
{
    DrawerId.Creature,              // F1 (slot 0)
    DrawerId.Stock,                 // F2 (slot 1)
    DrawerId.Work,                  // F3 (slot 2)
    DrawerId.PlacementManagement,   // F4 (slot 3)
    DrawerId.Military,              // F5 (slot 4)
    DrawerId.Country,               // F6 (slot 5)
    DrawerId.World,                 // F7 (slot 6)
    DrawerId.Log                    // F8 (slot 7)
};
```

**Files Modified**:
- `FortressState.cs` - Line 448-460: `OnOverlayLeftClickedLocal` method
- `FortressState.cs` - Line 1389-1390: Map-relative click handler (secondary handler)
- `FortressState.cs` - Line 1538-1549: `HandleDockClicksScreen` method (tertiary handler)

---

### 2. ZXCV Buttons Not Clickable
**Problem**: Mouse clicks on Z/X/C/V buttons did not trigger the quick menus.

**Root Causes**:
1. **Wrong Y coordinate**: Click handler checked `Height - 2` (one row above bottom), but buttons were rendered at `Height - 1` (bottom row, same as F1-F8).
2. **Missing V button**: Only 3 buttons defined (Orders, Zones, Build), missing Stockpile.
3. **Inconsistent position calculation**: Click detection used different math than rendering:
   - Rendering: `startX = center - totalWidth / 2`, then `xButton = startX + (buttonWidth + gap) * index`
   - Click detection: Used manual offsets like `center - (w + gap) - w / 2`

**Solution**: Unified click detection with rendering logic:

```csharp
// Quick ZXCV buttons (bottom row, same as F1-F8, centered)
int quickY = _uiSurface.Surface.Height - 1; // FIXED: Same row as F1-F8

int center = _uiSurface.Surface.Width / 2;
int buttonWidth = 5;
int gap = 2;

// 4 buttons: Z X C V (same calculation as UiRenderer.DrawQuickIconsScreen)
int totalWidth = (buttonWidth * 4) + (gap * 3);
int startX = center - totalWidth / 2;

// Calculate button positions (exactly matching UiRenderer)
int xZ = startX;
int xX = startX + buttonWidth + gap;
int xC = startX + (buttonWidth + gap) * 2;
int xV = startX + (buttonWidth + gap) * 3; // ADDED V button

var ranges = new (int start, int end, QuickMenuKind kind)[]
{
    (xZ, xZ + buttonWidth - 1, QuickMenuKind.Orders),      // Z
    (xX, xX + buttonWidth - 1, QuickMenuKind.Zones),       // X
    (xC, xC + buttonWidth - 1, QuickMenuKind.Build),       // C
    (xV, xV + buttonWidth - 1, QuickMenuKind.Stockpile),   // V (ADDED)
};
```

**Files Modified**:
- `FortressState.cs` - Line 471-507: `OnOverlayLeftClickedLocal` method

---

### 3. Right-Click Cancel Not Working
**Problem**: Right-clicking did not close menus or navigate back in the menu hierarchy.

**Root Cause**: The `UiOverlaySurface` class did not have a right-click event handler registered.

**Investigation**:
- Left-click worked: `_uiSurface.LeftClickedLocal += OnOverlayLeftClickedLocal;` (registered)
- Right-click handler existed in `FortressState.ProcessMouse` but was never reached because overlay consumed events first
- No `RightClickedLocal` event on `UiOverlaySurface`

**Solution**: Added right-click event support to `UiOverlaySurface`:

```csharp
// UiOverlaySurface.cs
public event Action<Point>? RightClickedLocal; // Added event

public override bool ProcessMouse(MouseScreenObjectState state)
{
    // Check for right-click and fire event
    if (state.Mouse.RightClicked)
    {
        var local = state.SurfaceCellPosition - Position;
        RightClickedLocal?.Invoke(local);
    }
    return base.ProcessMouse(state);
}
```

Then registered the handler in `FortressState.cs`:

```csharp
_uiSurface.RightClickedLocal += OnOverlayRightClickedLocal;
```

Implemented hierarchical back navigation:
1. Close tile panel if open
2. Close zone detail popup if open
3. Close stockpile edit popup if open
4. Navigate L3 submenu → L2 menu → Close QuickMenu
5. General cancel (close drawers, etc.)

**Files Modified**:
- `UiOverlaySurface.cs` - Line 15, 39-49: Added `RightClickedLocal` event and `ProcessMouse` override
- `FortressState.cs` - Line 181: Registered event handler
- `FortressState.cs` - Line 1809-1885: Implemented `OnOverlayRightClickedLocal` method

---

## Why Multiple Click Handlers Existed

The codebase had **THREE independent click handlers** for the same buttons:

1. **`OnOverlayLeftClickedLocal`** (Line 434) - Primary handler, uses overlay-local coordinates
   - This is the one actually being called (overlay is on top)
   - Had the F3/F4 swap bug

2. **Map-relative handler** (Line 1378) - Secondary handler, uses map-surface coordinates
   - Also had F3/F4 swap bug
   - Legacy code, not reached because overlay handles clicks first

3. **`HandleDockClicksScreen`** (Line 1526) - Tertiary handler, uses screen coordinates
   - Also had F3/F4 swap bug
   - Also not reached because overlay handles clicks first

Only the first handler (`OnOverlayLeftClickedLocal`) was actually being executed, but all three had the same bug, which made debugging confusing.

---

## Architecture Lesson

**Problem**: Click detection logic was duplicated and inconsistent with rendering logic.

**Better Approach**:
- Single source of truth for button positions
- Render and click detection should use identical calculations
- Avoid arithmetic enum casting when enum values are non-sequential
- Use explicit mapping arrays for clarity and maintainability

**Why This Happened**:
- Inserting `PlacementManagement` into `DrawerId` enum broke sequential assumption
- ZXCV rendering was refactored at some point, but click detection wasn't updated
- Multiple click handlers created technical debt
- No unit tests for UI coordinate calculations

---

## Debug Logs Added

Added comprehensive logging to help diagnose future issues:

```csharp
Logger.Log($"[CLICK-OVERLAY] Dock slot={slot} -> drawer={_ui.OpenDrawer}");
Logger.Log($"[CLICK-OVERLAY] Quick kind={kind} x=[{start},{end}] -> qmenu={_ui.QuickMenu}");
Logger.Log($"[RIGHT-CLICK-OVERLAY] Clicked at local=({x},{y}), tilePanelOpen={open}, QuickMenu={menu}");
```

Logs are written to: `publish\HumanFortress.App\fortress_debug.log`

---

## Testing Recommendations

1. Click each F1-F8 button and verify correct panel opens
2. Click each Z/X/C/V button and verify correct quick menu opens
3. Right-click in various menu states:
   - From L3 submenu → should go to L2
   - From L2 menu → should close QuickMenu
   - On tile panel → should close panel
   - On zone detail popup → should close popup
4. Verify keyboard shortcuts still work (F1-F8, Z/X/C/V keys)

---

## Files Changed Summary

- `src/HumanFortress.App/States/FortressState.cs`
  - Fixed F3/F4 mapping in `OnOverlayLeftClickedLocal` (primary fix)
  - Fixed F3/F4 mapping in map-relative handler (defensive fix)
  - Fixed F3/F4 mapping in `HandleDockClicksScreen` (defensive fix)
  - Fixed ZXCV button detection (Y coordinate, position calculation, added V)
  - Registered right-click event handler
  - Implemented `OnOverlayRightClickedLocal` with hierarchical navigation

- `src/HumanFortress.App/UI/UiOverlaySurface.cs`
  - Added `RightClickedLocal` event
  - Overrode `ProcessMouse` to detect and fire right-click events

---

## Prevention

To avoid similar issues in the future:

1. **Never insert enum values in the middle** - Always append to end, or use explicit values
2. **Keep rendering and hit-testing in sync** - Extract position calculations to shared methods
3. **Eliminate duplicate code** - Consolidate multiple click handlers into one
4. **Add coordinate unit tests** - Test button bounds calculations
5. **Use named constants** - Instead of magic numbers like `5` and `2` for button width/gap
