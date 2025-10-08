# Audio Effects UX (V2 Draft)

This document outlines a practical, incremental design for the Author Studio “Voice Lab.” The goal is to make auditioning and choosing vocal ambience/character fast and fun while keeping the underlying engine consistent and predictable.

## Objectives

- Fast audition: tweak → hear → keep/revert in seconds.
- Clear mental model: effects are an ordered chain (top → bottom).
- Presets first; details on demand; expert DSL fallback.
- Same graph in Author Studio and Runtime (WYSIWYG).
- Sensible defaults; safe ranges; small CPU footprint.

## UI Overview

- Preset Palette (Add Effect)
  - Categories: Environment, Pitch, Character, Fun
  - Click to add to the chain (at the end); drag to reorder
- Chain Overview
  - Chips with icon + short name; enable/disable; remove; reorder via drag
- Inspector Panel (context)
  - Sliders/dials for effect parameters with safe ranges
  - Simple ↔ Advanced toggle where applicable (e.g., EQ 2-band ↔ 5-band)
- Audition Bar
  - Play/Stop, A/B compare, Undo/Redo, Save Snapshot (per speaker)
- Variability Control
  - Optional per-effect randomness to add subtle variation per line

## MVP Presets

- Environment
  - Cave Echo → echo(delay=280ms, decay=0.45, mix=0.35)
  - Throne Room → echo(120ms, 0.30, 0.25) + echo(220ms, 0.25, 0.20)
  - Hall Reverb (future) → reverb(type=hall, preDelay=25ms, decay=1.8s, mix=0.2)
- Pitch
  - Pitch Up (+10%) → pitch(1.10)
  - Pitch Down (−10%) → pitch(0.90)
  - Random Pitch → pitch(random=0.9:1.1)
- Character
  - Warm EQ → eq(lowShelf@200Hz +2dB) + eq(highShelf@6k −1dB)
  - Bright EQ → eq(lowShelf@200Hz −2dB) + eq(highShelf@6k +2dB)
  - Compression (future) → comp(thr=−12dB, ratio=2:1)
- Fun
  - Squeaky → pitch(1.35)
  - Deep → pitch(0.80)
  - Radio (future) → bandpass(300–3500Hz) + noise(slight) + drive(light)

## Inspector Controls (Examples)

- Echo/Reverb
  - Mix (wet), Delay/Pre-Delay, Decay/Damping, Density (advanced)
- Pitch
  - Factor (0.5–2.0), Random min:max, Formant preserve (future)
- EQ
  - Simple: Warm/Bright slider
  - Advanced: 3–5 bands (freq, Q, gain)
- Dynamics (later)
  - Threshold, Ratio, Attack, Release, Makeup

## Expert Mode (DSL)

Round-trippable text and UI. Example:

```
reverb(type=hall, mix=0.25, decay=1.8) |
echo(delay=220ms, decay=0.25, mix=0.20) |
pitch(1.10, random=0.05)
```

- UI edits update DSL; DSL edits update UI.
- Backward-compatible with current free-form tags (“Cave Echo; Random pitch 0.8:1.2; Squeaky”).

## Data Model

- SpeakerProfile (pack config)
  - effects: JSON structured graph (preferred)
  - effectsTags: legacy free-form string (compat)
- Example JSON:

```
{
  "effects": [
    { "type": "pitch", "factor": 1.10, "random": { "min": 0.95, "max": 1.15 } },
    { "type": "echo",  "delayMs": 220, "decay": 0.25, "mix": 0.20 },
    { "type": "eq",    "bands": [
        { "kind": "lowShelf",  "freq": 200,  "gainDb": -2 },
        { "kind": "highShelf", "freq": 6000, "gainDb": 2 }
    ]}
  ]
}
```

## Engine Mapping

- EffectGraph builds an NAudio chain from the JSON:
  - Pitch → SmbPitchShiftingSampleProvider
  - Echo → Simple echo provider (MVP) → Reverb/Convolution (later)
  - EQ → BiQuad filters (NAudio.Dsp)
  - Dynamics → Simple compressor (later)
- Variability seeding option per line (e.g., seed by dialogue ID) to keep randomness reproducible.

## Audition & Caching

- Author Studio audition applies the chain to the engine-level voice preview (mp3) and plays immediately.
- Cache processed previews keyed by hash(voice, speed, effectsJSON, format) to avoid recompute.
- Export-time pre-bake (optional): apply effects and render final audio files into pack Audio/.

## Migration Plan

- Phase 1 (MVP)
  - Keep current Effects text; add structured JSON next to it and silently parse when present.
  - Add Effects Editor drawer (palette + chain + inspector) and audition against previews.
- Phase 2
  - Add EQ bands, compressor, better reverb; presets refactor to data-driven.
  - Add per-line variability toggle.
- Phase 3
  - Export/import preset libraries; pack-level presets.

## MVP Tag Reference (today)

Supported free-form tags (already wired in Runtime):

- `Random pitch a:b` → random pitch factor between a and b (e.g., `Random pitch 0.9:1.1`)
- `Pitch x` → fixed pitch factor (e.g., `Pitch 1.2`)
- `Squeaky` → fixed pitch 1.35
- `Cave Echo` → echo(delay≈280ms, decay≈0.45, mix≈0.35)
- `Throne Room` / `Hall` / `Reverb` → dense dual short echoes

These map to a simple NAudio chain in the runtime and will be superseded by the structured JSON graph as the UI lands.

