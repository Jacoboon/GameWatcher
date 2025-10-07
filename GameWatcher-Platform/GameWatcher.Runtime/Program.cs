using GameWatcher.Engine.Services;
using GameWatcher.Engine.Ocr;
using GameWatcher.Engine.Detection;
using GameWatcher.Runtime.Services;
using GameWatcher.Runtime.Services.Capture;
using GameWatcher.Runtime.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;

namespace GameWatcher.Runtime;

/// <summary>
/// Universal GameWatcher Runtime - orchestrates capture, detection, OCR and TTS for any registered game pack.
/// Preserves V1 performance optimizations while supporting unlimited games through modular pack system.
/// </summary>
public class Program
{
    public static async Task Main(string[] args)
    {
        var host = CreateHostBuilder(args).Build();
        
        var logger = host.Services.GetRequiredService<ILogger<Program>>();
        logger.LogInformation("Starting GameWatcher V2 Runtime...");
        
        try
        {
            await host.RunAsync();
        }
        catch (Exception ex)
        {
            logger.LogCritical(ex, "Runtime crashed: {Message}", ex.Message);
            throw;
        }
    }

    private static IHostBuilder CreateHostBuilder(string[] args) =>
        Host.CreateDefaultBuilder(args)
            .ConfigureAppConfiguration((context, config) =>
            {
                config.AddJsonFile("appsettings.json", optional: true);
                config.AddJsonFile($"appsettings.{context.HostingEnvironment.EnvironmentName}.json", optional: true);
                config.AddEnvironmentVariables("GAMEWATCHER_");
                config.AddCommandLine(args);
            })
            .ConfigureServices((context, services) =>
            {
                // Configuration
                services.Configure<RuntimeConfig>(context.Configuration.GetSection("Runtime"));
                
                // Core Runtime Services (Working implementations)
                services.AddSingleton<GameCaptureService>();
                services.AddSingleton<IOcrEngine, WindowsOcrEngine>();
                services.AddSingleton<ITextboxDetector, DynamicTextboxDetector>();
                
                // V2 Runtime Services
                services.AddSingleton<IPackManager, PackManager>();
                services.AddSingleton<IGameDetectionService, GameDetectionService>();
                services.AddSingleton<IProcessingPipeline, ProcessingPipeline>();
                
                // Runtime Orchestrator (replaces old GameWatcherService)
                services.AddHostedService<GameWatcherRuntimeService>();
            });
}

/// <summary>
/// Main GameWatcher V2 Runtime Service - orchestrates pack discovery, game detection, and processing pipeline.
/// Maintains V1 performance while enabling unlimited game support through modular pack system.
/// </summary>
public class GameWatcherRuntimeService : BackgroundService
{
    private readonly ILogger<GameWatcherRuntimeService> _logger;
    private readonly IPackManager _packManager;
    private readonly IGameDetectionService _gameDetection;
    private readonly IProcessingPipeline _pipeline;
    private readonly RuntimeConfig _config;

    public GameWatcherRuntimeService(
        ILogger<GameWatcherRuntimeService> logger,
        IPackManager packManager,
        IGameDetectionService gameDetection,
        IProcessingPipeline pipeline,
        Microsoft.Extensions.Options.IOptions<RuntimeConfig> config)
    {
        _logger = logger;
        _packManager = packManager;
        _gameDetection = gameDetection;
        _pipeline = pipeline;
        _config = config.Value;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("🎮 GameWatcher V2 Runtime starting...");

        try
        {
            // Phase 1: Discover and load game packs
            await InitializePacksAsync();

            // Phase 2: Auto-detect running games
            if (_config.Global.AutoDetectGames)
            {
                await AutoDetectAndLoadGameAsync();
            }

            // Phase 3: Start main processing pipeline
            _logger.LogInformation("🚀 Starting processing pipeline...");
            await _pipeline.StartAsync(stoppingToken);

            _logger.LogInformation("✅ GameWatcher V2 Runtime fully operational");

            // Keep running until cancellation
            await Task.Delay(Timeout.Infinite, stoppingToken);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("🛑 GameWatcher V2 Runtime shutdown requested");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "💥 Fatal error in GameWatcher V2 Runtime");
            throw;
        }
    }

    private async Task InitializePacksAsync()
    {
        _logger.LogInformation("📦 Discovering game packs...");

        var packDirectories = _config.PackDirectories.Any() 
            ? _config.PackDirectories 
            : GetDefaultPackDirectories();

        _logger.LogInformation("Searching pack directories: {Directories}", 
            string.Join(", ", packDirectories));

        var discoveredPacks = await _packManager.DiscoverPacksAsync(packDirectories);

        _logger.LogInformation("✅ Discovered {PackCount} game packs:", discoveredPacks.Count);
        
        foreach (var pack in discoveredPacks)
        {
            _logger.LogInformation("  📋 {PackId} - {DisplayName} (v{Version})", 
                pack.Manifest.Name, 
                pack.Manifest.DisplayName,
                pack.Manifest.Version);
        }

        if (!discoveredPacks.Any())
        {
            _logger.LogWarning("⚠️ No game packs found! Please ensure pack assemblies are in the search directories.");
        }
    }

    private async Task AutoDetectAndLoadGameAsync()
    {
        _logger.LogInformation("🔍 Auto-detecting running games...");

        var detectedGame = await _gameDetection.DetectActiveGameAsync();

        if (detectedGame != null)
        {
            _logger.LogInformation("🎯 Detected game: {GameName} -> Loading pack: {PackId} (Confidence: {Confidence:P1})",
                detectedGame.ProcessName,
                detectedGame.Pack.Manifest.Name,
                detectedGame.Confidence);

            var loadSuccess = await _packManager.LoadPackAsync(detectedGame.Pack);
            
            if (loadSuccess)
            {
                _logger.LogInformation("✅ Successfully loaded pack for {GameName}", detectedGame.ProcessName);
            }
            else
            {
                _logger.LogError("❌ Failed to load pack for {GameName}", detectedGame.ProcessName);
            }
        }
        else
        {
            _logger.LogInformation("💤 No supported games currently running");
        }
    }

    private List<string> GetDefaultPackDirectories()
    {
        var baseDirectory = AppContext.BaseDirectory;
        
        return new List<string>
        {
            Path.Combine(baseDirectory, "Packs"),
            Path.Combine(baseDirectory, "..", "FF1.PixelRemaster", "bin", "Debug", "net8.0"),
            baseDirectory // Look in runtime directory for pack assemblies
        };
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("🛑 Stopping GameWatcher V2 Runtime...");

        // Stop processing pipeline gracefully
        if (_pipeline.IsRunning)
        {
            await _pipeline.StopAsync();
        }

        // Unload all packs
        var loadedPacks = _packManager.GetLoadedPacks();
        foreach (var pack in loadedPacks)
        {
            await _packManager.UnloadPackAsync(pack.Manifest.Name);
        }

        await base.StopAsync(cancellationToken);
        
        _logger.LogInformation("✅ GameWatcher V2 Runtime stopped gracefully");
    }
}