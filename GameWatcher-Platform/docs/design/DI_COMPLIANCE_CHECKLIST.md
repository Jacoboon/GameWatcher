# DI Service Registration Compliance Checklist

Quick reference for validating each application's dependency injection configuration.

---

## GameWatcher.Studio (Player)

**File:** `GameWatcher.Studio/App.xaml.cs`

**Required Services:**
- [x] `services.AddSingleton<ITextboxDetector>(sp => new DynamicTextboxDetector(PackConfig.GetConfig(), logger))`
- [x] `services.AddSingleton<IOcrEngine, WindowsOcrEngine>()`
- [x] `services.AddSingleton<GameCaptureService>()`
- [ ] `services.AddSingleton<IAudioPlaybackService, AudioPlaybackService>()`
- [ ] `services.AddSingleton<IPackManager, PackManager>()`
- [ ] `services.AddSingleton<IDialogueCatalogService, DialogueCatalogService>()`
- [x] `services.AddLogging()`
- [x] Serilog configured with file + console output

**Current Status (2025-10-08):** ✅ Core capture services registered, audio/pack services pending

---

## GameWatcher.AuthorStudio (Creator Tools)

**File:** `GameWatcher.AuthorStudio/App.xaml.cs`

**Required Services:**
- [x] `services.AddSingleton<ITextboxDetector>(sp => new DynamicTextboxDetector(PackConfig.GetConfig(), logger))`
- [x] `services.AddSingleton<IOcrEngine, WindowsOcrEngine>()` (implicit via DiscoveryService)
- [x] `services.AddSingleton<DiscoveryService>()`
- [x] `services.AddSingleton<SpeakerStore>()`
- [x] `services.AddSingleton<SessionStore>()`
- [x] `services.AddSingleton<PackExporter>()`
- [x] `services.AddSingleton<PackLoader>()`
- [x] `services.AddSingleton<OpenAiTtsService>()`
- [x] `services.AddSingleton<AudioPlaybackService>()`
- [x] `services.AddSingleton<AuthorSettingsService>()`
- [x] `services.AddSingleton<OcrFixesStore>()`
- [x] `services.AddLogging()`
- [x] Serilog configured with file + console output

**Current Status (2025-10-08):** ✅ Fully compliant

---

## GameWatcher.Runtime (Headless Orchestrator)

**File:** `GameWatcher.Runtime/Program.cs`

**Required Services:**
- [x] `services.AddSingleton<ITextboxDetector>(sp => new DynamicTextboxDetector(PackConfig.GetConfig(), logger))`
- [x] `services.AddSingleton<IOcrEngine, WindowsOcrEngine>()`
- [x] `services.AddSingleton<GameCaptureService>()`
- [x] `services.AddSingleton<IPackManager, PackManager>()`
- [x] `services.AddSingleton<IGameDetectionService, GameDetectionService>()`
- [x] `services.AddSingleton<IAudioPlaybackService, AudioPlaybackService>()`
- [x] `services.AddSingleton<DialogueCatalogService>()`
- [x] `services.AddSingleton<OcrPostprocessor>()`
- [x] `services.AddSingleton<IProcessingPipeline, ProcessingPipeline>()`
- [x] `services.AddHostedService<GameWatcherRuntimeService>()`
- [x] `services.AddLogging()`
- [x] Serilog configured (console + optional file)

**Current Status (2025-10-08):** ✅ Fully compliant

---

## Common Mistakes to Avoid

### ❌ Assuming Studio is "UI-only"
**Wrong:** "Studio doesn't need capture because it's just a UI"  
**Right:** Studio IS the player - it MUST have full capture + playback pipeline

### ❌ Creating services with `new` keyword
**Wrong:** `_detector = new DynamicTextboxDetector(config);`  
**Right:** `_detector = serviceProvider.GetRequiredService<ITextboxDetector>();`

### ❌ Missing logger injection
**Wrong:** `new DynamicTextboxDetector(config)` (no logger)  
**Right:** `new DynamicTextboxDetector(config, logger)`

### ❌ Hardcoding game-specific config
**Wrong:** `new DynamicTextboxDetector(hardcodedBlueColor, hardcodedRect)`  
**Right:** `new DynamicTextboxDetector(FF1DetectionConfig.GetConfig(), logger)`

---

## Validation Steps

When modifying any App.xaml.cs or Program.cs:

1. **Check this checklist** - ensure all required services are registered
2. **Reference APPLICATION_ARCHITECTURE.md** - verify design intent
3. **Build the project** - `dotnet build <ProjectName>.csproj`
4. **Run smoke test** - verify app starts without DI exceptions
5. **Check logs** - ensure logging is working (file created in logs/)

---

## Quick Test Commands

```bash
# Studio
dotnet build GameWatcher.Studio/GameWatcher.Studio.csproj
dotnet run --project GameWatcher.Studio

# AuthorStudio
dotnet build GameWatcher.AuthorStudio/GameWatcher.AuthorStudio.csproj
dotnet run --project GameWatcher.AuthorStudio

# Runtime
dotnet build GameWatcher.Runtime/GameWatcher.Runtime.csproj
dotnet run --project GameWatcher.Runtime
```

**Expected:** No DI exceptions, logging starts, services initialize

---

## Version History

- **2025-10-08:** Created after Studio capture services oversight incident
- Purpose: Provide quick reference to prevent DI configuration mistakes
