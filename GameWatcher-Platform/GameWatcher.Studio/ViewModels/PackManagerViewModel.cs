using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using System.Collections.ObjectModel;
using GameWatcher.Engine.Packs;
using GameWatcher.Runtime.Services;

namespace GameWatcher.Studio.ViewModels;

public partial class PackManagerViewModel : ObservableObject, IDisposable
{
    private readonly ILogger<PackManagerViewModel> _logger;
    private readonly Runtime.Services.IPackManager _packManager;

    [ObservableProperty]
    private ObservableCollection<PackInfoViewModel> _availablePacks = new();

    [ObservableProperty]
    private PackInfoViewModel? _selectedPack;

    [ObservableProperty]
    private PackInfoViewModel? _activePack;

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    public PackManagerViewModel(ILogger<PackManagerViewModel> logger, Runtime.Services.IPackManager packManager)
    {
        _logger = logger;
        _packManager = packManager;
    }

    public async Task InitializeAsync()
    {
        try
        {
            _logger.LogInformation("Initializing PackManager ViewModel");
            await RefreshAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize PackManager ViewModel");
            StatusMessage = $"Initialization failed: {ex.Message}";
        }
    }

    [RelayCommand]
    public async Task RefreshAsync()
    {
        try
        {
            IsLoading = true;
            StatusMessage = "Discovering packs...";

            _logger.LogInformation("Refreshing available game packs");

            // Clear existing packs
            AvailablePacks.Clear();

            // Discover packs
            var packDirectories = new[] { "packs", "../FF1.PixelRemaster" }; // TODO: Get from config
            var packs = await _packManager.DiscoverPacksAsync(packDirectories);
            
            foreach (var pack in packs)
            {
                var packViewModel = new PackInfoViewModel
                {
                    Name = pack.Manifest.Name,
                    Version = pack.Manifest.Version,
                    Description = pack.Manifest.Description,
                    SupportedGames = pack.Manifest.GameExecutable,
                    IsLoaded = _packManager.GetLoadedPacks().Any(p => p.Manifest.Name == pack.Manifest.Name),
                    Pack = pack
                };

                AvailablePacks.Add(packViewModel);
            }

            // Update active pack
            var activePack = _packManager.GetActivePack();
            ActivePack = AvailablePacks.FirstOrDefault(p => p.Pack.Manifest.Name == (activePack?.Manifest.Name ?? ""));

            StatusMessage = $"Found {AvailablePacks.Count} pack(s)";
            _logger.LogInformation("Found {PackCount} available packs", AvailablePacks.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to refresh packs");
            StatusMessage = $"Refresh failed: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    public async Task LoadPackAsync(PackInfoViewModel? pack)
    {
        if (pack == null) return;

        try
        {
            _logger.LogInformation("Loading pack: {PackName}", pack.Name);
            StatusMessage = $"Loading {pack.Name}...";

            await _packManager.LoadPackAsync(pack.Pack);
            
            // Update pack status
            var loadedPacks = _packManager.GetLoadedPacks();
            foreach (var p in AvailablePacks)
                p.IsLoaded = loadedPacks.Any(lp => lp.Manifest.Name == p.Name);

            ActivePack = pack;
            StatusMessage = $"Loaded {pack.Name} successfully";

            _logger.LogInformation("Pack loaded successfully: {PackName}", pack.Name);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load pack: {PackName}", pack.Name);
            StatusMessage = $"Failed to load {pack.Name}: {ex.Message}";
        }
    }

    [RelayCommand]
    public async Task UnloadPackAsync(PackInfoViewModel? pack)
    {
        if (pack == null) return;

        try
        {
            _logger.LogInformation("Unloading pack: {PackName}", pack.Name);
            StatusMessage = $"Unloading {pack.Name}...";

            await _packManager.UnloadPackAsync(pack.Name);
            
            // Update pack status
            var loadedPacks = _packManager.GetLoadedPacks();
            foreach (var p in AvailablePacks)
                p.IsLoaded = loadedPacks.Any(lp => lp.Manifest.Name == p.Name);

            if (ActivePack == pack)
                ActivePack = null;

            StatusMessage = $"Unloaded {pack.Name} successfully";

            _logger.LogInformation("Pack unloaded successfully: {PackName}", pack.Name);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to unload pack: {PackName}", pack.Name);
            StatusMessage = $"Failed to unload {pack.Name}: {ex.Message}";
        }
    }

    partial void OnSelectedPackChanged(PackInfoViewModel? value)
    {
        // Additional logic when selection changes
    }

    public void Dispose()
    {
        // Clean up if needed
    }
}

public partial class PackInfoViewModel : ObservableObject
{
    [ObservableProperty]
    private string _name = string.Empty;

    [ObservableProperty]
    private string _version = string.Empty;

    [ObservableProperty]
    private string _description = string.Empty;

    [ObservableProperty]
    private string _supportedGames = string.Empty;

    [ObservableProperty]
    private bool _isLoaded;

    public IGamePack Pack { get; set; } = null!;
}