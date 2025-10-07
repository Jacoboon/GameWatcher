using GameWatcher.Engine.Packs;
using Microsoft.Extensions.Logging;
using System.Diagnostics;

namespace GameWatcher.Runtime.Services;

/// <summary>
/// Automatically detects running games and matches them to appropriate packs.
/// Provides confidence scoring for multi-game scenarios.
/// </summary>
public interface IGameDetectionService
{
    Task<DetectedGame?> DetectActiveGameAsync();
    Task<IReadOnlyList<DetectedGame>> DetectAllRunningGamesAsync();
    Task<double> CalculateConfidenceAsync(IGamePack pack);
}

public record DetectedGame(
    string ProcessName,
    string WindowTitle,
    IGamePack Pack,
    double Confidence,
    Process Process
);

public class GameDetectionService : IGameDetectionService
{
    private readonly IPackManager _packManager;
    private readonly ILogger<GameDetectionService> _logger;

    public GameDetectionService(IPackManager packManager, ILogger<GameDetectionService> logger)
    {
        _packManager = packManager;
        _logger = logger;
    }

    public async Task<DetectedGame?> DetectActiveGameAsync()
    {
        _logger.LogDebug("Detecting active game...");

        var runningGames = await DetectAllRunningGamesAsync();
        
        // Return the highest confidence game
        var activeGame = runningGames.OrderByDescending(g => g.Confidence).FirstOrDefault();
        
        if (activeGame != null)
        {
            _logger.LogInformation("Detected active game: {ProcessName} (Confidence: {Confidence:P1})", 
                activeGame.ProcessName, activeGame.Confidence);
        }
        else
        {
            _logger.LogDebug("No supported games detected");
        }

        return activeGame;
    }

    public async Task<IReadOnlyList<DetectedGame>> DetectAllRunningGamesAsync()
    {
        _logger.LogDebug("Detecting all running supported games...");

        var detectedGames = new List<DetectedGame>();
        var runningProcesses = Process.GetProcesses();

        try
        {
            var availablePacks = _packManager.GetLoadedPacks();
            if (!availablePacks.Any())
            {
                // Try to get discovered packs if none are loaded
                _logger.LogDebug("No loaded packs, checking discovered packs...");
            }

            foreach (var process in runningProcesses)
            {
                try
                {
                    // Skip system processes and processes without main window
                    if (string.IsNullOrEmpty(process.ProcessName) || 
                        string.IsNullOrEmpty(process.MainWindowTitle))
                    {
                        continue;
                    }

                    // Check each pack to see if it matches this process
                    foreach (var pack in availablePacks)
                    {
                        if (await DoesPackMatchProcessAsync(pack, process))
                        {
                            var confidence = await CalculateConfidenceAsync(pack, process);
                            
                            detectedGames.Add(new DetectedGame(
                                process.ProcessName,
                                process.MainWindowTitle,
                                pack,
                                confidence,
                                process
                            ));

                            _logger.LogDebug("Found matching game: {ProcessName} -> {PackId} (Confidence: {Confidence:P1})",
                                process.ProcessName, pack.Manifest.Name, confidence);
                        }
                    }
                }
                catch (Exception ex)
                {
                    // Skip processes we can't access (common for system processes)
                    _logger.LogTrace(ex, "Could not check process: {ProcessName}", 
                        process.ProcessName ?? "Unknown");
                }
            }
        }
        finally
        {
            // Dispose of processes we're not using
            foreach (var process in runningProcesses)
            {
                try
                {
                    if (!detectedGames.Any(g => g.Process == process))
                    {
                        process.Dispose();
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogTrace(ex, "Error disposing process");
                }
            }
        }

        _logger.LogInformation("Detected {Count} supported games", detectedGames.Count);
        return detectedGames.AsReadOnly();
    }

    private async Task<bool> DoesPackMatchProcessAsync(IGamePack pack, Process process)
    {
        try
        {
            // Check if pack's target game is running by calling the pack's detection logic
            if (await pack.IsTargetGameRunningAsync())
            {
                // Additional validation - check process name and window title patterns
                return IsProcessNameMatch(pack, process) || IsWindowTitleMatch(pack, process);
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Error checking pack {PackId} against process {ProcessName}", 
                pack.Manifest.Name, process.ProcessName);
        }

        return false;
    }

    private bool IsProcessNameMatch(IGamePack pack, Process process)
    {
        var manifest = pack.Manifest;
        
        // Check exact match
        if (string.Equals(manifest.GameExecutable, process.ProcessName, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        // Check without .exe extension
        var executableWithoutExtension = Path.GetFileNameWithoutExtension(manifest.GameExecutable);
        if (string.Equals(executableWithoutExtension, process.ProcessName, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return false;
    }

    private bool IsWindowTitleMatch(IGamePack pack, Process process)
    {
        var manifest = pack.Manifest;
        
        if (string.IsNullOrEmpty(manifest.WindowTitle) || string.IsNullOrEmpty(process.MainWindowTitle))
        {
            return false;
        }

        // Check if window title contains the expected title
        return process.MainWindowTitle.Contains(manifest.WindowTitle, StringComparison.OrdinalIgnoreCase);
    }

    public async Task<double> CalculateConfidenceAsync(IGamePack pack)
    {
        var runningGames = await DetectAllRunningGamesAsync();
        var matchingGame = runningGames.FirstOrDefault(g => g.Pack.Manifest.Name == pack.Manifest.Name);
        
        return matchingGame?.Confidence ?? 0.0;
    }

    private async Task<double> CalculateConfidenceAsync(IGamePack pack, Process process)
    {
        double confidence = 0.0;

        // Base confidence from pack's own detection
        try
        {
            if (await pack.IsTargetGameRunningAsync())
            {
                confidence += 0.6; // 60% base confidence
            }
        }
        catch
        {
            // If pack detection fails, reduce confidence
            confidence -= 0.2;
        }

        // Bonus for exact process name match
        if (IsProcessNameMatch(pack, process))
        {
            confidence += 0.3; // 30% bonus
        }

        // Bonus for window title match  
        if (IsWindowTitleMatch(pack, process))
        {
            confidence += 0.2; // 20% bonus
        }

        // Penalty for multiple instances (ambiguity)
        var sameGameProcesses = Process.GetProcessesByName(process.ProcessName);
        if (sameGameProcesses.Length > 1)
        {
            confidence -= 0.1 * (sameGameProcesses.Length - 1); // 10% penalty per additional instance
        }

        // Cleanup
        foreach (var proc in sameGameProcesses)
        {
            proc.Dispose();
        }

        return Math.Max(0.0, Math.Min(1.0, confidence)); // Clamp to 0-1 range
    }
}