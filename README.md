# GameWatcher

GameWatcher watches a chosen game window (starting with FF1), detects the dialogue textbox, OCRs its text, normalizes it, and plays mapped or generated audio. It’s non‑invasive and engine‑agnostic; works great with borderless windowed games.

## Quick Start
- Use borderless windowed mode (exclusive fullscreen often blocks capture).
- Install Tesseract CLI or set `TESSERACT_EXE` to `tesseract.exe`.
- One‑time: `./scripts/sync_artifacts.ps1` to copy textbox templates.
- Run live watch: `./scripts/watch.ps1 -Title "FINAL FANTASY" -Fps 20`.
- Put voices in `assets/voices/` and mapping in `assets/maps/dialogue.en.json`.
- Unknown lines are recorded under `data/` and listed in `data/misses.json`.

## Core Technology
- Capture: Windows Graphics Capture (Win10+), fallbacks available.
- Detection: Corner template match; caches region for performance.
- OCR: Tesseract CLI with grayscale+upscale+thresholding.
- Audio: NAudio playback; pre‑rendered WAVs.
- Authoring: Catalog + misses report, mapping JSON, optional TTS generation.

## Live Authoring Flow
- Keep `watch.ps1` running while you play.
- Generate voices for new lines:
  - Dry run: `./scripts/author_gen.ps1 -DryRun`
  - Live: `./scripts/author_gen.ps1 -Max 10` (requires `OPENAI_API_KEY`).
- The app hot‑reloads `assets/maps/dialogue.en.json` and `assets/maps/speakers.json`; new audio plays without restarting.
- Convenience loop (watch + periodic authoring):
  - `./scripts/live_author.ps1 -Title "FINAL FANTASY" -MaxPerPass 10 -IntervalSec 20`

## Smoke Test (OCR)
- Install Tesseract as above.
- Import templates: `./scripts/sync_artifacts.ps1`.
- Static test (uses a sample image): `dotnet run --project src/GameWatcher.App`
- Input image: `assets/templates/FF-TextBox-Position.png`
- Output crop: `out/crop.png`

## Detection Notes
- Corner templates are 19×19; default border inset is `19` px (`GW_TEXTBOX_INSET` to override).
- Detector searches full frame by default; set `GW_DETECT_ROI` to narrow the search region if needed.

