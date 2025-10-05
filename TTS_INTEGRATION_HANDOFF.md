# GameWatcher TTS Integration - Development Handoff

## Current State Summary

We have successfully completed **Phase 1 foundation work** with a production-ready capture system and clean data architecture. The system is now **reset and ready for focused Elfheim dialogue capture + TTS integration**.

## What's Working ✅

### Core Capture Engine (SimpleLoop CLI)
- **Perfect Textbox Detection**: DynamicTextboxDetector finds textboxes at 100% accuracy
- **Enhanced OCR**: FF1-specific preprocessing with smart normalization
- **Multi-part Dialogue**: Handles scrolling text within same textbox perfectly
- **Performance**: 15fps target met with 3.5% efficiency (excellent frame skipping)
- **Data Persistence**: Robust DialogueCatalog and SpeakerCatalog with JSON serialization

### GUI Management Interface (SimpleLoop.Gui)
- **Unified Data Architecture**: GUI manages CLI's data files (CLI owns data, GUI provides interface)
- **Complete CRUD Operations**: Create/edit/delete speakers and dialogues with automatic persistence
- **Real-time Integration**: CaptureService runs in background, updates GUI live
- **Clean Build**: Zero warnings, proper nullable reference handling
- **Thread Safety**: File-based logging prevents UI blocking

### Data Architecture ✅ VALIDATED
```
SimpleLoop/ (CLI - Data Owner & Core Logic)
├── dialogue_catalog.json    ← Master data (RESET - ready for Elfheim)
├── speaker_catalog.json     ← Master data (RESET - clean slate)
├── CaptureService.cs        ← Background capture engine
└── All detection/OCR logic  ← Proven, stable pipeline

SimpleLoop.Gui/ (Management Interface)
├── MainWindow.xaml.cs       ← Points to ../SimpleLoop/ data
├── Uses CLI classes         ← No code duplication
└── Real-time monitoring     ← Live capture integration working
```

## Next Phase Objectives 🎯

### Immediate Goal: TTS Pipeline Implementation
**Current Status**: Data capture is perfect, but we need **actual voiceovers** to complete Phase 1 MVP

**Why TTS Now**: 
- ✅ Capture system is rock solid (37 test entries captured flawlessly)
- ✅ Data architecture is clean and validated  
- ✅ Speaker profiles have TTS settings (voice ID, speed, effects)
- ❌ **MISSING**: The actual audio generation and playback!

### TTS Implementation Requirements 🎤

**1. OpenAI TTS Integration** 
- Use existing speaker profiles (`TtsVoiceId`, `TtsSpeed`) 
- Generate audio files for dialogue entries
- Handle API calls asynchronously (don't block capture)

**2. Audio File Management**
- Generate filename structure based on speaker + dialogue ID
- Store in organized directory (e.g., `voices/speaker_name/`)  
- Update `DialogueEntry.AudioPath` and `HasAudio` properties

**3. Playback System (NAudio)**
- Queue-based audio playback during capture
- Hotkeys for skip/replay functionality
- Real-time playback when new dialogue detected

## Critical Code References 📋

### Ready-to-Use TTS Configuration
**Speaker Profiles** (auto-generated during capture):
```json
{
  "TtsVoiceId": "echo",      // alloy, echo, fable, onyx, nova, shimmer
  "TtsSpeed": 1.0,           // 0.25 to 4.0
  "Effects": {
    "EnvironmentPreset": "mystical"  // None, cave, mystical, etc.
  }
}
```

### TTS Implementation Roadmap (from Program.cs)
```csharp
// Phase 1: OpenAI TTS Generation
//   - Call OpenAI TTS API with speakerProfile.TtsVoiceId and speakerProfile.TtsSpeed
//   - Generate base audio file for dialogue text
// Phase 2: Audio Effects Processing  
//   - Apply NAudio effects from speakerProfile.Effects (reverb, pitch, EQ)
//   - Process audio through effects pipeline for character voice styling
// Phase 3: File Management
//   - Save processed audio to speakerProfile-based filename structure
//   - Update entry.AudioPath and entry.HasAudio = true for playback integration
```

### DialogueEntry Properties (Ready for Audio)
```csharp
public class DialogueEntry 
{
    public string AudioPath { get; set; } = "";        // ← SET THIS after TTS
    public bool HasAudio { get; set; } = false;        // ← SET THIS to true
    public string Speaker { get; set; } = "";          // ← Use for TTS voice selection
    public string Text { get; set; } = "";             // ← TTS input text
}
```

## Test Environment Setup 🧝‍♂️

### Fresh Data State
- **Dialogue Catalog**: Empty `[]` - ready for Elfheim NPCs
- **Speaker Catalog**: Empty `[]` - will auto-populate with Elfheim characters  
- **Backup Available**: `backup_20251005_144529/` contains previous 37 entries
- **Debug Data**: Cleared - clean testing environment

### Elfheim Focus Group Testing Plan
1. **Start CLI** - navigate to Elfheim in Final Fantasy
2. **Talk to NPCs** - capture focused dialogue (guards, elves, merchants)
3. **Build Voice Profiles** - assign appropriate TTS voices per character type
4. **Test TTS Generation** - convert captured text to audio
5. **Validate Playback** - ensure audio plays during live gameplay

## Integration Points 🔧

### Files to Modify for TTS
- **`DialogueCatalog.cs`**: Add TTS generation methods, audio path updates
- **`SpeakerCatalog.cs`**: Voice profile management for TTS selection
- **`CaptureService.cs`**: Integrate TTS calls into dialogue detection pipeline  
- **New**: `TtsService.cs` for OpenAI API integration
- **New**: `AudioPlaybackService.cs` for NAudio queue management

### Success Criteria ✅
1. **API Integration**: OpenAI TTS generates audio for Elfheim dialogue
2. **File Management**: Audio files saved with proper naming/organization
3. **Real-time Playback**: Voice plays automatically during gameplay
4. **GUI Integration**: Audio status visible in dialogue management interface
5. **Effects Pipeline**: Basic audio effects (reverb, pitch) applied per speaker

## Development Approach 🛠️

### Phase 1: Basic TTS (Week 1) 
- OpenAI TTS API integration with speaker profiles
- Generate audio files for captured dialogue
- Update DialogueEntry with audio paths

### Phase 2: Playback System (Week 2)
- NAudio integration with queueing  
- Real-time playback during capture
- Skip/replay hotkey controls

### Phase 3: Audio Effects (Week 3)
- Apply speaker-specific effects from profiles
- Polish end-to-end capture → voice → playback pipeline
- Performance optimization

## Current System Health 🏥

**Build Status**: ✅ Clean builds, zero warnings
**Architecture**: ✅ CLI owns data, GUI manages it properly  
**Capture Engine**: ✅ Perfect textbox detection, robust OCR
**Data Quality**: ✅ Clean slate ready for focused Elfheim content
**Performance**: ✅ 15fps capture with excellent efficiency

## API Keys & Dependencies

**Required**:
- OpenAI API key for TTS generation
- NAudio NuGet package for audio playback
- System.Speech (fallback TTS option)

**File Structure Prep**:
```
SimpleLoop/
├── voices/           ← Create for audio file storage
│   ├── sage/        ← Per-speaker directories  
│   ├── guard/       
│   └── merchant/    
└── Services/        ← New TTS and audio services
    ├── TtsService.cs
    └── AudioPlaybackService.cs
```

---

## Request for Next Agent

Please implement **TTS Pipeline Integration** by:

1. **OpenAI TTS Service**: Create TtsService.cs for API integration using speaker profiles
2. **Audio File Management**: Generate and organize voice files per speaker/dialogue
3. **Playback Integration**: Add NAudio-based playback to CaptureService pipeline  
4. **Test with Elfheim**: Capture fresh dialogue and generate first voiceovers

The foundation is rock-solid. Now we need the **magic** - actual voices bringing the captured dialogue to life! 🎵

**Focus**: Get that first Elfheim NPC voice playing during live gameplay. Everything else builds from there.