using System.Collections.ObjectModel;
using System.Windows;
using GameWatcher.AuthorStudio.Services;
using GameWatcher.AuthorStudio.Models;
using Microsoft.Win32;
using Forms = System.Windows.Forms;
using System.IO;

namespace GameWatcher.AuthorStudio
{
    public partial class MainWindow : Window
    {
        private readonly DiscoveryService _discovery = new();
        private readonly SpeakerStore _speakerStore = new();
        private readonly SessionStore _sessionStore = new();
        private readonly PackExporter _exporter = new();

        public MainWindow()
        {
            InitializeComponent();
            DiscoveredGrid.ItemsSource = _discovery.Discovered;
            SpeakersGrid.ItemsSource = _speakerStore.Speakers;
            // Bind Review grid and its speaker column source
            ReviewGrid.ItemsSource = _discovery.Discovered;
            var speakerColumn = (System.Windows.Controls.DataGridComboBoxColumn)ReviewGrid.Columns[3];
            speakerColumn.ItemsSource = _speakerStore.Speakers;
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
    }
}
