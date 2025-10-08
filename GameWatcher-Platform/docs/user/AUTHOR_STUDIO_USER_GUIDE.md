# GameWatcher Author Studio User Guide

Welcome to GameWatcher Author Studio — creation tools for building dialogue packs. Use this app when you want to discover, edit, voice, and package dialogue for use in GameWatcher Studio (the player app).

## Getting Started

- System requirements: Windows 10/11 (64-bit), .NET 8.0
- First launch: run `GameWatcher.AuthorStudio.exe`
- Output: packaged packs that GameWatcher Studio can load and play

## Workflow Overview

1) Discovery
- Start a discovery session to capture dialogue while playing a game.
- Live feed shows detected text; unique lines are added to the session list.

2) Dialogue Review
- Review discovered lines, fix OCR issues, and merge duplicates.
- Assign or confirm speakers; accept entries for voice generation.

3) Voice Studio
- Select voice profiles per speaker, preview individual lines, and adjust speed.
- Generate bulk audio for approved lines; re‑generate low‑quality lines as needed.

4) Pack Builder
- Fill in pack metadata (name, version, description).
- Validate detection/voice mappings and build the pack.
- Export the pack to your packs directory.

## Using Your Pack in GameWatcher Studio

- Place the built pack in a directory scanned by the player:
  - Default: `./packs/` next to the player executable, or
  - Any folder you added under Settings → Pack Directories in GameWatcher Studio
- Open GameWatcher Studio, go to Pack Manager, Load your pack, and play.

## Tips

- Keep discovery sessions focused (per area or chapter) for clean catalogs.
- Normalize punctuation and spacing for better matching.
- Favor shorter voice previews while tuning settings; switch to bulk when satisfied.

## TTS API Key (OpenAI)

- Configure TTS from inside the app: click `TTS Settings...` in the top bar.
- Paste your key into the password box and Save. The app stores it in your user environment as `GWS_OPENAI_API_KEY`.
- You can Remove the key at any time from the same dialog.
- If no key is set, you will see `TTS unavailable: Configure key` and TTS actions are disabled.
- Advanced: a legacy fallback reads `Secrets/openai-api-key.txt` (first line). You can also set `GAMEWATCHER_SECRETS_DIR` to point at a folder containing that file.

## References

- Design: `docs/design/04-Studio-Tools-Design.md`
- Pack System: `docs/design/02-Game-Pack-System.md`
- Player Guide: `docs/user/USER_GUIDE.md`

## First-Time Author (Quick Start)

Follow this once to create your first pack and verify playback:

1) Start Discovery (5–10 min)
- Launch your game and open Author Studio.
- Start a Discovery Session and play through a small scene.
- Stop the session and review the discovered lines list.

2) Clean & Assign (5 min)
- Fix obvious OCR issues (punctuation, spacing, names).
- Assign speakers for the top few lines; mark them Approved.

3) Generate & Build (3–5 min)
- Preview a couple of lines to confirm voice selection.
- Run Bulk Generate on Approved lines.
- Open Pack Builder, fill metadata, and Build.

4) Play It
- Copy the built pack into the player’s `packs/` folder.
- Open GameWatcher Studio (Player), Load the pack, and play the same scene.

## Settings

- Audio Format: choose `wav` or `mp3` for both speaker previews and generated dialogue.
- TTS Settings: open from the top bar to add/remove your API key (stored as `GWS_OPENAI_API_KEY`).
- Batch Previews: from Settings, click “Generate All OpenAI Voice Previews” to pre-seed engine-level previews.

