# GameWatcher

**GameWatcher** is a Windows tool that watches any selected game window — starting with **Final Fantasy I** — detects dialogue boxes, OCRs their text, normalizes it, and plays pre-rendered or AI-generated voice lines. It’s non-invasive, engine-agnostic, and can be adapted to any retro RPG with a stable text box UI.

## Quick Start
1. Build `GameWatcher.App`.
2. Run → **Pick Window** → click the game window.
3. Place your audio lines in `/voices` and your mapping in `/maps/dialogue.en.json`.
4. Play. When a recognized line appears, the matching audio is queued and played.

## Core Technology
- **Capture:** Windows Graphics Capture (Win10+), fallback: PrintWindow.  
- **Detection:** OpenCV template match for dialogue UI; region cached for performance.  
- **OCR:** Tesseract with font-specific whitelist and upscaling for pixel text.  
- **Audio:** NAudio for gapless async playback with debounce and caching.  
- **Voice Generation:** Pre-rendered via OpenAI `gpt-4o-mini-tts` or ElevenLabs (optional).  

## Roadmap
GameWatcher is designed to grow:
- Phase 1 — Core voiceover playback  
- Phase 2 — Event detection & overlays  
- Phase 3 — Draft/Chaos streamer modes  
- Phase 4 — SDK integration for indie RPGs  
- Phase 5 — Full platform ecosystem  

See `/docs` for technical design and `/tools/tts_batch` for pre-generation scripts.
 
## Smoke Test (OCR)
- Requires Tesseract CLI. Install with winget `winget install -e --id UB-Mannheim.TesseractOCR` or chocolatey `choco install tesseract`, or set `TESSERACT_EXE` to the full path of `tesseract.exe`.
- One-time import of templates: `./scripts/sync_artifacts.ps1` (copies from `Artifacts/templates` to `assets/templates`).
- Run: `dotnet run --project src/GameWatcher.App`
- Input image: `assets/templates/FF-TextBox-Position.png`
- Output crop: `out/crop.png`
- Console prints raw and normalized OCR text.

## Detection Notes
- Corner templates are 19x19; default border inset is `19` px (override with `GW_TEXTBOX_INSET`).
- Detector uses dynamic corners when available; falls back to a normalized static rect.
