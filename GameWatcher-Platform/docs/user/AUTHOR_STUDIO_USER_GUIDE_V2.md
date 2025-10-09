# GameWatcher Author Studio User Guide

Welcome to GameWatcher Author Studio — creation tools for building dialogue packs. Use this app when you want to discover, edit, voice, and package dialogue for use in GameWatcher Studio (the player app).

## Architecture (V2 Rebuild - October 2025)

Author Studio has been rebuilt using modern MVVM architecture to match GameWatcher Studio:
- **MVVM Pattern**: ViewModels with CommunityToolkit.Mvvm, proper data binding, RelayCommands
- **Dependency Injection**: Microsoft.Extensions.DependencyInjection for services and ViewModels  
- **Logging**: Serilog with file and console outputs for troubleshooting
- **ModernWPF Theme**: Consistent dark theme matching Studio's visual design
- **Future-Ready**: Modular design supports V3+ expansion (Voice Lab, SDK plugins)

## Getting Started

- System requirements: Windows 10/11 (64-bit), .NET 8.0
- First launch: run `GameWatcher.AuthorStudio.exe`
- Logs: `logs/author-studio_<date>.log` for debugging
- Output: packaged packs that GameWatcher Studio can load and play

## Workflow Overview

### 1) Discovery Tab
- Start a discovery session to capture dialogue while playing a game.
- Live feed shows detected text; unique lines are added to the session list.
- Session status cards show: Status, Unique Lines Found, TTS Status
- Discovery log displays real-time capture events

### 2) Speakers Tab  
- Review speaker profiles: ID, Name, Voice, Speed, Effects
- Add/edit speakers manually or import from existing pack's `speakers.json`
- Preview button plays voice samples using Engine-level previews
- Effects field supports free-form tags (V3+ will add visual editor)

### 3) Voice Lab Tab (V3+ Placeholder)
- Future: Preset palette (Cave Echo, Throne Room, Random Pitch, etc.)
- Future: Effect chain editor with drag-and-drop reordering
- Future: Real-time auditioning with A/B compare
- Future: Per-effect randomness for variation
- Future: DSL export for advanced users

### 4) Pack Builder Tab
- Fill in pack metadata: Pack Name, Display Name, Version
- Output Folder: where the pack will be exported
- Open Pack: load existing pack for editing
- Export Pack: build and package the complete pack

### 5) Settings Tab
- **Audio Format**: Choose `wav` or `mp3` for generated audio
- **TTS API Key**: Save/Remove OpenAI API key (stored as `GWS_OPENAI_API_KEY`)
- Status messages show success/failure of operations

## Using Your Pack in GameWatcher Studio

- Place the built pack in a directory scanned by the player:
  - Default: `./packs/` next to the player executable, or
  - Any folder you added under Settings → Pack Directories in GameWatcher Studio
- Open GameWatcher Studio, go to Pack Manager, Load your pack, and play.

## Tips

- Keep discovery sessions focused (per area or chapter) for clean catalogs.
- Normalize punctuation and spacing for better matching.
- Favor shorter voice previews while tuning settings; switch to bulk when satisfied.
- Use logging (`logs/author-studio_*.log`) to troubleshoot issues

## TTS API Key (OpenAI)

- Configure TTS from Settings tab: paste your key into the password box and Save
- The app stores it in your user environment as `GWS_OPENAI_API_KEY`
- You can Remove the key at any time from the same tab
- Top bar shows status: `TTS ready` or `TTS unavailable: Configure key`
- If no key is set, TTS actions are disabled
- Advanced: a legacy fallback reads `Secrets/openai-api-key.txt` (first line). You can also set `GAMEWATCHER_SECRETS_DIR` to point at a folder containing that file.

## Voice Previews (Engine-Level)

- Speaker previews use engine-level cached audio from `GameWatcher.Engine/Voices/`
- Format: `<voice>-<speed>.mp3` (e.g., `alloy-1.2.mp3`)
- Sample phrase: "Hi! I'm <Name>. Calm. Excited! Curious? Let's begin."
- Previews are generated once and cached for reuse
- Batch generation script: `scripts/generate_engine_previews.ps1` (for developers)

## Architecture Details (for Developers)

### Dependency Injection
Services and ViewModels are registered in `App.xaml.cs`:
- **Services**: DiscoveryService, SpeakerStore, OpenAiTtsService, PackExporter, etc.
- **ViewModels**: MainWindowViewModel, DiscoveryViewModel, SpeakersViewModel, etc.
- All dependencies injected via constructor, enabling testability and modularity

### MVVM Data Flow
```
View (MainWindow.xaml)
  ↓ DataContext
ViewModel (MainWindowViewModel)
  ↓ Commands/Properties
Services (DiscoveryService, TtsService, etc.)
  ↓ Business Logic
Models (PendingDialogueEntry, SpeakerProfile)
```

### Future V3+ Extensibility
The architecture is designed for plugin-based expansion:
- Voice Lab: Full effects editor (reverb, pitch, EQ, compression)
- SDK Modules: Fantasy gaming, analytics, custom events
- Community Plugins: Extension points for advanced features

## References

- Design: `docs/design/04-Studio-Tools-Design.md`
- Effects System: `docs/design/08-Audio-Effects-UX.md`
- Voice Previews: `docs/design/09-Voice-Previews-and-TTS.md`
- Player Guide: `docs/user/USER_GUIDE.md`

## Troubleshooting

### App won't start
- Check `logs/author-studio_<date>.log` for startup errors
- Verify .NET 8.0 Runtime is installed
- Try running from command line: `dotnet run --project GameWatcher.AuthorStudio.csproj`

### TTS not working
- Verify API key is set in Settings tab
- Check environment variable: `[Environment]::GetEnvironmentVariable("GWS_OPENAI_API_KEY", "User")`
- Review logs for TTS-related errors

### Discovery session not capturing
- Ensure target game window is visible and active
- Check Engine configuration in appsettings.json
- Review logs for OCR/detection errors

## First-Time Author (Quick Start)

Follow this once to create your first pack and verify playback:

1) Start Discovery (5–10 min)
- Launch your game and open Author Studio.
- Go to Discovery tab, click "▶ Start Discovery"
- Play through a small scene.
- Click "⏹ Stop" and review the discovered lines list.

2) Configure Speakers (5 min)
- Go to Speakers tab
- Add speaker profiles or import from existing pack
- Assign voices and test with Preview buttons

3) Build Pack (3–5 min)
- Go to Pack Builder tab
- Fill in Pack Name, Display Name, Version
- Set Output Folder
- Click "📦 Export Pack"

4) Play It
- Copy the built pack into the player's `packs/` folder
- Open GameWatcher Studio (Player), Load the pack, and play the same scene

---

*GameWatcher Author Studio V2 - Rebuilt October 2025 with MVVM architecture for future extensibility*
