# Pack-Specific Player Settings Overrides

**Status:** ✅ **IMPLEMENTED** (Phase 1 & 2 Complete - October 10, 2025)  
**Created:** October 9, 2025  
**Author:** AI Assistant (based on user request)

## Implementation Status

✅ **Phase 1 Complete** - Core Infrastructure
- Model classes created (PackSettingsOverrides, CaptureOverrides, AudioOverrides, OcrOverrides)
- JSON serialization with snake_case naming
- Nullable types for selective overrides

✅ **Phase 2 Complete** - AuthorStudio UI
- New "Studio Settings Overrides" tab added
- Three-section layout (Capture, Audio, OCR) with enable/disable checkboxes
- Value sliders with min/max enforcement
- Description TextBox for explaining overrides
- Save/Preview/Clear commands functional
- Auto-loads existing player-overrides.json when pack opened

⏳ **Phase 3 Pending** - Studio (Player) Integration
- Load overrides when pack loads
- Show pack override notification in Settings tab
- Visual indicators for overridden settings
- User accept/reject controls

## Problem Statement

Different games have different optimal playback characteristics. For example:
- **Fast-paced games** might need shorter audio fade times and lower gap thresholds
- **Slow dialogue games** might benefit from longer gaps to avoid overlap
- **Story-heavy RPGs** might want higher OCR confidence to reduce errors
- **Action games** might prioritize performance over OCR accuracy

Currently, users must manually adjust Player settings for each game, which is cumbersome.

## Proposed Solution

Allow pack authors to define **recommended overrides** for Player settings in their pack configuration. When a pack is loaded in Studio (Player), these overrides are applied on top of user defaults.

### Key Design Principles

1. **User retains control**: Overrides are recommendations, not locks
2. **Transparency**: UI shows which settings came from pack
3. **Selective overrides**: Only override settings that matter for this game
4. **Persistence**: User can accept/reject/modify pack overrides per session

## Pack Configuration Format

### New File: `{PackFolder}/Configuration/player-overrides.json`

```json
{
  "version": "1.0",
  "description": "Optimized settings for FF1 Pixel Remaster",
  "overrides": {
    "capture": {
      "target_fps": 15,
      "confidence_threshold": 0.85,
      "enable_optimization": true,
      "enable_duplicate_detection": true
    },
    "audio": {
      "master_volume": 80,
      "enable_crossfade": true,
      "playback_speed": 1.0,
      "enable_caching": true
    },
    "ocr": {
      "confidence_threshold": 0.7,
      "enable_preprocessing": true,
      "scale_factor": 2.0,
      "convert_to_grayscale": true
    }
  }
}
```

### Schema Definition

```json
{
  "$schema": "http://json-schema.org/draft-07/schema#",
  "title": "Pack Player Settings Overrides",
  "type": "object",
  "properties": {
    "version": {
      "type": "string",
      "description": "Schema version",
      "const": "1.0"
    },
    "description": {
      "type": "string",
      "description": "Human-readable explanation of why these overrides exist"
    },
    "overrides": {
      "type": "object",
      "properties": {
        "capture": {
          "type": "object",
          "properties": {
            "target_fps": { 
              "type": "integer", 
              "minimum": 1, 
              "maximum": 60,
              "description": "Frame capture rate in FPS"
            },
            "confidence_threshold": { 
              "type": "number", 
              "minimum": 0.0, 
              "maximum": 1.0,
              "description": "Textbox detection confidence"
            },
            "enable_optimization": {
              "type": "boolean",
              "description": "Use search area optimization"
            },
            "enable_duplicate_detection": {
              "type": "boolean",
              "description": "Skip duplicate frames"
            }
          }
        },
        "audio": {
          "type": "object",
          "properties": {
            "master_volume": {
              "type": "integer",
              "minimum": 0,
              "maximum": 100,
              "description": "Master audio volume (0-100)"
            },
            "enable_crossfade": {
              "type": "boolean",
              "description": "Use crossfading between clips"
            },
            "playback_speed": {
              "type": "number",
              "minimum": 0.5,
              "maximum": 2.0,
              "description": "Audio playback speed multiplier"
            },
            "enable_caching": {
              "type": "boolean",
              "description": "Enable audio file caching"
            }
          }
        },
        "ocr": {
          "type": "object",
          "properties": {
            "confidence_threshold": {
              "type": "number",
              "minimum": 0.0,
              "maximum": 1.0,
              "description": "Minimum OCR confidence"
            },
            "enable_preprocessing": {
              "type": "boolean",
              "description": "Apply image preprocessing"
            },
            "scale_factor": {
              "type": "number",
              "minimum": 1.0,
              "maximum": 4.0,
              "description": "Image scaling for OCR"
            },
            "convert_to_grayscale": {
              "type": "boolean",
              "description": "Convert to grayscale before OCR"
            }
          }
        }
      }
    }
  },
  "required": ["version", "overrides"]
}
```

## Player Settings UI Changes

### Settings Tab Enhancement

Add a new section at the TOP of Settings tab:

```
┌─ Pack Settings Overrides ────────────────────────────────┐
│ ℹ️ This pack recommends custom settings:                  │
│                                                           │
│ "Optimized for FF1's slow dialogue pace and simple UI"   │
│                                                           │
│ ✓ Capture: Target FPS → 15 (default: 10)                 │
│ ✓ Capture: Confidence Threshold → 0.85 (default: 0.7)    │
│ ✓ OCR: Scale Factor → 2.5 (default: 2.0)                 │
│                                                           │
│ [Use Pack Settings]  [Ignore and Use Defaults]  [Edit]   │
└───────────────────────────────────────────────────────────┘
```

**Visual Indicators:**
- Settings with pack overrides show **blue highlight** in settings list
- Tooltip shows: "Pack Override: [value] (Your Default: [value])"
- User can toggle individual overrides on/off

### AuthorStudio: New "Studio Settings Overrides" Tab

Add a new dedicated tab for Player Settings Overrides (separating it from AuthorStudio's own settings):

**Tab Structure:**
- Discovery
- Speakers
- Voice Lab
- Pack Builder
- **Studio Settings Overrides** ← NEW
- **Author Studio Settings** ← RENAMED from "Settings"

**UI Layout:**

```
┌─ Studio Settings Overrides ──────────────────────────────┐
│                                                           │
│ Configure recommended player settings for this pack.     │
│ These will be suggested to users when loading your pack  │
│ in GameWatcher Studio (Player).                          │
│                                                           │
│ [✓] Override Capture Settings                            │
│     Target FPS: [15] (1-60)                               │
│     Confidence Threshold: [0.85] (0.0-1.0)                │
│     Enable Optimization: [✓]                              │
│     Enable Duplicate Detection: [✓]                       │
│                                                           │
│ [✓] Override Audio Settings                              │
│     Master Volume: [80] (0-100)                           │
│     Enable Crossfade: [✓]                                 │
│     Playback Speed: [1.0] (0.5-2.0)                       │
│     Enable Caching: [✓]                                   │
│                                                           │
│ [✓] Override OCR Settings                                │
│     Confidence Threshold: [0.7] (0.0-1.0)                 │
│     Enable Preprocessing: [✓]                             │
│     Scale Factor: [2.0] (1.0-4.0)                         │
│     Convert to Grayscale: [✓]                             │
│                                                           │
│ Description (why these settings?):                        │
│ ┌─────────────────────────────────────────────────────┐   │
│ │ Optimized for FF1's slow dialogue pace and simple  │   │
│ │ UI. Lower FPS reduces CPU usage while maintaining  │   │
│ │ perfect detection of text changes.                 │   │
│ └─────────────────────────────────────────────────────┘   │
│                                                           │
│ [Save to player-overrides.json]  [Preview JSON]  [Clear] │
└───────────────────────────────────────────────────────────┘
```

**Benefits of Separate Tab:**
- Clear separation: AuthorStudio settings vs Player settings
- More space to explain each override's purpose
- Won't overwhelm pack authors who don't need overrides
- Easy to find when needed

## Implementation Plan

### Phase 1: Core Infrastructure

1. **Model Classes**
   ```csharp
   public class PackSettingsOverrides
   {
       public string Version { get; set; } = "1.0";
       public string Description { get; set; } = "";
       public CaptureOverrides? Capture { get; set; }
       public AudioOverrides? Audio { get; set; }
       public OcrOverrides? Ocr { get; set; }
   }
   
   public class CaptureOverrides
   {
       public int? TargetFps { get; set; }
       public double? ConfidenceThreshold { get; set; }
       public bool? EnableOptimization { get; set; }
       public bool? EnableDuplicateDetection { get; set; }
   }
   
   public class AudioOverrides
   {
       public int? MasterVolume { get; set; }
       public bool? EnableCrossfade { get; set; }
       public double? PlaybackSpeed { get; set; }
       public bool? EnableCaching { get; set; }
   }
   
   public class OcrOverrides
   {
       public double? ConfidenceThreshold { get; set; }
       public bool? EnablePreprocessing { get; set; }
       public double? ScaleFactor { get; set; }
       public bool? ConvertToGrayscale { get; set; }
   }
   ```

2. **Pack Loader Integration**
   - Extend `PackLoader` to load `player-overrides.json`
   - Add to `GamePack` model: `PlayerOverrides` property

3. **Settings Merge Logic**
   - Create `SettingsMerger` service
   - Merge user defaults + pack overrides → effective settings
   - Track which settings came from pack vs user

### Phase 2: UI Implementation

1. **Studio Settings Tab**
   - Add PackOverridesViewModel
   - Render pack overrides section at top of Settings tab
   - Visual indicators for overridden settings (blue highlight)
   - Toggle controls for accept/reject individual overrides
   - "Reset to Pack Defaults" button

2. **AuthorStudio New Tab: "Studio Settings Overrides"**
   - Create new TabItem in MainWindow.xaml
   - Rename existing "Settings" tab to "Author Studio Settings"
   - Add StudioSettingsOverridesViewModel
   - Three-section layout: Capture, Audio, OCR
   - Checkbox to enable/disable each section
   - Description text area for explaining overrides
   - Validation for value ranges (enforce min/max)
   - "Save to player-overrides.json" button
   - "Preview JSON" button to show generated file
   - "Clear All Overrides" button

### Phase 3: User Experience

1. **Pack Load Notification**
   - Toast/banner: "This pack has recommended settings. [Apply Now] [Review]"
   - Activity Log entry: "Loaded pack with 3 setting overrides"

2. **Settings Persistence**
   - Save user's override accept/reject choices per pack
   - Restore choices on next pack load
   - Store in user's AppData (not in pack folder)

## Settings Eligibility for Overrides

### ✅ **Eligible Settings** (Pack CAN override these)

Based on user feedback, the following settings are eligible for pack-specific overrides:

**Capture Settings:**
- **Capture Rate (FPS)** - Frame capture rate (1-60 FPS)
  - Reason: Different games need different frame rates (slow RPGs vs fast action)
- **Enable Optimization** - Search area optimization
  - Reason: Some games benefit from this, others don't
- **Confidence Threshold** - Textbox detection sensitivity (0.0-1.0)
  - Reason: Different UI designs require different thresholds
- **Enable Duplicate Detection** - Skip duplicate frames
  - Reason: Performance vs accuracy tradeoff varies by game

**OCR Settings:**
- **Confidence Threshold** - Minimum OCR confidence (0.0-1.0)
  - Reason: Trade speed for accuracy based on game text quality
- **Enable Preprocessing** - Image preprocessing for OCR
  - Reason: Some fonts need preprocessing, others don't
- **Scale Factor** - Image scaling for OCR (1.0-4.0)
  - Reason: Different text sizes require different scaling
- **Convert to Grayscale** - Grayscale conversion before OCR
  - Reason: Some games have colored text that needs special handling

**Audio Settings:**
- **Master Volume** - Audio volume (0-100)
  - Reason: Balance voiceover with game audio
- **Enable Crossfade** - Crossfading between clips
  - Reason: Some games need smooth transitions, others benefit from hard cuts
- **Playback Speed** - Audio playback speed
  - Reason: Match game pacing (fast battles vs slow exploration)
- **Enable Caching** - Audio file caching
  - Reason: Performance tradeoff varies by pack size

### ❌ **Ineligible Settings** (Pack CANNOT override)

**General Settings (User Preferences & System Config):**
- Auto Start Monitoring - User workflow preference
- Game Detection Polling Rate - System-specific (not game-specific)
- Pack Directories - File system configuration
- Audio Device - Personal hardware preference (speakers vs headphones)
- Theme/appearance - Personal choice
- Hotkeys - Muscle memory
- Diagnostics/logging - Developer settings
- Window position/size - User preference

**Rationale:** These settings are personal preferences or system-specific configurations that should never be controlled by pack authors. Detection polling rate is about how often to check if the game window exists - this is a system performance preference, not a game-specific requirement.

### 📋 **Summary: Override Scope**

**Overridable Categories:**
- ✅ All Capture settings (4 settings)
- ✅ All OCR settings (4 settings)
- ✅ Audio settings (4 settings: volume, crossfade, speed, caching)
- ❌ General settings (not game-specific)
- ❌ Audio Device (personal hardware preference)

**Total:** 12 overridable settings out of 16 total settings

## Security & Validation

**Risks:**
- Malicious packs could set extreme values (FPS=1000, Volume=10.0)
- Broken overrides could make packs unusable

**Mitigations:**
1. **Schema Validation**: Enforce min/max bounds on all values
2. **Sandboxed Defaults**: If override JSON is malformed, fall back to user defaults
3. **User Review UI**: Show diff before applying overrides
4. **Reset Button**: Always allow user to reset to defaults
5. **Safe Mode**: Hold Shift during pack load to skip overrides

## Example Use Cases

### Use Case 1: FF1 Pixel Remaster

**Problem**: Default settings too aggressive for slow-paced RPG  
**Solution**:
```json
{
  "description": "Optimized for FF1's slow dialogue pace and simple UI",
  "overrides": {
    "capture": { 
      "target_fps": 15,
      "confidence_threshold": 0.85
    },
    "ocr": {
      "scale_factor": 2.5,
      "enable_preprocessing": true
    }
  }
}
```
**Benefit**: Reduces CPU usage by 50% while maintaining perfect detection

### Use Case 2: Fast-Paced Visual Novel

**Problem**: Rapid dialogue needs high frame rate and fast detection  
**Solution**:
```json
{
  "description": "Optimized for fast dialogue transitions",
  "overrides": {
    "capture": {
      "target_fps": 30,
      "enable_duplicate_detection": false
    },
    "audio": {
      "enable_crossfade": true,
      "playback_speed": 1.1
    }
  }
}
```
**Benefit**: Catches every dialogue change without missing rapid transitions

### Use Case 3: Retro Game with Pixel Font

**Problem**: Low OCR confidence due to pixel art text  
**Solution**:
```json
{
  "description": "Enhanced OCR for pixel fonts",
  "overrides": {
    "ocr": {
      "confidence_threshold": 0.6,
      "scale_factor": 3.0,
      "convert_to_grayscale": false
    }
  }
}
```
**Benefit**: Better text recognition without missing colored pixel fonts
```json
{
  "overrides": {
    "ocr": {
      "min_confidence": 0.6,
      "enable_autocorrect": true
    }
  }
}
```

## Future Enhancements

### Conditional Overrides

Allow overrides based on game state:

```json
{
  "overrides": {
    "battle": {
      "audio": { "gap_threshold_ms": 100 }
    },
    "dialogue": {
      "audio": { "gap_threshold_ms": 800 }
    }
  }
}
```

### Community Presets

- Share override configs on marketplace
- Users rate effectiveness
- Popular presets bubble up

### A/B Testing

- Pack authors test multiple override configs
- Gather telemetry (with user consent)
- Optimize for best user experience

## Migration Path

**For existing packs without overrides:**
- Pack loads normally (no overrides = use defaults)
- No breaking changes

**For pack authors:**
1. Add `player-overrides.json` to pack folder
2. AuthorStudio detects and offers to create it
3. Export pack includes overrides

## Open Questions

1. Should overrides be **per-game** or **per-pack**?
   - Decision: Per-pack (different voice actors might need different timing)

2. Should we allow **user-specific overrides** that persist?
   - Decision: Yes - save in user settings as `pack-{id}-overrides-accepted: true`

3. How granular should overrides be?
   - Decision: Start with category-level (capture/audio/ocr), expand to individual settings later

## Next Steps

1. ✅ **Create this design document**
2. ✅ **Get user feedback** on proposed UI and schema
3. ✅ **Implement Phase 1** (models + pack loader)
4. ✅ **Implement Phase 2** (AuthorStudio UI)
5. ⏳ **Implement Phase 3** (Studio Player integration)
6. ⏳ **Test with FF1 pack** as reference

---

## Implementation Notes (October 10, 2025)

### Changes from Original Design

**Audio Device Removed:**
- Original design included `audio_device` as an overridable setting
- **Decision**: Removed from implementation - this is a personal hardware preference (speakers vs headphones), not a pack-specific optimization
- Updated eligible settings count: 12 overridable settings (was 13)

**Tab Naming Clarified:**
- Renamed "Settings" → "Author Studio Settings" for clarity
- "Studio Settings Overrides" remains as designed
- Clear distinction helps authors understand: 
  - **Author Studio Settings** = How AuthorStudio itself behaves (audio format, TTS, OCR fixes)
  - **Studio Settings Overrides** = Recommended player settings to ship with the pack

### Files Created

1. `GameWatcher.AuthorStudio/Models/PackSettingsOverrides.cs`
   - PackSettingsOverrides, SettingsOverrides, CaptureOverrides, AudioOverrides, OcrOverrides
   - JSON serialization with snake_case (`target_fps`, `enable_crossfade`, etc.)
   - Nullable types for selective overrides

2. `GameWatcher.AuthorStudio/ViewModels/OverridesViewModel.cs`
   - Properties for all 12 overridable settings
   - Commands: SaveOverrides, PreviewJson, ClearOverrides
   - Auto-loads existing player-overrides.json on pack load
   - Only saves enabled sections (null sections omitted from JSON)

3. `GameWatcher.AuthorStudio/Views/MainWindow.xaml` (new tab)
   - "Studio Settings Overrides" tab with three GroupBoxes
   - Sliders with min/max enforcement matching schema
   - Description TextBox with helpful placeholder
   - Save/Preview/Clear buttons with emoji icons

### Integration

- Registered in DI container (`App.xaml.cs`)
- Added to MainWindowViewModel constructor
- PropertyChanged handler watches PackBuilderViewModel.OutputFolder
- Automatically calls `OverridesViewModel.SetPackDirectory()` when pack loads

### Validation

All schema constraints enforced in UI:
- Capture FPS: 1-60 (integer slider, tick=1)
- Capture Confidence: 0.0-1.0 (double slider, tick=0.05)
- Audio Volume: 0-100 (integer slider, tick=5)
- Audio Speed: 0.5-2.0 (double slider, tick=0.1)
- OCR Confidence: 0.0-1.0 (double slider, tick=0.05)
- OCR Scale: 1.0-4.0 (double slider, tick=0.5)

**Status**: ✅ AuthorStudio implementation complete and tested

---

**Status**: Phase 1 & 2 Complete - Awaiting Phase 3 (Studio Player integration)
