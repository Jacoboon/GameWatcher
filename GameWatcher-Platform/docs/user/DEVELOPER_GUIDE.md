# GameWatcher V2 Platform - Developer Guide

This guide provides comprehensive technical documentation for developers working with the GameWatcher V2 Platform architecture, game pack development, and system integration.

## 🏗️ Architecture Overview

### Core Components

#### GameWatcher.Engine
Core services library containing fundamental functionality:
```
GameWatcher.Engine/
├── Services/
│   ├── ICaptureService.cs      # Window capture abstraction
│   ├── CaptureService.cs       # Optimized capture implementation
│   └── ...
├── Ocr/
│   ├── IOcrEngine.cs          # Text recognition interface  
│   ├── WindowsOcrEngine.cs    # Windows OCR implementation
│   └── ...
├── Detection/
│   ├── ITextboxDetector.cs    # Detection strategy interface
│   ├── DynamicTextboxDetector.cs # Adaptive detection logic
│   └── ...
└── Packs/
    ├── IGamePack.cs           # Pack interface definition
    ├── IPackManager.cs        # Pack management interface
    └── ...
```

#### GameWatcher.Runtime
Universal orchestration system:
```
GameWatcher.Runtime/
├── Services/
│   ├── PackManager.cs         # Pack discovery and lifecycle
│   ├── GameDetectionService.cs # Auto-detection logic
│   ├── ProcessingPipeline.cs  # Real-time processing
│   └── RuntimeConfig.cs       # Configuration management
├── Program.cs                 # Console runtime entry
└── appsettings.json          # Runtime configuration
```

#### GameWatcher.Studio
Modern WPF GUI application:
```
GameWatcher.Studio/
├── ViewModels/               # MVVM view models
├── Views/                   # WPF user interface
├── Converters/             # Data binding converters
├── App.xaml(.cs)          # Application entry point
└── appsettings.json       # GUI configuration
```

### Performance Optimizations (V1 Preserved)

#### Targeted Detection (79.3% Search Area Reduction)
```csharp
public class OptimizedDetector : ITextboxDetector
{
    private Rectangle? _lastKnownRegion;
    
    public Rectangle? DetectTextbox(Bitmap frame)
    {
        // Search in last known area first
        if (_lastKnownRegion.HasValue)
        {
            var targeted = SearchRegion(frame, _lastKnownRegion.Value);
            if (targeted.HasValue) return targeted;
        }
        
        // Fall back to full search only if needed
        return FullFrameSearch(frame);
    }
}
```

#### Dynamic Similarity Thresholds (4.1x Performance Improvement)
```csharp
public class DynamicThresholdStrategy
{
    public double CalculateThreshold(DetectionHistory history)
    {
        // Adapt threshold based on detection success rate
        var successRate = history.GetSuccessRate();
        return Math.Max(0.7, 0.9 - (successRate * 0.2));
    }
}
```

## 🎮 Game Pack Development

### Pack Structure
```
YourGame.Pack/
├── pack-manifest.json        # Pack metadata and configuration
├── YourGamePack.cs          # Pack implementation
├── Detection/
│   └── YourGameDetector.cs  # Game-specific detection logic
├── Speakers/
│   ├── speaker-catalog.json # Speaker definitions
│   └── profiles/           # Individual speaker profiles
└── Config/
    └── detection-config.json # Detection parameters
```

### Implementing IGamePack

#### Basic Pack Implementation
```csharp
public class YourGamePack : GamePackBase
{
    public override PackManifest Manifest => new()
    {
        Name = "YourGame.CustomPack",
        Version = "1.0.0",
        DisplayName = "Your Game",
        Description = "Voiceover pack for Your Game",
        GameExecutable = "YourGame.exe",
        WindowTitle = "Your Game Window"
    };

    public override ITextboxDetector CreateDetectionStrategy()
    {
        return new YourGameHybridDetector(serviceProvider);
    }

    public override ISpeakerCollection GetSpeakers()
    {
        return LoadSpeakersFromJson("speakers/speaker-catalog.json");
    }
}
```

#### Custom Detection Strategy
```csharp
public class YourGameHybridDetector : ITextboxDetector
{
    private readonly IServiceProvider _serviceProvider;
    private readonly TemplateTextboxDetector _templateDetector;
    private readonly DynamicTextboxDetector _dynamicDetector;

    public YourGameHybridDetector(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
        _templateDetector = new TemplateTextboxDetector(
            LoadTemplates("templates/"), 
            threshold: 0.85
        );
        _dynamicDetector = new DynamicTextboxDetector();
    }

    public Rectangle? DetectTextbox(Bitmap frame)
    {
        // Try template matching first (faster)
        var templateResult = _templateDetector.DetectTextbox(frame);
        if (templateResult.HasValue) return templateResult;

        // Fall back to dynamic detection
        return _dynamicDetector.DetectTextbox(frame);
    }
}
```

### Pack Manifest Schema
```json
{
  "name": "YourGame.CustomPack",
  "version": "1.0.0",
  "displayName": "Your Game",
  "description": "Complete voiceover pack for Your Game",
  "author": "Your Name",
  "gameExecutable": "YourGame.exe",
  "windowTitle": "Your Game Window Title",
  "supportedVersions": ["1.0.0", "1.1.0"],
  "engineVersion": "2.0.0",
  "targetFrameRate": 15,
  "performance": {
    "targetedDetection": true,
    "searchAreaReduction": 75.0,
    "averageProcessingTime": "2.5ms"
  },
  "detection": {
    "strategy": "hybrid",
    "templateThreshold": 0.85,
    "dynamicFallback": true,
    "optimizationEnabled": true
  },
  "audio": {
    "format": "wav",
    "sampleRate": 44100,
    "crossfadeEnabled": true
  }
}
```

### Speaker Configuration
```json
{
  "speakers": [
    {
      "name": "Protagonist",
      "displayName": "Main Character",
      "voiceActor": "Your Voice Actor",
      "audioDirectory": "voices/protagonist/",
      "textPatterns": [
        "^[A-Z][a-z]+ says:",
        "Protagonist:",
        "You:"
      ],
      "priority": 100,
      "fallbackSpeaker": "Generic NPC"
    }
  ],
  "defaultSpeaker": "Generic NPC",
  "speakerMatchingStrategy": "pattern-priority"
}
```

## 🔧 Runtime Integration

### Dependency Injection Setup
```csharp
public static IHostBuilder CreateHostBuilder(string[] args) =>
    Host.CreateDefaultBuilder(args)
        .ConfigureServices((context, services) =>
        {
            // Core Engine Services
            services.AddSingleton<ICaptureService, CaptureService>();
            services.AddSingleton<IOcrEngine, WindowsOcrEngine>();
            
            // Runtime Services
            services.AddSingleton<IPackManager, PackManager>();
            services.AddSingleton<GameDetectionService>();
            services.AddSingleton<ProcessingPipeline>();
            
            // Configuration
            services.Configure<RuntimeConfig>(
                context.Configuration.GetSection("GameWatcher")
            );
        });
```

### Processing Pipeline Integration
```csharp
public class CustomProcessingPipeline : ProcessingPipeline
{
    protected override async Task ProcessFrameAsync(IGamePack pack, CancellationToken cancellationToken)
    {
        var startTime = DateTime.UtcNow;
        
        // Capture frame
        var screenshot = await CaptureScreenshotAsync(pack);
        if (screenshot == null) return;

        using (screenshot)
        {
            // Detect dialogue box
            var textboxRect = await DetectTextboxAsync(pack, screenshot);
            if (!textboxRect.HasValue) return;

            // Extract and process text  
            var dialogue = await ProcessDialogueAsync(pack, screenshot, textboxRect.Value);
            if (dialogue != null)
            {
                // Emit events for UI updates
                OnTextDetected(dialogue.Text);
                
                // Queue audio playback
                await QueueAudioAsync(dialogue);
            }
        }
        
        var processingTime = (DateTime.UtcNow - startTime).TotalMilliseconds;
        OnFrameProcessed(processingTime);
    }
}
```

## 📊 Performance Monitoring

### Metrics Collection
```csharp
public class PerformanceMetrics
{
    public class DetectionMetrics
    {
        public TimeSpan AverageDetectionTime { get; set; }
        public double SearchAreaReduction { get; set; }
        public int SuccessfulDetections { get; set; }
        public int TotalAttempts { get; set; }
        public double SuccessRate => (double)SuccessfulDetections / TotalAttempts;
    }

    public class ProcessingMetrics  
    {
        public TimeSpan AverageProcessingTime { get; set; }
        public int FramesProcessed { get; set; }
        public double FramesPerSecond { get; set; }
        public TimeSpan TotalRuntime { get; set; }
    }
}
```

### Performance Benchmarking
```csharp
public class PerformanceBenchmark
{
    public static async Task<BenchmarkResults> RunAsync(IGamePack pack, int frameCount = 1000)
    {
        var results = new BenchmarkResults();
        var stopwatch = Stopwatch.StartNew();
        
        for (int i = 0; i < frameCount; i++)
        {
            var frameStart = stopwatch.Elapsed;
            
            // Simulate frame processing
            await ProcessSingleFrameAsync(pack);
            
            var frameTime = stopwatch.Elapsed - frameStart;
            results.ProcessingTimes.Add(frameTime);
        }
        
        stopwatch.Stop();
        
        return new BenchmarkResults
        {
            TotalFrames = frameCount,
            TotalTime = stopwatch.Elapsed,
            AverageProcessingTime = results.ProcessingTimes.Average(t => t.TotalMilliseconds),
            FramesPerSecond = frameCount / stopwatch.Elapsed.TotalSeconds
        };
    }
}
```

## 🔌 Extension Points

### Custom OCR Engines
```csharp
public class CustomOcrEngine : IOcrEngine
{
    public async Task<string> ExtractTextAsync(Bitmap image)
    {
        // Implement your OCR logic
        // Could integrate Tesseract, Cloud APIs, etc.
        return await YourOcrImplementation(image);
    }

    public double GetConfidence(string text)
    {
        // Return confidence score for the extraction
        return CalculateConfidence(text);
    }
}
```

### Custom Capture Services
```csharp
public class CustomCaptureService : ICaptureService  
{
    public async Task InitializeAsync(string windowTitle)
    {
        // Initialize your capture method
        // Could use DirectX, Windows Graphics Capture, etc.
        await SetupCapture(windowTitle);
    }

    public Bitmap? GetLastFrame()
    {
        // Return captured frame
        return CaptureCurrentFrame();
    }
}
```

## 🚀 Deployment

### Building for Distribution
```bash
# Build all components
dotnet build GameWatcher-Platform.sln --configuration Release

# Publish self-contained application
dotnet publish GameWatcher.Studio -c Release -r win-x64 --self-contained

# Create installer package
dotnet pack GameWatcher.Studio -c Release
```

### Configuration Management
```csharp
public class DeploymentConfig
{
    public static void ConfigureForProduction(IServiceCollection services)
    {
        services.Configure<RuntimeConfig>(config =>
        {
            config.DetectionIntervalMs = 1000;
            config.EnableOptimization = true;
            config.LogLevel = LogLevel.Information;
        });
    }
}
```

## 🧪 Testing

### Unit Testing Game Packs
```csharp
[TestClass]
public class YourGamePackTests
{
    [TestMethod]  
    public async Task DetectionStrategy_ShouldFindTextbox_WhenDialoguePresent()
    {
        // Arrange
        var pack = new YourGamePack();
        var detector = pack.CreateDetectionStrategy();
        var testImage = LoadTestImage("dialogue-present.png");
        
        // Act
        var result = detector.DetectTextbox(testImage);
        
        // Assert
        Assert.IsTrue(result.HasValue);
        Assert.IsTrue(result.Value.Width > 0);
        Assert.IsTrue(result.Value.Height > 0);
    }
}
```

### Performance Testing
```csharp
[TestMethod]
public async Task ProcessingPipeline_ShouldMeetPerformanceTargets()
{
    // Benchmark against V1 performance targets
    var results = await PerformanceBenchmark.RunAsync(pack);
    
    Assert.IsTrue(results.AverageProcessingTime < 3.0); // Under 3ms
    Assert.IsTrue(results.FramesPerSecond > 10);        // At least 10 FPS
}
```

## 📈 Optimization Guidelines

### Detection Optimization
1. **Use template matching** for static UI elements
2. **Implement region caching** for consistent dialogue positions  
3. **Apply dynamic thresholds** based on success rates
4. **Limit search areas** when possible

### Memory Management
1. **Dispose bitmaps** promptly after use
2. **Pool image objects** for frequent operations
3. **Monitor GC pressure** in performance-critical paths
4. **Use streaming** for large audio files

### Threading Best Practices
1. **Keep UI thread responsive** with async/await
2. **Use background threads** for processing
3. **Implement cancellation** for long-running operations
4. **Avoid blocking calls** in event handlers

---

**GameWatcher V2 Platform Developer Guide**
*Building upon proven V1 optimizations with modern, extensible architecture*