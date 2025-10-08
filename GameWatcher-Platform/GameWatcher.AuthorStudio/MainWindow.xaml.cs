using System.Collections.ObjectModel;
using System.Windows;
using GameWatcher.AuthorStudio.Services;
using GameWatcher.AuthorStudio.Models;
using Microsoft.Win32;
using Forms = System.Windows.Forms;
using System.IO;
using System.Linq;

namespace GameWatcher.AuthorStudio
{
    public partial class MainWindow : Window
    {
        private readonly DiscoveryService _discovery = new();
        private readonly SpeakerStore _speakerStore = new();
        private readonly SessionStore _sessionStore = new();
        private readonly PackExporter _exporter = new();
        private readonly PackLoader _loader = new();
        private readonly OpenAiTtsService _tts = new();
        private readonly AudioPlaybackService _audio = new();
        private readonly AuthorSettingsService _settings = new();

        public MainWindow()
        {
            InitializeComponent();
            DiscoveredGrid.ItemsSource = _discovery.Discovered;
            SpeakersGrid.ItemsSource = _speakerStore.Speakers;
            // Bind Review grid and its speaker column source
            ReviewGrid.ItemsSource = _discovery.Discovered;
            var speakerColumn = (System.Windows.Controls.DataGridComboBoxColumn)ReviewGrid.Columns[3];
            speakerColumn.ItemsSource = _speakerStore.Speakers;

            // Bind Speakers grid voice dropdown
            var voiceColumn = (System.Windows.Controls.DataGridComboBoxColumn)SpeakersGrid.Columns[2];
            voiceColumn.ItemsSource = OpenAiVoicesProvider.All;

            UpdateTtsStatus();

            // Initialize settings UI
            if (_settings.Settings.AudioFormat.Equals("mp3", System.StringComparison.OrdinalIgnoreCase))
            {
                AudioFormatCombo.SelectedIndex = 1; // mp3
            }
            else
            {
                AudioFormatCombo.SelectedIndex = 0; // wav
            }
        }

        private async void Start_Click(object sender, RoutedEventArgs e)
        {
            await _discovery.StartAsync();
        }

        private async void Pause_Click(object sender, RoutedEventArgs e)
        {
            await _discovery.PauseAsync();
        }

        private async void Stop_Click(object sender, RoutedEventArgs e)
        {
            await _discovery.StopAsync();
        }

        private async void ImportSpeakers_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new OpenFileDialog
            {
                Filter = "Speakers JSON|speakers.json;*.json|All Files|*.*"
            };
            if (dlg.ShowDialog(this) == true)
            {
                await _speakerStore.ImportAsync(dlg.FileName);
            }
        }

        private async void ExportSpeakers_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new SaveFileDialog
            {
                FileName = "speakers.json",
                Filter = "Speakers JSON|*.json|All Files|*.*"
            };
            if (dlg.ShowDialog(this) == true)
            {
                await _speakerStore.ExportAsync(dlg.FileName);
            }
        }

        private async void SaveSession_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new SaveFileDialog
            {
                FileName = "author_session.json",
                Filter = "Author Session|*.json|All Files|*.*"
            };
            if (dlg.ShowDialog(this) == true)
            {
                await _sessionStore.SaveAsync(dlg.FileName, _discovery.Discovered);
            }
        }

        private async void LoadSession_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new OpenFileDialog
            {
                Filter = "Author Session|*.json|All Files|*.*"
            };
            if (dlg.ShowDialog(this) == true)
            {
                var items = await _sessionStore.LoadAsync(dlg.FileName);
                _discovery.Discovered.Clear();
                foreach (var d in items)
                {
                    _discovery.Discovered.Add(d);
                }
            }
        }

        private void BrowseOutput_Click(object sender, RoutedEventArgs e)
        {
            using var dlg = new Forms.FolderBrowserDialog();
            dlg.Description = "Select Pack Output Folder";
            if (dlg.ShowDialog() == Forms.DialogResult.OK)
            {
                OutputFolderBox.Text = dlg.SelectedPath;
            }
        }

        private async void ExportPack_Click(object sender, RoutedEventArgs e)
        {
            var name = PackNameBox.Text?.Trim();
            var display = DisplayNameBox.Text?.Trim();
            var version = VersionBox.Text?.Trim();
            var output = OutputFolderBox.Text?.Trim();
            if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(display) || string.IsNullOrWhiteSpace(output))
            {
                ExportStatus.Text = "Please fill Pack Name, Display Name, and Output Folder.";
                ExportStatus.Foreground = System.Windows.Media.Brushes.Red;
                return;
            }
            try
            {
                await _exporter.ExportAsync(output, name, display, string.IsNullOrWhiteSpace(version) ? "1.0.0" : version!, _discovery.Discovered, _speakerStore);
                ExportStatus.Text = $"Exported to {output}";
                ExportStatus.Foreground = System.Windows.Media.Brushes.Green;
            }
            catch (Exception ex)
            {
                ExportStatus.Text = $"Export failed: {ex.Message}";
                ExportStatus.Foreground = System.Windows.Media.Brushes.Red;
            }
        }

        private void AttachAudio_Click(object sender, RoutedEventArgs e)
        {
            if (ReviewGrid.SelectedItem is not PendingDialogueEntry entry) return;
            var dlg = new OpenFileDialog
            {
                Filter = "Audio Files|*.wav;*.mp3;*.ogg|All Files|*.*"
            };
            if (dlg.ShowDialog(this) == true)
            {
                entry.AudioPath = dlg.FileName;
                ReviewGrid.Items.Refresh();
                _audio.Play(entry.AudioPath);
            }
        }

        private async void GenerateTts_Click(object sender, RoutedEventArgs e)
        {
            if (ReviewGrid.SelectedItem is not PendingDialogueEntry entry) return;
            if (!_tts.IsConfigured)
            {
                ExportStatus.Text = "TTS unavailable: No API key (click TTS Settings)";
                ExportStatus.Foreground = System.Windows.Media.Brushes.Red;
                return;
            }

            var text = string.IsNullOrWhiteSpace(entry.EditedText) ? entry.Text : entry.EditedText!;
            if (string.IsNullOrWhiteSpace(text)) return;

            // Pick voice from assigned speaker (fallback to default voice of first speaker)
            var voice = "alloy";
            if (!string.IsNullOrWhiteSpace(entry.SpeakerId))
            {
                var sp = _speakerStore.Speakers.FirstOrDefault(s => s.Id == entry.SpeakerId);
                if (sp != null && !string.IsNullOrWhiteSpace(sp.Voice)) voice = sp.Voice;
            }
            else if (_speakerStore.Speakers.Count > 0)
            {
                var sp = _speakerStore.Speakers.First();
                if (!string.IsNullOrWhiteSpace(sp.Voice)) voice = sp.Voice;
            }

            var format = _settings.Settings.AudioFormat?.ToLowerInvariant() == "mp3" ? "mp3" : "wav";
            var ext = "." + format;
            // Output path: OutputFolderBox/Audio/dialogue_<hash>.<ext>
            var outputBase = string.IsNullOrWhiteSpace(OutputFolderBox.Text) ? Directory.GetCurrentDirectory() : OutputFolderBox.Text.Trim();
            var audioDir = Path.Combine(outputBase, "Audio");
            var id = $"dialogue_{Math.Abs(text.GetHashCode()):X8}";
            var outPath = Path.Combine(audioDir, id + ext);
            try
            {
                var ok = await _tts.GenerateAsync(text, voice, 1.0, format, outPath);
                if (ok)
                {
                    entry.AudioPath = outPath;
                    ReviewGrid.Items.Refresh();
                    ExportStatus.Text = $"Generated TTS: {Path.GetFileName(outPath)}";
                    ExportStatus.Foreground = System.Windows.Media.Brushes.Green;
                    _audio.Play(outPath);
                }
                else
                {
                    ExportStatus.Text = "TTS generation failed";
                    ExportStatus.Foreground = System.Windows.Media.Brushes.Red;
                }
            }
            catch (Exception ex)
            {
                ExportStatus.Text = $"TTS error: {ex.Message}";
                ExportStatus.Foreground = System.Windows.Media.Brushes.Red;
            }
        }

        private void PlayReviewItem_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not FrameworkElement fe) return;
            if (fe.DataContext is not PendingDialogueEntry entry) return;
            if (string.IsNullOrWhiteSpace(entry.AudioPath)) return;
            _audio.Play(entry.AudioPath);
        }

        private async void PreviewSpeaker_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not FrameworkElement fe) return;
            if (fe.DataContext is not SpeakerProfile sp) return;

            if (!_tts.IsConfigured)
            {
                ExportStatus.Text = "TTS unavailable: No API key (click TTS Settings)";
                ExportStatus.Foreground = System.Windows.Media.Brushes.Red;
                return;
            }

            var voice = string.IsNullOrWhiteSpace(sp.Voice) ? "alloy" : sp.Voice.Trim();
            var speed = sp.Speed <= 0 ? 1.0 : sp.Speed;
            var nameForLine = string.IsNullOrWhiteSpace(sp.Name) ? voice : sp.Name.Trim();
            var sampleText = $"Hi! I'm {nameForLine}. Calm. Excited! Curious? Let's begin.";

            var format = _settings.Settings.AudioFormat?.ToLowerInvariant() == "mp3" ? "mp3" : "wav";
            var samplePath = GameWatcher.Engine.Audio.VoicePreviewStore.GetPreviewPath(voice, speed, format);

            if (!File.Exists(samplePath))
            {
                try
                {
                    var ok = await _tts.GenerateAsync(sampleText, voice, speed, format, samplePath);
                    if (!ok)
                    {
                        ExportStatus.Text = "Preview generation failed";
                        ExportStatus.Foreground = System.Windows.Media.Brushes.Red;
                        return;
                    }
                }
                catch (Exception ex)
                {
                    ExportStatus.Text = $"Preview error: {ex.Message}";
                    ExportStatus.Foreground = System.Windows.Media.Brushes.Red;
                    return;
                }
            }

            _audio.Play(samplePath);
        }

        private void UpdateTtsStatus()
        {
            if (TtsStatus == null) return;
            if (_tts.IsConfigured)
            {
                TtsStatus.Text = "TTS ready";
                TtsStatus.Foreground = System.Windows.Media.Brushes.Green;
            }
            else
            {
                TtsStatus.Text = "TTS unavailable: Configure key";
                TtsStatus.Foreground = System.Windows.Media.Brushes.Red;
            }
        }

        private void SaveTtsKey_Click(object sender, RoutedEventArgs e)
        {
            var key = TtsKeyBox.Password?.Trim();
            if (string.IsNullOrWhiteSpace(key))
            {
                MessageBox.Show(this, "Please enter a valid API key.", "TTS", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            Environment.SetEnvironmentVariable("GWS_OPENAI_API_KEY", key, EnvironmentVariableTarget.User);
            _tts.ReloadApiKey();
            UpdateTtsStatus();
            MessageBox.Show(this, "API key saved.", "TTS", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void RemoveTtsKey_Click(object sender, RoutedEventArgs e)
        {
            Environment.SetEnvironmentVariable("GWS_OPENAI_API_KEY", null, EnvironmentVariableTarget.User);
            TtsKeyBox.Password = string.Empty;
            _tts.ReloadApiKey();
            UpdateTtsStatus();
            MessageBox.Show(this, "API key removed.", "TTS", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void AudioFormatCombo_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            try
            {
                var selected = (AudioFormatCombo.SelectedItem as System.Windows.Controls.ComboBoxItem)?.Content?.ToString()?.ToLowerInvariant();
                if (selected != "mp3" && selected != "wav") selected = "wav";
                _settings.Settings.AudioFormat = selected!;
                _settings.Save();
            }
            catch { }
        }

        private async void OpenPack_Click(object sender, RoutedEventArgs e)
        {
            using var dlg = new Forms.FolderBrowserDialog();
            dlg.Description = "Select Pack Folder (contains Configuration and Catalog)";
            if (dlg.ShowDialog() == Forms.DialogResult.OK)
            {
                var folder = dlg.SelectedPath;
                try
                {
                    // Load manifest + dialogue
                    var (name, display, version, entries) = await _loader.LoadAsync(folder);
                    PackNameBox.Text = name;
                    DisplayNameBox.Text = display;
                    VersionBox.Text = string.IsNullOrWhiteSpace(version) ? "1.0.0" : version;
                    OutputFolderBox.Text = folder;

                    // Load speakers if present
                    var speakersPath = System.IO.Path.Combine(folder, "Configuration", "speakers.json");
                    if (File.Exists(speakersPath))
                    {
                        await _speakerStore.ImportAsync(speakersPath);
                    }

                    // Populate review list
                    _discovery.Discovered.Clear();
                    foreach (var e2 in entries)
                    {
                        _discovery.Discovered.Add(e2);
                    }

                    // Load OCR fixes for discovery dedupe/preview
                    await _discovery.LoadOcrFixesAsync(folder);

                    ExportStatus.Text = $"Loaded pack: {display}";
                    ExportStatus.Foreground = System.Windows.Media.Brushes.Green;
                }
                catch (Exception ex)
                {
                    ExportStatus.Text = $"Open failed: {ex.Message}";
                    ExportStatus.Foreground = System.Windows.Media.Brushes.Red;
                }
            }
        }
    }
}
