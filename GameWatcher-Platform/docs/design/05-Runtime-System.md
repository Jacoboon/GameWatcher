# GameWatcher Runtime - Universal Game Support

## Overview

The GameWatcher Runtime is the **universal player** that orchestrates game packs, engine services, and streaming integrations. It provides a consistent experience across all supported games while maintaining the performance optimizations discovered in V1.

## Runtime Architecture

### Core Runtime Components

```
GameWatcher.Runtime/
├── 🎮 Pack Manager          # Load/unload game packs
├── 🎯 Game Controller       # Orchestrate detection & TTS 
├── 🔄 State Manager         # Track game and dialogue state
├── 🎵 Audio Pipeline        # TTS generation & playback
├── 📡 Streaming Bridge      # Twitch/OBS integration
├── ⚙️  Configuration Hub    # Settings management
└── 📊 Telemetry Service     # Performance monitoring
```

## Runtime Lifecycle

### 1. **Startup & Initialization**

```csharp
public class GameWatcherRuntime : IDisposable
{
    public async Task<RuntimeResult> StartAsync(RuntimeConfig config)
    {
        // Initialize core engine
        await _engine.InitializeAsync(config.Engine);
        
        // Discover and load available packs
        var packs = await _packManager.DiscoverPacksAsync(config.PackDirectories);
        
        // Auto-detect running game and load appropriate pack
        var activeGame = await DetectActiveGameAsync();
        if (activeGame != null)
        {
            await LoadPackForGameAsync(activeGame);
        }
        
        // Start capture and processing pipeline
        await _captureService.StartAsync(config.Capture);
        
        return RuntimeResult.Success();
    }
}
```

### 2. **Game Detection & Pack Loading**

```csharp
public class GameDetectionService
{
    public async Task<DetectedGame?> DetectActiveGameAsync()
    {
        var runningProcesses = Process.GetProcesses();
        
        foreach (var pack in _availablePacks)
        {
            if (await pack.IsTargetGameRunningAsync())
            {
                return new DetectedGame
                {
                    ProcessName = pack.Manifest.GameExecutable,
                    WindowTitle = pack.Manifest.WindowTitle,
                    Pack = pack,
                    Confidence = await CalculateConfidence(pack)
                };
            }
        }
        
        return null;
    }
    
    public async Task<bool> LoadPackAsync(IGamePack pack)
    {
        // Unload current pack if active
        if (_activePack != null)
        {
            await UnloadCurrentPackAsync();
        }
        
        // Load new pack configuration
        _detectionStrategy = pack.CreateDetectionStrategy(_engine);
        _speakers = pack.GetSpeakers();
        _ocrConfig = pack.GetOcrConfiguration();
        
        // Apply pack-specific optimizations (V1 learnings)
        await ApplyPerformanceOptimizations(pack);
        
        _activePack = pack;
        
        // Notify UI and streaming integrations
        await _eventBus.PublishAsync(new PackLoadedEvent(pack));
        
        return true;
    }
}
```

### 3. **Real-Time Processing Pipeline**

```csharp
public class ProcessingPipeline
{
    public async Task StartProcessingAsync()
    {
        await foreach (var frame in _captureService.GetFramesAsync())
        {
            // Skip duplicate frames (V1 optimization)
            if (await _optimizer.IsDuplicateFrameAsync(frame))
            {
                continue;
            }
            
            // Detect dialogue textbox
            var detection = await _detectionStrategy.DetectTextboxAsync(
                new DetectionRequest { Screenshot = frame });
            
            if (detection.TextboxArea.HasValue)
            {
                // Extract and process text
                var dialogue = await ProcessDialogueAsync(detection);
                
                if (dialogue != null)
                {
                    // Generate and queue audio
                    await _audioService.ProcessDialogueAsync(dialogue);
                    
                    // Update streaming overlays
                    await _streamingBridge.UpdateOverlayAsync(dialogue);
                }
            }
        }
    }
    
    private async Task<ProcessedDialogue?> ProcessDialogueAsync(DetectionResult detection)
    {
        // OCR text extraction
        var rawText = await _engine.ExtractTextAsync(
            detection.TextboxArea.Value, 
            detection.Screenshot, 
            _ocrConfig);
        
        // Text normalization and validation
        var normalizedText = await _textProcessor.NormalizeAsync(rawText);
        
        if (string.IsNullOrEmpty(normalizedText) || 
            await _duplicateDetector.IsDuplicateAsync(normalizedText))
        {
            return null;
        }
        
        // Speaker identification
        var speaker = _speakers.MatchSpeaker(normalizedText);
        
        return new ProcessedDialogue
        {
            Text = normalizedText,
            Speaker = speaker,
            Timestamp = DateTime.UtcNow,
            Confidence = detection.Confidence,
            ProcessingTime = detection.ProcessingTime
        };
    }
}
```

## Hot Swapping & Multi-Pack Support

### 🔄 **Runtime Pack Switching**

```csharp
public class HotSwapManager
{
    public async Task<bool> SwitchToPackAsync(string packId)
    {
        var targetPack = await _packManager.GetPackAsync(packId);
        if (targetPack == null) return false;
        
        // Gracefully pause current processing
        await _pipeline.PauseAsync();
        
        // Clear any pending audio
        await _audioService.ClearQueueAsync();
        
        // Load new pack (this validates compatibility)
        var loadResult = await _gameDetection.LoadPackAsync(targetPack);
        
        if (loadResult)
        {
            // Resume processing with new configuration
            await _pipeline.ResumeAsync();
            
            // Update UI to reflect new pack
            await _eventBus.PublishAsync(new PackSwitchedEvent(targetPack));
            
            return true;
        }
        
        // Rollback on failure
        await _pipeline.ResumeAsync();
        return false;
    }
}
```

### 🎮 **Multi-Game Session Support**

```csharp
public class MultiGameSession
{
    private readonly Dictionary<string, IGamePack> _activePacks = new();
    
    public async Task<bool> AddGameAsync(string gameExecutable)
    {
        var pack = await _packManager.FindPackForGameAsync(gameExecutable);
        if (pack == null) return false;
        
        // Allow multiple packs for different game instances
        _activePacks[gameExecutable] = pack;
        
        // Configure separate processing pipeline
        await CreatePipelineForPackAsync(pack);
        
        return true;
    }
    
    // Example: Streaming multiple Final Fantasy games simultaneously  
    // FF1, FF6, and Chrono Trigger running concurrently
    public async Task ProcessMultipleGamesAsync()
    {
        var tasks = _activePacks.Select(async kvp =>
        {
            var (gameExe, pack) = kvp;
            await ProcessGameAsync(gameExe, pack);
        });
        
        await Task.WhenAll(tasks);
    }
}
```

## Performance Management

### 🚀 **Runtime Performance Optimization**

```csharp
public class RuntimeOptimizer
{
    public async Task ApplyPerformanceOptimizations(IGamePack pack)
    {
        var config = pack.GetConfiguration();
        
        // Apply V1 targeted search optimization if supported
        if (config.SupportsTargetedDetection)
        {
            var searchArea = config.GetOptimalSearchArea();
            await _engine.SetTargetedSearchAreaAsync(searchArea);
            
            _telemetry.RecordOptimization("TargetedSearch", searchArea.ReductionPercent);
        }
        
        // Configure frame similarity thresholds (V1 isBusy logic)
        await _engine.SetSimilarityThresholdsAsync(new SimilarityConfig
        {
            IdleThreshold = config.IdleSimilarityThreshold,    // 500px sample rate
            BusyThreshold = config.BusySimilarityThreshold,    // 50px sample rate  
            AdaptiveBehavior = true
        });
        
        // Memory optimization for long sessions
        await _engine.SetMemoryManagementAsync(new MemoryConfig
        {
            MaxCacheSize = config.MaxCacheSize,
            CacheRetentionTime = TimeSpan.FromHours(2),
            EnableGarbageCollection = true
        });
    }
}
```

### 📊 **Real-Time Performance Monitoring**

```csharp
public class PerformanceMonitor
{
    public async Task StartMonitoringAsync()
    {
        _timer = new Timer(async _ =>
        {
            var metrics = await CollectMetricsAsync();
            
            // Check if performance degrades below V1 benchmarks
            if (metrics.AverageDetectionTime > TimeSpan.FromMilliseconds(5))
            {
                await _alertService.NotifyPerformanceDegradationAsync(metrics);
                await _optimizer.TriggerOptimizationAsync();
            }
            
            // Update live dashboard
            await _eventBus.PublishAsync(new PerformanceMetricsEvent(metrics));
            
        }, null, TimeSpan.Zero, TimeSpan.FromSeconds(5));
    }
    
    private async Task<RuntimeMetrics> CollectMetricsAsync()
    {
        return new RuntimeMetrics
        {
            // V1 Performance Targets
            DetectionTime = _telemetry.GetAverageDetectionTime(),      // Target: <5ms
            ProcessingFPS = _telemetry.GetProcessingFrameRate(),       // Target: 15fps  
            MemoryUsage = GC.GetTotalMemory(false),                    // Target: <200MB
            SearchAreaReduction = _optimizer.GetSearchAreaReduction(), // Target: >70%
            
            // Runtime Health
            ActivePacks = _packManager.GetLoadedPacks().Count(),
            QueuedAudioItems = _audioService.GetQueueSize(),
            StreamingConnections = _streamingBridge.GetActiveConnections(),
            
            // Accuracy Metrics
            DetectionAccuracy = _telemetry.GetDetectionAccuracy(),     // Target: >95%
            SpeakerMatchAccuracy = _telemetry.GetSpeakerMatchAccuracy() // Target: >90%
        };
    }
}
```

## User Interfaces

### 🖥️ **Desktop Runtime GUI**

```
┌─ GameWatcher Runtime ──────────────────────────────────┐
│ ○ ○ ○                                    [_] [□] [×]   │
├─────────────────────────────────────────────────────────┤
│ Game: Final Fantasy VII           Status: ● Active     │
│ Pack: FF7.Original v1.2.0        Performance: ✅ Good  │
├─────────────────────────────────────────────────────────┤
│ ┌─ Live Activity ──────────────────────────────────────┐ │
│ │ Cloud: "Let's mosey."                    [🎵] Playing │ │
│ │ Tifa: "Are you alright?"                    Queued    │ │
│ │ Barrett: "Yo, Cloud!"                       Queued    │ │
│ └───────────────────────────────────────────────────────┘ │
│ ┌─ Performance ─────────────────┐ ┌─ Settings ────────┐ │
│ │ Detection: 2.8ms              │ │ Auto-Play: ✅     │ │
│ │ Accuracy: 94.7%               │ │ Auto-Gen: ✅      │ │
│ │ Queue: 3 items                │ │ Volume: ████████░ │ │  
│ │ Memory: 67MB                  │ │ Speed: 1.2x       │ │
│ └───────────────────────────────┘ └───────────────────┘ │
├─────────────────────────────────────────────────────────┤
│ [⏸ Pause] [🔄 Restart] [⚙ Settings] [📊 Analytics]    │
└─────────────────────────────────────────────────────────┘
```

### 📱 **Web Dashboard for Remote Control**

```typescript
// Real-time web dashboard for streamers
interface StreamerDashboard {
  currentGame: GameInfo;
  activePack: PackInfo;
  liveMetrics: RuntimeMetrics;
  recentDialogue: DialogueItem[];
  queuedAudio: AudioItem[];
  
  // Remote control functions
  pauseCapture(): Promise<void>;
  skipCurrentAudio(): Promise<void>;
  switchPack(packId: string): Promise<boolean>;
  updateSettings(settings: RuntimeSettings): Promise<void>;
}

// Example dashboard URL: https://dashboard.gamewatcher.dev/stream/abc123
// Allows remote control during live streaming sessions
```

## Integration Ecosystem

### 🎥 **Streaming Platform Integration**

```csharp
public class StreamingBridge
{
    // Twitch Integration
    public async Task SendDialogueToTwitchAsync(ProcessedDialogue dialogue)
    {
        var chatMessage = $"🎭 {dialogue.Speaker.Name}: \"{dialogue.Text}\"";
        await _twitchClient.SendChatMessageAsync(chatMessage);
        
        // Create channel point predictions for dialogue choices  
        if (dialogue.IsChoice)
        {
            await _twitchClient.CreatePredictionAsync(dialogue.ChoiceOptions);
        }
    }
    
    // OBS Integration
    public async Task UpdateOBSOverlayAsync(ProcessedDialogue dialogue)
    {
        var overlayData = new
        {
            speaker = dialogue.Speaker.Name,
            text = dialogue.Text,
            timestamp = DateTime.Now,
            voice = dialogue.Speaker.Voice,
            game = _activePack.Manifest.DisplayName
        };
        
        await _obsWebSocket.UpdateSourceAsync("GameWatcher-Overlay", overlayData);
    }
    
    // Discord Integration
    public async Task PostToDiscordAsync(ProcessedDialogue dialogue)
    {
        var embed = new DiscordEmbedBuilder()
            .WithTitle($"{dialogue.Speaker.Name} in {_activePack.Manifest.DisplayName}")
            .WithDescription(dialogue.Text)
            .WithColor(dialogue.Speaker.Color)
            .Build();
            
        await _discordClient.SendMessageAsync(_channelId, embed);
    }
}
```

### 🔌 **Plugin Architecture for Extensions**

```csharp
public interface IRuntimePlugin
{
    string Name { get; }
    Task<bool> InitializeAsync(IRuntimeContext context);
    Task OnDialogueProcessedAsync(ProcessedDialogue dialogue);
    Task OnPackLoadedAsync(IGamePack pack);
    Task OnPerformanceMetricsAsync(RuntimeMetrics metrics);
    Task ShutdownAsync();
}

// Example: Community-created plugins
public class ChatbotIntegrationPlugin : IRuntimePlugin
{
    public async Task OnDialogueProcessedAsync(ProcessedDialogue dialogue)
    {
        // Send dialogue to chatbot for context-aware responses
        await _chatbot.ProcessGameDialogueAsync(dialogue);
    }
}

public class AnalyticsPlugin : IRuntimePlugin  
{
    public async Task OnPerformanceMetricsAsync(RuntimeMetrics metrics)
    {
        // Send telemetry to analytics platform
        await _analytics.TrackMetricsAsync(metrics);
    }
}
```

## Configuration Management

### ⚙️ **Hierarchical Configuration System**

```json
{
  "runtime": {
    "global": {
      "captureFrameRate": 15,
      "enablePerformanceOptimizations": true,
      "maxMemoryUsage": "200MB", 
      "logLevel": "Information"
    },
    "packs": {
      "FF1.PixelRemaster": {
        "detectionStrategy": "HybridOptimized",
        "targetedSearchArea": {
          "enabled": true,
          "coordinates": [0.196875, 0.050926, 0.604688, 0.282407]
        },
        "speakers": {
          "defaultVoice": "fable",
          "defaultSpeed": 1.2
        }
      }
    },
    "streaming": {
      "twitch": {
        "enabled": true,
        "sendDialogueToChat": true,
        "createPredictions": false
      },
      "obs": {
        "enabled": true,
        "overlayTemplate": "Modern",
        "updateFrequency": "OnDialogue"
      }
    }
  }
}
```

## Deployment & Distribution

### 📦 **Runtime Distribution Models**

1. **Standalone Executable** - Single .exe with embedded engine
2. **Installer Package** - Full setup with pack manager integration  
3. **Portable Version** - USB-friendly, no installation required
4. **Docker Container** - Cloud/server deployment for remote streaming
5. **Browser Extension** - Lightweight web-based runtime

### 🔄 **Auto-Update System**

```csharp
public class UpdateManager
{
    public async Task<UpdateInfo?> CheckForUpdatesAsync()
    {
        var currentVersion = Assembly.GetExecutingAssembly().GetName().Version;
        var latestVersion = await _updateService.GetLatestVersionAsync();
        
        if (latestVersion > currentVersion)
        {
            return new UpdateInfo
            {
                Version = latestVersion,
                ReleaseNotes = await _updateService.GetReleaseNotesAsync(latestVersion),
                DownloadUrl = await _updateService.GetDownloadUrlAsync(latestVersion),
                IsSecurityUpdate = await _updateService.IsSecurityUpdateAsync(latestVersion)
            };
        }
        
        return null;
    }
    
    public async Task<bool> ApplyUpdateAsync(UpdateInfo update)
    {
        // Download update in background
        var updatePackage = await _downloader.DownloadAsync(update.DownloadUrl);
        
        // Verify digital signature
        if (!await _security.VerifySignatureAsync(updatePackage))
        {
            throw new SecurityException("Update package signature verification failed");
        }
        
        // Apply update and restart
        await _installer.ApplyUpdateAsync(updatePackage);
        
        // Restart runtime with preserved settings
        Process.Start("GameWatcher.Runtime.exe", "--updated");
        Environment.Exit(0);
        
        return true;
    }
}
```

## Success Metrics & Monitoring

| Category | Metric | Target | Monitoring |
|----------|--------|--------|------------|
| **Performance** | Detection Speed | <5ms avg | Real-time telemetry |
| | Frame Rate | 15fps sustained | Capture metrics |
| | Memory Usage | <200MB | System monitoring |
| **Reliability** | Uptime | >99.5% | Health checks |
| | Crash Rate | <0.1% | Error reporting |
| | Auto-Recovery | >95% | Exception handling |
| **User Experience** | Pack Load Time | <3s | Startup metrics |
| | Hot Swap Time | <2s | Pack switching |
| | Detection Accuracy | >95% | Quality metrics |

---

*The Runtime system transforms our V1 single-game optimization into a universal platform capable of supporting unlimited games with consistent performance and reliability.*