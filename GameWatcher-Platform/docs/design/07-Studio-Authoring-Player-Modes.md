# GameWatcher Suite — Studio (Player) and Author Studio (Creator)

### What We Actually Built
```
GameWatcher-Platform/
├── GameWatcher.Studio/        # Player GUI
├── GameWatcher.AuthorStudio/  # Authoring tools (scaffolded)
├── GameWatcher.Runtime/       # Headless orchestrator
├── GameWatcher.Engine/        # Core engine
└── FF1.PixelRemaster/         # Sample pack
```

### Canonical Structure
```
GameWatcher-Platform/
├── GameWatcher.Studio/        # Player app (ships to players)
├── GameWatcher.AuthorStudio/  # Authoring tools (ships to creators)
├── GameWatcher.Runtime/       # Headless orchestrator (shared)
├── GameWatcher.Engine/        # Core services (shared)
└── GamePacks/                 # Community packs (optional)
```

### Current "Studio" Reality Check
Our current "Studio" project is essentially **crude discovery mode** masquerading as a player:
- ✅ Captures game frames perfectly
- ✅ Detects textboxes reliably
- ✅ Extracts text via OCR accurately
- ❌ No catalogue lookup for recognized text
- ❌ No speaker identification or audio playback
- ❌ No actual "playing" of voiceover content
- ❌ No authoring tools for pack creation

**Bottom Line**: GameWatcher Studio is the Player UI. Authoring lives in GameWatcher Author Studio.

## Overview

**ARCHITECTURE CORRECTION**: Our original design mixed up the project purposes. What we built as "Studio" is the Player UI (GameWatcher Studio). True authoring tools live in GameWatcher Author Studio. This design establishes the corrected architecture and separation of concerns.

## Business Model & Legal Strategy

### Commercial Core + Free Content Model
- ✅ **Sell the Platform**: GameWatcher Suite (Runtime + Studio + Engine) 
- ✅ **Give Away Packs**: Community-created game content (legally protected)
- ✅ **Legal Protection**: We sell tools, not copyrighted game content

**"We sell the hammer, not the house."** - Perfect IP separation strategy.

## Current State Analysis

### What We Actually Have
- **Detection & OCR Pipeline**: Captures and recognizes text from game windows
- **Basic UI**: Pack Manager, Activity Monitor, Settings (player-focused interface)
- **No Catalogue Integration**: Text detection works but isn't matched against any dialogue database
- **No Audio Playback**: OCR results aren't converted to speech or played back

### Reality Check
Our current loop is essentially **Discovery Mode** without the authoring tools:
- ✅ Captures game frames
- ✅ Detects textboxes 
- ✅ Extracts text via OCR
- ❌ No catalogue lookup for recognized text
- ❌ No speaker identification
- ❌ No audio generation/playback
- ❌ No dialogue storage for authoring

## Corrected Dual-Project Architecture

### GameWatcher Studio (Player)
**Purpose**: Simple, clean player interface for end users who just want to hear games talk.

**Target Audience**: 95% of users - casual gamers who want plug-and-play voiceover experience.

**Current State**: Has the GUI framework but needs catalogue integration and audio playback.

#### Player Project Components
```csharp
GameWatcher.Studio/
├── Views/
│   ├── PlayerWindow.xaml          # Rename from MainWindow
│   ├── PackBrowser.xaml           # Simple pack selection
│   └── NowPlaying.xaml            # Live dialogue display
├── Services/
│   ├── PackRuntimeLoader.cs       # Load and validate packs
│   ├── CatalogueEngine.cs         # Dialogue lookup and matching
│   └── AudioPlaybackManager.cs    # TTS playback pipeline
└── ViewModels/
    ├── PlayerViewModel.cs         # Main player interface
    └── PlaybackViewModel.cs       # Audio playback controls
```

### GameWatcher Author Studio (Creator) — Scaffolded
**Purpose**: Full authoring suite for pack creators and advanced users.

**Target Audience**: 5% of users - content creators, modders, advanced enthusiasts.

**Location**: `C:\Code Projects\GameWatcher\GameWatcher-Platform\GameWatcher.AuthorStudio\`

**Note**: This will be a **completely separate project** from our current misnamed "Studio".

#### Studio Project Architecture (NEW)
```csharp
GameWatcher.AuthorStudio/              # Authoring tools
├── Discovery/
│   ├── DiscoverySession.cs     # Manage discovery sessions
│   ├── DialogueCatalogBuilder.cs # Build catalogues from OCR
│   └── DiscoveryWindow.xaml    # Live discovery interface
├── Authoring/
│   ├── DialogueEditor.xaml     # Review and edit discovered text
│   ├── SpeakerManager.xaml     # Assign voices to characters
│   └── VoiceStudio.xaml        # Preview and generate TTS
├── PackBuilder/
│   ├── PackManifestEditor.xaml # Pack metadata and settings
│   ├── BuildPipeline.cs        # Generate final pack files
│   └── TestingSuite.cs         # Validate pack quality
└── Services/
    ├── VoiceGenerationPipeline.cs # Bulk TTS generation
    ├── SpeakerAnalyzer.cs         # AI-powered speaker detection
    └── PackageManager.cs          # Export and distribution
```

#### Core Authoring Components

**1. Discovery Session Manager**
```csharp
public class DiscoverySession : IDisposable
{
    public DiscoveryMode Mode { get; set; } // Passive, Active, Assisted
    public TimeSpan SessionDuration { get; }
    public int UniqueDialogueFound { get; }
    public ObservableCollection<PendingDialogueEntry> DiscoveredDialogue { get; }
    
    public Task StartDiscoveryAsync();
    public Task PauseDiscoveryAsync();
    public Task<PackBuildResult> BuildPackAsync();
}
```

**2. Dialogue Catalog Builder**
```csharp
public class DialogueCatalogBuilder
{
    // Take raw OCR results and build structured catalogue
    public Task<DialogueEntry> ProcessOcrResultAsync(string text, Bitmap screenshot);
    public Task<SpeakerSuggestion[]> SuggestSpeakersAsync(DialogueEntry[] entries);
    public Task<ValidationResult> ValidateDialogueSetAsync(DialogueEntry[] entries);
}
```

**3. Voice Generation Pipeline**
```csharp
public class VoiceGenerationPipeline
{
    public Task<AudioGenerationResult> GenerateBulkAudioAsync(DialogueEntry[] entries);
    public Task<VoicePreview> PreviewVoiceAsync(DialogueEntry entry, VoiceProfile voice);
    public Task<PackageResult> PackageVoiceFilesAsync(string packDirectory);
}
```

#### Authoring UI Flow

```
Discovery Tab:
├── Session Controls (Start/Pause/Stop Discovery)
├── Live Feed (Current detection + OCR results) 
├── Session Stats (Unique dialogue found, session time)
└── Quick Actions (Export session, Build pack)

Dialogue Review Tab:  
├── Discovered Dialogue Grid (Text, Timestamp, Screenshot)
├── Speaker Assignment (Manual + AI suggestions)
├── Text Editing (Clean up OCR errors)
└── Approval Workflow (Mark ready for voice generation)

Voice Studio Tab:
├── Speaker Profile Manager (Voice selection, settings)
├── Bulk Generation Tools (Generate all, batch processing)
├── Preview & Testing (Individual line preview)
└── Quality Control (Regenerate poor quality audio)

Pack Builder Tab:
├── Pack Metadata (Name, description, version)
├── Build Pipeline (Generate catalogues, package files)
├── Testing Suite (Validate against target game)
└── Export Options (Local pack, community upload)
```

### 🎮 **Runtime Project Transformation** (Current "Studio" Becomes This)
Transform our existing project into actual playback functionality with catalogue integration.

#### Core Components

**1. Catalogue Runtime Engine**
```csharp
public class CatalogueRuntimeEngine
{
    private Dictionary<string, DialogueEntry> _dialogueIndex;
    private Dictionary<string, SpeakerProfile> _speakerProfiles;
    
    public Task<AudioPlaybackResult> ProcessDetectedTextAsync(string text);
    public Task<SpeakerProfile> IdentifySpeakerAsync(string text);
    public Task PreloadAudioCacheAsync();
}
```

**2. Audio Playback Manager**
```csharp
public class AudioPlaybackManager
{
    public Task PlayDialogueAsync(DialogueEntry entry);
    public Task QueueNextLineAsync(DialogueEntry entry);
    public Task HandleSkipRequestAsync();
    public AudioPlaybackStatus CurrentStatus { get; }
}
```

**3. Pack Runtime Loader**
```csharp
public class PackRuntimeLoader
{
    public Task<LoadResult> LoadPackAsync(string packPath);
    public Task<PackManifest> ValidatePackAsync(string packPath);
    public Task<CatalogueData> LoadCataloguesAsync(string packPath);
}
```

#### Player UI Flow

```
Now Playing Tab:
├── Current Game Status (Detected game, loaded pack)
├── Live Dialogue Display (Current text, speaker, audio status)
├── Playback Controls (Skip, replay, volume)
└── Session Stats (Lines played, accuracy, performance)

Pack Manager Tab: (Enhanced)
├── Installed Packs (Local packs with metadata)
├── Pack Details (Dialogue count, voice profiles, compatibility)
├── Pack Actions (Load, unload, update, remove)
└── Community Browser (Download new packs)

Activity Monitor Tab: (Refocused)
├── Playback Performance (Audio latency, cache hits)
├── Detection Accuracy (Recognition success rate)
├── System Resources (Memory, CPU usage)
└── Session History (Recent dialogue, error log)
```

## Technical Implementation Strategy

### Phase 1: Project Restructure (Week 1)

**1.1 Confirm App Roles (No Rename)**
```text
# Final naming (no renames required)
GameWatcher.Studio       = Player GUI
GameWatcher.AuthorStudio = Authoring tools
GameWatcher.Runtime      = Headless orchestrator
GameWatcher.Engine       = Shared core services
```

**1.2 Verify Author Studio Project**
```bash
# Author Studio already scaffolded
dir GameWatcher-Platform/GameWatcher.AuthorStudio/
```

**1.2 Refactor Current Capture Service**
```csharp
public class CaptureService
{
    public CaptureMode Mode { get; set; } // Discovery, Playback
    
    // Author mode: Store all detected text for review
    public event EventHandler<DialogueDiscoveredEventArgs> DialogueDiscovered;
    
    // Player mode: Lookup text in catalogue and play audio
    public event EventHandler<DialogueMatchedEventArgs> DialogueMatched;
}
```

### Phase 2: Runtime Player Implementation (Week 2-3)

**2.1 Transform Current "Studio" into Runtime Player**
- Rename project and update branding  
- Add catalogue lookup functionality to capture service
- Implement audio playback pipeline
- Replace "discovery logging" with "pack playback"

**2.2 Player-Focused UI Simplification**
- Simplify interface for casual users (95% of audience)
- Add "Now Playing" display for current dialogue
- Implement pack browser and selection
- Remove authoring-focused complexity

### Phase 3: Dialogue Authoring Tools (Week 4-6)

**3.1 Authoring Project Foundation (V2 Scope)**
- Create separate WPF project for dialogue cataloguing
- Build discovery session management
- Implement dialogue capture and review interface
- **Focus**: Keep it simple - dialogue only, no advanced features

**3.2 Essential Authoring Workflow**
- Speaker analysis and assignment tools
- Voice generation and preview system  
- Pack building and export pipeline
- Basic pack sharing and distribution

### Phase 4: SDK Foundation Planning (V3+ Roadmap)

**4.1 Plugin Architecture Design**
```csharp
// Future SDK for advanced extensions
public interface IGameWatcherPlugin
{
    string Name { get; }
    Task<bool> CanHandleGame(string executable);
    Task<GameEvent[]> ProcessGameState(GameContext context);
    Task<OverlayData> GenerateOverlayAsync(GameState state);
}

// Example: Fantasy RPG Plugin (inspired by Gyre)
public class FantasyRPGPlugin : IGameWatcherPlugin
{
    // Character drafting, scoring, predictions
    // Live leaderboards, viewer engagement
    // Twitch integration, channel points
}
```

**4.2 Platform Extensibility Vision**
- **Fantasy Gaming**: Draft characters, score points, live leaderboards
- **Advanced Analytics**: Combat analysis, efficiency metrics, completion tracking  
- **Streaming Tools**: Dynamic overlays, viewer predictions, community challenges
- **Custom Detection**: Game-specific event recognition beyond dialogue
- **Marketplace**: Community plugins, advanced packs, premium features

### Phase 4: Polish & Integration (Week 5)

**4.1 Mode Switching**
- Seamless transition between modes
- Shared settings and preferences
- Data migration between modes

**4.2 Advanced Features**
- Pack testing workflow
- Community pack integration
- Analytics and reporting

## Data Flow Transformation

### Current Flow (Crude Discovery)
```
Game Window → Screen Capture → Text Detection → OCR → Display Text
```

### Author Mode Flow
```
Game Window → Screen Capture → Text Detection → OCR → 
    → Dialogue Storage → Speaker Analysis → 
    → Voice Generation → Pack Building
```

### Player Mode Flow  
```
Game Window → Screen Capture → Text Detection → OCR →
    → Dialogue Lookup → Speaker Identification → 
    → Audio Playback → User Experience
```

## Migration Strategy

### Backwards Compatibility
- Current users see "Author Mode" by default (preserves existing behavior)
- "Player Mode" is additive functionality
- Settings and configuration remain shared
- No breaking changes to existing workflows

### User Experience Transition
1. **Immediate**: Current users continue working as before (discovery/authoring)
2. **Progressive**: Player mode becomes available as catalogue features mature
3. **Future**: Users naturally graduate from authoring to playing their own packs

## Success Metrics

### Author Mode Goals
- **Discovery Efficiency**: 80% reduction in manual dialogue entry
- **Voice Generation**: Bulk TTS generation in under 5 minutes per pack
- **Pack Quality**: 95% dialogue recognition accuracy in generated packs

### Player Mode Goals  
- **Playback Latency**: <200ms from text detection to audio start
- **Recognition Accuracy**: 90% successful dialogue lookup
- **User Experience**: Seamless real-time voiceover during gameplay

## Product Roadmap & Strategic Vision

### � **V2: Dialogue Foundation** (Ship First)
- **GameWatcher.Studio**: Simple player for dialogue packs
 - **GameWatcher Author Studio**: Dialogue cataloguer + voice generator
- **Core Value**: "Make your favorite RPGs talk"
- **Scope**: Dialogue detection, OCR, TTS, pack creation

### 🚀 **V3+: Platform SDK Vision** (Future Roadmap)
Expand beyond dialogue into full game analytics and community engagement:

```
GameWatcher Platform SDK
├── 🎭 Dialogue System (V2 - shipping)
├── 🏈 Fantasy Gaming (inspired by twitch.tv/gyre)
├── 📊 Live Analytics & Scoring
├── 🎮 Custom Game Events
├── 📡 Advanced Streaming Integration
└── 🌐 Community Marketplace
```

**Vision**: Transform into the "Fantasy Football for RPG Streaming" - where viewers draft characters, track stats, predict outcomes, and engage with streamers in real-time.

### 🏗️ **Separate Projects Architecture**
- **GameWatcher.Studio**: Simple player for 95% of users  
 - **GameWatcher Author Studio**: Dialogue tools for 5% of creators
- **GameWatcher.SDK**: Plugin system for advanced developers (V3+)
- **Better UX**: Each audience gets interface designed for their needs

### 💰 **Commercial Strategy**  
- **Sell Platform Suite**: Runtime + Studio + Engine (our technology)
- **Free Community Packs**: User-generated content (legal protection)
- **IP Separation**: We own the tools, community creates the content
- **Legal Loophole**: "We sell the hammer, not the house"

### 🎯 **User Segmentation**
```
Casual Gamers (95%):
├── Want: Simple, plug-and-play voiceover experience
├── Use: GameWatcher.Studio
└── Pay: One-time platform purchase

Content Creators (5%):
├── Want: Full authoring and pack creation tools  
├── Use: Author Studio + Studio
└── Pay: Same platform purchase + community recognition
```

Correction applied above.

## Product Strategy & Market Positioning

### 🎯 **V2: Focused Launch Strategy**
**Ship the Dialogue Cataloguer First**
- ✅ **Achievable Scope**: Dialogue detection, TTS, pack creation
- ✅ **Clear Value Prop**: "Make your favorite RPGs talk"  
- ✅ **Proven Market**: Existing demand for game voiceover
- ✅ **Foundation**: Sets up platform for future expansion

### 🚀 **V3+: Platform Vision Marketing**
**Tease the Bigger Picture**
- 🎮 **"Fantasy Football for RPG Streaming"** - community engagement
- 📊 **Advanced Game Analytics** - beyond just dialogue
- 🏆 **Creator Tools** - custom events, scoring systems, overlays
- 🌐 **Community Marketplace** - plugins, extensions, premium features

### 💡 **Strategic Benefits of Roadmap Approach**

**Public Interest Generation:**
- Creates anticipation for future features
- Attracts different types of users (players, streamers, developers)
- Establishes GameWatcher as platform, not just tool
- Builds community around long-term vision

**Business Development:**
- Dialogue system proves technical capability
- SDK vision attracts advanced users and partners
- Platform approach justifies premium pricing
- Creates multiple revenue opportunities

**Technical Foundation:**
- V2 dialogue system becomes one plugin of many
- Proper architecture supports future extensions  
- Community feedback guides V3+ development
- Proven user base before big platform investment

## Conclusion

**Present**: Build essential dialogue cataloguer (V2) - simple, focused, shippable
**Future**: Expand into full gaming analytics platform (V3+) - ambitious, extensible, scalable  

This approach lets us **ship practical value now** while **building excitement for the vision**. Users get immediate benefit from dialogue packs, while the roadmap keeps them engaged and attracts new audiences (streamers, developers, gaming communities).

**Key Insight**: The dialogue cataloguer isn't the end goal - it's the foundation for transforming how people experience and engage with classic RPGs in the streaming era.
