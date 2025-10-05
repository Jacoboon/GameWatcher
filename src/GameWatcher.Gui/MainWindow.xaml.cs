using System.Collections.ObjectModel;
using System.Drawing;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Threading;
using GameWatcher.App.Author;
using GameWatcher.App.Capture;
using GameWatcher.App.Catalog;
using GameWatcher.App.Events;
using GameWatcher.App.Mapping;
using GameWatcher.App.Ocr;
using GameWatcher.App.Text;
using GameWatcher.App.Vision;

namespace GameWatcher.Gui;

public partial class MainWindow : Window
{
    private record WindowItem(IntPtr Handle, string Title)
    {
        public override string ToString() => Title;
    }

    private ObservableCollection<WindowItem> _windows = new();
    private CancellationTokenSource? _cts;

    private readonly string _root;
    private readonly string _mapsDir;
    private readonly string _voicesDir;
    private readonly string _templatesDir;
    private readonly string _dataDir;
    private readonly string _speakersPath;

    public MainWindow()
    {
        InitializeComponent();
        WindowsList.ItemsSource = _windows;
        _root = FindRepoRoot();
        _mapsDir = Path.Combine(_root, "assets", "maps");
        _voicesDir = Path.Combine(_root, "assets", "voices");
        _templatesDir = Path.Combine(_root, "assets", "templates");
        _dataDir = Path.Combine(_root, "data");
        _speakersPath = Path.Combine(_mapsDir, "speakers.json");
        RefreshWindows();
        LoadSpeakers();
    }

    private void RefreshWindows()
    {
        _windows.Clear();
        string filter = FilterBox.Text?.Trim() ?? string.Empty;
        Win32.EnumWindows((h, l) =>
        {
            if (!Win32.IsWindowVisible(h)) return true;
            int len = Win32.GetWindowTextLength(h);
            if (len <= 0) return true;
            var sb = new System.Text.StringBuilder(len + 1);
            Win32.GetWindowText(h, sb, sb.Capacity);
            var title = sb.ToString();
            if (string.IsNullOrWhiteSpace(filter) || title.IndexOf(filter, StringComparison.OrdinalIgnoreCase) >= 0)
                _windows.Add(new WindowItem(h, title));
            return true;
        }, IntPtr.Zero);
    }

    private void LoadSpeakers()
    {
        try
        {
            if (File.Exists(_speakersPath))
            {
                var map = JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(_speakersPath)) ?? new();
                foreach (var s in map.Values.Distinct())
                    SpeakerBox.Items.Add(s);
            }
        }
        catch { }
    }

    private async void Start_Click(object sender, RoutedEventArgs e)
    {
        if (WindowsList.SelectedItem is not WindowItem item)
        {
            MessageBox.Show("Select a window first.");
            return;
        }
        if (!int.TryParse(FpsBox.Text, out var fps) || fps < 1 || fps > 60) fps = 20;

        StartBtn.IsEnabled = false;
        StopBtn.IsEnabled = true;
        FilterBox.IsEnabled = false;
        WindowsList.IsEnabled = false;

        _cts = new CancellationTokenSource();
        await Task.Run(() => RunCapture(item.Handle, fps, _cts.Token));
    }

    private void Stop_Click(object sender, RoutedEventArgs e)
    {
        _cts?.Cancel();
        StartBtn.IsEnabled = true;
        StopBtn.IsEnabled = false;
        FilterBox.IsEnabled = true;
        WindowsList.IsEnabled = true;
    }

    private void Refresh_Click(object sender, RoutedEventArgs e) => RefreshWindows();

    private void EditPersona_Click(object sender, RoutedEventArgs e)
    {
        if (SpeakerBox.Text is not { Length: > 0 } speaker) return;
        var personaPath = Path.Combine(_voicesDir, "personas", speaker + ".json");
        var dlg = new PersonaEditorWindow(personaPath);
        dlg.Owner = this;
        dlg.ShowDialog();
    }

    private void RunCapture(IntPtr hwnd, int fps, CancellationToken ct)
    {
        var detector = new GameWatcher.App.Vision.TextboxDetector(_templatesDir);
        var mapping = new DialogMapping(_mapsDir, _voicesDir);
        var catalog = new CatalogService(_dataDir);
        var emitter = new EventEmitter(_dataDir);
        var speakerResolver = new SpeakerResolver(_speakersPath);
        var ocr = new TesseractCliOcrEngine();
        var normalizer = new SimpleNormalizer();
        Rectangle? rectCache = null;
        string? lastId = null;
        string? lastCropHash = null;
        using var player = new GameWatcher.App.Audio.PlaybackAgent();

        var frameDelay = TimeSpan.FromMilliseconds(1000.0 / fps);
        while (!ct.IsCancellationRequested)
        {
            using var frame = Win32Capture.CaptureClient(hwnd);
            if (frame == null) { Thread.Sleep(frameDelay); continue; }

            if (rectCache is null)
            {
                rectCache = detector.DetectTextbox(frame) ?? new Rectangle(0, 0, frame.Width, frame.Height);
            }

            var rect = rectCache.Value;
            using var crop = new Bitmap(rect.Width, rect.Height);
            using (var g = Graphics.FromImage(crop))
            {
                g.DrawImage(frame, new Rectangle(0, 0, rect.Width, rect.Height), rect, GraphicsUnit.Pixel);
            }

            try
            {
                var cropHash = ImageHasher.ComputeSHA1(crop);
                if (cropHash == lastCropHash) { Thread.Sleep(frameDelay); continue; }
                lastCropHash = cropHash;

                var raw = ocr.ReadTextAsync(crop).GetAwaiter().GetResult();
                var norm = normalizer.Normalize(raw);
                if (string.IsNullOrWhiteSpace(norm)) { Thread.Sleep(frameDelay); continue; }
                var id = catalog.ComputeId(norm);
                if (id == lastId) { Thread.Sleep(frameDelay); continue; }
                lastId = id;

                using var pre = ImagePreprocessor.GrayscaleUpscaleThreshold(crop);
                var speaker = speakerResolver.Resolve(norm);

                if (mapping.TryResolve(norm, out var audio))
                {
                    player.PlayAsync(audio).GetAwaiter().GetResult();
                    catalog.Record(norm, raw, rect, Path.GetFileName(audio), crop, pre);
                    emitter.Emit(new DialogueEvent
                    {
                        Type = "dialogue",
                        Id = id,
                        Normalized = norm,
                        Raw = raw,
                        Rect = new[] { rect.X, rect.Y, rect.Width, rect.Height },
                        Speaker = speaker,
                        Audio = Path.GetFileName(audio),
                        Timestamp = DateTimeOffset.UtcNow
                    });
                }
                else
                {
                    catalog.Record(norm, raw, rect, null, crop, pre);
                    emitter.Emit(new DialogueEvent
                    {
                        Type = "dialogue",
                        Id = id,
                        Normalized = norm,
                        Raw = raw,
                        Rect = new[] { rect.X, rect.Y, rect.Width, rect.Height },
                        Speaker = speaker,
                        Audio = null,
                        Timestamp = DateTimeOffset.UtcNow
                    });
                    // Prompt speaker assignment if speaker is default
                    if (speaker == "default")
                    {
                        Dispatcher.Invoke(() => PromptAssignSpeaker(norm));
                    }
                }

                Dispatcher.Invoke(() =>
                {
                    LogBox.AppendText($"{DateTime.Now:HH:mm:ss} {norm}\n");
                    LogBox.ScrollToEnd();
                });
            }
            catch (Exception ex)
            {
                Dispatcher.Invoke(() =>
                {
                    LogBox.AppendText($"ERROR: {ex.Message}\n");
                    LogBox.ScrollToEnd();
                });
            }

            Thread.Sleep(frameDelay);
        }
    }

    private void PromptAssignSpeaker(string norm)
    {
        var dlg = new SpeakerAssignWindow(_speakersPath, norm, SpeakerBox.Items.Cast<string>().ToList());
        dlg.Owner = this;
        if (dlg.ShowDialog() == true)
        {
            if (!SpeakerBox.Items.Contains(dlg.SelectedSpeaker))
                SpeakerBox.Items.Add(dlg.SelectedSpeaker);
            SpeakerBox.Text = dlg.SelectedSpeaker;
        }
    }

    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(Environment.CurrentDirectory);
        while (dir != null)
        {
            if (dir.EnumerateFiles("GameWatcher.sln").Any()) return dir.FullName;
            dir = dir.Parent!;
        }
        return Environment.CurrentDirectory;
    }
}
