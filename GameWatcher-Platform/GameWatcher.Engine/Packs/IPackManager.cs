using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace GameWatcher.Engine.Packs;

/// <summary>
/// Manager for loading, unloading, and orchestrating game packs
/// </summary>
public interface IPackManager
{
    /// <summary>
    /// Load a game pack from file path
    /// </summary>
    Task<PackLoadResult> LoadPackAsync(string packPath);
    
    /// <summary>
    /// Unload a currently loaded pack
    /// </summary>
    Task<bool> UnloadPackAsync(string packId);
    
    /// <summary>
    /// Get the currently active pack
    /// </summary>
    Task<IGamePack?> GetActivePackAsync();
    
    /// <summary>
    /// Get all available/discovered packs
    /// </summary>
    Task<IEnumerable<PackManifest>> GetAvailablePacksAsync();
    
    /// <summary>
    /// Validate a pack before loading
    /// </summary>
    Task<PackValidationResult> ValidatePackAsync(string packPath);
    
    /// <summary>
    /// Auto-detect which pack to use based on running games
    /// </summary>
    Task<IGamePack?> AutoDetectPackAsync();
}

/// <summary>
/// Result of pack loading operation
/// </summary>
public class PackLoadResult
{
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    public IGamePack? LoadedPack { get; set; }
    public TimeSpan LoadTime { get; set; }
    
    public static PackLoadResult Successful(IGamePack pack, TimeSpan loadTime)
    {
        return new PackLoadResult
        {
            Success = true,
            LoadedPack = pack,
            LoadTime = loadTime
        };
    }
    
    public static PackLoadResult Failed(string error)
    {
        return new PackLoadResult
        {
            Success = false,
            ErrorMessage = error
        };
    }
}

/// <summary>
/// Result of pack validation
/// </summary>
public class PackValidationResult
{
    public bool IsValid { get; set; }
    public string[] Errors { get; set; } = Array.Empty<string>();
    public string[] Warnings { get; set; } = Array.Empty<string>();
    public PackCompatibilityInfo Compatibility { get; set; } = new();
    
    public bool HasErrors => Errors.Any();
    public bool HasWarnings => Warnings.Any();
}

public class PackCompatibilityInfo
{
    public bool IsEngineVersionSupported { get; set; }
    public bool AreRequiredServicesAvailable { get; set; }
    public bool IsGameVersionSupported { get; set; }
    public string RequiredEngineVersion { get; set; } = "";
    public string CurrentEngineVersion { get; set; } = "";
}