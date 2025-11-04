# Agents Overview

This repository uses a modular, agent‑based architecture to break down complex tasks into specialized workers. Each agent encapsulates a focused responsibility—capturing game frames, detecting UI elements, recognizing text, playing audio, emitting events—and communicates through well‑defined interfaces.

## What Are Agents?

Agents are classes or services that run concurrently (or asynchronously) and collaborate via message passing or shared channels. By keeping responsibilities narrow, agents remain testable and replaceable. Adding new features becomes a matter of adding or extending agents rather than rewriting existing code.

## Core Agents

### Capture Agent

* **Responsibility:** Orchestrates frame capture, textbox detection, OCR, and event emission in a unified pipeline.

* **V2 Implementation (2025-11-03):** All apps (Studio, AuthorStudio, Runtime) use `IDetectionLoop` from GameWatcher.Engine with delegate injection pattern for platform-agnostic operation.

* **Delegate Pattern:** Accepts function delegates for platform-specific operations:
  - `captureFrame: () => Bitmap?` - Platform-specific screen capture (Windows Graphics Capture, PrintWindow, etc.)
  - `compareImages: (Bitmap, Bitmap, int) => bool` - Frame similarity checking for optimization
  - `applyOcrFixes: (string) => string` - Pack-specific OCR error corrections
  - `normalizeText: (string) => string` - Text normalization for matching/lookup

* **Configuration:** DetectionLoopConfig controls timing (TargetFps), optimization (StableSampleRate, ChangeSampleRate), and hash checking (EnableHashCheck).

* **V1 Legacy (Archived):** SimpleLoop used direct frame capture with isBusy detection. V2 refines this with configurable thresholds and textbox caching.

* **Output:** DialogueDetected events with normalized text, OCR confidence, textbox bounds, debug images, and performance statistics.

* **Note:** GameCaptureService was deleted in Nov 2025 refactoring (622 lines) - redundant with IDetectionLoop. All apps unified on IDetectionLoop architecture.

### Textbox Detection Agent

* **Responsibility:** Locates the in‑game dialogue box in each frame.

* **Implementation:** Template matching against the blue FF1 textbox or other game‑specific templates. DynamicTextboxDetector implements cache-based optimization (caches textbox location for stable frames).

* **V2 Optimization:** Pre-crop strategy detects textbox in small region first, then full detection only if cache miss or frame changed significantly.

* **Output:** Bounding rectangle for the text area, or null if no textbox is found. Cache hit rate tracked in DetectionLoop statistics.

### OCR Agent

* **Responsibility:** Extracts text from the detected dialogue box.

* **V2 Implementation:** WindowsOcrEngine uses Windows.Media.Ocr API (faster and more accurate than Tesseract for FF1 dialogue).

* **V1 Legacy:** EnhancedOCR used Tesseract with 2x scaling and grayscale preprocessing. V2 simplified this - Windows OCR works best without preprocessing.

* **OCR Fixes:** OcrFixesStore applies pack-specific corrections (e.g., "Ijhen" → "When"). Two-pass algorithm handles multi-word patterns first, then single-word tokens. Case-insensitive matching with case-preserved output.

* **Output:** Raw text string, potentially with imperfect characters (corrected by OCR fixes).

### Normalization Agent

* **Responsibility:** Cleans up OCR output to improve matching.

* **Implementation:** Lowercases text, replaces smart quotes with ASCII equivalents, collapses ellipses, and trims whitespace.

* **Output:** Normalized text used as the lookup key for audio mapping and event emission.

### Mapping Agent

* **Responsibility:** Maps normalized text to pre‑rendered audio files.

* **Implementation:** Loads mapping JSON from /maps/, then looks up the normalized text. Handles missing keys gracefully.

* **Output:** Path to the audio file to play, or null if no match is found.

### Playback Agent

* **Responsibility:** Queues and plays audio files with minimal latency.

* **Implementation:** Uses NAudio (or an equivalent library) for gapless playback. Preloads the next line into memory for smooth transitions. Supports hotkeys to skip or replay lines.

* **Output:** Audible voiceover corresponding to on‑screen dialogue.

### Event Emitter Agent

* **Responsibility:** Emits semantic events from OCR and detection results.

* **Implementation:** Subscribes to the output of other agents and constructs GameEvent objects such as DialogueEvent, ChoiceEvent and BattleStartEvent. Publishes these events on an asynchronous bus.

* **Output:** Events consumed by Twitch integration, overlays, draft and chaos modules.

### Twitch Module Agent (Phase 2+)

* **Responsibility:** Interfaces with Twitch chat, polls and channel points.

* **Implementation:** Uses TwitchLib; listens for ChoiceEvent and BattleStartEvent to start polls and betting rounds; relays dialogue into chat.

* **Output:** Viewer interactions feed back into the event system and overlays.

### Overlay Agent (Phase 2+)

* **Responsibility:** Writes overlay state to JSON files that OBS or browser sources can render.

* **Implementation:** Generates JSON snapshots like now\_playing.json, poll.json, draft.json, etc. Updates them on relevant events.

* **Output:** Machine‑readable files consumed by overlay templates.

### Draft Manager Agent (Phase 3+)

* **Responsibility:** Handles viewer drafts of characters, NPCs or monsters and updates scores based on events.

* **Implementation:** Maintains a roster of drafted entities, applies scoring rules defined in /draft.rules.json, and emits score update events.

* **Output:** Draft state and scores written to overlays and available for analytics.

### Chaos Manager Agent (Phase 3+)

* **Responsibility:** Introduces controlled randomness through viewer channel‑point redemptions or random triggers.

* **Implementation:** Reads chaos definitions from /chaos.events.json, chooses events according to weighting, and applies effects such as audio/visual filters.

* **Output:** Chaos events logged and applied to overlays or audio.

### SDK & Plugin Agents (Phase 4+)

* **Responsibility:** Expose public APIs and engine plugins for external games.

* **Implementation:** Provide NuGet packages, C bindings, and plugins for RPG Maker, Unity and Godot to allow third‑party developers to emit voiceover events and consume the FFVoiceover stack.

* **Output:** APIs and CLI tools enabling integration into custom games.

### Platform & Marketplace Agents (Phase 5+)

* **Responsibility:** Operate SaaS backend, Twitch extension, marketplace and community hub.

* **Implementation:** Runs cloud services to manage user authentication, license keys, pack hosting and community contributions. Implements the Twitch extension for real‑time overlays and pack management.

* **Output:** Scalable platform services and dashboards for users, streamers and developers.

## Agent Communication

Agents communicate via:

* **Asynchronous queues and channels** (e.g., audio queues).

* **Event buses** (e.g., GameEvent emitter consumed by Twitch and overlay agents).

* **Shared state files** (e.g., JSON snapshots for overlays).

* **Plugin APIs** (e.g., SDK calls in Phase 4).

Breaking these components into agents helps maintain separation of concerns, improves testability, and allows incremental adoption of features. When adding new functionality, consider whether it belongs in an existing agent or warrants a new one.

## Extending Agents

To add a new agent:

1. **Define its responsibility** clearly and ensure it does not overlap existing agents.

2. **Implement a class or service** that subscribes to inputs it needs (events, raw data, settings).

3. **Publish outputs** through existing channels or introduce a new bus if appropriate.

4. **Document the agent’s behavior and configuration** in /docs/ as well as updating this file.

---

## Repo-Specific Agent Guidance (V2 Naming, 2025-10)

These instructions apply to the entire repository. Prefer these when working in this repo; user/developer prompts still take precedence.

- Project naming (final):
  - `GameWatcher.Studio` = Player GUI (end users) - **REQUIRES full capture pipeline**
  - `GameWatcher.AuthorStudio` = Creator tools (pack authors) - **REQUIRES full capture pipeline**
  - `GameWatcher.Runtime` = Headless orchestrator - **REQUIRES full capture pipeline**
  - `GameWatcher.Engine` = Core services (shared by all apps)

- **Architecture compliance:**
  - See `GameWatcher-Platform/docs/design/APPLICATION_ARCHITECTURE.md` for MANDATORY service requirements per app
  - Don't assume an app doesn't need a service based on minimal current code - check design intent
  - All three apps (Studio, AuthorStudio, Runtime) REQUIRE ITextboxDetector, IOcrEngine, and capture services

- Docs consistency:
  - Keep “Studio” = player; “Author Studio” = creator. Avoid legacy “rename current Studio” guidance.
  - Prefer ASCII arrows in docs when Unicode causes patching issues.

- Code editing:
  - Use `apply_patch` with small, focused diffs and preserve surrounding style.
  - Don’t commit or change branches unless the user asks. Instead, propose the git plan and commands.

- Build/run hints:
  - Studio (player): `dotnet build GameWatcher-Platform/GameWatcher.Studio`
  - Author Studio (creator): `dotnet build GameWatcher-Platform/GameWatcher.AuthorStudio`
  - Runtime host: `dotnet run --project GameWatcher-Platform/GameWatcher.Runtime`

- Git management (when requested):
  - Branch naming: `feat/<area>-<short-topic>` or `docs/<topic>` or `fix/<area>-<issue>`
  - Commit messages: Conventional style: `docs: align studio naming in design docs`
  - Recommended flow:
    1. `git checkout -b docs/studio-author-naming`
    2. Apply patches
    3. `git add -A && git commit -m "docs: align Studio vs Author Studio naming"`
    4. `git push -u origin docs/studio-author-naming`
  - Always summarize changes and link to touched paths in the MR/PR.

## V1 (SimpleLoop) as Reference — Porting Guidance

V2 is being built fresh. If needed, treat `Archive/SimpleLoop/` as the stable, optimized reference for specific algorithms and behaviors — but cherry‑pick sparingly.

- What to consult in V1
  - Core loop patterns and performance hints in `SimpleLoop/CaptureService.cs`
  - Textbox detection strategy in `SimpleLoop/DynamicTextboxDetector.cs`
  - OCR preprocessing ideas in `SimpleLoop/EnhancedOCR.cs`
  - Dialogue catalog structure in `SimpleLoop/DialogueCatalog.cs` and `DialogueEntry`

- What to avoid
  - Legacy GUI (`SimpleLoop.Gui`) and debug scaffolding/log path hacks
  - Ad‑hoc config or hardcoded paths; use V2 DI + options instead

- Porting rules
  - Prefer re‑implementing logic behind V2 interfaces (Engine/Runtime) over copying whole classes.
  - Preserve proven micro‑optimizations; update naming and null/async patterns to V2 style.
  - Write small shims only if unavoidable; plan to remove them once native V2 services cover the need.

- Sanity checks when porting
  - Compile touched projects and run a quick smoke path that exercises the change.
  - Confirm logs/metrics are consistent with V2 expectations (not V1’s console prints).
