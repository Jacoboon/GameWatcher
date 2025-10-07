using GameWatcher.Engine.Packs;
using Microsoft.Extensions.Logging;
using System.Reflection;
using EnginePack = GameWatcher.Engine.Packs;

namespace GameWatcher.Runtime.Services;

/// <summary>
/// Manages discovery, loading, and lifecycle of game packs in the runtime environment.
/// Provides hot-swapping capabilities and multi-pack support.
/// </summary>
public interface IPackManager
{
    Task<IReadOnlyList<IGamePack>> DiscoverPacksAsync(IEnumerable<string> directories);
    Task<IGamePack?> GetPackAsync(string packId);
    Task<IGamePack?> FindPackForGameAsync(string gameExecutable);
    Task<bool> LoadPackAsync(IGamePack pack);
    Task<bool> UnloadPackAsync(string packId);
    IReadOnlyList<IGamePack> GetLoadedPacks();
    IGamePack? GetActivePack();
}

public class PackManager : IPackManager, GameWatcher.Engine.Packs.IPackManager
{
    private readonly ILogger<PackManager> _logger;
    private readonly Dictionary<string, IGamePack> _discoveredPacks = new();
    private readonly Dictionary<string, IGamePack> _loadedPacks = new();
    private IGamePack? _activePack;

    public PackManager(ILogger<PackManager> logger)
    {
        _logger = logger;
    }

    public async Task<IReadOnlyList<IGamePack>> DiscoverPacksAsync(IEnumerable<string> directories)
    {
        _logger.LogInformation("Discovering game packs in directories: {Directories}", 
            string.Join(", ", directories));

        var discoveredPacks = new List<IGamePack>();

        foreach (var directory in directories)
        {
            if (!Directory.Exists(directory))
            {
                _logger.LogWarning("Pack directory does not exist: {Directory}", directory);
                continue;
            }

            // Look for pack assemblies
            var packFiles = Directory.GetFiles(directory, "*.dll", SearchOption.AllDirectories)
                .Where(f => Path.GetFileName(f).Contains("Pack") || 
                          Path.GetFileName(f).Contains("PixelRemaster"));

            foreach (var packFile in packFiles)
            {
                try
                {
                    await DiscoverPackFromAssemblyAsync(packFile, discoveredPacks);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to load pack from {PackFile}", packFile);
                }
            }
        }

        // Store discovered packs
        foreach (var pack in discoveredPacks)
        {
            _discoveredPacks[pack.Manifest.Name] = pack;
        }

        _logger.LogInformation("Discovered {Count} game packs", discoveredPacks.Count);
        return discoveredPacks.AsReadOnly();
    }

    private async Task DiscoverPackFromAssemblyAsync(string assemblyPath, List<IGamePack> discoveredPacks)
    {
        _logger.LogDebug("Loading assembly: {AssemblyPath}", assemblyPath);
        
        var assembly = Assembly.LoadFrom(assemblyPath);
        var packTypes = assembly.GetTypes()
            .Where(t => typeof(IGamePack).IsAssignableFrom(t) && !t.IsAbstract);

        foreach (var packType in packTypes)
        {
            _logger.LogDebug("Creating pack instance: {PackType}", packType.Name);
            
            var pack = (IGamePack)Activator.CreateInstance(packType)!;
            // Initialize with null service provider for discovery phase
            await pack.InitializeAsync(null!);
            
            discoveredPacks.Add(pack);
            _logger.LogInformation("Discovered pack: {PackId} - {DisplayName}", 
                pack.Manifest.Name, pack.Manifest.DisplayName);
        }
    }

    public Task<IGamePack?> GetPackAsync(string packId)
    {
        _discoveredPacks.TryGetValue(packId, out var pack);
        return Task.FromResult(pack);
    }

    public async Task<IGamePack?> FindPackForGameAsync(string gameExecutable)
    {
        _logger.LogDebug("Finding pack for game executable: {GameExecutable}", gameExecutable);

        foreach (var pack in _discoveredPacks.Values)
        {
            if (await pack.IsTargetGameRunningAsync())
            {
                _logger.LogInformation("Found matching pack: {PackId} for game: {GameExecutable}", 
                    pack.Manifest.Name, gameExecutable);
                return pack;
            }
        }

        _logger.LogWarning("No pack found for game: {GameExecutable}", gameExecutable);
        return null;
    }

    public async Task<bool> LoadPackAsync(IGamePack pack)
    {
        _logger.LogInformation("Loading pack: {PackId} - {DisplayName}", 
            pack.Manifest.Name, pack.Manifest.DisplayName);

        try
        {
            // Unload current active pack if different
            if (_activePack != null && _activePack.Manifest.Name != pack.Manifest.Name)
            {
                await UnloadCurrentPackAsync();
            }

            // Load the new pack
            await pack.InitializeAsync(null!);
            
            _loadedPacks[pack.Manifest.Name] = pack;
            _activePack = pack;

            _logger.LogInformation("Successfully loaded pack: {PackId}", pack.Manifest.Name);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load pack: {PackId}", pack.Manifest.Name);
            return false;
        }
    }

    public async Task<bool> UnloadPackAsync(string packId)
    {
        _logger.LogInformation("Unloading pack: {PackId}", packId);

        if (_loadedPacks.TryGetValue(packId, out var pack))
        {
            try
            {
                if (pack is IDisposable disposable)
                {
                    disposable.Dispose();
                }

                _loadedPacks.Remove(packId);
                
                if (_activePack?.Manifest.Name == packId)
                {
                    _activePack = null;
                }

                _logger.LogInformation("Successfully unloaded pack: {PackId}", packId);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to unload pack: {PackId}", packId);
                return false;
            }
        }

        _logger.LogWarning("Pack not found for unloading: {PackId}", packId);
        return false;
    }

    private async Task UnloadCurrentPackAsync()
    {
        if (_activePack != null)
        {
            await UnloadPackAsync(_activePack.Manifest.Name);
        }
    }

    public IReadOnlyList<IGamePack> GetLoadedPacks()
    {
        return _loadedPacks.Values.ToList().AsReadOnly();
    }

    public IGamePack? GetActivePack()
    {
        return _activePack;
    }

    // Implementation of GameWatcher.Engine.Packs.IPackManager interface
    async Task<EnginePack.PackLoadResult> EnginePack.IPackManager.LoadPackAsync(string packPath)
    {
        try
        {
            var packs = await DiscoverPacksAsync(new[] { packPath });
            var pack = packs.FirstOrDefault();
            
            if (pack == null)
            {
                return EnginePack.PackLoadResult.Failed("Pack not found at specified path");
            }
            
            var startTime = DateTime.UtcNow;
            var success = await LoadPackAsync(pack);
            var loadTime = DateTime.UtcNow - startTime;
            
            return success 
                ? EnginePack.PackLoadResult.Successful(pack, loadTime)
                : EnginePack.PackLoadResult.Failed("Failed to load pack");
        }
        catch (Exception ex)
        {
            return EnginePack.PackLoadResult.Failed(ex.Message);
        }
    }

    Task<IGamePack?> EnginePack.IPackManager.GetActivePackAsync()
    {
        return Task.FromResult(GetActivePack());
    }

    async Task<IEnumerable<EnginePack.PackManifest>> EnginePack.IPackManager.GetAvailablePacksAsync()
    {
        var packs = await DiscoverPacksAsync(new[] { "." });
        return packs.Select(p => p.Manifest);
    }

    Task<EnginePack.PackValidationResult> EnginePack.IPackManager.ValidatePackAsync(string packPath)
    {
        // Basic validation for now
        var result = new EnginePack.PackValidationResult
        {
            IsValid = File.Exists(packPath) || Directory.Exists(packPath),
            Compatibility = new EnginePack.PackCompatibilityInfo
            {
                IsEngineVersionSupported = true,
                AreRequiredServicesAvailable = true,
                IsGameVersionSupported = true
            }
        };
        
        if (!result.IsValid)
        {
            result.Errors = new[] { "Pack path does not exist" };
        }
        
        return Task.FromResult(result);
    }

    Task<IGamePack?> EnginePack.IPackManager.AutoDetectPackAsync()
    {
        // Use existing auto-detection logic
        return Task.FromResult(GetActivePack());
    }
}