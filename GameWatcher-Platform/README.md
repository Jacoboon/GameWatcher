# GameWatcher V2 Platform

Modern, modular voiceover platform for classic games. Built with .NET 8, dependency injection, and a game pack architecture.

## Project Structure

```
GameWatcher-Platform/
├── GameWatcher.Engine/          # Universal detection, OCR, pack system
├── GameWatcher.Studio/          # Player app (end users)
├── GameWatcher.AuthorStudio/    # Pack creation tool (creators)
├── GameWatcher.Runtime/         # Headless orchestrator (automation)
├── FF1.PixelRemaster/           # Reference game pack (Final Fantasy I)
└── docs/                        # Design documentation
    ├── design/                  # Architecture & API specs
    └── user/                    # User guides
```

## Quick Start

### For End Users (Playing with Voiceovers)

```bash
# Build and run the player
dotnet run --project GameWatcher.Studio

# Expected: UI opens, detects FF1 when running, plays voiceovers
```

### For Pack Creators (Making Voice Packs)

```bash
# Build and run the authoring tool
dotnet run --project GameWatcher.AuthorStudio

# Expected: UI opens, can discover dialogue, generate voices
```

### For Developers (Headless Testing)

```bash
# Build and run the orchestrator
dotnet run --project GameWatcher.Runtime

# Expected: Console output, auto-detects games, processes dialogue
```

## Architecture

See [`docs/design/APPLICATION_ARCHITECTURE.md`](docs/design/APPLICATION_ARCHITECTURE.md) for detailed component responsibilities.

**Key Principles:**
- **Engine** = Universal (works for any game)
- **Packs** = Game-specific (FF1, Chrono Trigger, etc.)
- **Apps** = Different workflows (playing vs authoring)

All three apps use the same capture/detection pipeline:
- `ITextboxDetector` - Finds dialogue boxes (configured per game)
- `IOcrEngine` - Extracts text (Windows OCR or Tesseract)
- `GameCaptureService` - Orchestrates frame capture and processing

## Building

```bash
# Build entire solution
dotnet build GameWatcher-Platform.sln

# Build specific project
dotnet build GameWatcher.Studio/GameWatcher.Studio.csproj

# Build for release
dotnet build -c Release
```

## Configuration

Each app uses `appsettings.json` for configuration:

**Studio** (`GameWatcher.Studio/appsettings.json`):
- Game detection intervals
- Audio playback settings
- Pack directories

**AuthorStudio** (`GameWatcher.AuthorStudio/appsettings.json`):
- OpenAI API key for TTS
- Default speakers
- OCR fixes

**Runtime** (`GameWatcher.Runtime/appsettings.json`):
- Pack discovery paths
- Auto-start settings
- Processing pipeline config

## Logging

All apps use Serilog with structured logging:

- **Console:** Formatted output with timestamps and levels
- **Files:** `logs/<app-name>_<date>.log` (7-day retention)

Example log output:
```
[12:34:56.789] [INF] GameWatcher Studio starting up
[12:34:56.801] [DBG] 🎯 Targeted search: 1165x544 (79.3% reduction)
[12:34:56.805] [INF] 🎯 TEXTBOX FOUND: {X=378,Y=98,Width=1142,Height=169}
```

## Creating a New Game Pack

1. Create a new .NET 8 class library project
2. Reference `GameWatcher.Engine`
3. Implement detection config:

```csharp
using GameWatcher.Engine.Detection;
using System.Drawing;

namespace MyGame.Pack.Detection;

public static class MyGameDetectionConfig
{
    public static TextboxDetectionConfig GetConfig() => new()
    {
        // Game-specific border colors (RGB)
        BorderColors = new List<Color> 
        { 
            Color.FromArgb(255, 100, 100)  // Your game's textbox color
        },
        
        // Targeted search area (normalized percentages)
        TargetSearchArea = new RectangleF(20f, 60f, 60f, 30f), // X%, Y%, Width%, Height%
        
        // Size constraints
        MinSize = new Size(200, 50),
        MaxSize = new Size(2000, 400),
        
        // Matching tolerance
        ColorTolerance = 25,
        RequireLandscapeAspect = true
    };
}
```

4. See [`FF1.PixelRemaster/`](FF1.PixelRemaster/) for complete reference implementation

## Documentation

- **[APPLICATION_ARCHITECTURE.md](docs/design/APPLICATION_ARCHITECTURE.md)** - Required services per app
- **[DI_COMPLIANCE_CHECKLIST.md](docs/design/DI_COMPLIANCE_CHECKLIST.md)** - Service registration validation
- **[02-Game-Pack-System.md](docs/design/02-Game-Pack-System.md)** - Pack architecture details
- **[AGENTS.md](AGENTS.md)** - Development guidelines and conventions

## Troubleshooting

### "No game detected"
- Ensure game is running in borderless windowed mode (not exclusive fullscreen)
- Check `appsettings.json` detection intervals
- Verify pack is in search directories

### "Textbox not detected"
- Check detection config border colors match your game
- Adjust `TargetSearchArea` or use full-screen search (null)
- Enable debug logging to see detection attempts

### "OCR returns garbage"
- Verify Windows OCR language pack is installed (Settings → Language)
- Check textbox crop quality in logs
- Consider adding OCR fixes in pack configuration

## Contributing

See [`AGENTS.md`](AGENTS.md) for development conventions and architecture guidance.

**Before modifying DI configurations:**
1. Check `docs/design/APPLICATION_ARCHITECTURE.md` for mandatory services
2. Validate with `docs/design/DI_COMPLIANCE_CHECKLIST.md`
3. Test all three apps (Studio, AuthorStudio, Runtime)

## Version History

- **V2 (2025)**: Modern platform with DI, game pack system, three distinct apps
- **V1 (Archive/SimpleLoop)**: Original working prototype, preserved as reference

## License

[Your License Here]
