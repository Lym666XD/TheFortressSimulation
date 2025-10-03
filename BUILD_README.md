# HumanFortress Build Instructions

## Quick Start

### Build the game:
```bash
# Double-click or run:
build.bat
```

### Run the game:
```bash
# After building, the executable is at:
publish\HumanFortress.App\HumanFortress.App.exe
```

---

## What Changed

### 1. Fixed Output Directory
- **Before**: Multiple confusing output directories (`bin/Debug/`, `bin/Release/`, etc.)
- **After**: Always outputs to **`publish\HumanFortress.App\`**
- **How**: Use `build.bat` instead of manual `dotnet publish` commands

### 2. Fixed Process Not Exiting
- **Before**: After closing the game, the process remained running, locking DLLs and preventing recompilation
- **After**: Process exits cleanly when game window closes
- **How**:
  - Changed simulation thread to background thread (`IsBackground = true` in TickScheduler.cs)
  - Added proper shutdown sequence (`GameStateManager.Shutdown()` in Program.cs)

---

## Technical Details

### Why was the process not exiting?

The simulation system (`TickScheduler`) was running on a **foreground thread**:
```csharp
// Old code (WRONG):
IsBackground = false  // Foreground thread prevents process exit!
```

When you closed the game window:
1. Main thread exits
2. But simulation thread keeps running (foreground thread)
3. Process stays alive
4. DLL files remain locked
5. Compilation fails with "file in use" error

### The Fix:

**1. Changed to background thread:**
```csharp
// New code (CORRECT):
IsBackground = true  // Background thread will not prevent process exit
```

**2. Added proper shutdown:**
```csharp
// In GameStateManager.cs:
public void Shutdown()
{
    if (_tickScheduler.IsRunning)
        _tickScheduler.Stop();  // Stop simulation gracefully

    if (_currentState != null)
        _currentState.Exit();   // Exit current game state
}
```

**3. Called shutdown before exit:**
```csharp
// In Program.cs:
Game.Instance.Run();              // Game loop (blocks until window closed)
_gameStateManager?.Shutdown();    // Stop all systems
Game.Instance.Dispose();          // Clean up MonoGame
Logger.Close();                   // Close log file
```

---

## Build Directory Structure

```
TheFortressSimulation/
├── build.bat                    ← Use this to build!
├── publish/
│   └── HumanFortress.App/
│       ├── HumanFortress.App.exe   ← Your game executable
│       ├── *.dll                   ← All dependencies
│       ├── content/                ← Game content (zones.json, etc.)
│       └── data/                   ← Game data (creatures, items, etc.)
├── src/
│   └── HumanFortress.App/
│       └── bin/Release/...      ← Intermediate build files (ignore this)
└── ...
```

**Always run:** `publish\HumanFortress.App\HumanFortress.App.exe`

---

## Troubleshooting

### Problem: "File is in use" error during build
**Solution:** Make sure the game is fully closed. The new code should prevent this, but if it still happens:
1. Close the game window
2. Wait 2 seconds
3. Run `build.bat` again

### Problem: Changes not reflected in the game
**Solution:**
1. Close the game completely
2. Run `build.bat`
3. Run the exe from `publish\HumanFortress.App\HumanFortress.App.exe`

### Problem: Game crashes on startup
**Solution:** Check `publish\HumanFortress.App\fortress_debug.log` for error messages

---

## Zone System Features (F4 Panel)

After building, you now have:

### F1-F8 Buttons:
- **F1** = Creatures
- **F2** = Stock/Items
- **F3** = Work
- **F4** = **Placement Management** ✨ (NEW!)
- **F5** = Military
- **F6** = Country
- **F7** = World
- **F8** = Log

### F4 Panel Tabs:
1. **Zones** - View all created zones (ID, Name, Type, Cells)
2. **Stockpiles** - View all stockpiles
3. **Settings** - Placeholder for future features

### Creating Zones:
1. Press **X** to open zone menu
2. Select category (Z/X/C/V/F)
3. Select zone type
4. Click two corners on the map
5. Zone created! View it in **F4 → Zones tab**

---

## For Developers

### Manual Build (if you don't want to use build.bat):
```bash
dotnet publish src\HumanFortress.App\HumanFortress.App.csproj -c Release -r win-x64 --self-contained true -o publish\HumanFortress.App
```

### Clean Build:
```bash
dotnet clean
build.bat
```

### Run Tests:
```bash
publish\HumanFortress.App\HumanFortress.App.exe --test
```
