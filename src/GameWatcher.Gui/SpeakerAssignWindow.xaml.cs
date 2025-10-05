using System.Text.Json;
using System.IO;
using System.Windows;

namespace GameWatcher.Gui;

public partial class SpeakerAssignWindow : Window
{
    private readonly string _speakersPath;
    private readonly string _normalized;
    public string SelectedSpeaker => SpeakerCombo.Text.Trim();

    public SpeakerAssignWindow(string speakersPath, string normalized, List<string> existing)
    {
        InitializeComponent();
        _speakersPath = speakersPath;
        _normalized = normalized;
        NormBox.Text = normalized;
        foreach (var s in existing) SpeakerCombo.Items.Add(s);
    }

    private void Ok_Click(object sender, RoutedEventArgs e)
    {
        var sp = SelectedSpeaker;
        if (string.IsNullOrWhiteSpace(sp)) { DialogResult = false; return; }
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_speakersPath)!);
            var map = File.Exists(_speakersPath)
                ? JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(_speakersPath)) ?? new()
                : new();
            map[_normalized] = sp;
            var json = JsonSerializer.Serialize(map, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(_speakersPath, json);
            DialogResult = true;
        }
        catch
        {
            DialogResult = false;
        }
        Close();
    }

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
