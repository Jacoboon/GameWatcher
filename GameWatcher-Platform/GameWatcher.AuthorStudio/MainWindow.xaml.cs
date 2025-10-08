using System.Collections.ObjectModel;
using System.Windows;
using GameWatcher.AuthorStudio.Services;
using GameWatcher.AuthorStudio.Models;
using Microsoft.Win32;

namespace GameWatcher.AuthorStudio
{
    public partial class MainWindow : Window
    {
        private readonly DiscoveryService _discovery = new();
        private readonly SpeakerStore _speakerStore = new();

        public MainWindow()
        {
            InitializeComponent();
            DiscoveredGrid.ItemsSource = _discovery.Discovered;
            SpeakersGrid.ItemsSource = _speakerStore.Speakers;
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
    }
}
