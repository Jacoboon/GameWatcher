# Voice Lab - Audio Effects System

**Status:** Phase 1 Complete ✅ (Core Effects Engine)  
**Created:** October 10, 2025  
**Last Updated:** October 10, 2025  
**Author:** AI Assistant (based on user request)

## Implementation Status

✅ **Phase 1 Complete** - Core Effects Engine (October 10, 2025)
- IAudioEffect interface and base classes
- **5 Effects Implemented:**
  - VolumeEffect (gain adjustment in dB)
  - LowPassFilterEffect (muffled/underwater sounds)
  - HighPassFilterEffect (telephone/robot sounds)
  - EchoEffect (delay with decay and feedback)
  - ReverbEffect (Schroeder reverb with 8 comb filters)
- AudioEffectsEngine with chain processing
- **6 Built-in Presets:**
  - Telephone, Underwater, Whisper (communication/emotional)
  - Cave Echo, Cathedral, Small Room (environmental/spatial)
- Console test app with 12 test cases
- All effects tested and working with real audio ✅

⏳ **Phase 2 Pending** - Voice Lab UI
⏳ **Phase 3 Pending** - Preset System
⏳ **Phase 4 Pending** - AI Preset Generator
⏳ **Phase 5 Pending** - Export & Runtime Integration

## Overview

The Voice Lab is a critical feature in AuthorStudio that allows pack authors to apply audio effects to TTS-generated voiceover files. This enables creative sound design without requiring external audio editing tools.

## Problem Statement

TTS-generated audio is clean and neutral, but games often need varied audio characteristics:
- **Environmental effects** - Echoes in caves, muffled voices underwater
- **Communication effects** - Radio distortion, telephone quality, PA system
- **Emotional effects** - Whispers, shouts, distant voices
- **Special effects** - Robot voices, dream sequences, ethereal sounds

Currently, authors must:
1. Export TTS audio
2. Open external DAW (Audacity, etc.)
3. Apply effects manually
4. Export and re-import
5. Repeat for every line variation

This is time-consuming and requires audio engineering knowledge.

## Proposed Solution

**Voice Lab** - An integrated, non-destructive audio effects system within AuthorStudio.

### Key Design Principles

1. **Non-Destructive Workflow** - Never modify original TTS files
2. **Real-Time Preview** - Hear effects instantly without re-encoding
3. **Preset Library** - Built-in effects for common scenarios
4. **AI-Powered Generation** - No-code preset creation via natural language
5. **Runtime Application** - Effects applied during playback, not baked into files
6. **Export Options** - Option to "bake" effects for final distribution

## Architecture

### Core Components

```
Voice Lab System
├── AudioEffectsEngine (NAudio-based)
│   ├── Effect Processors (Volume, Filters, Reverb, Echo, etc.)
│   ├── Effects Chain Pipeline
│   └── Real-time Playback with Effects
├── VoiceLabViewModel
│   ├── Selected Audio File
│   ├── Effects Chain Management
│   ├── Preset Management
│   └── AI Preset Generator
└── Metadata Storage
    ├── {audio_file}.effects.json (per-file effects)
    └── presets/ (built-in + custom presets)
```

### Data Flow

```
TTS Audio File (original.mp3)
    ↓
Load into Voice Lab
    ↓
Author applies effects (non-destructive)
    ↓
Save effects.json metadata
    ↓
Player Runtime: Load audio + effects.json → Apply effects chain → Playback
```

## Technical Design

### Non-Destructive vs Destructive Editing

**Decision: Non-Destructive (Runtime Effects)**

| Aspect | Non-Destructive (✅ Chosen) | Destructive (❌) |
|--------|---------------------------|-----------------|
| Original preservation | ✅ Always keep original | ❌ Original lost |
| Iteration speed | ✅ Instant preview | ❌ Re-encode each change |
| Disk usage | ✅ Store metadata only | ❌ Multiple audio files |
| Flexibility | ✅ Revert/tweak anytime | ❌ Must regenerate |
| Professional standard | ✅ DAW industry norm | ❌ Destructive workflow |

**Implementation:**
- Base audio: `voices/character_name/line_001.mp3` (never modified)
- Effects metadata: `voices/character_name/line_001.effects.json`
- Runtime: Apply effects chain during playback
- Optional: "Bake Effects" export for final MP3

### Effects Metadata Format

**File: `line_001.effects.json`**

```json
{
  "version": "1.0",
  "audio_file": "line_001.mp3",
  "preset_name": "Cave Echo",
  "effects": [
    {
      "type": "LowPassFilter",
      "enabled": true,
      "parameters": {
        "cutoff_frequency": 3000,
        "resonance": 0.5
      }
    },
    {
      "type": "Reverb",
      "enabled": true,
      "parameters": {
        "room_size": 0.7,
        "damping": 0.5,
        "wet_level": 0.3,
        "dry_level": 0.7
      }
    },
    {
      "type": "Volume",
      "enabled": true,
      "parameters": {
        "gain_db": -3.0
      }
    }
  ]
}
```

### Preset Format

**File: `presets/builtin/cave_echo.json`**

```json
{
  "name": "Cave Echo",
  "description": "Large reverberant space with long echo tail",
  "category": "Environmental",
  "effects": [
    {
      "type": "Reverb",
      "parameters": {
        "room_size": 0.9,
        "damping": 0.3,
        "wet_level": 0.5,
        "dry_level": 0.5
      }
    },
    {
      "type": "Echo",
      "parameters": {
        "delay_ms": 400,
        "decay": 0.6,
        "wet_level": 0.4
      }
    }
  ]
}
```

## NAudio Implementation

### Available Effects

NAudio supports the following effects through `ISampleProvider` chain:

**Volume & Dynamics:**
- ✅ Volume adjustment (gain in dB)
- ✅ Normalize (auto-level)
- ✅ Fade in/out
- ✅ Compressor/Limiter

**Frequency (EQ):**
- ✅ Low-pass filter (BiQuadFilter)
- ✅ High-pass filter (BiQuadFilter)
- ✅ Band-pass filter
- ✅ Parametric EQ

**Time-based:**
- ✅ Echo/Delay
- ✅ Reverb (can use external library or simple implementation)
- ⚠️ Pitch shift (complex, may need SoundTouch)
- ⚠️ Time stretch (complex, may need SoundTouch)

**Distortion:**
- ✅ Overdrive/Clipping
- ✅ Bit crushing (digital/retro effect)
- ✅ Noise gate

**Spatial:**
- ✅ Panning (left/right balance)
- ✅ Stereo width

### Effect Implementation Pattern

```csharp
public interface IAudioEffect
{
    string Type { get; }
    bool Enabled { get; set; }
    Dictionary<string, object> Parameters { get; set; }
    ISampleProvider Apply(ISampleProvider source);
}

public class LowPassFilterEffect : IAudioEffect
{
    public string Type => "LowPassFilter";
    public bool Enabled { get; set; } = true;
    public Dictionary<string, object> Parameters { get; set; } = new();
    
    public ISampleProvider Apply(ISampleProvider source)
    {
        var cutoff = (float)Parameters["cutoff_frequency"];
        var resonance = (float)Parameters["resonance"];
        return new LowPassFilterSampleProvider(source, cutoff, resonance);
    }
}

public class LowPassFilterSampleProvider : ISampleProvider
{
    private readonly ISampleProvider source;
    private readonly BiQuadFilter[] filters;
    
    public WaveFormat WaveFormat => source.WaveFormat;
    
    public LowPassFilterSampleProvider(ISampleProvider source, float cutoffFreq, float q)
    {
        this.source = source;
        filters = new BiQuadFilter[source.WaveFormat.Channels];
        for (int i = 0; i < filters.Length; i++)
        {
            filters[i] = BiQuadFilter.LowPassFilter(
                source.WaveFormat.SampleRate, 
                cutoffFreq, 
                q
            );
        }
    }
    
    public int Read(float[] buffer, int offset, int count)
    {
        int samplesRead = source.Read(buffer, offset, count);
        
        for (int i = 0; i < samplesRead; i++)
        {
            int channel = i % filters.Length;
            buffer[offset + i] = filters[channel].Transform(buffer[offset + i]);
        }
        
        return samplesRead;
    }
}
```

### Effects Chain Pipeline

```csharp
public class AudioEffectsEngine
{
    public ISampleProvider ApplyEffectsChain(
        ISampleProvider source, 
        List<IAudioEffect> effects)
    {
        ISampleProvider current = source;
        
        foreach (var effect in effects.Where(e => e.Enabled))
        {
            current = effect.Apply(current);
        }
        
        return current;
    }
    
    public async Task PlayWithEffects(
        string audioFilePath, 
        List<IAudioEffect> effects)
    {
        using var reader = new AudioFileReader(audioFilePath);
        var effectsChain = ApplyEffectsChain(reader, effects);
        
        using var output = new WaveOutEvent();
        output.Init(effectsChain);
        output.Play();
        
        while (output.PlaybackState == PlaybackState.Playing)
        {
            await Task.Delay(100);
        }
    }
}
```

## AI Preset Generator

### Design Decision: AI-Powered No-Code Effect Creation

**Problem:** Authors may not know audio engineering terminology or how to achieve specific sounds.

**Solution:** Natural language effect generation using OpenAI.

### Implementation

```csharp
public class AiPresetGenerator
{
    private readonly OpenAiTtsService _openAi;
    
    public async Task<EffectPreset> GenerateFromDescription(string description)
    {
        var prompt = BuildPrompt(description);
        var response = await _openAi.GenerateText(prompt);
        return ParseEffectPreset(response);
    }
    
    private string BuildPrompt(string description)
    {
        return $@"You are an audio engineer. Create an audio effects chain to achieve this sound:

'{description}'

Available effects and their parameters:

1. LowPassFilter
   - cutoff_frequency: 500-8000 Hz (removes frequencies above this)
   - resonance: 0.0-1.0 (peak at cutoff)

2. HighPassFilter
   - cutoff_frequency: 50-2000 Hz (removes frequencies below this)
   - resonance: 0.0-1.0

3. Reverb
   - room_size: 0.0-1.0 (0=small room, 1=large hall)
   - damping: 0.0-1.0 (0=bright, 1=dark/muffled)
   - wet_level: 0.0-1.0 (effect amount)
   - dry_level: 0.0-1.0 (original signal)

4. Echo
   - delay_ms: 50-2000 (time between echoes)
   - decay: 0.0-1.0 (how fast echoes fade)
   - wet_level: 0.0-1.0

5. Volume
   - gain_db: -20 to 20 (negative = quieter, positive = louder)

6. Distortion
   - amount: 0.0-1.0 (0=clean, 1=heavy distortion)

7. Panning
   - balance: -1.0 to 1.0 (-1=left, 0=center, 1=right)

Respond ONLY with valid JSON in this exact format:
{{
  ""name"": ""Generated Effect Name"",
  ""description"": ""Brief explanation"",
  ""effects"": [
    {{
      ""type"": ""LowPassFilter"",
      ""parameters"": {{
        ""cutoff_frequency"": 3000,
        ""resonance"": 0.5
      }}
    }}
  ]
}}";
    }
}
```

### Example AI Interactions

**User Input:** "Make this sound like it's coming from an old telephone"

**AI Response:**
```json
{
  "name": "Old Telephone",
  "description": "Band-limited audio with slight distortion simulating vintage telephone quality",
  "effects": [
    {
      "type": "HighPassFilter",
      "parameters": {
        "cutoff_frequency": 300,
        "resonance": 0.3
      }
    },
    {
      "type": "LowPassFilter",
      "parameters": {
        "cutoff_frequency": 3000,
        "resonance": 0.4
      }
    },
    {
      "type": "Distortion",
      "parameters": {
        "amount": 0.15
      }
    },
    {
      "type": "Volume",
      "parameters": {
        "gain_db": -2.0
      }
    }
  ]
}
```

**User Input:** "Ethereal dream sequence voice"

**AI Response:**
```json
{
  "name": "Ethereal Dream",
  "description": "Soft, spacious voice with heavy reverb and subtle echo",
  "effects": [
    {
      "type": "Reverb",
      "parameters": {
        "room_size": 0.95,
        "damping": 0.7,
        "wet_level": 0.6,
        "dry_level": 0.4
      }
    },
    {
      "type": "Echo",
      "parameters": {
        "delay_ms": 250,
        "decay": 0.5,
        "wet_level": 0.3
      }
    },
    {
      "type": "LowPassFilter",
      "parameters": {
        "cutoff_frequency": 6000,
        "resonance": 0.2
      }
    },
    {
      "type": "Volume",
      "parameters": {
        "gain_db": -4.0
      }
    }
  ]
}
```

## UI Design

### Voice Lab Tab Layout

```
┌─ Voice Lab ──────────────────────────────────────────────────┐
│                                                               │
│ ┌─ Audio Selection ──────────────────────────────────────┐   │
│ │ File: [voices/princess_sara/line_042.mp3  ] [📂 Browse] │   │
│ │ From: Discovery Entry #42 - "The prophecy speaks..."   │   │
│ └─────────────────────────────────────────────────────────┘   │
│                                                               │
│ ┌─ Waveform Viewer ──────────────────────────────────────┐   │
│ │  ▁▂▃▅▇█▇▅▃▂▁  ▁▂▃▅▇█▇▅▃▂▁  ▁▂▃▅▇█▇▅▃▂▁               │   │
│ │                                                        │   │
│ │  [▶ Play Original]  [▶ Play with Effects]  [⏹ Stop]   │   │
│ │  Duration: 3.2s  |  Volume: -12 dB  |  Sample: 44.1kHz│   │
│ └────────────────────────────────────────────────────────┘   │
│                                                               │
│ ┌─ Effects Chain ────────────────────────────────────────┐   │
│ │ ┌────────────────────────────────────────────────────┐ │   │
│ │ │ 1. ☑ Low-Pass Filter (3000 Hz) ──── [↑][↓][✕][⚙] │ │   │
│ │ │ 2. ☑ Reverb (Room: Medium) ──────── [↑][↓][✕][⚙] │ │   │
│ │ │ 3. ☐ Volume (-3 dB) DISABLED ────── [↑][↓][✕][⚙] │ │   │
│ │ └────────────────────────────────────────────────────┘ │   │
│ │                                                        │   │
│ │ [➕ Add Effect ▼]  [🗑 Clear All]  [📋 Load Preset ▼] │   │
│ └────────────────────────────────────────────────────────┘   │
│                                                               │
│ ┌─ Effect Parameters ────────────────────────────────────┐   │
│ │ 🎛 Low-Pass Filter                                     │   │
│ │                                                        │   │
│ │ Cutoff Frequency: [━━━━━●━━━━] 3000 Hz                │   │
│ │                   ↓            ↓                       │   │
│ │                  500          8000                     │   │
│ │                                                        │   │
│ │ Resonance:        [━━●━━━━━━━] 0.5                    │   │
│ │                   ↓            ↓                       │   │
│ │                  0.0          1.0                      │   │
│ └────────────────────────────────────────────────────────┘   │
│                                                               │
│ ┌─ AI Preset Generator ─────────────────────────────────┐   │
│ │ ✨ Describe the sound you want:                       │   │
│ │ ┌──────────────────────────────────────────────────┐  │   │
│ │ │ Make this sound like it's coming from an old    │  │   │
│ │ │ telephone with static and slight distortion     │  │   │
│ │ └──────────────────────────────────────────────────┘  │   │
│ │                                                        │   │
│ │ [✨ Generate Effect Chain]  [💾 Save as Preset...]    │   │
│ │                                                        │   │
│ │ Status: ✓ Generated "Old Telephone" preset with 4    │   │
│ │         effects. Click effects to adjust parameters.  │   │
│ └────────────────────────────────────────────────────────┘   │
│                                                               │
│ ┌─ Preset Library ───────────────────────────────────────┐   │
│ │ 🎭 Built-in Presets:                                   │   │
│ │ [Cave Echo]  [Radio]  [Underwater]  [Whisper]         │   │
│ │ [Shout]  [Distant]  [Robot]  [Dream Sequence]         │   │
│ │ [Telephone]  [Megaphone]  [Ghostly]  [Muffled]        │   │
│ │                                                        │   │
│ │ 💾 Custom Presets:                                     │   │
│ │ [My Battle Cry]  [Spooky Ghost]  [Ancient Spirit]    │   │
│ │                                                        │   │
│ │ [➕ Save Current as Preset]  [🗑 Delete Custom]        │   │
│ └────────────────────────────────────────────────────────┘   │
│                                                               │
│ ┌─ Actions ──────────────────────────────────────────────┐   │
│ │ [💾 Save Effects to .json]  [📤 Export with Effects]  │   │
│ │ [🔄 Reset to Original]      [📋 Copy to Clipboard]    │   │
│ └────────────────────────────────────────────────────────┘   │
│                                                               │
│ Status: Effects saved to line_042.effects.json               │
└───────────────────────────────────────────────────────────────┘
```

### UI Features

**Effects Chain:**
- Drag-and-drop reordering (up/down arrows)
- Enable/disable individual effects (checkbox)
- Remove effects (✕ button)
- Edit parameters (⚙ button expands parameters panel)
- Visual indication of effect order

**Waveform Viewer:**
- Visual representation of audio
- Play original vs play with effects comparison
- Real-time level meter during playback

**Parameters Panel:**
- Dynamic UI based on selected effect
- Sliders with value labels
- Min/max range indicators
- Real-time preview on change

**AI Generator:**
- Natural language input
- Loading indicator during generation
- Success/error feedback
- Option to save generated preset

**Preset Library:**
- Categorized presets (Built-in vs Custom)
- Quick-load buttons
- Preview description on hover
- Manage custom presets

## Built-in Presets

### Environmental

**1. Cave Echo**
```json
{
  "name": "Cave Echo",
  "description": "Large reverberant space with long echo tail",
  "effects": [
    {
      "type": "Reverb",
      "parameters": { "room_size": 0.9, "damping": 0.3, "wet_level": 0.5, "dry_level": 0.5 }
    },
    {
      "type": "Echo",
      "parameters": { "delay_ms": 400, "decay": 0.6, "wet_level": 0.4 }
    }
  ]
}
```

**2. Underwater**
```json
{
  "name": "Underwater",
  "description": "Muffled and distant as if submerged",
  "effects": [
    {
      "type": "LowPassFilter",
      "parameters": { "cutoff_frequency": 1500, "resonance": 0.7 }
    },
    {
      "type": "Reverb",
      "parameters": { "room_size": 0.6, "damping": 0.8, "wet_level": 0.4, "dry_level": 0.6 }
    },
    {
      "type": "Volume",
      "parameters": { "gain_db": -6.0 }
    }
  ]
}
```

### Communication

**3. Radio Distortion**
```json
{
  "name": "Radio",
  "description": "Two-way radio or walkie-talkie quality",
  "effects": [
    {
      "type": "HighPassFilter",
      "parameters": { "cutoff_frequency": 400, "resonance": 0.5 }
    },
    {
      "type": "LowPassFilter",
      "parameters": { "cutoff_frequency": 3500, "resonance": 0.4 }
    },
    {
      "type": "Distortion",
      "parameters": { "amount": 0.2 }
    }
  ]
}
```

**4. Telephone**
```json
{
  "name": "Telephone",
  "description": "Landline phone quality",
  "effects": [
    {
      "type": "HighPassFilter",
      "parameters": { "cutoff_frequency": 300, "resonance": 0.3 }
    },
    {
      "type": "LowPassFilter",
      "parameters": { "cutoff_frequency": 3000, "resonance": 0.4 }
    },
    {
      "type": "Volume",
      "parameters": { "gain_db": -2.0 }
    }
  ]
}
```

**5. Megaphone**
```json
{
  "name": "Megaphone",
  "description": "PA system or bullhorn",
  "effects": [
    {
      "type": "HighPassFilter",
      "parameters": { "cutoff_frequency": 500, "resonance": 0.6 }
    },
    {
      "type": "Distortion",
      "parameters": { "amount": 0.3 }
    },
    {
      "type": "Volume",
      "parameters": { "gain_db": 3.0 }
    }
  ]
}
```

### Emotional

**6. Whisper**
```json
{
  "name": "Whisper",
  "description": "Soft, quiet, close-mic whisper",
  "effects": [
    {
      "type": "HighPassFilter",
      "parameters": { "cutoff_frequency": 200, "resonance": 0.2 }
    },
    {
      "type": "Volume",
      "parameters": { "gain_db": -8.0 }
    }
  ]
}
```

**7. Shout**
```json
{
  "name": "Shout",
  "description": "Loud, aggressive, slight distortion",
  "effects": [
    {
      "type": "Volume",
      "parameters": { "gain_db": 6.0 }
    },
    {
      "type": "Distortion",
      "parameters": { "amount": 0.15 }
    }
  ]
}
```

**8. Distant**
```json
{
  "name": "Distant",
  "description": "Far away, muffled by distance",
  "effects": [
    {
      "type": "LowPassFilter",
      "parameters": { "cutoff_frequency": 2500, "resonance": 0.3 }
    },
    {
      "type": "Reverb",
      "parameters": { "room_size": 0.7, "damping": 0.6, "wet_level": 0.3, "dry_level": 0.7 }
    },
    {
      "type": "Volume",
      "parameters": { "gain_db": -5.0 }
    }
  ]
}
```

### Special Effects

**9. Robot**
```json
{
  "name": "Robot",
  "description": "Metallic, synthesized voice",
  "effects": [
    {
      "type": "HighPassFilter",
      "parameters": { "cutoff_frequency": 800, "resonance": 0.8 }
    },
    {
      "type": "Distortion",
      "parameters": { "amount": 0.4 }
    },
    {
      "type": "Echo",
      "parameters": { "delay_ms": 50, "decay": 0.3, "wet_level": 0.2 }
    }
  ]
}
```

**10. Dream Sequence**
```json
{
  "name": "Dream Sequence",
  "description": "Ethereal, spacious, otherworldly",
  "effects": [
    {
      "type": "Reverb",
      "parameters": { "room_size": 0.95, "damping": 0.7, "wet_level": 0.6, "dry_level": 0.4 }
    },
    {
      "type": "Echo",
      "parameters": { "delay_ms": 250, "decay": 0.5, "wet_level": 0.3 }
    },
    {
      "type": "LowPassFilter",
      "parameters": { "cutoff_frequency": 6000, "resonance": 0.2 }
    }
  ]
}
```

**11. Ghostly**
```json
{
  "name": "Ghostly",
  "description": "Spectral, haunting voice",
  "effects": [
    {
      "type": "Reverb",
      "parameters": { "room_size": 0.85, "damping": 0.5, "wet_level": 0.7, "dry_level": 0.3 }
    },
    {
      "type": "HighPassFilter",
      "parameters": { "cutoff_frequency": 400, "resonance": 0.4 }
    },
    {
      "type": "Echo",
      "parameters": { "delay_ms": 180, "decay": 0.6, "wet_level": 0.4 }
    },
    {
      "type": "Volume",
      "parameters": { "gain_db": -4.0 }
    }
  ]
}
```

**12. Muffled**
```json
{
  "name": "Muffled",
  "description": "Speaking through thick material or barrier",
  "effects": [
    {
      "type": "LowPassFilter",
      "parameters": { "cutoff_frequency": 1200, "resonance": 0.6 }
    },
    {
      "type": "Volume",
      "parameters": { "gain_db": -3.0 }
    }
  ]
}
```

## Implementation Plan

### Phase 1: Core Effects Engine (Week 1)

**Deliverables:**
- [ ] AudioEffectsEngine service
- [ ] IAudioEffect interface
- [ ] Basic effects implementations:
  - [ ] Volume
  - [ ] Low-Pass Filter
  - [ ] High-Pass Filter
  - [ ] Reverb (simple implementation)
  - [ ] Echo/Delay
- [ ] Effects chain pipeline
- [ ] Non-destructive playback with effects

**Files to Create:**
- `GameWatcher.Engine/Audio/AudioEffectsEngine.cs`
- `GameWatcher.Engine/Audio/Effects/IAudioEffect.cs`
- `GameWatcher.Engine/Audio/Effects/VolumeEffect.cs`
- `GameWatcher.Engine/Audio/Effects/LowPassFilterEffect.cs`
- `GameWatcher.Engine/Audio/Effects/HighPassFilterEffect.cs`
- `GameWatcher.Engine/Audio/Effects/ReverbEffect.cs`
- `GameWatcher.Engine/Audio/Effects/EchoEffect.cs`

### Phase 2: Voice Lab UI (Week 2)

**Deliverables:**
- [ ] VoiceLabViewModel
- [ ] Voice Lab tab XAML
- [ ] Effects chain management (add/remove/reorder/enable-disable)
- [ ] Parameter panel (dynamic UI per effect type)
- [ ] Play original vs play with effects
- [ ] Save/load effects.json

**Files to Create:**
- `GameWatcher.AuthorStudio/ViewModels/VoiceLabViewModel.cs`
- `GameWatcher.AuthorStudio/Views/VoiceLabTab.xaml` (update existing stub)
- `GameWatcher.AuthorStudio/Models/AudioEffectMetadata.cs`

### Phase 3: Preset System (Week 3)

**Deliverables:**
- [ ] Preset model and storage
- [ ] 12 built-in presets (JSON files)
- [ ] Preset manager (load/save/delete)
- [ ] Preset browser UI
- [ ] Apply preset to current audio

**Files to Create:**
- `GameWatcher.AuthorStudio/Models/EffectPreset.cs`
- `GameWatcher.AuthorStudio/Services/PresetManager.cs`
- `presets/builtin/` (12 JSON files)

### Phase 4: AI Preset Generator (Week 4)

**Deliverables:**
- [ ] AI preset generator service
- [ ] OpenAI prompt engineering
- [ ] JSON response parsing
- [ ] AI generator UI integration
- [ ] Save AI-generated presets

**Files to Create:**
- `GameWatcher.AuthorStudio/Services/AiPresetGenerator.cs`

### Phase 5: Export & Runtime Integration (Week 5)

**Deliverables:**
- [ ] "Bake Effects" export functionality
- [ ] Pack export includes effects.json files
- [ ] Player runtime loads and applies effects
- [ ] Performance optimization
- [ ] Documentation

**Files to Modify:**
- `GameWatcher.Engine/Packs/PackLoader.cs`
- `GameWatcher.Engine/Audio/AudioPlaybackService.cs`
- `GameWatcher.Studio` (player runtime effects application)

## Security & Performance

### Performance Considerations

**Real-time Effects:**
- Effects applied during playback, not pre-rendered
- CPU cost: Moderate (depends on effects complexity)
- Memory: Minimal (streaming audio, not buffered)

**Optimization Strategies:**
1. Use efficient NAudio sample providers
2. Cache effect processors (don't recreate per sample)
3. Optional "Bake Effects" for complex chains
4. Limit max effects in chain (e.g., 10 effects)

### Security

**AI-Generated Presets:**
- Validate JSON schema before applying
- Enforce parameter bounds (prevent extreme values)
- Sandbox: No file system or network access from effects

**Preset Files:**
- JSON schema validation
- Reject unknown effect types
- Clamp all parameters to safe ranges

## User Workflow Examples

### Example 1: Quick Preset Application

1. Author selects audio file in Voice Lab
2. Clicks "Cave Echo" preset
3. Hears preview with effects
4. Clicks "Save Effects"
5. Effects metadata saved, done!

### Example 2: Custom Effect Chain

1. Author loads audio
2. Adds Low-Pass Filter (3000 Hz)
3. Adds Reverb (room_size: 0.7)
4. Tweaks parameters with sliders
5. Plays with effects to hear result
6. Saves as custom preset "My Dungeon Voice"

### Example 3: AI-Generated Preset

1. Author types: "Make this sound like a ghost speaking from beyond the grave"
2. Clicks "Generate Effect Chain"
3. AI creates: Reverb + High-Pass + Echo + Volume
4. Author previews, adjusts reverb wet_level
5. Saves as "Ghostly Apparition"
6. Applies to all ghost character lines

## Future Enhancements

### Advanced Effects (Post-V1)
- [ ] Pitch shift (requires SoundTouch library)
- [ ] Time stretch (speed without pitch change)
- [ ] Chorus effect
- [ ] Flanger
- [ ] Phaser
- [ ] Parametric EQ with multiple bands
- [ ] Compressor with attack/release

### Workflow Improvements
- [ ] Batch apply preset to multiple files
- [ ] A/B comparison (two effect chains side-by-side)
- [ ] Undo/redo effect changes
- [ ] Effect chain templates per character
- [ ] Visual waveform with effect overlay

### AI Enhancements
- [ ] AI analyzes audio and suggests appropriate effects
- [ ] "Match this reference" - AI creates preset from sample
- [ ] Voice style transfer (experimental)

## Success Metrics

**Adoption:**
- % of pack authors using Voice Lab
- Average effects per audio file
- Most popular presets

**Quality:**
- Audio quality ratings
- Pack downloads (effects vs no effects)
- User feedback on effect realism

**Performance:**
- Effects application latency
- CPU usage during playback
- Memory footprint

---

## Appendix: NAudio Resources

**Key NAudio Classes:**
- `ISampleProvider` - Audio stream interface
- `AudioFileReader` - Load MP3/WAV files
- `WaveOutEvent` - Playback device
- `BiQuadFilter` - EQ filters
- `VolumeSampleProvider` - Volume control
- `OffsetSampleProvider` - Delay/echo building block

**External Libraries (Optional):**
- SoundTouch - High-quality pitch/tempo shifting
- CSCore - Alternative audio framework
- FMOD - Professional audio engine (commercial)

---

**Status:** Design complete - Ready for implementation  
**Next Step:** Begin Phase 1 - Core Effects Engine
