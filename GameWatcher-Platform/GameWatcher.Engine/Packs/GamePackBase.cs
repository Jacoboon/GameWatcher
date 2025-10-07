using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using GameWatcher.Engine.Detection;
using GameWatcher.Engine.Ocr;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace GameWatcher.Engine.Packs;

/// <summary>
/// Base implementation for game packs with common functionality
/// </summary>
public abstract class GamePackBase : IGamePack
{
    protected ILogger? _logger;
    protected IServiceProvider? _serviceProvider;
    
    public abstract PackManifest Manifest { get; }
    
    public virtual async Task<bool> InitializeAsync(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
        _logger = serviceProvider.GetService<ILogger<GamePackBase>>();
        
        _logger?.LogInformation("Initializing pack: {PackName} v{Version}", 
            Manifest.DisplayName, Manifest.Version);
        
        // Validate pack configuration
        var validation = await ValidatePackConfigurationAsync();
        if (!validation.IsValid)
        {
            _logger?.LogError("Pack validation failed: {Errors}", 
                string.Join(", ", validation.Errors));
            return false;
        }
        
        // Initialize pack-specific resources
        await InitializePackResourcesAsync();
        
        _logger?.LogInformation("Successfully initialized pack: {PackName}", Manifest.DisplayName);
        return true;
    }
    
    public virtual async Task<bool> IsTargetGameRunningAsync()
    {
        try
        {
            var processes = Process.GetProcessesByName(
                Path.GetFileNameWithoutExtension(Manifest.GameExecutable));
            
            if (processes.Length == 0) return false;
            
            // Additional validation: check window title if specified
            if (!string.IsNullOrEmpty(Manifest.WindowTitle))
            {
                return processes.Any(p => 
                    p.MainWindowTitle.Contains(Manifest.WindowTitle, StringComparison.OrdinalIgnoreCase));
            }
            
            return true;
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Error checking if game {GameExe} is running", Manifest.GameExecutable);
            return false;
        }
    }
    
    public virtual async Task<bool> ValidateGameVersionAsync()
    {
        // Default implementation checks if game is running
        // Override in specific packs for version-specific validation
        var isRunning = await IsTargetGameRunningAsync();
        
        if (!isRunning)
        {
            _logger?.LogWarning("Game {GameExe} is not running for version validation", 
                Manifest.GameExecutable);
            return false;
        }
        
        // If no specific versions are defined, assume compatibility
        if (Manifest.SupportedVersions.Length == 0)
        {
            return true;
        }
        
        // For now, return true - implement version detection in specific packs
        return true;
    }
    
    public virtual Task DisposeAsync()
    {
        _logger?.LogInformation("Disposing pack: {PackName}", Manifest.DisplayName);
        
        // Clean up pack-specific resources
        DisposePackResources();
        
        return Task.CompletedTask;
    }
    
    // Abstract methods that must be implemented by specific packs
    public abstract ITextboxDetector CreateDetectionStrategy();
    public abstract ISpeakerCollection GetSpeakers();
    public abstract OcrConfig GetOcrConfiguration();
    
    // Virtual methods that can be overridden
    protected virtual async Task<PackValidationResult> ValidatePackConfigurationAsync()
    {
        var result = new PackValidationResult { IsValid = true };
        
        // Basic validation
        if (string.IsNullOrEmpty(Manifest.Name))
        {
            result.Errors = result.Errors.Append("Pack name is required").ToArray();
        }
        
        if (string.IsNullOrEmpty(Manifest.GameExecutable))
        {
            result.Errors = result.Errors.Append("Game executable is required").ToArray();
        }
        
        result.IsValid = !result.Errors.Any();
        
        await Task.CompletedTask; // Make async for future validation needs
        return result;
    }
    
    protected virtual async Task InitializePackResourcesAsync()
    {
        // Override in specific packs to load templates, audio, etc.
        await Task.CompletedTask;
    }
    
    protected virtual void DisposePackResources()
    {
        // Override in specific packs to clean up resources
    }
    
    /// <summary>
    /// Load JSON configuration file from pack directory
    /// </summary>
    protected T? LoadConfigurationFile<T>(string fileName) where T : class
    {
        try
        {
            var packDirectory = GetPackDirectory();
            var configPath = Path.Combine(packDirectory, "Configuration", fileName);
            
            if (!File.Exists(configPath))
            {
                _logger?.LogWarning("Configuration file not found: {ConfigPath}", configPath);
                return null;
            }
            
            var json = File.ReadAllText(configPath);
            return JsonSerializer.Deserialize<T>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Error loading configuration file: {FileName}", fileName);
            return null;
        }
    }
    
    /// <summary>
    /// Get the directory where this pack is located
    /// </summary>
    protected virtual string GetPackDirectory()
    {
        // Default to assembly location - override if pack structure differs
        var assembly = GetType().Assembly;
        return Path.GetDirectoryName(assembly.Location) ?? Environment.CurrentDirectory;
    }
}