# GameWatcher V2 Platform - API Reference

Complete API documentation for the GameWatcher V2 Platform, covering all interfaces, classes, and extension points for developers integrating with or extending the platform.

## 🏗️ Core Interfaces

### GameWatcher.Engine

#### ICaptureService
Primary interface for game window capture functionality.

```csharp
namespace GameWatcher.Engine.Services;

public interface ICaptureService
{
    /// <summary>
    /// Initialize capture for specified window title
    /// </summary>
    Task InitializeAsync(string windowTitle);
    
    /// <summary>
    /// Get the most recently captured frame
    /// </summary>
    Bitmap? GetLastFrame();
    
    /// <summary>
    /// Check if capture is currently active
    /// </summary>
    bool IsActive { get; }
    
    /// <summary>
    /// Release capture resources
    /// </summary>
    void Dispose();
}
```

**Implementation Notes:**
- Uses Windows Graphics Capture API on Windows 10+
- Falls back to PrintWindow on older systems
- Automatically handles window focus changes
- Optimized for real-time capture (15+ FPS)

---

#### IOcrEngine
Text recognition interface with confidence scoring.

```csharp
namespace GameWatcher.Engine.Ocr;

public interface IOcrEngine
{
    /// <summary>
    /// Extract text from image region
    /// </summary>
    Task<string> ExtractTextAsync(Bitmap image);
    
    /// <summary>
    /// Get confidence score for extracted text (0.0-1.0)
    /// </summary>
    double GetConfidence(string extractedText);
    
    /// <summary>
    /// Configure OCR language settings
    /// </summary>
    Task SetLanguageAsync(string languageCode);
}
```

**WindowsOcrEngine Implementation:**
- Uses Windows Runtime OCR APIs
- Supports 25+ languages out of box
- Average confidence scores: 0.85-0.95 for game text
- Processing time: <1ms for dialogue regions

---

#### ITextboxDetector
Detection strategy interface for locating dialogue areas.

```csharp
namespace GameWatcher.Engine.Detection;

public interface ITextboxDetector
{
    /// <summary>
    /// Detect dialogue textbox in game frame
    /// </summary>
    Rectangle? DetectTextbox(Bitmap frame);
    
    /// <summary>
    /// Get detection confidence for last result
    /// </summary>
    double GetLastConfidence();
    
    /// <summary>
    /// Configure detection parameters
    /// </summary>
    void Configure(DetectionConfig config);
}

public class DetectionConfig
{
    public double SimilarityThreshold { get; set; } = 0.85;
    public bool EnableOptimization { get; set; } = true;
    public Rectangle? SearchRegion { get; set; }
    public TimeSpan CacheTimeout { get; set; } = TimeSpan.FromSeconds(5);
}
```

**Available Implementations:**
- `TemplateTextboxDetector` - Template matching approach
- `DynamicTextboxDetector` - Adaptive detection
- `HybridTextboxDetector` - Combined strategy (recommended)

---

### GameWatcher.Engine.Packs

#### IGamePack
Core interface for game-specific functionality packages.

```csharp
namespace GameWatcher.Engine.Packs;

public interface IGamePack
{
    /// <summary>
    /// Pack metadata and configuration
    /// </summary>
    PackManifest Manifest { get; }
    
    /// <summary>
    /// Create detection strategy for this game
    /// </summary>
    ITextboxDetector CreateDetectionStrategy();
    
    /// <summary>
    /// Get speaker collection for dialogue mapping
    /// </summary>
    ISpeakerCollection GetSpeakers();
    
    /// <summary>
    /// Initialize pack with service provider
    /// </summary>
    Task InitializeAsync(IServiceProvider serviceProvider);
    
    /// <summary>
    /// Cleanup pack resources
    /// </summary>
    void Dispose();
}

public class PackManifest
{
    public string Name { get; set; } = "";
    public string Version { get; set; } = "1.0.0";
    public string DisplayName { get; set; } = "";
    public string Description { get; set; } = "";
    public string Author { get; set; } = "";
    public string GameExecutable { get; set; } = "";
    public string WindowTitle { get; set; } = "";
    public string[] SupportedVersions { get; set; } = Array.Empty<string>();
    public string EngineVersion { get; set; } = "2.0.0";
    public int TargetFrameRate { get; set; } = 15;
    
    public PackPerformance Performance { get; set; } = new();
    public PackDetection Detection { get; set; } = new();
    public PackAudio Audio { get; set; } = new();
}
```

---

#### ISpeakerCollection
Interface for managing character voice mappings.

```csharp
namespace GameWatcher.Engine.Packs;

public interface ISpeakerCollection
{
    /// <summary>
    /// Match dialogue text to appropriate speaker
    /// </summary>
    SpeakerProfile? MatchSpeaker(string dialogueText);
    
    /// <summary>
    /// Get all available speakers
    /// </summary>
    IReadOnlyList<SpeakerProfile> GetAllSpeakers();
    
    /// <summary>
    /// Get speaker by name
    /// </summary>
    SpeakerProfile? GetSpeaker(string speakerName);
    
    /// <summary>
    /// Default speaker for unmatched dialogue
    /// </summary>
    SpeakerProfile DefaultSpeaker { get; }
}

public class SpeakerProfile
{
    public string Name { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public string VoiceActor { get; set; } = "";
    public string AudioDirectory { get; set; } = "";
    public string[] TextPatterns { get; set; } = Array.Empty<string>();
    public int Priority { get; set; } = 50;
    public string? FallbackSpeaker { get; set; }
}
```

## 🚀 Runtime Services

### GameWatcher.Runtime.Services

#### IPackManager
Central pack management and lifecycle service.

```csharp
namespace GameWatcher.Runtime.Services;

public interface IPackManager
{
    /// <summary>
    /// Discover packs in specified directories
    /// </summary>
    Task<IReadOnlyList<IGamePack>> DiscoverPacksAsync(IEnumerable<string> directories);
    
    /// <summary>
    /// Get pack by identifier
    /// </summary>
    Task<IGamePack?> GetPackAsync(string packId);
    
    /// <summary>
    /// Find appropriate pack for running game
    /// </summary>
    Task<IGamePack?> FindPackForGameAsync(string gameExecutable);
    
    /// <summary>
    /// Load and activate pack
    /// </summary>
    Task<bool> LoadPackAsync(IGamePack pack);
    
    /// <summary>
    /// Unload pack by identifier
    /// </summary>
    Task<bool> UnloadPackAsync(string packId);
    
    /// <summary>
    /// Get all currently loaded packs
    /// </summary>
    IReadOnlyList<IGamePack> GetLoadedPacks();
    
    /// <summary>
    /// Get currently active pack
    /// </summary>
    IGamePack? GetActivePack();
}
```

---

#### GameDetectionService
Automatic game detection and pack matching.

```csharp
namespace GameWatcher.Runtime.Services;

public class GameDetectionService
{
    /// <summary>
    /// Detect currently active supported game
    /// </summary>
    public async Task<DetectedGame?> DetectActiveGameAsync();
    
    /// <summary>
    /// Detect all running supported games
    /// </summary>
    public async Task<IReadOnlyList<DetectedGame>> DetectAllRunningGamesAsync();
    
    /// <summary>
    /// Calculate confidence score for pack compatibility
    /// </summary>
    public async Task<double> CalculateConfidenceAsync(IGamePack pack);
}

public record DetectedGame(
    string ProcessName,
    string WindowTitle, 
    IGamePack Pack,
    double Confidence
);
```

---

#### ProcessingPipeline
Real-time frame processing coordination.

```csharp
namespace GameWatcher.Runtime.Services;

public class ProcessingPipeline : IDisposable
{
    /// <summary>
    /// Start processing pipeline
    /// </summary>
    public async Task StartAsync(CancellationToken cancellationToken = default);
    
    /// <summary>
    /// Stop processing pipeline
    /// </summary>
    public async Task StopAsync();
    
    /// <summary>
    /// Pause processing temporarily
    /// </summary>
    public Task PauseAsync();
    
    /// <summary>
    /// Resume paused processing
    /// </summary>
    public Task ResumeAsync();
    
    /// <summary>
    /// Check if pipeline is currently running
    /// </summary>
    public bool IsRunning { get; }
    
    /// <summary>
    /// Check if pipeline is paused
    /// </summary>
    public bool IsPaused { get; }
    
    // Events for monitoring
    public event EventHandler<FrameProcessedEventArgs>? FrameProcessed;
    public event EventHandler<TextDetectedEventArgs>? TextDetected;
    public event EventHandler<AudioPlayedEventArgs>? AudioPlayed;
}

// Event argument types
public class FrameProcessedEventArgs : EventArgs
{
    public double ProcessingTimeMs { get; set; }
}

public class TextDetectedEventArgs : EventArgs  
{
    public string Text { get; set; } = string.Empty;
}

public class AudioPlayedEventArgs : EventArgs
{
    public string AudioFile { get; set; } = string.Empty;
}
```

## 🎨 Studio UI Components

### GameWatcher.Studio.ViewModels

#### MainWindowViewModel
Primary view model for the Studio interface.

```csharp
namespace GameWatcher.Studio.ViewModels;

public partial class MainWindowViewModel : ObservableObject
{
    // Observable Properties
    [ObservableProperty] private bool _isRunning;
    [ObservableProperty] private string _statusText = "Ready";
    [ObservableProperty] private string _currentGame = "No game detected";
    [ObservableProperty] private string _currentPack = "No pack loaded";
    
    // Child ViewModels
    public PackManagerViewModel PackManagerViewModel { get; }
    public ActivityMonitorViewModel ActivityMonitorViewModel { get; }
    public SettingsViewModel SettingsViewModel { get; }
    
    // Commands
    [RelayCommand] public async Task StartAsync();
    [RelayCommand] public async Task StopAsync();
    [RelayCommand] public async Task RefreshPacksAsync();
}
```

---

#### PackManagerViewModel  
Pack management interface view model.

```csharp
namespace GameWatcher.Studio.ViewModels;

public partial class PackManagerViewModel : ObservableObject
{
    [ObservableProperty] private ObservableCollection<PackInfoViewModel> _availablePacks = new();
    [ObservableProperty] private PackInfoViewModel? _selectedPack;
    [ObservableProperty] private PackInfoViewModel? _activePack;
    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private string _statusMessage = string.Empty;
    
    [RelayCommand] public async Task RefreshAsync();
    [RelayCommand] public async Task LoadPackAsync(PackInfoViewModel? pack);
    [RelayCommand] public async Task UnloadPackAsync(PackInfoViewModel? pack);
}

public partial class PackInfoViewModel : ObservableObject
{
    [ObservableProperty] private string _name = string.Empty;
    [ObservableProperty] private string _version = string.Empty;
    [ObservableProperty] private string _description = string.Empty;
    [ObservableProperty] private string _supportedGames = string.Empty;
    [ObservableProperty] private bool _isLoaded;
    
    public IGamePack Pack { get; set; } = null!;
}
```

---

#### ActivityMonitorViewModel
Performance monitoring view model.

```csharp
namespace GameWatcher.Studio.ViewModels;

public partial class ActivityMonitorViewModel : ObservableObject  
{
    [ObservableProperty] private ObservableCollection<ActivityLogEntry> _activityLog = new();
    [ObservableProperty] private int _framesProcessed;
    [ObservableProperty] private int _textDetections;
    [ObservableProperty] private int _audioPlayed;
    [ObservableProperty] private double _averageProcessingTime;
    [ObservableProperty] private double _cpuUsage;
    [ObservableProperty] private double _memoryUsage;
    [ObservableProperty] private string _lastDetectedText = string.Empty;
    [ObservableProperty] private DateTime _lastActivityTime = DateTime.Now;
    
    public void RefreshMetrics();
}

public class ActivityLogEntry
{
    public DateTime Timestamp { get; set; }
    public string Message { get; set; } = string.Empty;
    public ActivityLogLevel Level { get; set; }
    public string FormattedTimestamp => Timestamp.ToString("HH:mm:ss.fff");
}

public enum ActivityLogLevel { Debug, Info, Warning, Error }
```

## 🔧 Configuration APIs

### Runtime Configuration
```csharp
namespace GameWatcher.Runtime;

public class RuntimeConfig
{
    public bool AutoStart { get; set; } = true;
    public int DetectionIntervalMs { get; set; } = 2000;
    public string[] PackDirectories { get; set; } = Array.Empty<string>();
    
    public CaptureConfig Capture { get; set; } = new();
    public OcrConfig OCR { get; set; } = new();
    public AudioConfig Audio { get; set; } = new();
}

public class CaptureConfig
{
    public int TargetFps { get; set; } = 10;
    public bool EnableOptimization { get; set; } = true;
    public double OptimizationThreshold { get; set; } = 0.85;
}

public class OcrConfig
{
    public string Language { get; set; } = "en-US";
    public double ConfidenceThreshold { get; set; } = 0.7;
    public bool EnablePreprocessing { get; set; } = true;
}

public class AudioConfig
{
    public int MasterVolume { get; set; } = 80;
    public string OutputDevice { get; set; } = "Default";
    public bool EnableCrossfade { get; set; } = true;
}
```

## 🎯 Extension Points

### Custom Game Pack Implementation
```csharp
public class CustomGamePack : GamePackBase
{
    public override PackManifest Manifest => new()
    {
        Name = "CustomGame.Pack",
        Version = "1.0.0",
        DisplayName = "Custom Game",
        GameExecutable = "CustomGame.exe"
    };
    
    public override ITextboxDetector CreateDetectionStrategy()
    {
        return new CustomGameDetector(_serviceProvider);
    }
    
    public override ISpeakerCollection GetSpeakers()  
    {
        return LoadSpeakersFromJson("speakers/custom-speakers.json");
    }
}
```

### Custom Detection Strategy
```csharp
public class CustomGameDetector : ITextboxDetector
{
    private double _lastConfidence;
    
    public Rectangle? DetectTextbox(Bitmap frame)
    {
        // Implement game-specific detection logic
        var result = PerformCustomDetection(frame);
        _lastConfidence = result.Confidence;
        return result.TextboxRect;
    }
    
    public double GetLastConfidence() => _lastConfidence;
    
    public void Configure(DetectionConfig config)
    {
        // Apply configuration settings
        ApplyConfig(config);
    }
}
```

### Custom OCR Engine
```csharp
public class CustomOcrEngine : IOcrEngine
{
    public async Task<string> ExtractTextAsync(Bitmap image)
    {
        // Implement custom OCR logic
        // Could integrate Tesseract, cloud APIs, etc.
        return await ProcessImageWithCustomOcr(image);
    }
    
    public double GetConfidence(string extractedText)
    {
        return CalculateCustomConfidence(extractedText);
    }
    
    public async Task SetLanguageAsync(string languageCode)
    {
        await ConfigureLanguageSettings(languageCode);
    }
}
```

## 📊 Performance Monitoring APIs

### Metrics Collection
```csharp
public class PerformanceCollector
{
    public class ProcessingMetrics
    {
        public TimeSpan AverageProcessingTime { get; set; }
        public int FramesProcessed { get; set; }
        public double FramesPerSecond { get; set; }
        public double SearchAreaReduction { get; set; }
        public double DetectionSuccessRate { get; set; }
    }
    
    public ProcessingMetrics GetCurrentMetrics();
    public void ResetMetrics();
    public Task<ProcessingMetrics> BenchmarkAsync(TimeSpan duration);
}
```

### Resource Monitoring
```csharp
public class ResourceMonitor
{
    public double GetCpuUsage();
    public long GetMemoryUsage();
    public double GetGpuUsage();
    public TimeSpan GetUptime();
    
    public event EventHandler<ResourceUsageEventArgs>? ResourceThresholdExceeded;
}
```

## 🛠️ Utility APIs

### Logging Infrastructure
```csharp
// Built on Microsoft.Extensions.Logging
public static class GameWatcherLogging
{
    public static ILogger CreateLogger<T>() => LoggerFactory.CreateLogger<T>();
    public static void ConfigureSerilog(LoggerConfiguration config);
}
```

### Dependency Injection
```csharp
// Standard Microsoft DI container
public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddGameWatcherEngine(this IServiceCollection services);
    public static IServiceCollection AddGameWatcherRuntime(this IServiceCollection services);
    public static IServiceCollection AddGameWatcherStudio(this IServiceCollection services);
}
```

---

**GameWatcher V2 Platform API Reference**
*Complete developer reference for building with and extending the GameWatcher platform*

**Version**: 2.0.0  
**Compatibility**: .NET 8.0+, Windows 10 1903+