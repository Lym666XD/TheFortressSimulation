# HumanFortress - Launch Guide

## 🎮 HOW TO RUN THE GAME

### Quick Launch
**Double-click `RunGame.bat`** - That's it!

### Alternative Methods
1. Navigate to the `game` folder
2. Double-click `HumanFortress.App.exe`

### Run Tests
Double-click `RunTests.bat` to verify all systems work correctly

## 📁 Game Files Location

The complete game is in the **`game`** folder with all dependencies included:
- **HumanFortress.App.exe** - Main executable
- **191 DLL files** - All required libraries including:
  - MonoGame.Framework.dll (graphics/game framework)
  - SadConsole.dll (console UI framework)
  - SDL2.dll (window management)
  - soft_oal.dll (OpenAL audio)
  - Full .NET 8 runtime (self-contained)

## 🎮 Game Controls

### Main Menu
- **F** - Start Fortress Mode
- **Q** - Quit Game

### Fortress Mode
- **ESC** - Return to Main Menu
- More controls coming in Phase B!

## 🏗️ Current Features (Phase A Complete)

✅ **Core Architecture**
- Fixed 50 TPS tick scheduler with deterministic simulation
- Read-parallel/write-serialized execution model
- Command queue for input replay
- DiffLog system for atomic writes

✅ **World System**
- 4x4 chunks (128x128 tiles total)
- 50 Z-levels
- 32x32 tiles per chunk
- LOD system (L0-L4) for performance

✅ **Foundation Systems**
- Deterministic RNG with named streams
- Event bus for decoupled communication
- Game state management
- Error resilience with quarantine

## 🚀 System Requirements

- **OS**: Windows 10/11 (64-bit)
- **RAM**: 512MB minimum
- **Disk**: 200MB for game files
- **Graphics**: Any GPU supporting OpenGL 3.3
- **.NET**: NOT REQUIRED (self-contained)

## ❓ Troubleshooting

### Game won't start
1. Make sure you're running from `RunGame.bat` or the `game` folder
2. Check Windows Defender isn't blocking the exe
3. Try right-click → "Run as Administrator"

### Black screen
- The game uses SadConsole with MonoGame
- Update your graphics drivers
- Make sure OpenGL 3.3 is supported

### Missing DLL errors
- All dependencies are in the `game` folder
- Don't move the exe outside this folder
- If files are missing, republish with:
  ```
  cd src/HumanFortress.App
  dotnet publish -c Release -r win-x64 --self-contained true -o ../../game
  ```

## 📊 Performance

The game runs at a fixed 50 TPS (ticks per second) with:
- Deterministic simulation
- Multi-threaded read phase
- Single-threaded write phase
- Automatic LOD management

## 🔧 Development

This is Phase A of the implementation following MILESTONE.md:
- ✅ Platform & CI Foundations
- ⏳ Phase B: WorldGen & WorldMap (Next)
- ⏳ Phase C: Embark & Fortress Bootstrap
- ⏳ Phase D: Navigation & Connectivity
- ⏳ Phase E: Items, Stockpiles, Zones
- ⏳ Phase F: Job Scheduler & Hauling
- ⏳ Phase G: Buildables & Construction
- ⏳ Phase H: Storyteller
- ⏳ Phase I: Combat MVP
- ⏳ Phase J: Persistence & Replay
- ⏳ Phase K: Performance & HUD
- ⏳ Phase L: Modding & Content

## 📝 Version Info

- **Version**: 0.1.0 (Phase A)
- **Build**: Release, Self-Contained, Windows x64
- **Framework**: .NET 8.0
- **Graphics**: MonoGame 3.8.1.303
- **UI**: SadConsole 10.0.3

---

**Enjoy your Dwarf Fortress-like game!**

The simulation is deterministic, thread-safe, and ready for the next phases of development.