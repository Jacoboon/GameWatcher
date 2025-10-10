using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using GameWatcher.Engine.Audio;
using GameWatcher.Engine.Audio.Effects;
using System.Collections.ObjectModel;
using Microsoft.Win32;
using System.IO;

namespace GameWatcher.AuthorStudio.ViewModels;

/// <summary>
/// Voice Lab - Effect chain editing, auditioning, presets.
/// Enables pack authors to apply audio effects to dialogue files.
/// </summary>
public partial class VoiceLabViewModel : ObservableObject, IDisposable
{
    private readonly ILogger<VoiceLabViewModel> _logger;
    private readonly AudioEffectsEngine _effectsEngine;
    private CancellationTokenSource? _playbackCts;

    [ObservableProperty]
    private string _selectedAudioFile = string.Empty;

    [ObservableProperty]
    private string _selectedAudioFileName = "No file selected";

    [ObservableProperty]
    private bool _hasAudioFile = false;

    [ObservableProperty]
    private bool _isPlaying = false;

    [ObservableProperty]
    private string _statusText = "Select an audio file to begin";

    public ObservableCollection<AudioEffectViewModel> Effects { get; } = new();
    public ObservableCollection<PresetViewModel> Presets { get; } = new();

    public VoiceLabViewModel(ILogger<VoiceLabViewModel> logger, AudioEffectsEngine effectsEngine)
    {
        _logger = logger;
        _effectsEngine = effectsEngine;
    }

    public async Task InitializeAsync()
    {
        _logger.LogInformation("Initializing Voice Lab ViewModel");
        
        // Load built-in presets
        LoadBuiltInPresets();
        
        // Try to load effects for current audio file if it exists
        if (!string.IsNullOrEmpty(SelectedAudioFile) && File.Exists(SelectedAudioFile))
        {
            await LoadEffectsForCurrentFileAsync();
        }
        
        await Task.CompletedTask;
    }

    [RelayCommand]
    private void SelectAudioFile()
    {
        var dialog = new OpenFileDialog
        {
            Title = "Select Audio File",
            Filter = "Audio Files|*.mp3;*.wav;*.ogg;*.flac|All Files|*.*",
            InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments)
        };

        if (dialog.ShowDialog() == true)
        {
            SelectedAudioFile = dialog.FileName;
            SelectedAudioFileName = Path.GetFileName(dialog.FileName);
            HasAudioFile = true;
            StatusText = $"Loaded: {SelectedAudioFileName}";
            
            // Notify commands that depend on HasAudioFile
            PlayOriginalCommand.NotifyCanExecuteChanged();
            
            _ = LoadEffectsForCurrentFileAsync();
        }
    }

    [RelayCommand(CanExecute = nameof(HasAudioFile))]
    private async Task PlayOriginalAsync()
    {
        if (string.IsNullOrEmpty(SelectedAudioFile) || IsPlaying)
            return;

        try
        {
            IsPlaying = true;
            StatusText = "Playing original audio...";
            _playbackCts = new CancellationTokenSource();

            await _effectsEngine.PlayWithEffects(SelectedAudioFile, new List<IAudioEffect>(), _playbackCts.Token);

            StatusText = "Playback complete";
        }
        catch (OperationCanceledException)
        {
            StatusText = "Playback stopped";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error playing original audio");
            StatusText = $"Error: {ex.Message}";
        }
        finally
        {
            IsPlaying = false;
            _playbackCts?.Dispose();
            _playbackCts = null;
        }
    }    [RelayCommand(CanExecute = nameof(CanPlayWithEffects))]
    private async Task PlayWithEffectsAsync()
    {
        if (string.IsNullOrEmpty(SelectedAudioFile) || IsPlaying)
            return;

        try
        {
            IsPlaying = true;
            var enabledEffects = Effects.Where(e => e.IsEnabled).Select(e => e.Effect).ToList();
            StatusText = $"Playing with {enabledEffects.Count} effect(s)...";
            _playbackCts = new CancellationTokenSource();

            await _effectsEngine.PlayWithEffects(SelectedAudioFile, enabledEffects, _playbackCts.Token);

            StatusText = "Playback complete";
        }
        catch (OperationCanceledException)
        {
            StatusText = "Playback stopped";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error playing audio with effects");
            StatusText = $"Error: {ex.Message}";
        }
        finally
        {
            IsPlaying = false;
            _playbackCts?.Dispose();
            _playbackCts = null;
        }
    }

    private bool CanPlayWithEffects() => HasAudioFile && Effects.Any(e => e.IsEnabled);

    [RelayCommand]
    private void StopPlayback()
    {
        _playbackCts?.Cancel();
    }

    [RelayCommand]
    private void AddVolumeEffect()
    {
        var effect = new VolumeEffect();
        effect.Parameters["gain_db"] = 0.0;
        AddEffect(effect, "Volume (0 dB)");
    }

    [RelayCommand]
    private void AddLowPassFilterEffect()
    {
        var effect = new LowPassFilterEffect();
        effect.Parameters["cutoff_frequency"] = 2000.0;
        effect.Parameters["resonance"] = 0.7;
        AddEffect(effect, "Low-Pass Filter (2000 Hz)");
    }

    [RelayCommand]
    private void AddHighPassFilterEffect()
    {
        var effect = new HighPassFilterEffect();
        effect.Parameters["cutoff_frequency"] = 200.0;
        effect.Parameters["resonance"] = 0.5;
        AddEffect(effect, "High-Pass Filter (200 Hz)");
    }

    [RelayCommand]
    private void AddEchoEffect()
    {
        var effect = new EchoEffect();
        effect.Parameters["delay_ms"] = 300.0;
        effect.Parameters["decay"] = 0.5;
        effect.Parameters["wet_level"] = 0.3;
        AddEffect(effect, "Echo (300ms)");
    }

    [RelayCommand]
    private void AddReverbEffect()
    {
        var effect = new ReverbEffect();
        effect.Parameters["room_size"] = 0.5;
        effect.Parameters["damping"] = 0.5;
        effect.Parameters["wet_level"] = 0.3;
        effect.Parameters["dry_level"] = 0.7;
        AddEffect(effect, "Reverb (Medium Room)");
    }

    private void AddEffect(IAudioEffect effect, string displayName)
    {
        var viewModel = new AudioEffectViewModel(effect, displayName);
        viewModel.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(AudioEffectViewModel.IsEnabled))
            {
                PlayWithEffectsCommand.NotifyCanExecuteChanged();
            }
        };
        Effects.Add(viewModel);
        StatusText = $"Added {displayName}";
        PlayWithEffectsCommand.NotifyCanExecuteChanged();
    }

    [RelayCommand]
    private void RemoveEffect(AudioEffectViewModel? effectVm)
    {
        if (effectVm != null)
        {
            Effects.Remove(effectVm);
            StatusText = $"Removed {effectVm.DisplayName}";
            PlayWithEffectsCommand.NotifyCanExecuteChanged();
        }
    }

    [RelayCommand]
    private void ClearAllEffects()
    {
        Effects.Clear();
        StatusText = "Cleared all effects";
        PlayWithEffectsCommand.NotifyCanExecuteChanged();
    }

    [RelayCommand]
    private void ApplyPreset(PresetViewModel? presetVm)
    {
        if (presetVm == null) return;

        Effects.Clear();
        
        foreach (var effect in presetVm.Effects)
        {
            var viewModel = new AudioEffectViewModel(effect, GetEffectDisplayName(effect));
            viewModel.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(AudioEffectViewModel.IsEnabled))
                {
                    PlayWithEffectsCommand.NotifyCanExecuteChanged();
                }
            };
            Effects.Add(viewModel);
        }

        StatusText = $"Applied preset: {presetVm.Name}";
        PlayWithEffectsCommand.NotifyCanExecuteChanged();
    }

    [RelayCommand]
    private async Task SaveEffectsAsync()
    {
        if (string.IsNullOrEmpty(SelectedAudioFile))
            return;

        try
        {
            var effectsList = Effects.Select(e => e.Effect).ToList();
            var metadata = DialogueAudioMetadata.Create(SelectedAudioFile, effectsList);
            metadata.SaveToFile(SelectedAudioFile);

            var metadataPath = DialogueAudioMetadata.GetMetadataPath(SelectedAudioFile);
            StatusText = $"Saved effects to {Path.GetFileName(metadataPath)}";
            _logger.LogInformation("Saved effects to {Path}", metadataPath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error saving effects");
            StatusText = $"Error saving: {ex.Message}";
        }

        await Task.CompletedTask;
    }

    private async Task LoadEffectsForCurrentFileAsync()
    {
        if (string.IsNullOrEmpty(SelectedAudioFile))
            return;

        try
        {
            var metadata = DialogueAudioMetadata.LoadFromFile(SelectedAudioFile);
            if (metadata != null)
            {
                Effects.Clear();
                
                foreach (var effectMeta in metadata.Effects)
                {
                    var effect = effectMeta.ToEffect();
                    var viewModel = new AudioEffectViewModel(effect, GetEffectDisplayName(effect))
                    {
                        IsEnabled = effectMeta.Enabled
                    };
                    viewModel.PropertyChanged += (s, e) =>
                    {
                        if (e.PropertyName == nameof(AudioEffectViewModel.IsEnabled))
                        {
                            PlayWithEffectsCommand.NotifyCanExecuteChanged();
                        }
                    };
                    Effects.Add(viewModel);
                }

                StatusText = $"Loaded {Effects.Count} effect(s) from metadata";
                PlayWithEffectsCommand.NotifyCanExecuteChanged();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading effects metadata");
        }

        await Task.CompletedTask;
    }

    private void LoadBuiltInPresets()
    {
        Presets.Clear();

        // Communication presets
        Presets.Add(new PresetViewModel("Telephone", "📞", "300-3000Hz band-limit", 
            AudioEffectsEngine.EffectFactory.CreateTelephonePreset()));
        
        Presets.Add(new PresetViewModel("Underwater", "🌊", "Muffled, submerged sound", 
            AudioEffectsEngine.EffectFactory.CreateUnderwaterPreset()));
        
        Presets.Add(new PresetViewModel("Whisper", "🤫", "Soft, quiet, close-mic", 
            AudioEffectsEngine.EffectFactory.CreateWhisperPreset()));

        // Environmental presets
        Presets.Add(new PresetViewModel("Cave Echo", "🏔️", "Deep cave with massive reverb", 
            AudioEffectsEngine.EffectFactory.CreateCaveEchoPreset()));
        
        Presets.Add(new PresetViewModel("Cathedral", "⛪", "Large sacred space with long reverb", 
            AudioEffectsEngine.EffectFactory.CreateCathedralPreset()));
        
        Presets.Add(new PresetViewModel("Small Room", "🏠", "Tight space with short reverb", 
            AudioEffectsEngine.EffectFactory.CreateSmallRoomPreset()));
    }

    private string GetEffectDisplayName(IAudioEffect effect)
    {
        return effect.Type switch
        {
            "Volume" => $"Volume ({effect.Parameters.GetValueOrDefault("gain_db", 0.0):F1} dB)",
            "LowPassFilter" => $"Low-Pass Filter ({effect.Parameters.GetValueOrDefault("cutoff_frequency", 2000.0):F0} Hz)",
            "HighPassFilter" => $"High-Pass Filter ({effect.Parameters.GetValueOrDefault("cutoff_frequency", 200.0):F0} Hz)",
            "Echo" => $"Echo ({effect.Parameters.GetValueOrDefault("delay_ms", 300.0):F0}ms)",
            "Reverb" => "Reverb",
            _ => effect.Type
        };
    }

    public void Dispose()
    {
        _playbackCts?.Cancel();
        _playbackCts?.Dispose();
    }
}

/// <summary>
/// ViewModel for a single audio effect in the chain.
/// </summary>
public partial class AudioEffectViewModel : ObservableObject
{
    public IAudioEffect Effect { get; }

    [ObservableProperty]
    private string _displayName;

    [ObservableProperty]
    private bool _isEnabled = true;

    public AudioEffectViewModel(IAudioEffect effect, string displayName)
    {
        Effect = effect;
        _displayName = displayName;
    }
}

/// <summary>
/// ViewModel for an effect preset.
/// </summary>
public class PresetViewModel
{
    public string Name { get; }
    public string Icon { get; }
    public string Description { get; }
    public List<IAudioEffect> Effects { get; }

    public PresetViewModel(string name, string icon, string description, List<IAudioEffect> effects)
    {
        Name = name;
        Icon = icon;
        Description = description;
        Effects = effects;
    }
}
