# HumanFortress - How to Run

## Quick Start

### Option 1: Run the Self-Contained Version (Recommended)
**No .NET installation required!**

1. Double-click `RunGame-Direct.bat`
   - OR -
2. Navigate to the `publish` folder and run `HumanFortress.App.exe`

### Option 2: Run with Options Menu
1. Double-click `RunGame.bat`
2. Choose from the menu:
   - Option 1: Self-contained version (68MB, no .NET required)
   - Option 2: Framework-dependent version (1.6MB, requires .NET 8)
   - Option 3: Run tests
   - Option 4: Exit

## Published Versions

### Self-Contained (`publish` folder)
- **Size**: ~68MB
- **Requirements**: None - includes .NET runtime
- **File**: `publish/HumanFortress.App.exe`
- **Best for**: Users without .NET installed

### Framework-Dependent (`publish-small` folder)
- **Size**: ~1.6MB
- **Requirements**: .NET 8 Runtime
- **File**: `publish-small/HumanFortress.App.exe`
- **Best for**: Developers or users with .NET 8 installed

## Game Controls

### Main Menu
- **F** - Start Fortress Mode
- **Q** - Quit Game

### Fortress Mode
- **ESC** - Return to Main Menu
- Game runs at 50 TPS (ticks per second)
- 4x4 chunks (128x128 tiles)
- 50 Z-levels

## Command Line Options

Run tests:
```
./RunTests.sh
```

On Windows, use `RunTests.bat`.

## Troubleshooting

### Game doesn't start
1. Try the self-contained version in the `publish` folder
2. Make sure Windows Defender isn't blocking the executable
3. Right-click the .exe and select "Run as administrator" if needed

### Framework-dependent version doesn't work
- Install .NET 8 Runtime from: https://dotnet.microsoft.com/download/dotnet/8.0

### Black screen or graphics issues
- The game uses SadConsole with MonoGame backend
- Update your graphics drivers
- Try running in compatibility mode

### SDL/Keyboard initialization crash (publish exe)
- If you see: `The type initializer for 'Keyboard' threw an exception` or `The type initializer for 'Sdl' threw an exception`, it usually means native libraries (SDL2/OpenAL) were not located by the single-file bundle.
- Fix: use the multi-file self-contained build in `publish` (now includes `SDL2.dll` and `soft_oal.dll`), or republish without single-file bundling:
  - `dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=false -o ../../publish`
  - Then run `publish/HumanFortress.App.exe` again.

## System Requirements

- **OS**: Windows 10/11 (64-bit)
- **RAM**: 512MB minimum
- **Disk**: 100MB free space
- **.NET**: Not required for self-contained version

## Phase A Features Implemented

✅ Fixed 50 TPS tick scheduler with read-parallel/write-serialized execution
✅ Deterministic command queue for input handling
✅ DiffLog system for write-phase mutations
✅ Deterministic RNG with named streams
✅ World/Chunk structure (32×32 tiles per chunk)
✅ LOD system (L0-L4) for performance
✅ Event bus for post-commit events
✅ Game state management with transitions
✅ SadConsole v10 integration

## Development

To rebuild from source:
```
cd src/HumanFortress.App
dotnet build
dotnet run
```

To publish new executable:
```
cd src/HumanFortress.App
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o ../../publish
```
