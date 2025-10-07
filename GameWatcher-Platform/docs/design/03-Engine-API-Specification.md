# GameWatcher Engine API Specification

## Overview

The GameWatcher Engine provides **game-agnostic core services** that any game pack can leverage. It encapsulates all the performance optimizations and patterns we discovered in V1 into reusable, testable interfaces.

## Core Engine Services

### 🎯 **IDetectionEngine** - Textbox Detection Services

```csharp
namespace GameWatcher.Engine.Detection
{
    public interface IDetectionEngine
    {
        Task<DetectionResult> DetectTextboxAsync(DetectionRequest request);
        Task<string> ExtractTextAsync(Rectangle textboxArea, Bitmap screenshot, OcrConfig config);
        DetectionMetrics GetPerformanceMetrics();
        void RegisterStrategy(string name, IDetectionStrategy strategy);
    }
    
    public class DetectionRequest
    {
        public Bitmap Screenshot { get; set; }
        public Rectangle? TargetArea { get; set; }        // V1 optimization: targeted search
        public DetectionStrategy Strategy { get; set; }
        public Dictionary<string, object> Parameters { get; set; }
        public bool EnablePerformanceOptimization { get; set; } = true;
    }
    
    public class DetectionResult
    {
        public Rectangle? TextboxArea { get; set; }
        public double Confidence { get; set; }
        public TimeSpan ProcessingTime { get; set; }
        public int SamplePointsProcessed { get; set; }    // V1 metric: track efficiency
        public string Strategy { get; set; }
        public bool WasOptimized { get; set; }            // Whether targeted search was used
    }
}
```

### 🖼️ **ICaptureEngine** - Screen Capture Services

```csharp
namespace GameWatcher.Engine.Capture
{
    public interface ICaptureEngine
    {
        Task<CaptureResult> CaptureGameWindowAsync(string windowTitle);
        Task<bool> IsGameRunningAsync(string executableName);
        void StartContinuousCapture(CaptureConfig config, Action<Bitmap> onFrameCaptured);
        void StopContinuousCapture();
        CaptureMetrics GetPerformanceMetrics();
    }
    
    public class CaptureConfig
    {
        public int TargetFPS { get; set; } = 15;
        public bool EnableFrameSkipping { get; set; } = true;       // V1 optimization
        public bool EnableDuplicateDetection { get; set; } = true;   // V1 isBusy logic
        public Rectangle? CropArea { get; set; }
        public CaptureQuality Quality { get; set; } = CaptureQuality.Balanced;
    }
    
    public class CaptureResult
    {
        public Bitmap Frame { get; set; }
        public Rectangle WindowBounds { get; set; }
        public DateTime CaptureTime { get; set; }
        public bool IsDuplicateFrame { get; set; }      // V1 optimization detection
        public TimeSpan ProcessingTime { get; set; }
    }
}
```

### 🔤 **IOcrEngine** - Text Recognition Services

```csharp
namespace GameWatcher.Engine.OCR
{
    public interface IOcrEngine
    {
        Task<OcrResult> RecognizeTextAsync(Bitmap image, OcrConfig config);
        Task<string> PostProcessTextAsync(string rawText, PostProcessingConfig config);
        bool IsLanguageSupported(string languageCode);
        OcrMetrics GetPerformanceMetrics();
    }
    
    public class OcrConfig
    {
        public string Language { get; set; } = "en-US";
        public OcrPreprocessing Preprocessing { get; set; } = new();
        public double ScaleFactor { get; set; } = 2.0;   // V1 learning: 2x scaling helps
        public bool EnableThresholding { get; set; } = true;
        public Rectangle? Region { get; set; }
    }
    
    public class OcrPreprocessing
    {
        public bool ConvertToGrayscale { get; set; } = true;
        public bool ApplyThreshold { get; set; } = true;
        public bool ReduceNoise { get; set; } = true;
        public bool EnhanceContrast { get; set; } = false;
    }
    
    public class OcrResult
    {
        public string Text { get; set; }
        public double Confidence { get; set; }
        public TimeSpan ProcessingTime { get; set; }
        public Rectangle[] WordBoundaries { get; set; }
        public string RawOutput { get; set; }        // Before post-processing
        public string ProcessedOutput { get; set; }  // After normalization
    }
}
```

### 🎵 **IAudioEngine** - TTS and Playback Services

```csharp
namespace GameWatcher.Engine.Audio
{
    public interface IAudioEngine
    {
        Task<AudioResult> GenerateSpeechAsync(AudioRequest request);
        Task PlayAudioAsync(string filePath);
        Task QueueAudioAsync(string filePath);
        void ClearQueue();
        AudioStatus GetPlaybackStatus();
        AudioMetrics GetPerformanceMetrics();
    }
    
    public class AudioRequest
    {
        public string Text { get; set; }
        public VoiceProfile Voice { get; set; }
        public double Speed { get; set; } = 1.0;
        public AudioFormat OutputFormat { get; set; } = AudioFormat.MP3;
        public bool CacheResult { get; set; } = true;
    }
    
    public class VoiceProfile
    {
        public string Provider { get; set; }  // "OpenAI", "Azure", "ElevenLabs"
        public string VoiceId { get; set; }   // "fable", "shimmer", etc.
        public double Stability { get; set; } = 0.8;
        public double Clarity { get; set; } = 0.8;
        public Dictionary<string, object> CustomParameters { get; set; }
    }
    
    public class AudioResult
    {
        public byte[] AudioData { get; set; }
        public string FilePath { get; set; }
        public TimeSpan Duration { get; set; }
        public AudioFormat Format { get; set; }
        public double GenerationCost { get; set; }    // API cost tracking
        public bool WasCached { get; set; }
    }
}
```

## Performance Optimization APIs

### 🚀 **IPerformanceOptimizer** - V1 Optimizations as Engine Services

```csharp
namespace GameWatcher.Engine.Optimization
{
    public interface IPerformanceOptimizer
    {
        // Targeted detection areas (79.3% reduction from V1)
        Rectangle CalculateOptimalSearchArea(Bitmap screenshot, GamePackConfig config);
        
        // Frame similarity detection (isBusy logic from V1)
        bool AreFramesSimilar(Bitmap frame1, Bitmap frame2, SimilarityConfig config);
        
        // Dynamic threshold adjustment based on activity
        SimilarityThreshold GetDynamicThreshold(bool isBusy);
        
        // Memory optimization for continuous capture
        void OptimizeMemoryUsage(MemoryOptimizationConfig config);
    }
    
    public class SimilarityConfig
    {
        public int SampleRate { get; set; } = 500;      // V1 default: 500px when idle
        public int BusySampleRate { get; set; } = 50;   // V1 optimization: 50px when busy
        public double Tolerance { get; set; } = 0.95;
        public Rectangle? ComparisonArea { get; set; }
    }
    
    public class SimilarityThreshold
    {
        public int SampleRate { get; set; }
        public string Reason { get; set; }
        public TimeSpan RecommendedFrameSkip { get; set; }
    }
}
```

## Pack Integration APIs

### 🔌 **IPackManager** - Pack Loading and Management

```csharp
namespace GameWatcher.Engine.Packs
{
    public interface IPackManager
    {
        Task<PackLoadResult> LoadPackAsync(string packPath);
        Task<bool> UnloadPackAsync(string packId);
        Task<IGamePack> GetActivePackAsync();
        Task<IEnumerable<PackManifest>> GetAvailablePacksAsync();
        Task<PackValidationResult> ValidatePackAsync(string packPath);
    }
    
    public interface IGamePack
    {
        PackManifest Manifest { get; }
        
        // Detection Strategy Factory
        IDetectionStrategy CreateDetectionStrategy(IDetectionEngine engine);
        
        // Speaker and Voice Management  
        ISpeakerCollection GetSpeakers();
        
        // Configuration Providers
        OcrConfig GetOcrConfiguration();
        CaptureConfig GetCaptureConfiguration();
        AudioConfig GetAudioConfiguration();
        
        // Game-specific Logic
        Task<bool> IsTargetGameRunningAsync();
        Task<GameState> GetGameStateAsync();
        
        // Event Handlers
        Task OnDialogueDetectedAsync(DialogueDetectedEventArgs args);
        Task OnAudioGeneratedAsync(AudioGeneratedEventArgs args);
        Task OnErrorAsync(ErrorEventArgs args);
    }
}
```

### 🎮 **IDetectionStrategy** - Game-Specific Detection Logic

```csharp
namespace GameWatcher.Engine.Packs
{
    public interface IDetectionStrategy
    {
        string Name { get; }
        Task<DetectionResult> DetectTextboxAsync(DetectionRequest request);
        DetectionCapabilities GetCapabilities();
        Task<bool> ValidateAsync(Bitmap testImage);
    }
    
    // FF1 Implementation Example
    public class FF1HybridDetectionStrategy : IDetectionStrategy
    {
        private readonly IDetectionEngine _engine;
        
        public string Name => "FF1.HybridDetection";
        
        public async Task<DetectionResult> DetectTextboxAsync(DetectionRequest request)
        {
            // Apply V1 optimizations: targeted search area
            var targetArea = new Rectangle(
                (int)(request.Screenshot.Width * 0.196875) - 25,
                (int)(request.Screenshot.Height * 0.050926) - 25,
                (int)(request.Screenshot.Width * 0.604688) + 50,
                (int)(request.Screenshot.Height * 0.282407) + 50
            );
            
            request.TargetArea = targetArea;
            
            // Use engine's optimized detection
            return await _engine.DetectTextboxAsync(request);
        }
    }
}
```

## Configuration System

### ⚙️ **Engine Configuration**

```csharp
namespace GameWatcher.Engine.Configuration
{
    public class EngineConfig
    {
        public DetectionConfig Detection { get; set; } = new();
        public CaptureConfig Capture { get; set; } = new();
        public OcrConfig OCR { get; set; } = new();
        public AudioConfig Audio { get; set; } = new();
        public PerformanceConfig Performance { get; set; } = new();
    }
    
    public class PerformanceConfig
    {
        public bool EnableTargetedSearch { get; set; } = true;    // V1 optimization
        public bool EnableFrameSkipping { get; set; } = true;     // V1 optimization  
        public bool EnableMemoryOptimization { get; set; } = true;
        public int MaxConcurrentOperations { get; set; } = 4;
        public TimeSpan CacheRetention { get; set; } = TimeSpan.FromHours(24);
    }
}
```

## Metrics and Monitoring

### 📊 **Performance Monitoring**

```csharp
namespace GameWatcher.Engine.Metrics
{
    public class EngineMetrics
    {
        // V1 Performance Benchmarks
        public TimeSpan AverageDetectionTime { get; set; }        // Target: <5ms (V1: 2.3ms)
        public double SearchAreaReduction { get; set; }           // Target: >70% (V1: 79.3%)
        public int FramesPerSecond { get; set; }                  // Target: 15fps (V1: 14.9fps)
        
        // Resource Usage
        public long MemoryUsageBytes { get; set; }
        public double CpuUsagePercent { get; set; }
        public int ActiveThreadCount { get; set; }
        
        // Accuracy Metrics
        public double DetectionAccuracy { get; set; }             // Target: >95%
        public double SpeakerMatchingAccuracy { get; set; }       // Target: >90%
        public double OcrAccuracy { get; set; }                   // Target: >92%
        
        // Cache Efficiency
        public double CacheHitRatio { get; set; }
        public int CachedAudioFiles { get; set; }
        public long CacheSizeBytes { get; set; }
    }
}
```

## Error Handling and Resilience

### 🛡️ **Robust Error Management**

```csharp
namespace GameWatcher.Engine.Resilience
{
    public interface IErrorHandler
    {
        Task<bool> HandleDetectionErrorAsync(DetectionException ex, DetectionRequest request);
        Task<bool> HandleCaptureErrorAsync(CaptureException ex, CaptureConfig config);
        Task<bool> HandleOcrErrorAsync(OcrException ex, OcrConfig config);
        Task<bool> HandleAudioErrorAsync(AudioException ex, AudioRequest request);
    }
    
    public class RetryPolicy
    {
        public int MaxRetries { get; set; } = 3;
        public TimeSpan InitialDelay { get; set; } = TimeSpan.FromMilliseconds(100);
        public TimeSpan MaxDelay { get; set; } = TimeSpan.FromSeconds(5);
        public double BackoffMultiplier { get; set; } = 2.0;
        public bool EnableCircuitBreaker { get; set; } = true;
    }
}
```

---

*The Engine API captures all our V1 performance learnings while providing a clean, testable foundation for unlimited game support.*