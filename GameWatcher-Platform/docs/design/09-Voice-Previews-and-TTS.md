# Voice Previews and TTS (V2)

This document captures how we handle voice previews, supported voices, TTS key configuration, and the one-time batch generation flow.

## Engine-Level Previews

- Location (ship with Engine): `GameWatcher.Engine/Voices`
- Naming: `<voice>-<speed>.mp3` where `speed` has one decimal place (e.g., `alloy-1.2.mp3`)
- Author Studio Preview Button
  - Uses engine-level previews if present; otherwise generates once and caches to the same folder
  - Sample phrase: `Hi! I'm <Name>. Calm. Excited! Curious? Let's begin.`
- Installation/runtime fallback
  - If the Engine output directory is not writable, fallback: `%APPDATA%/GameWatcher/Engine/Voices`

## Supported Voices

Available via OpenAI TTS endpoint (invalid ones removed):

- alloy, coral, echo, fable, nova, onyx, sage, shimmer, verse

> If adding/removing names, update `OpenAiVoicesProvider.All` and optional batch script defaults.

## Batch Generation (one-time, developers)

- Script: `scripts/generate_engine_previews.ps1`
- Defaults:
  - Voices: supported list above
  - Speeds: 0.5 → 1.5 inclusive, step 0.1 (11 per voice)
  - Format: `mp3`
- Usage:

```
# Use defaults (mp3, supported voices, 0.5–1.5 x0.1)
pwsh scripts/generate_engine_previews.ps1

# Custom subset and format
pwsh scripts/generate_engine_previews.ps1 -Voices alloy,shimmer -Start 0.8 -End 1.2 -Step 0.1 -Format mp3
```

- API key source (required):
  - User env var `GWS_OPENAI_API_KEY`, or
  - Fallback: `Secrets/openai-api-key.txt` (first line)

## Author Studio Settings

- TTS API Key
  - Settings tab → password box → Save stores user env `GWS_OPENAI_API_KEY`
  - Remove clears the key from user env
  - Top bar shows status: `TTS ready` or `TTS unavailable: Configure key`
- Audio Format
  - Default `mp3`; can switch to `wav`
  - Applies to previews (when generated) and generated dialogue lines

## Pack Export Behavior

- Approved dialogue audio files (attached or generated) are copied into `Audio/` under the pack output
- `Catalog/dialogue.json` stores relative `audio` paths (e.g., `Audio/dialogue_ABC12345.mp3`)
- Engine-level previews are not included in packs (they ship with the Engine)

## Notes & Future Work

- We can add additional preview speeds or alternate sample phrases by adjusting the script/Author Studio code.
- Consider per-voice “primary speed” tagging or a small curated subset for shipping if total size is a concern.
- Upcoming: Effects UI/graph will allow auditioning processed previews and caching processed variants by effect hash.

