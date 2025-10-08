# GameWatcher Platform V2 - Architecture Overview

## Vision Statement

GameWatcher Platform V2 transforms real-time game voiceover from a single-game solution into a **modular, extensible platform** that supports unlimited games through a **game pack system**.

## Core Architectural Principles

### 🎯 **Separation of Concerns**
- **Engine**: Game-agnostic core functionality (OCR, TTS, Audio)
- **Packs**: Game-specific detection and configuration logic  
- **Runtime**: Universal player that loads any pack
- **Studio**: Tools for creating and testing game packs

### 🔌 **Plugin Architecture**
```
GameWatcher.Runtime
├── Loads → GameWatcher.Engine (Core Services)
├── Loads → GamePack.dll (Game-Specific Logic)
└── Provides → Unified API for Streaming/GUI
```

### 🎮 **Game Pack System**
Each game becomes a **self-contained package**:
```
FF1.PixelRemaster/
├── Detection/
│   ├── TextboxDetectors/
│   ├── Templates/
│   └── Coordinates/
├── Audio/
│   ├── Speakers/
│   └── Voices/
├── Configuration/
│   ├── pack.json
│   └── detection.json
└── FF1.PixelRemaster.dll
```

## V1 → V2 Evolution

### What V1 Taught Us ✅

| **V1 Learning** | **V2 Implementation** |
|----------------|---------------------|
| Targeted search areas = 4.1x performance | Engine provides coordinate-based detection |
| OCR needs game-specific tuning | Packs define OCR parameters per game |
| Cross-compilation is messy | Clean dependency injection architecture |
| Performance matters for streaming | Engine optimized, Packs stay lightweight |
| Different games need different strategies | Plugin system handles variety naturally |

### Architecture Comparison

**V1 (Monolithic)**
```
SimpleLoop/
├── FF1-specific hardcoded logic ❌
├── Cross-compiled into GUI ❌  
├── Single game support ❌
└── Performance optimized ✅
```

**V2 (Modular)**
```
GameWatcher.Engine/      ← Core services
GameWatcher.Runtime/     ← Universal player  
GameWatcher.Packs/       ← Game-specific logic
└── GameWatcher.AuthorStudio/  ← Pack creation tools
```

## Component Architecture

### 🔧 **GameWatcher.Engine**
**Purpose**: Game-agnostic core services
```csharp
namespace GameWatcher.Engine
{
    // Core Services
    interface ITextDetectionEngine
    interface IAudioEngine  
    interface ICaptureEngine
    interface IOcrEngine
    
    // Plugin System
    interface IGamePack
    interface IDetectionStrategy
    interface ISpeakerProfile
}
```

### 🎮 **GameWatcher.Packs**
**Purpose**: Game-specific implementations
```csharp
namespace GameWatcher.Packs.FF1
{
    class FF1Pack : IGamePack
    class FF1TextboxDetector : IDetectionStrategy  
    class FF1SpeakerProfiles : ISpeakerCollection
}
```

### 🎬 **GameWatcher.Runtime**
**Purpose**: Universal game player
```csharp
namespace GameWatcher.Runtime
{
    class PackManager        // Loads/unloads game packs
    class GameWatcherHost    // Orchestrates everything
    class StreamingBridge    // Twitch/OBS integration
}
```

### 🛠️ GameWatcher Author Studio
**Purpose**: Pack creation and testing tools
```csharp
namespace GameWatcher.AuthorStudio
{
    class PackBuilder        // Creates new game packs
    class DetectionTester    // Tests textbox detection  
    class VoiceGenerator     // Bulk TTS generation
    class PackValidator      // Ensures pack quality
}
```

## Key Design Decisions

### 🚀 **Performance First**
- Engine uses **targeted detection areas** (learned from V1's 4.1x improvement)
- **Lazy loading** of pack resources
- **Async/await** throughout for non-blocking operations
- **Memory pooling** for high-frequency objects

### 🔄 **Hot Swapping**
- **Runtime pack switching** without restart
- **Live configuration updates** 
- **A/B testing** different detection strategies

### 🧪 **Testability**
- **Dependency injection** throughout
- **Mock-friendly interfaces**
- **Unit testable** detection algorithms
- **Integration test harnesses** for packs

### 📦 **Distribution**
- **NuGet packages** for Engine/Runtime
- **Zip-based pack distribution**
- **Automatic updates** via package manager
- **Community pack sharing**

## Success Metrics

### Developer Experience
- ⏱️ **New pack creation**: < 4 hours for experienced developer
- 🧪 **Testing cycle**: < 30 seconds for detection validation  
- 📦 **Pack deployment**: One-click publish to community

### Runtime Performance  
- 🚀 **Detection speed**: < 5ms average (maintaining V1's gains)
- 💾 **Memory usage**: < 200MB with 3 packs loaded
- 🔄 **Pack switching**: < 2 seconds hot swap

### User Experience
- 🎮 **Game support**: 10+ games within 6 months
- 🔧 **Configuration**: Zero-config for popular games
- 🎭 **Voice quality**: Indistinguishable from manual TTS

## Next Steps

1. **Engine Foundation**: Core interfaces and services
2. **FF1 Pack Port**: Migrate V1 logic as reference implementation  
3. **Runtime Shell**: Basic pack loading and orchestration
4. **Studio MVP**: Simple pack creation tools
5. **Community Platform**: Pack sharing and distribution

---

*This architecture leverages everything we learned from V1's 4.1x performance optimizations while creating a foundation for unlimited game support.*
