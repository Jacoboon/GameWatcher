# Pack-Specific Player Settings Overrides

**Status:** Design Proposal  
**Created:** October 9, 2025  
**Author:** AI Assistant (based on user request)

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
      "fps": 15,
      "confidence_threshold": 0.85
    },
    "audio": {
      "gap_threshold_ms": 500,
      "fade_in_ms": 100,
      "fade_out_ms": 150
    },
    "ocr": {
      "min_confidence": 0.7,
      "enable_autocorrect": true
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
      "description": "Schema version"
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
            "fps": { "type": "integer", "minimum": 1, "maximum": 60 },
            "confidence_threshold": { "type": "number", "minimum": 0.0, "maximum": 1.0 }
          }
        },
        "audio": {
          "type": "object",
          "properties": {
            "gap_threshold_ms": { "type": "integer", "minimum": 0, "maximum": 5000 },
            "fade_in_ms": { "type": "integer", "minimum": 0, "maximum": 1000 },
            "fade_out_ms": { "type": "integer", "minimum": 0, "maximum": 1000 },
            "volume": { "type": "number", "minimum": 0.0, "maximum": 1.0 }
          }
        },
        "ocr": {
          "type": "object",
          "properties": {
            "min_confidence": { "type": "number", "minimum": 0.0, "maximum": 1.0 },
            "enable_autocorrect": { "type": "boolean" }
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
│ "Optimized settings for FF1 Pixel Remaster"              │
│                                                           │
│ ✓ Audio Gap Threshold: 500ms (default: 1000ms)           │
│ ✓ Capture FPS: 15 (default: 30)                          │
│ ✓ OCR Min Confidence: 0.7 (default: 0.5)                 │
│                                                           │
│ [Use Pack Settings]  [Ignore and Use Defaults]  [Edit]   │
└───────────────────────────────────────────────────────────┘
```

**Visual Indicators:**
- Settings with pack overrides show **blue highlight** in settings list
- Tooltip shows: "Pack Override: [value] (Your Default: [value])"
- User can toggle individual overrides on/off

### AuthorStudio: Pack Builder Settings Editor

Add a new section in AuthorStudio Settings tab:

```
┌─ Player Settings Overrides (for this pack) ──────────────┐
│                                                           │
│ Define recommended player settings for optimal playback. │
│                                                           │
│ [ ] Override Capture Settings                            │
│     FPS: [15] (1-60)                                      │
│     Confidence Threshold: [0.85] (0.0-1.0)                │
│                                                           │
│ [ ] Override Audio Settings                              │
│     Gap Threshold: [500] ms                               │
│     Fade In: [100] ms                                     │
│     Fade Out: [150] ms                                    │
│                                                           │
│ [ ] Override OCR Settings                                │
│     Min Confidence: [0.7] (0.0-1.0)                       │
│     Auto-correct: [✓]                                     │
│                                                           │
│ Description:                                              │
│ [Optimized settings for FF1 Pixel Remaster              ]│
│                                                           │
│ [Save Pack Overrides]                                     │
└───────────────────────────────────────────────────────────┘
```

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
       public int? Fps { get; set; }
       public double? ConfidenceThreshold { get; set; }
   }
   
   public class AudioOverrides
   {
       public int? GapThresholdMs { get; set; }
       public int? FadeInMs { get; set; }
       public int? FadeOutMs { get; set; }
       public double? Volume { get; set; }
   }
   
   public class OcrOverrides
   {
       public double? MinConfidence { get; set; }
       public bool? EnableAutocorrect { get; set; }
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
   - Render pack overrides section at top
   - Visual indicators for overridden settings
   - Toggle controls for accept/reject overrides

2. **AuthorStudio Settings Tab**
   - Add Pack Overrides editor
   - Validation for value ranges
   - Export to `player-overrides.json` on save

### Phase 3: User Experience

1. **Pack Load Notification**
   - Toast/banner: "This pack has recommended settings. [Apply Now] [Review]"
   - Activity Log entry: "Loaded pack with 3 setting overrides"

2. **Settings Persistence**
   - Save user's override accept/reject choices per pack
   - Restore choices on next pack load

## Settings Eligibility for Overrides

### ✅ **Good Candidates** (Pack should override these)

**Capture Settings:**
- FPS (frame rate) - affects performance vs accuracy
- Confidence threshold - affects textbox detection sensitivity

**Audio Settings:**
- Gap threshold - prevents dialogue overlap
- Fade in/out times - smooth transitions
- Volume - balances with game audio

**OCR Settings:**
- Min confidence - trade speed for accuracy
- Auto-correct - enable for games with consistent text

### ❌ **Poor Candidates** (Pack should NOT override)

**General Settings:**
- Window position/size - user preference
- Theme/appearance - personal choice
- Hotkeys - muscle memory
- Diagnostics logging - developer settings

**Audio Settings (some):**
- Master mute - user control
- Device selection - hardware specific

**Capture Settings (some):**
- Monitor selection - hardware specific
- GPU acceleration - performance depends on user's GPU

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

**Problem**: Default 30 FPS is overkill for slow dialogue RPG  
**Solution**:
```json
{
  "overrides": {
    "capture": { "fps": 15 },
    "audio": { "gap_threshold_ms": 500 }
  }
}
```

### Use Case 2: Fast-Paced Visual Novel

**Problem**: Rapid dialogue needs low gap threshold  
**Solution**:
```json
{
  "overrides": {
    "audio": {
      "gap_threshold_ms": 200,
      "fade_in_ms": 50,
      "fade_out_ms": 50
    }
  }
}
```

### Use Case 3: Retro Game with Pixel Font

**Problem**: Low OCR confidence due to pixel art text  
**Solution**:
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
2. **Get user feedback** on proposed UI and schema
3. **Implement Phase 1** (models + pack loader)
4. **Implement Phase 2** (Studio UI)
5. **Implement Phase 3** (AuthorStudio editor)
6. **Test with FF1 pack** as reference

---

**Status**: Awaiting user feedback before implementation
