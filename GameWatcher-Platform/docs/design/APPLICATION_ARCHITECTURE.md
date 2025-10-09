# Application Architecture - V2 Platform

## Overview

The GameWatcher V2 Platform consists of three distinct applications, each with specific responsibilities and agent requirements. This document defines what each application MUST support to fulfill its design purpose.

---

## 1. GameWatcher.Studio (Player Application)

**Purpose:** End-user application for playing voiceovers in real-time while gaming.

**Target User:** Gamers who want voiced dialogue in their games.

**Core Responsibilities:**
- ✅ MUST detect game window and capture frames in real-time
- ✅ MUST detect textbox regions using ITextboxDetector
- ✅ MUST perform OCR on detected dialogue
- ✅ MUST map dialogue to audio files
- ✅ MUST play audio synchronized with game dialogue
- ✅ MUST load and manage game packs
- ✅ SHOULD provide UI for settings, pack management, and monitoring

**Required Agents/Services:**
```csharp
// Capture & Detection (MANDATORY)
services.AddSingleton<ITextboxDetector>(sp => /* pack-specific detector */);
services.AddSingleton<IOcrEngine, WindowsOcrEngine>();
services.AddSingleton<GameCaptureService>();

// Pack Management (MANDATORY)
services.AddSingleton<IPackManager, PackManager>();
services.AddSingleton<IGameDetectionService, GameDetectionService>();

// Audio Playback (MANDATORY)
services.AddSingleton<IAudioPlaybackService, AudioPlaybackService>();

// Dialogue Mapping (MANDATORY)
services.AddSingleton<IDialogueCatalogService, DialogueCatalogService>();

// UI (MANDATORY)
services.AddSingleton<MainWindow>();
// ViewModels as needed
```

**Logging:**
- File: `logs/gamewatcher-studio_*.log`
- Console output for debug builds
- Serilog with structured logging

**Key Design Principle:** Studio is a FULL-FEATURED player. It is NOT "UI-only" or a thin client. It contains the complete capture and playback pipeline.

---

## 2. GameWatcher.AuthorStudio (Pack Creation Tool)

**Purpose:** Creator tool for building voice packs (discovering dialogue, assigning voices, generating audio).

**Target User:** Content creators building voice packs for games.

**Core Responsibilities:**
- ✅ MUST detect game window and capture frames for discovery
- ✅ MUST detect textbox regions using ITextboxDetector
- ✅ MUST perform OCR to extract dialogue text
- ✅ MUST deduplicate and catalog unique dialogue lines
- ✅ MUST allow speaker assignment and audio generation
- ✅ MUST export complete pack structure
- ✅ SHOULD provide preview/playback for testing

**Required Agents/Services:**
```csharp
// Discovery & Capture (MANDATORY)
services.AddSingleton<ITextboxDetector>(sp => /* pack-specific detector */);
services.AddSingleton<IOcrEngine, WindowsOcrEngine>();
services.AddSingleton<DiscoveryService>();

// Author Tools (MANDATORY)
services.AddSingleton<SpeakerStore>();
services.AddSingleton<SessionStore>();
services.AddSingleton<PackExporter>();
services.AddSingleton<PackLoader>();
services.AddSingleton<OcrFixesStore>();

// TTS (MANDATORY for audio generation)
services.AddSingleton<OpenAiTtsService>();

// Audio Preview (RECOMMENDED)
services.AddSingleton<AudioPlaybackService>();

// Settings (MANDATORY)
services.AddSingleton<AuthorSettingsService>();

// UI (MANDATORY)
services.AddSingleton<MainWindow>();
// ViewModels as needed
```

**Logging:**
- File: `logs/author-studio_*.log`
- Console output for debug builds
- Serilog with structured logging

**Key Design Principle:** AuthorStudio shares capture/detection agents with Studio but adds authoring-specific tools (speaker management, TTS generation, export).

---

## 3. GameWatcher.Runtime (Headless Orchestrator)

**Purpose:** Headless service for automation, testing, and non-GUI scenarios.

**Target User:** Developers, CI/CD pipelines, automation scripts.

**Core Responsibilities:**
- ✅ MUST detect game window and capture frames
- ✅ MUST detect textbox regions using ITextboxDetector
- ✅ MUST perform OCR on detected dialogue
- ✅ MAY play audio (optional based on configuration)
- ✅ MUST support pack discovery and loading
- ✅ MUST expose processing pipeline for extensibility
- ✅ SHOULD log all events for debugging/analysis

**Required Agents/Services:**
```csharp
// Capture & Detection (MANDATORY)
services.AddSingleton<ITextboxDetector>(sp => /* pack-specific detector */);
services.AddSingleton<IOcrEngine, WindowsOcrEngine>();
services.AddSingleton<GameCaptureService>();

// Pack Management (MANDATORY)
services.AddSingleton<IPackManager, PackManager>();
services.AddSingleton<IGameDetectionService, GameDetectionService>();

// Processing Pipeline (MANDATORY)
services.AddSingleton<IProcessingPipeline, ProcessingPipeline>();
services.AddSingleton<DialogueCatalogService>();
services.AddSingleton<OcrPostprocessor>();

// Audio (OPTIONAL - config-driven)
services.AddSingleton<IAudioPlaybackService, AudioPlaybackService>();

// Runtime Service (MANDATORY)
services.AddHostedService<GameWatcherRuntimeService>();
```

**Logging:**
- Console output (structured JSON for automation)
- Optional file logging
- Serilog with structured logging

**Key Design Principle:** Runtime is Studio without the GUI. It runs the same capture/detection/playback pipeline but can be automated.

---

## Shared Agent Requirements

### All Three Apps MUST Have:

1. **ITextboxDetector** - Configured per game pack
   ```csharp
   services.AddSingleton<ITextboxDetector>(sp =>
   {
       var logger = sp.GetService<ILogger<DynamicTextboxDetector>>();
       return new DynamicTextboxDetector(PackDetectionConfig.GetConfig(), logger);
   });
   ```

2. **IOcrEngine** - Text extraction from textbox regions
   ```csharp
   services.AddSingleton<IOcrEngine, WindowsOcrEngine>();
   ```

3. **ILogger** - Structured logging via Serilog
   ```csharp
   services.AddLogging();
   ```

4. **Pack References** - Each app must reference at least one pack (e.g., FF1.PixelRemaster) for detection config

### Only Studio & AuthorStudio Need:
- WPF UI infrastructure (MainWindow, ViewModels, Converters)
- User settings persistence

### Only AuthorStudio Needs:
- TTS services (OpenAiTtsService)
- Pack export/import
- Speaker management
- Session persistence

---

## Compliance Checklist

When modifying any application's DI configuration, verify:

### Studio Checklist:
- [ ] ITextboxDetector registered with pack-specific config
- [ ] IOcrEngine registered
- [ ] GameCaptureService registered
- [ ] IAudioPlaybackService registered
- [ ] IPackManager registered
- [ ] Logging configured (file + console)
- [ ] All capture events wired to UI

### AuthorStudio Checklist:
- [ ] ITextboxDetector registered with pack-specific config
- [ ] IOcrEngine registered
- [ ] DiscoveryService registered
- [ ] TTS services registered
- [ ] Speaker/Session stores registered
- [ ] Logging configured (file + console)
- [ ] All discovery events wired to UI

### Runtime Checklist:
- [ ] ITextboxDetector registered with pack-specific config
- [ ] IOcrEngine registered
- [ ] GameCaptureService registered
- [ ] IProcessingPipeline registered
- [ ] IPackManager registered
- [ ] Logging configured (console + optional file)
- [ ] Background service hosted

---

## Anti-Patterns to Avoid

### ❌ DON'T:
- Assume Studio is "UI-only" - it needs full capture pipeline
- Assume Runtime is "just for testing" - it's a first-class deployment option
- Create detectors manually with `new` - always use DI
- Use `Console.WriteLine` in shared services - use ILogger
- Hardcode game-specific values in Engine - use pack configs

### ✅ DO:
- Register all required services in each app's DI container
- Use ILogger in all services for proper log routing
- Configure detectors with pack-specific configs via DI
- Keep Engine universal, packs game-specific
- Document when adding new required services

---

## Testing Each Application

### Studio (Player):
```bash
# Build and run
dotnet run --project GameWatcher.Studio
# Expected: UI opens, can detect FF1, plays voiceovers
```

### AuthorStudio (Creator):
```bash
# Build and run
dotnet run --project GameWatcher.AuthorStudio
# Expected: UI opens, can discover dialogue, generate audio
```

### Runtime (Headless):
```bash
# Build and run
dotnet run --project GameWatcher.Runtime
# Expected: Console output, detects game, processes dialogue
```

---

## Version History

- **2025-10-08**: Initial document created to clarify app responsibilities after Studio capture services oversight
- Purpose: Prevent confusion between "current code state" and "design intent"
