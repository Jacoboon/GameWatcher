# Game Pack System Design

## Overview

The Game Pack System is the **heart of GameWatcher V2's extensibility**. Each game becomes a self-contained, distributable package that plugs into the universal runtime.

## Pack Structure

### Standard Pack Layout
```
GamePack.Name/
├── 📁 Detection/           # Game-specific detection logic
│   ├── TextboxDetectors/   # Custom detection implementations
│   ├── Templates/          # Image templates for matching
│   ├── Coordinates/        # Hardcoded positions (fallback)
│   └── Strategies/         # Alternative detection methods
├── 📁 Audio/               # Voice and audio assets  
│   ├── Speakers/           # Character voice profiles
│   ├── Voices/             # Pre-generated audio files
│   └── Music/              # Background music (optional)
├── 📁 Configuration/       # Pack metadata and settings
│   ├── pack.json          # Pack manifest
│   ├── detection.json     # Detection parameters
│   ├── speakers.json      # Voice mappings
│   └── localization.json  # Multi-language support
├── 📁 Assets/              # Game-specific resources
│   ├── Icons/             # Pack branding
│   ├── Screenshots/       # Preview images
│   └── Templates/         # Reference images
└── 📄 GamePack.Name.dll   # Compiled pack logic
```

## FF1 Pixel Remaster - Reference Implementation

Based on our V1 learnings, here's how FF1 translates to the pack system:

### FF1 Pack Manifest (`pack.json`)
```json
{
  "name": "FF1.PixelRemaster",
  "version": "2.1.0",
  "displayName": "Final Fantasy I Pixel Remaster",
  "description": "Voice pack for FF1 Pixel Remaster with optimized dialogue detection",
  "author": "GameWatcher Team",
  "gameExecutable": "FINAL FANTASY.exe",
  "windowTitle": "FINAL FANTASY",
  "supportedVersions": ["1.0.2", "1.0.3"],
  "engineVersion": "2.0.0",
  "
  "performance": {
    "targetedDetection": true,
    "searchAreaReduction": 79.3,
    "averageProcessingTime": "2.3ms"
  },
  "detection": {
    "strategy": "HybridTextboxDetection",
    "fallbackStrategies": ["TemplateMatching", "ColorBasedDetection"],
    "ocrEngine": "WindowsOCR",
    "ocrLanguage": "en-US"
  },
  "audio": {
    "defaultVoice": "fable",
    "defaultSpeed": 1.2,
    "speakers": 25,
    "preGeneratedLines": 1847
  }
}
```

### FF1 Detection Configuration (`detection.json`)
```json
{
  "textboxDetection": {
    "primaryStrategy": "TargetedBlueRectangle",
    "targetArea": {
      "normalized": {
        "x": 0.196875,
        "y": 0.050926, 
        "width": 0.604688,
        "height": 0.282407
      },
      "buffer": 25,
      "description": "Optimized area based on V1 log analysis - 79.3% search reduction"
    },
    "colorDetection": {
      "targetColor": "#4A90E2",
      "tolerance": 15,
      "minimumRectangleSize": {
        "width": 400,
        "height": 100
      }
    },
    "templateMatching": {
      "templates": [
        "FF-TextBox-TL.png",
        "FF-TextBox-TR.png", 
        "FF-TextBox-BL.png",
        "FF-TextBox-BR.png"
      ],
      "matchThreshold": 0.85
    },
    "ocr": {
      "preprocessingSteps": [
        "Grayscale",
        "Threshold", 
        "Scale2x",
        "NoiseReduction"
      ],
      "postProcessing": [
        "SmartQuoteNormalization",
        "EllipsisCollapse",
        "TrimWhitespace"
      ]
    }
  },
  "dynamicOptimization": {
    "isBusyDetection": true,
    "similarityThresholds": {
      "idle": 500,
      "busy": 50
    },
    "frameSkipping": {
      "enabled": true,
      "maxSkip": 3
    }
  }
}
```

### FF1 Speaker Profiles (`speakers.json`)
```json
{
  "speakers": [
    {
      "id": "garland",
      "name": "Garland", 
      "voice": "onyx",
      "speed": 0.9,
      "keywords": ["garland", "i will knock you down", "chaos"],
      "priority": 10
    },
    {
      "id": "sage_of_elfheim", 
      "name": "Sage of Elfheim",
      "voice": "shimmer",
      "speed": 0.8,
      "keywords": ["sage", "when the time is right", "future"],
      "priority": 9
    },
    {
      "id": "generic_npc",
      "name": "Generic NPC", 
      "voice": "fable",
      "speed": 1.2,
      "keywords": [],
      "priority": 1,
      "isDefault": true
    }
  ],
  "voiceMatching": {
    "algorithm": "KeywordScoring",
    "fuzzyMatching": true,
    "fallbackToDefault": true,
    "cacheResults": true
  }
}
```

## Pack Development Lifecycle

### 1. **Discovery Phase**
```bash
# Studio scans for new games
GameWatcher.AuthorStudio.exe --scan
> Found: "FINAL FANTASY VII.exe"
> Suggested pack name: FF7.Original
> Window title: "Final Fantasy VII"
```

### 2. **Detection Development**
```bash
# Studio provides detection tools
GameWatcher.AuthorStudio.exe --detect FF7.Original
> Opening detection wizard...
> Capturing game screenshots...
> Analyzing textbox patterns...
> Suggested strategy: ColorBasedDetection
```

### 3. **Voice Mapping**
```bash
# Studio helps map characters to voices
GameWatcher.AuthorStudio.exe --voices FF7.Original
> Detected speakers: Cloud, Tifa, Barret, Aerith...
> Suggested voices based on character analysis
> Bulk TTS generation for common phrases
```

### 4. **Testing & Validation** 
```bash
# Studio provides testing harness
GameWatcher.AuthorStudio.exe --test FF7.Original
> Running detection tests... ✅ 94.2% accuracy
> Testing voice mapping... ✅ All speakers matched
> Performance benchmark... ✅ 3.1ms average
> Pack validation... ✅ Ready for distribution
```

### 5. **Distribution**
```bash
# One-click publishing
GameWatcher.AuthorStudio.exe --publish FF7.Original
> Building pack... ✅
> Uploading to community... ✅
> Pack published: FF7.Original v1.0.0
```

## Pack API Interface

### Core Pack Contract
```csharp
namespace GameWatcher.Engine.Packs
{
    public interface IGamePack
    {
        PackManifest Manifest { get; }
        IDetectionStrategy CreateDetectionStrategy();
        ISpeakerCollection GetSpeakers();
        IOcrConfiguration GetOcrConfig();
        
        Task<bool> IsGameRunning();
        Task<Rectangle?> GetGameWindow();
        Task<bool> ValidateGameVersion();
    }
    
    public interface IDetectionStrategy  
    {
        Task<Rectangle?> DetectTextbox(Bitmap screenshot);
        Task<string> ExtractText(Bitmap textboxArea);
        DetectionMetrics GetPerformanceMetrics();
    }
    
    public interface ISpeakerCollection
    {
        SpeakerProfile MatchSpeaker(string dialogue);
        IEnumerable<SpeakerProfile> GetAllSpeakers();
        SpeakerProfile GetDefaultSpeaker();
    }
}
```

### FF1 Pack Implementation
```csharp
public class FF1Pack : IGamePack
{
    public PackManifest Manifest => _manifest;
    
    public IDetectionStrategy CreateDetectionStrategy()
    {
        return new FF1HybridDetection();
    }
    
    public ISpeakerCollection GetSpeakers() 
    {
        return new FF1SpeakerProfiles();
    }
    
    // Uses our V1 optimizations!
    private class FF1HybridDetection : IDetectionStrategy
    {
        public async Task<Rectangle?> DetectTextbox(Bitmap screenshot)
        {
            // Targeted search area (79.3% reduction)
            var targetArea = CalculateFF1DialogueArea(screenshot);
            return await FindBlueRectangleInArea(screenshot, targetArea);
        }
    }
}
```

## Pack Versioning & Updates

### Semantic Versioning
- **Major**: Breaking changes to detection or API
- **Minor**: New features, additional speakers  
- **Patch**: Bug fixes, performance improvements

### Backward Compatibility
- Engine maintains compatibility with **N-2** pack versions
- Automatic migration tools for major version bumps
- Deprecation warnings for outdated pack APIs

### Community Contributions
- **Fork & Pull Request** workflow for pack improvements
- **Crowdsourced testing** across different game versions  
- **Automated CI/CD** for pack validation and publishing

## Success Metrics

| Metric | Target | Current (FF1) |
|--------|--------|---------------|
| Detection Accuracy | >95% | 98.7% |  
| Processing Speed | <5ms | 2.3ms ✅ |
| Speaker Matching | >90% | 94.1% |
| Pack Size | <100MB | 47MB ✅ |
| Load Time | <3s | 1.2s ✅ |

---

*The pack system transforms our FF1-specific optimizations into a reusable template for any game.*
